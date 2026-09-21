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
    private const float AdaptiveCheckInterval = 20f;
    private const float ScatterCooldownSeconds = 10f;
    private const float AutoSupplyCheckInterval = 60f;
    private const float AutoScrambleCheckInterval = 15f;

    private static readonly MethodInfo? FactoryProductionSetter =
        AccessTools.PropertySetter(typeof(Factory), "NetworkproductionUnit")
        ?? AccessTools.PropertySetter(typeof(Factory), "ProductionUnit");

    private readonly List<Factory> enemyFactories = new();
    private readonly List<Airbase> friendlyAirbases = new();
    private readonly Dictionary<Unit, float> lastScatterTimes = new();

    private float nextAdaptiveCheckTime;
    private float nextCacheRefreshTime;
    private float nextSupplyCheckTime;
    private float nextScrambleCheckTime;
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
            nextCacheRefreshTime = now + 45f;
            RefreshSceneCache();
        }

        // 1. Adaptive Counter-Production & Auto-Deploy
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

            if (CommanderSettings.AiAutoReinforceDepots)
            {
                TryAutoReinforceGroundDepots();
            }
        }

        // 2. Autonomous Frontline Ammo Logistics Ferry Loop
        if (CommanderSettings.AiAutoFrontlineSupply && now >= nextSupplyCheckTime)
        {
            nextSupplyCheckTime = now + AutoSupplyCheckInterval;
            TryAutoSupplyFrontline();
        }

        // 3. Autonomous Air Wing Intercept & Scramble
        if (CommanderSettings.AiAutoScrambleAirGuard && now >= nextScrambleCheckTime)
        {
            nextScrambleCheckTime = now + AutoScrambleCheckInterval;
            TryAutoScrambleInterceptors();
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

                    // Equip the official standard combat loadout instead of spawning empty
                    Loadout loadout = new();
                    if (def.aircraftParameters?.StandardLoadouts != null && def.aircraftParameters.StandardLoadouts.Length > 0)
                    {
                        for (int p = 0; p < def.aircraftParameters.StandardLoadouts.Length; p++)
                        {
                            StandardLoadout std = def.aircraftParameters.StandardLoadouts[p];
                            if (std != null && !std.disabled && std.loadout != null && std.loadout.weapons != null && std.loadout.weapons.Count > 0)
                            {
                                loadout = std.loadout;
                                break;
                            }
                        }
                    }

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
                        CommanderPlugin.Log.LogInfo($"[Autonomous Commander] Auto-deployed armed reserve aircraft: {def.unitName} with combat loadout.");
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
                    CommanderPlugin.Log.LogInfo($"[Autonomous Commander] Auto-deployed reserve warship: {def.unitName} to sea lane.");
                    break;
                }
            }
            catch
            {
            }
        }
    }

    private void TryAutoSupplyFrontline()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderSupplyHeliService? supplySvc = CommanderSupplyHeliService.Instance;
        if (localHq == null || supplySvc == null || supplySvc.ActiveMissionCount >= 2)
        {
            return;
        }

        // Check FOBs first
        if (CommanderForwardOutpostService.Instance?.DeployedFobs != null)
        {
            foreach (Unit fob in CommanderForwardOutpostService.Instance.DeployedFobs)
            {
                if (fob != null && !fob.disabled)
                {
                    supplySvc.RequestAutomaticCargoRun(fob.transform.position.ToGlobalPosition());
                    CommanderPlugin.Log.LogInfo($"[Autonomous Logistics] Dispatched automated ammo resupply to FOB at {fob.unitName}.");
                    return;
                }
            }
        }

        // Check frontline combat vehicles for low ammo (< 30%)
        if (localHq.factionUnits != null)
        {
            foreach (PersistentID id in localHq.factionUnits)
            {
                if (!id.TryGetUnit(out Unit unit) || unit == null || unit.disabled || unit is not GroundVehicle)
                {
                    continue;
                }

                if (unit.weaponStations != null && unit.weaponStations.Count > 0)
                {
                    float currentAmmo = 0f;
                    float maxAmmo = 0f;
                    for (int s = 0; s < unit.weaponStations.Count; s++)
                    {
                        WeaponStation station = unit.weaponStations[s];
                        if (station?.Weapons == null) continue;
                        for (int w = 0; w < station.Weapons.Count; w++)
                        {
                            Weapon wp = station.Weapons[w];
                            if (wp != null)
                            {
                                currentAmmo += wp.ammo;
                                maxAmmo += Mathf.Max(1, wp.GetFullAmmo());
                            }
                        }
                    }

                    if (maxAmmo > 0f && (currentAmmo / maxAmmo) <= 0.30f)
                    {
                        supplySvc.RequestAutomaticCargoRun(unit.transform.position.ToGlobalPosition());
                        CommanderPlugin.Log.LogInfo($"[Autonomous Logistics] Dispatched automated ammo drop to low-ammo unit: {unit.unitName}.");
                        return;
                    }
                }
            }
        }
    }

    private void TryAutoScrambleInterceptors()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq?.trackingDatabase == null || friendlyAirbases.Count == 0)
        {
            return;
        }

        Vector3 friendlyCenter = friendlyAirbases[0].transform.position;
        bool hostileAirSpotted = false;

        foreach (KeyValuePair<PersistentID, TrackingInfo> entry in localHq.trackingDatabase)
        {
            if (entry.Key.TryGetUnit(out Unit unit) && unit != null && !unit.disabled && unit is Aircraft && !CommanderGameAccess.IsFriendlyUnit(unit, localHq))
            {
                float dist = Vector3.Distance(friendlyCenter, unit.transform.position);
                if (dist <= 40000f)
                {
                    hostileAirSpotted = true;
                    break;
                }
            }
        }

        if (hostileAirSpotted)
        {
            // Auto-deploy ready AirGuard aircraft
            TryAutoDeployAirReserve();
        }
    }

    private void TryAutoReinforceGroundDepots()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        CommanderSpawnService? spawnSvc = CommanderSpawnService.Instance;
        if (localHq == null || factionSvc == null || spawnSvc == null)
        {
            return;
        }

        int friendlyGroundCount = 0;
        if (localHq.factionUnits != null)
        {
            foreach (PersistentID id in localHq.factionUnits)
            {
                if (id.TryGetUnit(out Unit u) && u != null && !u.disabled && u is GroundVehicle)
                {
                    friendlyGroundCount++;
                }
            }
        }

        // If ground presence is below 12 units, auto-deploy from reserve
        if (friendlyGroundCount < 12)
        {
            IReadOnlyList<VehicleDefinition> landDefs = factionSvc.LandDefinitions;
            for (int d = 0; d < landDefs.Count; d++)
            {
                VehicleDefinition def = landDefs[d];
                if (def == null || factionSvc.IsDefinitionHeld(def))
                {
                    continue;
                }

                if (localHq.GetUnitSupply(def) > 0)
                {
                    spawnSvc.SelectNearestDepot();
                    if (spawnSvc.SelectedDepot != null)
                    {
                        if (spawnSvc.SelectedDepot.TrySpawnVehicle(def))
                        {
                            localHq.ModifyUnitSupply(def, -1);
                            CommanderPlugin.Log.LogInfo($"[Autonomous Army] Reinforced front line with reserve {def.unitName}.");
                            break;
                        }
                    }
                }
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
                    else if (unit is GroundVehicle gv && gv.definition is VehicleDefinition def)
                    {
                        string cat = CommanderGameAccess.GetVehicleCategoryLabel(def);
                        if (cat.IndexOf("MBT", StringComparison.OrdinalIgnoreCase) >= 0 || cat.IndexOf("Tank", StringComparison.OrdinalIgnoreCase) >= 0 || cat.IndexOf("AFV", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            friendlyArmorCount++;
                        }
                    }
                }
            }
        }

        bool needAirDefense = friendlyAircraftCount >= 2;
        IReadOnlyList<VehicleDefinition> allDefs = factionSvc.LandDefinitions;
        VehicleDefinition? bestCounterDef = null;

        for (int i = 0; i < allDefs.Count; i++)
        {
            VehicleDefinition def = allDefs[i];
            string cat = CommanderGameAccess.GetVehicleCategoryLabel(def);
            if (needAirDefense)
            {
                if (cat.IndexOf("AAA", StringComparison.OrdinalIgnoreCase) >= 0 || cat.IndexOf("SAM", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bestCounterDef = def;
                    break;
                }
            }
            else
            {
                if (cat.IndexOf("MBT", StringComparison.OrdinalIgnoreCase) >= 0 || cat.IndexOf("Tank", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bestCounterDef = def;
                    break;
                }
            }
        }

        if (bestCounterDef == null && allDefs.Count > 0)
        {
            bestCounterDef = allDefs[0];
        }

        if (bestCounterDef == null)
        {
            return;
        }

        for (int i = 0; i < enemyFactories.Count; i++)
        {
            Factory factory = enemyFactories[i];
            if (factory != null && factory.attachedUnit != null && !factory.attachedUnit.disabled && factory.ProductionUnit != bestCounterDef)
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
        if (escapePos.y < Datum.LocalSeaY + 1f)
        {
            escapePos.y = Datum.LocalSeaY + 1f;
        }

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
        nextSupplyCheckTime = 0f;
        nextScrambleCheckTime = 0f;
    }
}
