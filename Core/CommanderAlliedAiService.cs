using System;
using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderAlliedAiService
{
    private const float ThreatScanIntervalSeconds = 5f;
    private const float AirScrambleCooldownSeconds = 60f;
    private const float EconomyInvestCooldownSeconds = 15f;
    private const float FactoryRetoolCooldownSeconds = 30f;
    private const float BattlegroupScanIntervalSeconds = 8f;

    private readonly CommanderMoveService moveService;
    private readonly List<Unit> idleBattlegroupUnits = new();
    private readonly List<Unit> hostileUnitsScratch = new();

    private float nextThreatScanTime;
    private float nextAirScrambleTime;
    private float nextEconomyInvestTime;
    private float nextFactoryRetoolTime;
    private float nextBattlegroupTime;

    private int trackedEnemyAir;
    private int trackedEnemyArmor;
    private int friendlySamCount;
    private int friendlyTankCount;

    internal static CommanderAlliedAiService? Instance { get; private set; }

    internal bool IsEnabled
    {
        get => CommanderSettings.AlliedAutoCommanderEnabled;
        set => CommanderSettings.AlliedAutoCommanderEnabled = value;
    }

    internal string StatusText { get; private set; } = "ALLIED AUTO-COMMANDER: IDLE (MONITORING FRONT)";

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
        hostileUnitsScratch.Clear();

        // 1. Scan Tracked Hostiles in Intelligence DB
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
                else if (enemy is GroundVehicle gv)
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
                if (label.Contains("sam") || label.Contains("strato") || label.Contains("radar") || label.Contains("spaag") || label.Contains("23mm"))
                {
                    friendlySamCount++;
                }
                else if (friendly is GroundVehicle && (label.Contains("tank") || label.Contains("mbt") || label.Contains("ifv") || label.Contains("armored")))
                {
                    friendlyTankCount++;
                }
            }
        }

        StatusText = $"ALLIED AI: ENEMY AIR: {trackedEnemyAir} | ENEMY ARMOR: {trackedEnemyArmor} | FRIENDLY SAM: {friendlySamCount} | TANKS: {friendlyTankCount}";
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
        VehicleDefinition? bestLogisticsDef = null;

        for (int i = 0; i < available.Count; i++)
        {
            VehicleDefinition def = available[i];
            string name = def.unitName.ToLowerInvariant();
            string cat = CommanderGameAccess.GetVehicleCategoryLabel(def).ToLowerInvariant();

            if (bestSamDef == null && (name.Contains("sam") || name.Contains("spaag") || cat.Contains("air defense") || name.Contains("strato")))
            {
                bestSamDef = def;
            }
            if (bestTankDef == null && (name.Contains("tank") || name.Contains("mbt") || cat.Contains("tank") || name.Contains("heavy")))
            {
                bestTankDef = def;
            }
            if (bestLogisticsDef == null && (name.Contains("rearm") || name.Contains("ammo") || name.Contains("munition") || name.Contains("repair")))
            {
                bestLogisticsDef = def;
            }
        }

        for (int f = 0; f < factories.Count; f++)
        {
            Factory factory = factories[f];
            if (factory == null || factory.attachedUnit == null || factory.attachedUnit.disabled) continue;

            // Balance factory assignments dynamically
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

    private void ExecuteAirDefenseScramble()
    {
        if (trackedEnemyAir <= 0) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        // Check if hostiles are within 40 km of friendly bases
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
        if (localHq == null || localHq.factionFunds < 100000f) return;

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
        nextAirScrambleTime = 0f;
        nextEconomyInvestTime = 0f;
        nextFactoryRetoolTime = 0f;
        nextBattlegroupTime = 0f;
        trackedEnemyAir = 0;
        trackedEnemyArmor = 0;
        friendlySamCount = 0;
        friendlyTankCount = 0;
        StatusText = "ALLIED AUTO-COMMANDER: READY";
    }
}
