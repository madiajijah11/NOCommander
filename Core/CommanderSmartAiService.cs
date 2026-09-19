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
    private const float AdaptiveCheckInterval = 30f;
    private const float ScatterCooldownSeconds = 10f;

    private static readonly MethodInfo? FactoryProductionSetter =
        AccessTools.PropertySetter(typeof(Factory), "NetworkproductionUnit")
        ?? AccessTools.PropertySetter(typeof(Factory), "ProductionUnit");

    private readonly List<Factory> enemyFactories = new();
    private readonly List<Airbase> friendlyAirbases = new();
    private readonly Dictionary<Unit, float> lastScatterTimes = new();

    private float nextAdaptiveCheckTime;
    private float nextCacheRefreshTime;
    private bool cachedSceneObjects;

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

        float now = Time.unscaledTime;
        if (!cachedSceneObjects || now >= nextCacheRefreshTime)
        {
            nextCacheRefreshTime = now + 60f;
            RefreshSceneCache();
        }

        if (now >= nextAdaptiveCheckTime)
        {
            nextAdaptiveCheckTime = now + AdaptiveCheckInterval;

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

    private void RefreshSceneCache()
    {
        cachedSceneObjects = true;
        enemyFactories.Clear();
        friendlyAirbases.Clear();

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            return;
        }

        Factory[] allFactories = UnityEngine.Object.FindObjectsOfType<Factory>();
        for (int i = 0; i < allFactories.Length; i++)
        {
            Factory f = allFactories[i];
            if (f != null && f.attachedUnit != null && !f.attachedUnit.disabled && f.attachedUnit.NetworkHQ != localHq)
            {
                enemyFactories.Add(f);
            }
        }

        IEnumerable<Airbase> ownedAirbases = localHq.GetAirbases();
        if (ownedAirbases != null)
        {
            foreach (Airbase ab in ownedAirbases)
            {
                if (ab != null && !ab.disabled)
                {
                    friendlyAirbases.Add(ab);
                }
            }
        }
    }

    private void TryAutoDeployAirReserve()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        if (localHq == null || factionSvc == null || friendlyAirbases.Count == 0)
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

            for (int a = 0; a < friendlyAirbases.Count; a++)
            {
                Airbase airbase = friendlyAirbases[a];
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

            GlobalPosition spawnPoint = default;
            float minDistance = float.MaxValue;
            Vector3 friendlyAnchor = localHq.transform.position;
            if (friendlyAirbases.Count > 0)
            {
                friendlyAnchor = friendlyAirbases[0].transform.position;
            }

            for (int r = 0; r < seaLanes.roads.Count; r++)
            {
                Road road = seaLanes.roads[r];
                if (road?.points == null || road.points.Count == 0) continue;

                for (int p = 0; p < road.points.Count; p++)
                {
                    float dist = Vector3.Distance(road.points[p].ToLocalPosition(), friendlyAnchor);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        spawnPoint = road.points[p];
                    }
                }
            }

            if (minDistance == float.MaxValue)
            {
                continue;
            }

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
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        if (localHq == null || factionSvc == null || enemyFactories.Count == 0)
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

        IReadOnlyList<VehicleDefinition> landDefs = factionSvc.LandDefinitions;
        VehicleDefinition? bestCounterDef = null;
        for (int i = 0; i < landDefs.Count; i++)
        {
            VehicleDefinition def = landDefs[i];
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

        for (int i = 0; i < enemyFactories.Count; i++)
        {
            Factory factory = enemyFactories[i];
            if (factory != null && !factory.attachedUnit.disabled && factory.ProductionUnit != bestCounterDef)
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
        enemyFactories.Clear();
        friendlyAirbases.Clear();
        lastScatterTimes.Clear();
        cachedSceneObjects = false;
        nextAdaptiveCheckTime = 0f;
        nextCacheRefreshTime = 0f;
    }
}
