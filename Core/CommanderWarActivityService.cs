using System;
using System.Collections.Generic;
using NuclearOption.Networking;
using RoadPathfinding;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderWarActivityService
{
    private readonly CommanderFactionVehicleService factionVehicles;
    private readonly CommanderSpawnService spawnService;
    private readonly CommanderAirCommandService airCommandService;
    private readonly CommanderNavalPurchaseService navalPurchaseService;
    private readonly CommanderSupplyHeliService? supplyHeliService;
    private int lastNavalCount;
    private int previousGroundCount = -1;
    private int replacementGroundDebt;
    private readonly HashSet<Unit> knownFriendlyUnits = new();
    private readonly List<Unit> friendlyUnits = new();
    private float nextCheckAt;
    private float nextAirCheckAt;
    private float nextNavalCheckAt;
    private float statusUntil;
    private float nextFrontlineOrderAt;
    private float nextSupplyRunAt;
    private string status = string.Empty;

    internal CommanderWarActivityService(
        CommanderFactionVehicleService factionVehicles,
        CommanderSpawnService spawnService,
        CommanderAirCommandService airCommandService,
        CommanderNavalPurchaseService navalPurchaseService,
        CommanderSupplyHeliService? supplyHeliService = null)
    {
        this.factionVehicles = factionVehicles;
        this.spawnService = spawnService;
        this.airCommandService = airCommandService;
        this.navalPurchaseService = navalPurchaseService;
        this.supplyHeliService = supplyHeliService;
    }

    internal string StatusText => Time.unscaledTime <= statusUntil ? status : string.Empty;

    internal void Activate()
    {
        nextCheckAt = CommanderScheduler.Stagger("war.activity", CommanderSettings.WarActivityIntervalSeconds, 1f);
        nextAirCheckAt = CommanderScheduler.Stagger("war.air", CommanderSettings.WarActivityAirIntervalSeconds, 1f);
        nextNavalCheckAt = CommanderScheduler.Stagger("war.naval", CommanderSettings.WarActivityNavalIntervalSeconds, 1f);
        nextFrontlineOrderAt = CommanderScheduler.Stagger("war.frontline", CommanderSettings.WarActivityFrontlineIntervalSeconds, 1f);
        nextSupplyRunAt = CommanderScheduler.Stagger("war.supply", CommanderSettings.WarActivitySupplyIntervalSeconds, 1f);
        status = string.Empty;
    }

    internal void Deactivate() { }

    internal void Tick()
    {
        if (!CommanderSettings.WarActivityEnabled
            || !CommanderFeatureGate.AdvancedFeaturesEnabled
            || Time.unscaledTime < nextCheckAt)
        {
            return;
        }

        nextCheckAt = Time.unscaledTime + CommanderSettings.WarActivityIntervalSeconds;
        CollectFriendlyForces();
        int ground = 0;
        int air = 0;
        int naval = 0;
        int logistics = 0;
        for (int i = 0; i < friendlyUnits.Count; i++)
        {
            Unit unit = friendlyUnits[i];
            if (unit is Aircraft) air++;
            else if (unit is Ship) naval++;
            else ground++;
            if (unit.GetComponentInChildren<Rearmer>(true) != null
                || unit.GetComponentInChildren<Repairer>(true) != null)
            {
                logistics++;
            }
        }

        if (previousGroundCount >= 0 && ground < previousGroundCount)
        {
            replacementGroundDebt += previousGroundCount - ground;
        }
        previousGroundCount = ground;
        lastNavalCount = naval;
        if (!IsHost() || factionVehicles.FactionFunds < CommanderSettings.WarActivityReserveFunds)
        {
            SetStatus(ground, air, naval, logistics, false);
            return;
        }

        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null)
        {
            SetStatus(ground, air, naval, logistics, false);
            return;
        }

        if (logistics < CommanderSettings.WarActivityMinimumLogistics
            && supplyHeliService != null
            && Time.unscaledTime >= nextSupplyRunAt)
        {
            nextSupplyRunAt = Time.unscaledTime + CommanderSettings.WarActivitySupplyIntervalSeconds;
            supplyHeliService.RequestAutomaticCargoRun(hq.transform.GlobalPosition());
        }

        if (Time.unscaledTime >= nextFrontlineOrderAt)
        {
            nextFrontlineOrderAt = Time.unscaledTime + CommanderSettings.WarActivityFrontlineIntervalSeconds;
            RouteAvailableGroundUnit();
        }

        if (Time.unscaledTime >= nextAirCheckAt)
        {
            nextAirCheckAt = Time.unscaledTime + CommanderSettings.WarActivityAirIntervalSeconds;
            TryReinforceAir(hq: CommanderGameAccess.GetLocalHq());
        }

        if (Time.unscaledTime >= nextNavalCheckAt)
        {
            nextNavalCheckAt = Time.unscaledTime + CommanderSettings.WarActivityNavalIntervalSeconds;
            if (lastNavalCount < CommanderSettings.WarActivityMinimumNaval)
            {
                navalPurchaseService.RequestAutonomousNavalPurchase();
            }
            else
            {
                RouteNavalPatrol();
            }
        }

        bool needsGround = ground < CommanderSettings.WarActivityMinimumGround
            || replacementGroundDebt > 0;
        VehicleDefinition? choice = SelectLandReplacement(ground, logistics, needsGround);
        if (choice == null)
        {
            SetStatus(ground, air, naval, logistics, false);
            return;
        }

        int reserve = factionVehicles.GetReserveCount(choice);
        if (reserve == 0)
        {
            if (!factionVehicles.TryBuyStockToReserve(choice, 1, out _))
            {
                SetStatus(ground, air, naval, logistics, false);
                return;
            }
        }

        if (spawnService.TryQueueVehicleAtAnyDepot(choice))
        {
            if (replacementGroundDebt > 0)
            {
                replacementGroundDebt--;
            }
            SetStatus(ground, air, naval, logistics, true);
        }
        else
        {
            SetStatus(ground, air, naval, logistics, false);
        }
    }

    internal void ResetSession()
    {
        friendlyUnits.Clear();
        knownFriendlyUnits.Clear();
        nextCheckAt = 0f;
        nextAirCheckAt = 0f;
        nextNavalCheckAt = 0f;
        nextFrontlineOrderAt = 0f;
        nextSupplyRunAt = 0f;
        lastNavalCount = 0;
        previousGroundCount = -1;
        replacementGroundDebt = 0;
        status = string.Empty;
        statusUntil = 0f;
    }

    private void RouteNavalPatrol()
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        RoadNetwork? seaLanes = NetworkSceneSingleton<LevelInfo>.i?.seaLanes;
        if (hq == null || seaLanes == null || !seaLanes.Exists()
            || !seaLanes.TryGetNearestPoint(hq.transform.GlobalPosition(), out GlobalPosition patrolPoint, out _))
        {
            return;
        }

        for (int i = 0; i < friendlyUnits.Count; i++)
        {
            if (friendlyUnits[i] is Ship ship && ship != null && !ship.disabled)
            {
                CommanderGameAccess.TrySetDestination(ship, patrolPoint);
                return;
            }
        }
    }

    private void CollectFriendlyForces()
    {
        friendlyUnits.Clear();
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq?.factionUnits == null)
        {
            return;
        }

        foreach (PersistentID id in hq.factionUnits)
        {
            if (id.TryGetUnit(out Unit unit)
                && unit != null
                && !unit.disabled
                && CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                friendlyUnits.Add(unit);
                knownFriendlyUnits.Add(unit);
            }
        }
    }

    private void RouteAvailableGroundUnit()
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        knownFriendlyUnits.RemoveWhere(static unit => unit == null || unit.disabled);
        if (hq == null || friendlyUnits.Count == 0)
        {
            return;
        }

        for (int i = 0; i < friendlyUnits.Count; i++)
        {
            Unit unit = friendlyUnits[i];
            if (unit == null || unit.disabled || unit is Aircraft || unit is Ship
                || unit.GetComponentInChildren<Rearmer>(true) != null
                || unit.GetComponentInChildren<Repairer>(true) != null)
            {
                continue;
            }

            GlobalPosition destination = hq.transform.GlobalPosition();
            if (CommanderGameAccess.TryGetCurrentCommandPosition(unit, out GlobalPosition current)
                && FastMath.Distance(current, destination) < 150f)
            {
                continue;
            }

            CommanderGameAccess.TrySetDestination(unit, destination);
            return;
        }
    }

    private void TryReinforceAir(FactionHQ? hq)
    {
        if (hq == null || airCommandService == null)
        {
            return;
        }

        int activeAir = 0;
        for (int i = 0; i < friendlyUnits.Count; i++)
        {
            if (friendlyUnits[i] is Aircraft && !friendlyUnits[i].disabled)
            {
                activeAir++;
            }
        }

        if (activeAir >= CommanderSettings.WarActivityMinimumAir
            || airCommandService.ActiveMissionCount >= CommanderSettings.WarActivityMinimumAir)
        {
            return;
        }

        GlobalPosition target = hq.transform.GlobalPosition();
        CommanderAirCommandService.AirCommandMode mode = CommanderAirCommandService.AirCommandMode.AirGuard;
        string doctrine = CommanderSettings.WarActivityDoctrine;
        if (string.Equals(doctrine, "Ground Offensive", StringComparison.OrdinalIgnoreCase))
        {
            mode = CommanderAirCommandService.AirCommandMode.Cas;
        }
        else if (string.Equals(doctrine, "Naval Pressure", StringComparison.OrdinalIgnoreCase))
        {
            mode = CommanderAirCommandService.AirCommandMode.Arad;
        }
        airCommandService.RequestAutonomousAirMission(mode, target, 25f);
    }

    private VehicleDefinition? SelectLandReplacement(int ground, int logistics, bool replacementDue)
    {
        IReadOnlyList<VehicleDefinition> definitions = factionVehicles.LandDefinitions;
        bool needLogistics = logistics < CommanderSettings.WarActivityMinimumLogistics;
        string doctrine = CommanderSettings.WarActivityDoctrine;
        bool airDoctrine = string.Equals(doctrine, "Air Superiority", StringComparison.OrdinalIgnoreCase);
        bool groundDoctrine = string.Equals(doctrine, "Ground Offensive", StringComparison.OrdinalIgnoreCase);
        for (int i = 0; i < definitions.Count; i++)
        {
            VehicleDefinition definition = definitions[i];
            string category = CommanderGameAccess.GetVehicleCategoryLabel(definition);
            if (string.Equals(category, "Trailer", StringComparison.OrdinalIgnoreCase)) continue;
            if (needLogistics && (definition.unitPrefab?.GetComponentInChildren<Rearmer>(true) != null
                || definition.unitPrefab?.GetComponentInChildren<Repairer>(true) != null))
            {
                return definition;
            }
            if ((replacementDue || ground < CommanderSettings.WarActivityMinimumGround)
                && (!string.Equals(doctrine, "Naval Pressure", StringComparison.OrdinalIgnoreCase) || replacementDue)
                && (!airDoctrine || groundDoctrine || replacementDue))
            {
                return definition;
            }
        }
        return null;
    }

    private void SetStatus(int ground, int air, int naval, int logistics, bool queued)
    {
        status = queued
            ? $"WAR {ground}/{air}/{naval} | LOG {logistics} | reinforcement queued"
            : $"WAR {ground}/{air}/{naval} | LOG {logistics}";
        statusUntil = Time.unscaledTime + 3f;
    }

    private static bool IsHost()
    {
        return NetworkManagerNuclearOption.i != null
            && NetworkManagerNuclearOption.i.Server.Active;
    }
}
