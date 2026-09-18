using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using RoadPathfinding;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderSmartAiService
{
    private const float AdaptiveCheckInterval = 10f;
    private const float ScatterCooldownSeconds = 10f;

    private static readonly MethodInfo? FactoryProductionSetter =
        AccessTools.PropertySetter(typeof(Factory), "NetworkproductionUnit")
        ?? AccessTools.PropertySetter(typeof(Factory), "ProductionUnit");

    private readonly List<Factory> factoryBuffer = new();
    private readonly List<VehicleDefinition> candidateDefinitions = new();
    private readonly Dictionary<Unit, float> lastScatterTimes = new();

    private float nextAdaptiveCheckTime;

    internal static CommanderSmartAiService? Instance { get; private set; }

    internal CommanderSmartAiService()
    {
        Instance = this;
    }

    internal void Tick()
    {
        if (!CommanderSettings.SmartAiEnabled)
        {
            return;
        }

        if (Time.unscaledTime >= nextAdaptiveCheckTime)
        {
            nextAdaptiveCheckTime = Time.unscaledTime + AdaptiveCheckInterval;

            if (CommanderSettings.AiAdaptiveProduction)
            {
                EvaluateAndAdjustAiProduction();
            }

            if (CommanderSettings.AiAutoDeployAir)
            {
                TryAutoDeployAirReserve();
            }

            if (CommanderSettings.AiAutoDeployNaval)
            {
                TryAutoDeployNavalReserve();
            }
        }

        PruneDeadReferences();
    }

    private void TryAutoDeployAirReserve()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        if (localHq == null || factionSvc == null)
        {
            return;
        }

        IEnumerable<Airbase> ownedAirbases = localHq.GetAirbases();
        if (ownedAirbases == null)
        {
            return;
        }

        IReadOnlyList<AircraftDefinition> airDefs = factionSvc.AirDefinitions;
        for (int d = 0; d < airDefs.Count; d++)
        {
            AircraftDefinition def = airDefs[d];
            if (def == null || factionSvc.IsDefinitionHeld(def) || localHq.GetUnitSupply(def) <= 0)
            {
                continue;
            }

            foreach (Airbase airbase in ownedAirbases)
            {
                if (airbase != null && !airbase.disabled && airbase.CanSpawnAircraft(def))
                {
                    int liveryIndex = def.aircraftParameters != null
                        ? def.aircraftParameters.GetRandomLiveryForFaction(localHq.faction)
                        : 0;

                    Loadout loadout = new();
                    float fuel = def.aircraftParameters != null ? def.aircraftParameters.DefaultFuelLevel : 1f;

                    Airbase.TrySpawnResult result = airbase.TrySpawnAircraft(
                        null,
                        def,
                        new LiveryKey(liveryIndex),
                        loadout,
                        fuel);

                    if (result.Allowed)
                    {
                        localHq.ModifyUnitSupply(def, -1);
                        CommanderPlugin.Log.LogInfo($"[Smart AI] Auto-deployed reserve aircraft: {def.unitName} from airbase.");
                        break;
                    }
                }
            }
        }
    }

    private void TryAutoDeployNavalReserve()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        Spawner? spawner = NetworkSceneSingleton<Spawner>.i;
        if (localHq == null || factionSvc == null || spawner == null)
        {
            return;
        }

        if (NetworkManagerNuclearOption.i == null || !NetworkManagerNuclearOption.i.Server.Active)
        {
            return;
        }

        LevelInfo? levelInfo = NetworkSceneSingleton<LevelInfo>.i;
        if (levelInfo?.seaLanes == null || !levelInfo.seaLanes.Exists())
        {
            return;
        }

        IReadOnlyList<ShipDefinition> navalDefs = factionSvc.NavalDefinitions;
        for (int d = 0; d < navalDefs.Count; d++)
        {
            ShipDefinition def = navalDefs[d];
            if (def == null || factionSvc.IsDefinitionHeld(def) || localHq.GetUnitSupply(def) <= 0)
            {
                continue;
            }

            RoadNetwork seaLanes = levelInfo.seaLanes;
            if (seaLanes.roads == null || seaLanes.roads.Count == 0)
            {
                continue;
            }

            Road road = seaLanes.roads[0];
            if (road.points == null || road.points.Count == 0)
            {
                continue;
            }

            GlobalPosition spawnPoint = road.points[0];
            Vector3 localPos = spawnPoint.ToLocalPosition();
            localPos.y = Datum.LocalSeaY + def.spawnOffset.y;
            GlobalPosition spawnPos = localPos.ToGlobalPosition();
            Quaternion rot = Quaternion.identity;

            try
            {
                Ship ship = spawner.SpawnShip(
                    def.unitPrefab,
                    spawnPos,
                    rot,
                    localHq,
                    null,
                    1f,
                    holdPosition: false);

                if (ship != null)
                {
                    localHq.ModifyUnitSupply(def, -1);
                    CommanderPlugin.Log.LogInfo($"[Smart AI] Auto-deployed reserve naval vessel: {def.unitName} to sea lane.");
                    break;
                }
            }
            catch
            {
            }
        }
    }

    private void EvaluateAndAdjustAiProduction()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            return;
        }

        factoryBuffer.Clear();
        Factory[] allFactories = UnityEngine.Object.FindObjectsOfType<Factory>();
        for (int i = 0; i < allFactories.Length; i++)
        {
            Factory f = allFactories[i];
            if (f != null && f.attachedUnit != null && !f.attachedUnit.disabled && f.attachedUnit.NetworkHQ != localHq)
            {
                factoryBuffer.Add(f);
            }
        }

        if (factoryBuffer.Count == 0)
        {
            return;
        }

        int friendlyAircraftCount = 0;
        int friendlyArmorCount = 0;

        if (localHq.factionUnits != null)
        {
            foreach (PersistentID id in localHq.factionUnits)
            {
                if (id.TryGetUnit(out Unit unit) && unit != null && !unit.disabled)
                {
                    if (unit is Aircraft)
                    {
                        friendlyAircraftCount++;
                    }
                    else if (unit is GroundVehicle gv && gv.definition is VehicleDefinition vdef)
                    {
                        string cat = CommanderGameAccess.GetVehicleCategoryLabel(vdef);
                        if (string.Equals(cat, "Tank", StringComparison.OrdinalIgnoreCase) || string.Equals(cat, "Armor", StringComparison.OrdinalIgnoreCase))
                        {
                            friendlyArmorCount++;
                        }
                    }
                }
            }
        }

        string targetCategory = "Tank";
        if (friendlyAircraftCount >= 2)
        {
            targetCategory = "AAA";
        }
        else if (friendlyArmorCount >= 3)
        {
            targetCategory = "Tank";
        }

        candidateDefinitions.Clear();
        CommanderGameAccess.TryGetLocalVehicleDefinitions(candidateDefinitions);

        VehicleDefinition? bestCounterDef = null;
        for (int i = 0; i < candidateDefinitions.Count; i++)
        {
            VehicleDefinition def = candidateDefinitions[i];
            string cat = CommanderGameAccess.GetVehicleCategoryLabel(def);
            if (string.Equals(cat, targetCategory, StringComparison.OrdinalIgnoreCase))
            {
                bestCounterDef = def;
                break;
            }
        }

        if (bestCounterDef == null)
        {
            return;
        }

        for (int i = 0; i < factoryBuffer.Count; i++)
        {
            Factory factory = factoryBuffer[i];
            if (factory != null && factory.ProductionUnit != bestCounterDef)
            {
                try
                {
                    FactoryProductionSetter?.Invoke(factory, new object[] { bestCounterDef });
                }
                catch (Exception ex)
                {
                    CommanderPlugin.Log.LogWarning($"Failed to set AI factory counter unit: {ex.Message}");
                }
            }
        }
    }

    internal void TryTriggerAiScatter(Unit targetUnit, Vector3 hazardPosition, float hazardRadius = 100f)
    {
        if (!CommanderSettings.SmartAiEnabled || !CommanderSettings.AiReactiveScatter)
        {
            return;
        }

        if (targetUnit == null || targetUnit.disabled || targetUnit is not GroundVehicle)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (lastScatterTimes.TryGetValue(targetUnit, out float lastTime) && (now - lastTime) < ScatterCooldownSeconds)
        {
            return;
        }

        lastScatterTimes[targetUnit] = now;

        Vector3 unitPos = targetUnit.transform.position;
        Vector3 diff = unitPos - hazardPosition;
        diff.y = 0f;
        Vector3 escapeDir = diff.sqrMagnitude > 1f ? diff.normalized : UnityEngine.Random.insideUnitSphere;
        escapeDir.y = 0f;
        escapeDir.Normalize();

        Vector3 escapePos = unitPos + escapeDir * UnityEngine.Random.Range(40f, 75f);
        GlobalPosition targetGlobal = escapePos.ToGlobalPosition();

        UnitCommand? command = CommanderGameAccess.GetUnitCommand(targetUnit);
        command?.SetDestination(targetGlobal, true);
    }

    internal static float CalculateThreatScore(Unit? candidate, Unit? observer)
    {
        if (candidate == null || candidate.disabled)
        {
            return -1f;
        }

        float score = 10f;
        if (candidate is Aircraft)
        {
            score = 100f;
        }
        else if (candidate is GroundVehicle gv && gv.definition is VehicleDefinition def)
        {
            string cat = CommanderGameAccess.GetVehicleCategoryLabel(def);
            if (string.Equals(cat, "AAA", StringComparison.OrdinalIgnoreCase)) score = 85f;
            else if (string.Equals(cat, "Tank", StringComparison.OrdinalIgnoreCase)) score = 80f;
            else if (string.Equals(cat, "Armor", StringComparison.OrdinalIgnoreCase) || string.Equals(cat, "IFV", StringComparison.OrdinalIgnoreCase)) score = 60f;
            else if (string.Equals(cat, "Logistics", StringComparison.OrdinalIgnoreCase) || string.Equals(cat, "Support", StringComparison.OrdinalIgnoreCase)) score = 20f;
        }
        else if (candidate is Ship)
        {
            score = 90f;
        }

        if (observer != null)
        {
            float dist = Vector3.Distance(candidate.transform.position, observer.transform.position);
            score += Mathf.Clamp(1000f - dist, 0f, 50f);
        }

        return score;
    }

    internal void PruneDeadReferences()
    {
        List<Unit>? deadKeys = null;
        foreach (KeyValuePair<Unit, float> pair in lastScatterTimes)
        {
            if (pair.Key == null || pair.Key.disabled)
            {
                deadKeys ??= new List<Unit>();
                deadKeys.Add(pair.Key);
            }
        }
        if (deadKeys != null)
        {
            for (int i = 0; i < deadKeys.Count; i++) lastScatterTimes.Remove(deadKeys[i]);
        }
    }

    internal void ResetSession()
    {
        factoryBuffer.Clear();
        candidateDefinitions.Clear();
        lastScatterTimes.Clear();
        nextAdaptiveCheckTime = 0f;
    }
}
