using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderMoveService
{
    private const float GroundFormationSpacingMeters = 25f;
    private const float ShipFormationSpacingMeters = 80f;
    private const float WaypointArrivalDistance = 25f;
    private const float GuardFollowThreshold = 35f;
    private const float ThreatWarnIntervalSeconds = 5f;

    private static readonly MethodInfo? RearmVehicleWaitMethod = AccessTools.Method(typeof(RearmVehicleAI), "Wait");
    private static readonly MethodInfo? RearmVehicleRestockMethod = AccessTools.Method(typeof(RearmVehicleAI), "DriveToRestock");

    private readonly CommanderSelectionService selectionService;
    private readonly CommanderOrderAuthority? orderAuthority;
    private readonly CommanderDoctrineService? doctrineService;
    private readonly HashSet<Unit> stoppedUnits = new();
    private readonly Dictionary<Unit, GlobalPosition> playerDestinations = new();
    private readonly Dictionary<Unit, Queue<GlobalPosition>> waypointQueues = new();
    private readonly Dictionary<Unit, Unit> focusAttackTargets = new();

    private bool awaitingBarrageSelection;
    private bool awaitingAttackMoveSelection;
    private float nextPruneTime;
    private float nextThreatWarnTime;

    internal static CommanderMoveService? Instance { get; private set; }

    internal bool AwaitingBarrageSelection => awaitingBarrageSelection;
    internal bool AwaitingAttackMoveSelection => awaitingAttackMoveSelection;

    internal bool HasCommandableSelection
    {
        get
        {
            for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
            {
                if (CommanderGameAccess.ShouldAllowCommanderMove(selectionService.SelectedUnits[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal CommanderMoveService(CommanderSelectionService selectionService, CommanderOrderAuthority? orderAuthority = null, CommanderDoctrineService? doctrineService = null)
    {
        this.selectionService = selectionService;
        this.orderAuthority = orderAuthority;
        this.doctrineService = doctrineService;
        Instance = this;
    }

    private bool CanExecuteOrder(Unit unit, CommanderOrderKind kind, GlobalPosition destination)
    {
        if (GameManager.gameState == GameState.Multiplayer
            && (orderAuthority == null || !CommanderHostAuthority.IsHostAuthority()))
        {
            return false;
        }

        if (!IsDestinationSafe(unit, kind, destination))
        {
            return false;
        }

        if (GameManager.gameState != GameState.Multiplayer)
        {
            return true;
        }

        CommanderOrderEnvelope envelope = new CommanderOrderEnvelope(
            orderAuthority!.NextCommandId(),
            orderAuthority.CurrentSessionToken,
            kind,
            unit,
            destination,
            Time.unscaledTime);
        return orderAuthority.TryAccept(envelope, out _);
    }

    /// <summary>
    /// Threat-aware movement gate. Host-side only in multiplayer: the decision must be made
    /// on the authority that will own the SetDestination, never on a client.
    /// Autonomous roles keep their doctrine standoff distance; explicit player orders into a
    /// known danger zone are warned about but still honoured.
    /// </summary>
    private bool IsDestinationSafe(Unit unit, CommanderOrderKind kind, GlobalPosition destination)
    {
        if (!CommanderSettings.ThreatAwareMovementEnabled
            || kind != CommanderOrderKind.Move
            || CommanderRadarService.Instance == null)
        {
            return true;
        }

        float dangerRadius = CommanderSettings.ThreatDangerRadiusMeters;
        if (!CommanderRadarService.Instance.IsPositionDangerous(destination, dangerRadius))
        {
            return true;
        }

        bool autonomous = doctrineService != null
            && doctrineService.TryGetRole(unit, out CommanderDoctrineRole role2)
            && role2 != CommanderDoctrineRole.Frontline;
        if (autonomous)
        {
            return false;
        }

        if (Time.unscaledTime >= nextThreatWarnTime)
        {
            nextThreatWarnTime = Time.unscaledTime + ThreatWarnIntervalSeconds;
            CommanderAlertService.PostTickerEvent(
                "[THREAT] Move destination is inside a known hostile fire zone",
                new Color(1f, 0.45f, 0.2f, 0.95f));
        }

        return true;
    }

    internal void BeginBarrageOrder()
    {
        if (!HasCommandableSelection) return;
        awaitingBarrageSelection = true;
        awaitingAttackMoveSelection = false;
    }

    internal void CancelBarrageOrder()
    {
        awaitingBarrageSelection = false;
    }

    internal void BeginAttackMoveOrder()
    {
        if (!HasCommandableSelection) return;
        awaitingAttackMoveSelection = true;
        awaitingBarrageSelection = false;
    }

    internal void CancelAttackMoveOrder()
    {
        awaitingAttackMoveSelection = false;
    }

    internal bool TrySetAttackMoveDestination(Vector2 screenPosition)
    {
        if (!awaitingAttackMoveSelection || selectionService.SelectedUnits.Count == 0)
        {
            return false;
        }

        bool hasGroundPos = CommanderGameAccess.TryRaycastWorldPosition(screenPosition, out GlobalPosition groundPos);
        bool hasWaterPos = CommanderGameAccess.TryRaycastWaterPosition(screenPosition, out GlobalPosition waterPos);
        if (!hasGroundPos && !hasWaterPos)
        {
            awaitingAttackMoveSelection = false;
            return false;
        }

        GlobalPosition targetPos = hasGroundPos ? groundPos : waterPos;
        IssueDirectMoveOrder(targetPos, queueWaypoint: false);

        // Set Stance to Free Fire automatically on attack move
        for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
        {
            CommanderStanceService.Instance?.ApplyHoldFire(selectionService.SelectedUnits[i], false);
        }

        awaitingAttackMoveSelection = false;
        return true;
    }

    internal bool TrySetBarrageTarget(Vector2 screenPosition)
    {
        if (!awaitingBarrageSelection || selectionService.SelectedUnits.Count == 0)
        {
            return false;
        }

        bool hasGroundPos = CommanderGameAccess.TryRaycastWorldPosition(screenPosition, out GlobalPosition groundPos);
        bool hasWaterPos = CommanderGameAccess.TryRaycastWaterPosition(screenPosition, out GlobalPosition waterPos);
        if (!hasGroundPos && !hasWaterPos)
        {
            awaitingBarrageSelection = false;
            return false;
        }

        GlobalPosition targetPos = hasGroundPos ? groundPos : waterPos;
        for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];
            if (!CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                continue;
            }

            if (!CanExecuteOrder(unit, CommanderOrderKind.Move, targetPos))
            {
                continue;
            }

            stoppedUnits.Remove(unit);
            focusAttackTargets.Remove(unit);

            playerDestinations[unit] = targetPos;
            CommanderGameAccess.SetUnitHoldPosition(unit, false);
            CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(targetPos, true);
        }

        awaitingBarrageSelection = false;
        return true;
    }

    internal void IssueDirectMoveOrder(GlobalPosition targetPosition, bool queueWaypoint = false)
    {
        if (GameManager.gameState == GameState.Multiplayer && !CommanderHostAuthority.IsHostAuthority())
        {
            return;
        }

        if (selectionService.SelectedUnits.Count == 0)
        {
            return;
        }

        int groundSlot = 0;
        int shipSlot = 0;
        for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];
            if (!CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                continue;
            }

            focusAttackTargets.Remove(unit);

            GlobalPosition destination = unit is Ship
                ? CommanderDestinationFormation.ApplyOffset(targetPosition, shipSlot++, ShipFormationSpacingMeters)
                : CommanderDestinationFormation.ApplyOffset(targetPosition, groundSlot++, GroundFormationSpacingMeters);

            if (!CanExecuteOrder(unit, CommanderOrderKind.Move, destination))
            {
                continue;
            }

            stoppedUnits.Remove(unit);
            CommanderGameAccess.SetUnitHoldPosition(unit, false);

            if (queueWaypoint && playerDestinations.ContainsKey(unit))
            {
                if (!waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
                {
                    queue = new Queue<GlobalPosition>();
                    waypointQueues[unit] = queue;
                }
                queue.Enqueue(destination);
            }
            else
            {
                if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
                {
                    queue.Clear();
                }
                playerDestinations[unit] = destination;
                UnitCommand? unitCommand = CommanderGameAccess.GetUnitCommand(unit);
                unitCommand?.SetDestination(destination, true);
            }
        }
    }

    internal void TryIssueMoveOrder(Vector2 screenPosition, bool queueWaypoint = false)
    {
        if (selectionService.SelectedUnits.Count == 0)
        {
            return;
        }

        if (awaitingBarrageSelection)
        {
            TrySetBarrageTarget(screenPosition);
            return;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();

        // 1. Check if an Enemy Unit was clicked -> Attack Order / Focus Fire
        if (CommanderGameAccess.TryRaycastSelectableUnit(screenPosition, out Unit targetUnit)
            && targetUnit != null
            && !targetUnit.disabled
            && localHq != null)
        {
            if (!CommanderGameAccess.IsFriendlyUnit(targetUnit, localHq))
            {
                IssueAttackOrder(targetUnit);
                return;
            }
        }

        // 2. Normal Ground / Water Move Order
        bool hasGroundDestination = CommanderGameAccess.TryRaycastWorldPosition(screenPosition, out GlobalPosition groundDestination);
        bool hasWaterDestination = CommanderGameAccess.TryRaycastWaterPosition(screenPosition, out GlobalPosition waterDestination);
        int groundSlot = 0;
        int shipSlot = 0;
        for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];
            if (!CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                continue;
            }

            focusAttackTargets.Remove(unit);

            GlobalPosition destination = unit is Ship
                ? CommanderDestinationFormation.ApplyOffset(waterDestination, shipSlot++, ShipFormationSpacingMeters)
                : CommanderDestinationFormation.ApplyOffset(groundDestination, groundSlot++, GroundFormationSpacingMeters);

            if (!CanExecuteOrder(unit, CommanderOrderKind.Move, destination))
            {
                continue;
            }

            stoppedUnits.Remove(unit);
            CommanderGameAccess.SetUnitHoldPosition(unit, false);

            if (queueWaypoint && playerDestinations.ContainsKey(unit))
            {
                if (!waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
                {
                    queue = new Queue<GlobalPosition>();
                    waypointQueues[unit] = queue;
                }
                queue.Enqueue(destination);
            }
            else
            {
                if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
                {
                    queue.Clear();
                }
                playerDestinations[unit] = destination;
                UnitCommand? unitCommand = CommanderGameAccess.GetUnitCommand(unit);
                unitCommand?.SetDestination(destination, true);
            }
        }
    }

    internal void AssignFocusAttackTarget(Unit unit, Unit enemyTarget)
    {
        if (unit == null || unit.disabled || enemyTarget == null || enemyTarget.disabled || !CommanderGameAccess.ShouldAllowCommanderMove(unit))
        {
            return;
        }

        GlobalPosition targetPos = enemyTarget.transform.GlobalPosition();
        if (!CanExecuteOrder(unit, CommanderOrderKind.Attack, targetPos))
        {
            return;
        }

        stoppedUnits.Remove(unit);
        CommanderGameAccess.SetUnitHoldPosition(unit, false);
        focusAttackTargets[unit] = enemyTarget;

        if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
        {
            queue.Clear();
        }

        playerDestinations[unit] = targetPos;
        CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(targetPos, true);
    }

    private void IssueAttackOrder(Unit enemyTarget)
    {
        for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];
            AssignFocusAttackTarget(unit, enemyTarget);
        }
    }

    internal void Tick()
    {
        // Periodic dead collection pruning (every 2s instead of every frame to eliminate GC drops)
        if (Time.unscaledTime >= nextPruneTime)
        {
            nextPruneTime = Time.unscaledTime + 2.0f;
            stoppedUnits.RemoveWhere(static unit => unit == null || unit.disabled);
            PruneDeadReferences();
        }

        List<Unit>? staleDestinations = null;
        foreach (KeyValuePair<Unit, GlobalPosition> entry in playerDestinations)
        {
            if (entry.Key == null || entry.Key.disabled)
            {
                staleDestinations ??= new List<Unit>();
                staleDestinations.Add(entry.Key!);
                continue;
            }

            float dist = CommanderGameAccess.HorizontalDistance(entry.Key.transform.position, entry.Value.ToLocalPosition());
            if (dist <= WaypointArrivalDistance)
            {
                // Check queued waypoints
                if (waypointQueues.TryGetValue(entry.Key, out Queue<GlobalPosition> queue) && queue.Count > 0)
                {
                    // Peek first: a rejected waypoint must stay queued instead of being silently lost.
                    GlobalPosition nextWaypoint = queue.Peek();
                    if (!CanExecuteOrder(entry.Key, CommanderOrderKind.Move, nextWaypoint))
                    {
                        continue;
                    }
                    queue.Dequeue();
                    playerDestinations[entry.Key] = nextWaypoint;
                    CommanderGameAccess.GetUnitCommand(entry.Key)?.SetDestination(nextWaypoint, true);
                }
                else
                {
                    staleDestinations ??= new List<Unit>();
                    staleDestinations.Add(entry.Key);
                }
            }
        }

        if (staleDestinations != null)
        {
            for (int i = 0; i < staleDestinations.Count; i++)
            {
                playerDestinations.Remove(staleDestinations[i]);
            }
        }

        if (stoppedUnits.Count == 0)
        {
            return;
        }

        foreach (Unit unit in stoppedUnits)
        {
            if (unit.rb == null)
            {
                continue;
            }

            Vector3 vel = unit.rb.velocity;
            Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
            if (horizontalVel.sqrMagnitude < 0.25f)
            {
                unit.rb.velocity = new Vector3(0f, vel.y, 0f);
                unit.rb.angularVelocity = Vector3.zero;
            }
        }
    }

    internal bool TryGetFocusAttackTarget(Unit unit, out Unit target)
    {
        return focusAttackTargets.TryGetValue(unit, out target!) && target != null && !target.disabled;
    }

    internal bool TryGetPlayerDestination(Unit unit, out GlobalPosition destination)
    {
        return playerDestinations.TryGetValue(unit, out destination);
    }

    internal bool HasActivePlayerDestination(Unit unit)
    {
        return playerDestinations.ContainsKey(unit);
    }

    internal bool TryGetQueuedWaypoints(Unit unit, List<GlobalPosition> buffer)
    {
        buffer.Clear();
        if (!waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue) || queue.Count == 0)
        {
            return false;
        }

        foreach (GlobalPosition wp in queue)
        {
            buffer.Add(wp);
        }
        return true;
    }

    internal static void NotifyPlayerDestination(UnitCommand command, GlobalPosition waypoint, Player player)
    {
        if (Instance == null || command == null)
        {
            return;
        }

        Unit? unit = command.GetComponent<Unit>() ?? command.GetComponentInParent<Unit>();
        if (unit != null && !unit.disabled)
        {
            Instance.playerDestinations[unit] = waypoint;
        }
    }

    internal void StopSelectedUnits()
    {
        for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];
            if (!CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                continue;
            }

            CommanderGameAccess.SetUnitHoldPosition(unit, true);
            CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(unit.transform.GlobalPosition(), false);
            stoppedUnits.Add(unit);
            playerDestinations.Remove(unit);
            focusAttackTargets.Remove(unit);
            if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
            {
                queue.Clear();
            }
        }
    }

    internal void ResumeAiForSelectedUnits()
    {
        for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];
            if (!CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                continue;
            }

            CommanderGameAccess.SetUnitHoldPosition(unit, false);
            stoppedUnits.Remove(unit);
            playerDestinations.Remove(unit);
            focusAttackTargets.Remove(unit);
            if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
            {
                queue.Clear();
            }
            if (TryReturnToBasegameLogistics(unit))
            {
                continue;
            }
            if (MissionPosition.TryGetClosestPosition(unit, out GlobalPosition destination))
            {
                CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(destination, false);
            }
        }
    }

    internal void PruneDeadReferences()
    {
        stoppedUnits.RemoveWhere(static u => u == null || u.disabled);


        List<Unit>? deadWaypointKeys = null;
        foreach (KeyValuePair<Unit, Queue<GlobalPosition>> pair in waypointQueues)
        {
            if (pair.Key == null || pair.Key.disabled)
            {
                deadWaypointKeys ??= new List<Unit>();
                deadWaypointKeys.Add(pair.Key!);
            }
        }
        if (deadWaypointKeys != null)
        {
            for (int i = 0; i < deadWaypointKeys.Count; i++) waypointQueues.Remove(deadWaypointKeys[i]);
        }

        List<Unit>? deadAttackKeys = null;
        foreach (KeyValuePair<Unit, Unit> pair in focusAttackTargets)
        {
            if (pair.Key == null || pair.Key.disabled || pair.Value == null || pair.Value.disabled)
            {
                deadAttackKeys ??= new List<Unit>();
                deadAttackKeys.Add(pair.Key!);
            }
        }
        if (deadAttackKeys != null)
        {
            for (int i = 0; i < deadAttackKeys.Count; i++) focusAttackTargets.Remove(deadAttackKeys[i]);
        }
    }

    internal void ResetSession()
    {
        stoppedUnits.Clear();
        playerDestinations.Clear();
        waypointQueues.Clear();
        focusAttackTargets.Clear();
        nextPruneTime = 0f;
        awaitingBarrageSelection = false;
        awaitingAttackMoveSelection = false;
    }

    private static bool TryReturnToBasegameLogistics(Unit unit)
    {
        if (!unit.TryGetComponent(out RearmVehicleAI rearmAi)
            || !unit.TryGetComponent(out Rearmer rearmer))
        {
            return false;
        }

        RearmMissionController? controller = unit.NetworkHQ?.RearmMissionController;
        if (controller != null)
        {
            for (int i = controller.Missions.Count - 1; i >= 0; i--)
            {
                RearmMissionController.RearmMission mission = controller.Missions[i];
                if (ReferenceEquals(mission.Rearmer, rearmer))
                {
                    mission.AssignRearmer(null);
                }
            }
        }

        rearmAi.AssignMission(null!);
        bool needsRestock = rearmer.GetMaxCapacity() > 0f
            && rearmer.Capacity < rearmer.GetMaxCapacity() * 0.5f;
        MethodInfo? stateMethod = needsRestock ? RearmVehicleRestockMethod : RearmVehicleWaitMethod;
        try
        {
            stateMethod?.Invoke(rearmAi, null);
        }
        catch (System.Exception exception)
        {
            CommanderPlugin.Log.LogWarning($"Failed to return {unit.unitName} to Basegame rearm AI: {exception.Message}");
            if (unit is GroundVehicle vehicle)
            {
                vehicle.SetHoldPosition(false);
            }
            return false;
        }

        return true;
    }
}
