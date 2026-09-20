using System;
using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderAlliedAiService
{
    private const float ThreatScanIntervalSeconds = 4f;
    private const float MilitaryProcurementIntervalSeconds = 10f;
    private const float AirScrambleCooldownSeconds = 45f;
    private const float EconomyInvestCooldownSeconds = 15f;
    private const float FactoryRetoolCooldownSeconds = 25f;
    private const float BattlegroupScanIntervalSeconds = 8f;

    private readonly CommanderMoveService moveService;
    private readonly List<Unit> idleBattlegroupUnits = new();
    private readonly List<Unit> hostileUnitsScratch = new();

    private float nextThreatScanTime;
    private float nextProcurementTime;
    private float nextAirScrambleTime;
    private float nextEconomyInvestTime;
    private float nextFactoryRetoolTime;
    private float nextBattlegroupTime;

    private int trackedEnemyAir;
    private int trackedEnemyArmor;
    private int friendlySamCount;
    private int friendlyTankCount;
    private int friendlyShipCount;

    internal static CommanderAlliedAiService? Instance { get; private set; }

    internal bool IsEnabled
    {
        get => CommanderSettings.AlliedAutoCommanderEnabled;
        set => CommanderSettings.AlliedAutoCommanderEnabled = value;
    }

    internal string StatusText { get; private set; } = "ALLIED AUTO-COMMANDER: ACTIVE (MONITORING FRONT)";

    internal CommanderAlliedAiService(CommanderMoveService moveService)
    {
        this.moveService = moveService;
        Instance = this;
    }

    internal void Tick()
    {
        if (!IsEnabled)
        {
            StatusText = "ALLIED AUTO-COMMANDER: DISABLED";
            return;
        }

        float now = Time.unscaledTime;

        if (now >= nextThreatScanTime)
        {
            nextThreatScanTime = now + ThreatScanIntervalSeconds;
            EvaluateFrontlineThreats();
        }

        if (now >= nextProcurementTime)
        {
            nextProcurementTime = now + MilitaryProcurementIntervalSeconds;
            ExecuteAutonomousMilitaryProcurement();
        }

        if (now >= nextFactoryRetoolTime)
        {
            nextFactoryRetoolTime = now + FactoryRetoolCooldownSeconds;
            ExecuteSmartFactoryRetooling();
        }

        if (now >= nextAirScrambleTime)
        {
            ExecuteAirDefenseScramble();
        }

        if (now >= nextEconomyInvestTime)
        {
            nextEconomyInvestTime = now + EconomyInvestCooldownSeconds;
            ExecuteAutonomousEconomyReinvestment();
        }

        if (now >= nextBattlegroupTime)
        {
            nextBattlegroupTime = now + BattlegroupScanIntervalSeconds;
            ExecuteBattlegroupCoordination();
        }
    }

    private void EvaluateFrontlineThreats()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        trackedEnemyAir = 0;
        trackedEnemyArmor = 0;
        friendlySamCount = 0;
        friendlyTankCount = 0;
        friendlyShipCount = 0;
        hostileUnitsScratch.Clear();

        // 1. Scan Tracked Hostiles in Intelligence Database
        if (localHq.trackingDatabase != null)
        {
            foreach (KeyValuePair<PersistentID, TrackingInfo> entry in localHq.trackingDatabase)
            {
                if (!entry.Key.TryGetUnit(out Unit enemy) || enemy == null || enemy.disabled) continue;

                if (enemy is Aircraft)
                {
                    trackedEnemyAir++;
                    hostileUnitsScratch.Add(enemy);
                }
                else if (enemy is GroundVehicle)
                {
                    trackedEnemyArmor++;
                    hostileUnitsScratch.Add(enemy);
                }
            }
        }

        // 2. Scan Friendly Forces
        if (localHq.factionUnits != null)
        {
            foreach (PersistentID id in localHq.factionUnits)
            {
                if (!id.TryGetUnit(out Unit friendly) || friendly == null || friendly.disabled) continue;

                string label = friendly.unitName.ToLowerInvariant();
                if (friendly is Ship)
                {
                    friendlyShipCount++;
                }
                else if (label.Contains("strato") || label.Contains("spaag") || label.Contains("sam") || label.Contains("23mm") || label.Contains("radar"))
                {
                    friendlySamCount++;
                }
                else if (friendly is GroundVehicle && !label.Contains("tanker") && !label.Contains("fuel") && !label.Contains("trailer") && (label.Contains("t-98") || label.Contains("vanguard") || label.Contains("mbt") || label.Contains("tank") || label.Contains("ifv") || label.Contains("afv")))
                {
                    friendlyTankCount++;
                }
            }
        }

        StatusText = $"ALLIED AI: ENEMY AIR: {trackedEnemyAir} | ENEMY ARMOR: {trackedEnemyArmor} | FRIENDLY SAM: {friendlySamCount} | TANKS: {friendlyTankCount} | SHIPS: {friendlyShipCount}";
    }

    private void ExecuteAutonomousMilitaryProcurement()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        CommanderSpawnService? spawnSvc = CommanderSpawnService.Instance;
        if (localHq == null || factionSvc == null || spawnSvc == null) return;

        float funds = localHq.factionFunds;
        // Keep a $35k safety buffer for player manual purchases
        if (funds < 35000f) return;

        IReadOnlyList<VehicleDefinition> landDefs = factionSvc.LandDefinitions;
        if (landDefs.Count == 0) return;

        VehicleDefinition? bestMbt = null;
        VehicleDefinition? bestSam = null;
        VehicleDefinition? bestIfv = null;

        for (int i = 0; i < landDefs.Count; i++)
        {
            VehicleDefinition def = landDefs[i];
            if (bestSam == null && IsAirDefense(def))
            {
                bestSam = def;
            }
            if (bestMbt == null && IsMainBattleTank(def))
            {
                bestMbt = def;
            }
            if (bestIfv == null && IsInfantryFightingVehicle(def))
            {
                bestIfv = def;
            }
        }

        // 1. Procure Ground Forces (Strictly MBTs, SAMs, and IFVs)
        VehicleDefinition? chosenLand = null;
        if (trackedEnemyAir > friendlySamCount && bestSam != null)
        {
            chosenLand = bestSam;
        }
        else if (bestMbt != null && (friendlyTankCount < 10 || friendlyTankCount <= friendlySamCount * 2))
        {
            chosenLand = bestMbt;
        }
        else if (bestIfv != null)
        {
            chosenLand = bestIfv;
        }
        else if (bestSam != null)
        {
            chosenLand = bestSam;
        }

        if (chosenLand != null)
        {
            if (spawnSvc.TryQueueVehicleAtAnyDepot(chosenLand))
            {
                CommanderPlugin.Log.LogInfo($"[Allied Auto-Commander] Procured & queued {chosenLand.unitName} to frontline depot.");
            }
        }

        // 2. Procure Combat Aircraft to Reserve if funds are abundant (>= $80k)
        if (funds >= 80000f)
        {
            IReadOnlyList<AircraftDefinition> airDefs = factionSvc.AirDefinitions;
            for (int a = 0; a < airDefs.Count; a++)
            {
                AircraftDefinition airDef = airDefs[a];
                int stock = factionSvc.GetReserveCount(airDef);
                if (stock < 2 && funds >= airDef.value * 1.5f)
                {
                    if (factionSvc.TryBuyStockToReserve(airDef, 1, out _))
                    {
                        CommanderPlugin.Log.LogInfo($"[Allied Auto-Commander] Purchased combat aircraft {airDef.unitName} to faction reserve.");
                        break;
                    }
                }
            }
        }

        // 3. Procure Naval Warships if sea lanes exist and funds >= $120k
        if (funds >= 120000f && friendlyShipCount < 2)
        {
            LevelInfo? levelInfo = NetworkSceneSingleton<LevelInfo>.i;
            if (levelInfo?.seaLanes != null && levelInfo.seaLanes.Exists())
            {
                IReadOnlyList<ShipDefinition> shipDefs = factionSvc.NavalDefinitions;
                if (shipDefs.Count > 0)
                {
                    ShipDefinition bestShip = shipDefs[0];
                    if (factionSvc.TryBuyStockToReserve(bestShip, 1, out _))
                    {
                        CommanderPlugin.Log.LogInfo($"[Allied Auto-Commander] Procured warship {bestShip.unitName} to naval reserve.");
                    }
                }
            }
        }
    }

    private void ExecuteSmartFactoryRetooling()
    {
        CommanderFactoryProductionService? factorySvc = CommanderFactoryProductionService.Instance;
        if (factorySvc == null || factorySvc.FriendlyFactories.Count == 0) return;

        IReadOnlyList<Factory> factories = factorySvc.FriendlyFactories;
        IReadOnlyList<VehicleDefinition> available = factorySvc.AvailableVehicleDefinitions;
        if (available.Count == 0) return;

        VehicleDefinition? bestSamDef = null;
        VehicleDefinition? bestTankDef = null;

        for (int i = 0; i < available.Count; i++)
        {
            VehicleDefinition def = available[i];
            if (bestSamDef == null && IsAirDefense(def))
            {
                bestSamDef = def;
            }
            if (bestTankDef == null && IsMainBattleTank(def))
            {
                bestTankDef = def;
            }
        }

        for (int f = 0; f < factories.Count; f++)
        {
            Factory factory = factories[f];
            if (factory == null || factory.attachedUnit == null || factory.attachedUnit.disabled) continue;

            if (trackedEnemyAir > friendlySamCount && bestSamDef != null && f % 2 == 0)
            {
                if (!ReferenceEquals(factory.ProductionUnit, bestSamDef))
                {
                    factorySvc.SetProductionUnit(factory, bestSamDef);
                }
            }
            else if (bestTankDef != null)
            {
                if (!ReferenceEquals(factory.ProductionUnit, bestTankDef))
                {
                    factorySvc.SetProductionUnit(factory, bestTankDef);
                }
            }
        }
    }

    private static bool IsMainBattleTank(VehicleDefinition def)
    {
        if (def == null) return false;
        if (def.vehicleType == VehicleType.MBT) return true;

        string name = def.unitName.ToLowerInvariant();
        if (name.Contains("tanker") || name.Contains("fuel") || name.Contains("water") || name.Contains("trailer") || name.Contains("truck"))
        {
            return false;
        }

        return name.Contains("t-98") || name.Contains("vanguard") || name.Contains("mbt") || name.Contains("heavy tank");
    }

    private static bool IsAirDefense(VehicleDefinition def)
    {
        if (def == null) return false;
        string name = def.unitName.ToLowerInvariant();
        string cat = CommanderGameAccess.GetVehicleCategoryLabel(def).ToLowerInvariant();

        return name.Contains("strato") || name.Contains("spaag") || name.Contains("sam") || name.Contains("23mm") || name.Contains("irm") || name.Contains("radar") || cat.Contains("air defense");
    }

    private static bool IsInfantryFightingVehicle(VehicleDefinition def)
    {
        if (def == null) return false;
        if (def.vehicleType == VehicleType.AFV) return true;

        string name = def.unitName.ToLowerInvariant();
        if (name.Contains("tanker") || name.Contains("fuel") || name.Contains("trailer") || name.Contains("truck"))
        {
            return false;
        }

        return name.Contains("ifv") || name.Contains("apc") || name.Contains("bolide") || name.Contains("armored");
    }

    private void ExecuteAirDefenseScramble()
    {
        if (trackedEnemyAir <= 0) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        bool hostileNearBase = false;
        IEnumerable<Airbase> airbases = localHq.GetAirbases();
        if (airbases != null)
        {
            foreach (Airbase ab in airbases)
            {
                if (ab == null || ab.disabled) continue;
                Vector3 basePos = ab.transform.position;

                for (int i = 0; i < hostileUnitsScratch.Count; i++)
                {
                    Unit enemy = hostileUnitsScratch[i];
                    if (enemy != null && !enemy.disabled && Vector3.Distance(enemy.transform.position, basePos) <= 40000f)
                    {
                        hostileNearBase = true;
                        break;
                    }
                }
                if (hostileNearBase) break;
            }
        }

        if (hostileNearBase)
        {
            CommanderAirCommandService? airSvc = CommanderAirCommandService.Instance;
            if (airSvc != null)
            {
                if (airSvc.QuickCallInMission(CommanderAirCommandService.AirCommandMode.AirGuard))
                {
                    nextAirScrambleTime = Time.unscaledTime + AirScrambleCooldownSeconds;
                    StatusText = "ALLIED AI: SCRAMBLED COMBAT AIR PATROL (CAP) INTERCEPTORS!";
                }
            }
        }
    }

    private void ExecuteAutonomousEconomyReinvestment()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || localHq.factionFunds < 90000f) return;

        CommanderBuildingEconomyService? bldSvc = CommanderBuildingEconomyService.Instance;
        if (bldSvc == null) return;

        // 1. Prioritize Strategic Capital Projects
        IReadOnlyList<CommanderBuildingEconomyService.EconomicProject> projects = bldSvc.Projects;
        for (int p = 0; p < projects.Count; p++)
        {
            CommanderBuildingEconomyService.EconomicProject proj = projects[p];
            if (proj.CurrentLevel < proj.MaxLevel && localHq.factionFunds >= proj.CurrentCost)
            {
                bldSvc.TryInvestInProject(proj.Id, out _);
                return;
            }
        }

        // 2. Upgrade Friendly Economy Facilities
        IReadOnlyList<CommanderBuildingEconomyService.BuildingEntry> econBuildings = bldSvc.EconomyEntries;
        for (int b = 0; b < econBuildings.Count; b++)
        {
            CommanderBuildingEconomyService.BuildingEntry entry = econBuildings[b];
            if (entry.Building != null && entry.UpgradeLevel < 3 && localHq.factionFunds >= (60000f * entry.UpgradeLevel))
            {
                bldSvc.TryUpgradeBuildingFacility(entry.Building, out _);
                return;
            }
        }
    }

    private void ExecuteBattlegroupCoordination()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || localHq.factionUnits == null) return;

        idleBattlegroupUnits.Clear();

        // Find idle combat ground units waiting near friendly depots
        foreach (PersistentID id in localHq.factionUnits)
        {
            if (!id.TryGetUnit(out Unit unit) || unit == null || unit.disabled) continue;

            if (unit is GroundVehicle vehicle && !CommanderGameAccess.IsTrailerVehicleDefinition(vehicle.definition as VehicleDefinition))
            {
                if (vehicle.UnitCommand != null && !moveService.HasActivePlayerDestination(vehicle))
                {
                    idleBattlegroupUnits.Add(vehicle);
                }
            }
        }

        // When a platoon of 3-5 units has gathered, push towards nearest enemy or frontline
        if (idleBattlegroupUnits.Count >= 3)
        {
            if (MissionPosition.TryGetClosestPosition(idleBattlegroupUnits[0], out GlobalPosition objective))
            {
                for (int i = 0; i < idleBattlegroupUnits.Count; i++)
                {
                    Unit unit = idleBattlegroupUnits[i];
                    CommanderGameAccess.SetUnitHoldPosition(unit, false);
                    CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(objective, false);
                }
                StatusText = $"ALLIED AI: DISPATCHED BATTLEGROUP OF {idleBattlegroupUnits.Count} UNITS TO FRONTLINE!";
            }
        }
    }

    internal void ResetSession()
    {
        idleBattlegroupUnits.Clear();
        hostileUnitsScratch.Clear();
        nextThreatScanTime = 0f;
        nextProcurementTime = 0f;
        nextAirScrambleTime = 0f;
        nextEconomyInvestTime = 0f;
        nextFactoryRetoolTime = 0f;
        nextBattlegroupTime = 0f;
        trackedEnemyAir = 0;
        trackedEnemyArmor = 0;
        friendlySamCount = 0;
        friendlyTankCount = 0;
        friendlyShipCount = 0;
        StatusText = "ALLIED AUTO-COMMANDER: READY";
    }
}
