using HarmonyLib;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using NuclearOption.Networking;

namespace NuclearOptionCommander;

internal sealed class CommanderMoveService
{
    private const float GroundFormationSpacingMeters = 25f;
    private const float ShipFormationSpacingMeters = 80f;
    private const float WaypointArrivalDistance = 25f;

    private static readonly MethodInfo? RearmVehicleWaitMethod = AccessTools.Method(typeof(RearmVehicleAI), "Wait");
    private static readonly MethodInfo? RearmVehicleRestockMethod = AccessTools.Method(typeof(RearmVehicleAI), "DriveToRestock");

    private readonly CommanderSelectionService selectionService;
    private readonly HashSet<Unit> stoppedUnits = new();
    private readonly Dictionary<Unit, GlobalPosition> playerDestinations = new();
    private readonly Dictionary<Unit, Queue<GlobalPosition>> waypointQueues = new();
    private readonly Dictionary<Unit, Unit> focusAttackTargets = new();
    private readonly Dictionary<Unit, List<GlobalPosition>> patrolRoutes = new();
    private readonly Dictionary<Unit, int> patrolIndices = new();

    private bool awaitingPatrolSelection;

    internal static CommanderMoveService? Instance { get; private set; }

    internal bool AwaitingPatrolSelection => awaitingPatrolSelection;

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

    internal CommanderMoveService(CommanderSelectionService selectionService)
    {
        this.selectionService = selectionService;
        Instance = this;
    }

    internal void BeginPatrolOrder()
    {
        if (!HasCommandableSelection)
        {
            return;
        }

        awaitingPatrolSelection = true;
    }

    internal void CancelPatrolOrder()
    {
        awaitingPatrolSelection = false;
    }

    internal bool TrySetPatrolDestination(Vector2 screenPosition)
    {
        if (!awaitingPatrolSelection || selectionService.SelectedUnits.Count == 0)
        {
            return false;
        }

        bool hasGroundPos = CommanderGameAccess.TryRaycastWorldPosition(screenPosition, out GlobalPosition groundPos);
        bool hasWaterPos = CommanderGameAccess.TryRaycastWaterPosition(screenPosition, out GlobalPosition waterPos);
        if (!hasGroundPos && !hasWaterPos)
        {
            return false;
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

            GlobalPosition targetPos = unit is Ship
                ? CommanderDestinationFormation.ApplyOffset(waterPos, shipSlot++, ShipFormationSpacingMeters)
                : CommanderDestinationFormation.ApplyOffset(groundPos, groundSlot++, GroundFormationSpacingMeters);

            GlobalPosition startPos = unit.transform.GlobalPosition();
            patrolRoutes[unit] = new List<GlobalPosition> { startPos, targetPos };
            patrolIndices[unit] = 1;

            stoppedUnits.Remove(unit);
            focusAttackTargets.Remove(unit);
            if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
            {
                queue.Clear();
            }

            CommanderGameAccess.SetUnitHoldPosition(unit, false);
            playerDestinations[unit] = targetPos;
            CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(targetPos, true);
        }

        awaitingPatrolSelection = false;
        return true;
    }

    internal void ScatterSelectedUnits(float radius = 55f)
    {
        IReadOnlyList<Unit> selected = selectionService.SelectedUnits;
        int count = selected.Count;
        if (count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Unit unit = selected[i];
            if (!CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                continue;
            }

            float angle = ((float)i / count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.25f, 0.25f);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 localTarget = unit.transform.position + offset;
            GlobalPosition destination = localTarget.ToGlobalPosition();

            stoppedUnits.Remove(unit);
            focusAttackTargets.Remove(unit);
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);
            if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
            {
                queue.Clear();
            }

            CommanderGameAccess.SetUnitHoldPosition(unit, false);
            playerDestinations[unit] = destination;
            CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(destination, true);
        }
    }

    internal void IssueDirectMoveOrder(GlobalPosition targetPosition, bool queueWaypoint = false)
    {
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
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);

            GlobalPosition destination = unit is Ship
                ? CommanderDestinationFormation.ApplyOffset(targetPosition, shipSlot++, ShipFormationSpacingMeters)
                : CommanderDestinationFormation.ApplyOffset(targetPosition, groundSlot++, GroundFormationSpacingMeters);

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

        if (awaitingPatrolSelection)
        {
            TrySetPatrolDestination(screenPosition);
            return;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();

        // 1. Check if an Enemy Unit was clicked -> Attack Order / Focus Fire
        if (CommanderGameAccess.TryRaycastSelectableUnit(screenPosition, out Unit targetUnit)
            && targetUnit != null
            && !targetUnit.disabled
            && localHq != null
            && !CommanderGameAccess.IsFriendlyUnit(targetUnit, localHq))
        {
            IssueAttackOrder(targetUnit);
            return;
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
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);

            GlobalPosition destination;
            if (unit is Ship)
            {
                if (!hasWaterDestination) continue;
                destination = CommanderDestinationFormation.ApplyOffset(
                    waterDestination,
                    shipSlot++,
                    ShipFormationSpacingMeters);
            }
            else
            {
                if (!hasGroundDestination) continue;
                destination = CommanderDestinationFormation.ApplyOffset(
                    groundDestination,
                    groundSlot++,
                    GroundFormationSpacingMeters);
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

    private void IssueAttackOrder(Unit enemyTarget)
    {
        int count = 0;
        for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];
            if (!CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                continue;
            }

            stoppedUnits.Remove(unit);
            CommanderGameAccess.SetUnitHoldPosition(unit, false);
            focusAttackTargets[unit] = enemyTarget;
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);

            if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
            {
                queue.Clear();
            }

            GlobalPosition targetPos = enemyTarget.transform.GlobalPosition();
            playerDestinations[unit] = targetPos;
            CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(targetPos, true);
            count++;
        }
    }

    internal void Tick()
    {
        stoppedUnits.RemoveWhere(static unit => unit == null || unit.disabled);

        // Prune dead waypoint keys
        List<Unit>? deadWaypointKeys = null;
        foreach (KeyValuePair<Unit, Queue<GlobalPosition>> pair in waypointQueues)
        {
            if (pair.Key == null || pair.Key.disabled)
            {
                deadWaypointKeys ??= new List<Unit>();
                deadWaypointKeys.Add(pair.Key);
            }
        }
        if (deadWaypointKeys != null)
        {
            for (int i = 0; i < deadWaypointKeys.Count; i++) waypointQueues.Remove(deadWaypointKeys[i]);
        }

        // Prune dead attack targets
        List<Unit>? deadAttackKeys = null;
        foreach (KeyValuePair<Unit, Unit> pair in focusAttackTargets)
        {
            if (pair.Key == null || pair.Key.disabled || pair.Value == null || pair.Value.disabled)
            {
                deadAttackKeys ??= new List<Unit>();
                deadAttackKeys.Add(pair.Key);
            }
        }
        if (deadAttackKeys != null)
        {
            for (int i = 0; i < deadAttackKeys.Count; i++) focusAttackTargets.Remove(deadAttackKeys[i]);
        }

        // Prune dead patrol routes
        List<Unit>? deadPatrolKeys = null;
        foreach (KeyValuePair<Unit, List<GlobalPosition>> pair in patrolRoutes)
        {
            if (pair.Key == null || pair.Key.disabled)
            {
                deadPatrolKeys ??= new List<Unit>();
                deadPatrolKeys.Add(pair.Key);
            }
        }
        if (deadPatrolKeys != null)
        {
            for (int i = 0; i < deadPatrolKeys.Count; i++)
            {
                patrolRoutes.Remove(deadPatrolKeys[i]);
                patrolIndices.Remove(deadPatrolKeys[i]);
            }
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
                // Check if patrolling
                if (patrolRoutes.TryGetValue(entry.Key, out List<GlobalPosition> route) && route.Count > 1)
                {
                    int currentIndex = patrolIndices.TryGetValue(entry.Key, out int idx) ? idx : 0;
                    int nextIndex = (currentIndex + 1) % route.Count;
                    patrolIndices[entry.Key] = nextIndex;
                    GlobalPosition nextPatrolPos = route[nextIndex];
                    playerDestinations[entry.Key] = nextPatrolPos;
                    CommanderGameAccess.GetUnitCommand(entry.Key)?.SetDestination(nextPatrolPos, true);
                    continue;
                }

                // Check queued waypoints
                if (waypointQueues.TryGetValue(entry.Key, out Queue<GlobalPosition> queue) && queue.Count > 0)
                {
                    GlobalPosition nextWaypoint = queue.Dequeue();
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
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);
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
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);
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

    internal bool TryGetPlayerDestination(Unit unit, out GlobalPosition destination)
    {
        return playerDestinations.TryGetValue(unit, out destination);
    }

    internal bool TryGetQueuedWaypoints(Unit unit, List<GlobalPosition> buffer)
    {
        buffer.Clear();
        if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue) && queue.Count > 0)
        {
            buffer.AddRange(queue);
            return true;
        }
        return false;
    }

    internal bool TryGetFocusAttackTarget(Unit unit, out Unit target)
    {
        return focusAttackTargets.TryGetValue(unit, out target);
    }

    internal bool TryGetPatrolRoute(Unit unit, List<GlobalPosition> buffer)
    {
        buffer.Clear();
        if (patrolRoutes.TryGetValue(unit, out List<GlobalPosition> route) && route.Count > 1)
        {
            buffer.AddRange(route);
            return true;
        }
        return false;
    }

    internal static void NotifyPlayerDestination(UnitCommand command, GlobalPosition waypoint, Player player)
    {
        if (Instance == null || command == null)
        {
            return;
        }

        Unit? unit = command.GetComponent<Unit>();
        if (unit != null)
        {
            Instance.playerDestinations[unit] = waypoint;
        }
    }

    internal void ResetSession()
    {
        stoppedUnits.Clear();
        playerDestinations.Clear();
        waypointQueues.Clear();
        focusAttackTargets.Clear();
        patrolRoutes.Clear();
        patrolIndices.Clear();
        awaitingPatrolSelection = false;
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
