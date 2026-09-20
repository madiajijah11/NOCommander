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

    private static readonly MethodInfo? RearmVehicleWaitMethod = AccessTools.Method(typeof(RearmVehicleAI), "Wait");
    private static readonly MethodInfo? RearmVehicleRestockMethod = AccessTools.Method(typeof(RearmVehicleAI), "DriveToRestock");

    private readonly CommanderSelectionService selectionService;
    private readonly HashSet<Unit> stoppedUnits = new();
    private readonly Dictionary<Unit, GlobalPosition> playerDestinations = new();
    private readonly Dictionary<Unit, Queue<GlobalPosition>> waypointQueues = new();
    private readonly Dictionary<Unit, Unit> focusAttackTargets = new();
    private readonly Dictionary<Unit, Unit> guardTargets = new();
    private readonly Dictionary<Unit, List<GlobalPosition>> patrolRoutes = new();
    private readonly Dictionary<Unit, int> patrolIndices = new();
    private readonly HashSet<Unit> autoRtbUnits = new();

    private bool awaitingPatrolSelection;
    private bool awaitingGuardSelection;
    private bool awaitingBarrageSelection;
    private bool awaitingAttackMoveSelection;
    private FormationShape currentFormation = FormationShape.Ring;
    private float nextRtbCheckTime;

    internal static CommanderMoveService? Instance { get; private set; }

    internal bool AwaitingPatrolSelection => awaitingPatrolSelection;
    internal bool AwaitingGuardSelection => awaitingGuardSelection;
    internal bool AwaitingBarrageSelection => awaitingBarrageSelection;
    internal bool AwaitingAttackMoveSelection => awaitingAttackMoveSelection;
    internal FormationShape CurrentFormation => currentFormation;

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

    internal void CycleFormation()
    {
        currentFormation = (FormationShape)(((int)currentFormation + 1) % 6);
    }

    internal void SetFormation(FormationShape shape)
    {
        currentFormation = shape;
    }

    internal void BeginPatrolOrder()
    {
        if (!HasCommandableSelection) return;
        awaitingPatrolSelection = true;
        awaitingGuardSelection = false;
        awaitingBarrageSelection = false;
        awaitingAttackMoveSelection = false;
    }

    internal void CancelPatrolOrder()
    {
        awaitingPatrolSelection = false;
    }

    internal void BeginGuardOrder()
    {
        if (!HasCommandableSelection) return;
        awaitingGuardSelection = true;
        awaitingPatrolSelection = false;
        awaitingBarrageSelection = false;
        awaitingAttackMoveSelection = false;
    }

    internal void CancelGuardOrder()
    {
        awaitingGuardSelection = false;
    }

    internal void BeginBarrageOrder()
    {
        if (!HasCommandableSelection) return;
        awaitingBarrageSelection = true;
        awaitingPatrolSelection = false;
        awaitingGuardSelection = false;
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
        awaitingPatrolSelection = false;
        awaitingGuardSelection = false;
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

    internal bool TrySetGuardTarget(Vector2 screenPosition)
    {
        if (!awaitingGuardSelection || selectionService.SelectedUnits.Count == 0)
        {
            return false;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (CommanderGameAccess.TryRaycastSelectableUnit(screenPosition, out Unit targetUnit)
            && targetUnit != null
            && !targetUnit.disabled
            && localHq != null
            && CommanderGameAccess.IsFriendlyUnit(targetUnit, localHq))
        {
            for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
            {
                Unit unit = selectionService.SelectedUnits[i];
                if (ReferenceEquals(unit, targetUnit) || !CommanderGameAccess.ShouldAllowCommanderMove(unit))
                {
                    continue;
                }

                stoppedUnits.Remove(unit);
                focusAttackTargets.Remove(unit);
                patrolRoutes.Remove(unit);
                patrolIndices.Remove(unit);
                guardTargets[unit] = targetUnit;
                CommanderGameAccess.SetUnitHoldPosition(unit, false);
            }
            awaitingGuardSelection = false;
            return true;
        }

        awaitingGuardSelection = false;
        return false;
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

            stoppedUnits.Remove(unit);
            focusAttackTargets.Remove(unit);
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);
            guardTargets.Remove(unit);

            playerDestinations[unit] = targetPos;
            CommanderGameAccess.SetUnitHoldPosition(unit, false);
            CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(targetPos, true);
        }

        awaitingBarrageSelection = false;
        return true;
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
                ? CommanderDestinationFormation.ApplyOffset(waterPos, shipSlot++, ShipFormationSpacingMeters, currentFormation)
                : CommanderDestinationFormation.ApplyOffset(groundPos, groundSlot++, GroundFormationSpacingMeters, currentFormation);

            GlobalPosition startPos = unit.transform.GlobalPosition();
            patrolRoutes[unit] = new List<GlobalPosition> { startPos, targetPos };
            patrolIndices[unit] = 1;

            stoppedUnits.Remove(unit);
            focusAttackTargets.Remove(unit);
            guardTargets.Remove(unit);
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
            guardTargets.Remove(unit);
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

    internal void ToggleAutoRtbForSelection()
    {
        IReadOnlyList<Unit> selected = selectionService.SelectedUnits;
        if (selected.Count == 0) return;

        bool anyEnabled = false;
        for (int i = 0; i < selected.Count; i++)
        {
            if (autoRtbUnits.Contains(selected[i]))
            {
                anyEnabled = true;
                break;
            }
        }

        bool newState = !anyEnabled;
        for (int i = 0; i < selected.Count; i++)
        {
            if (newState) autoRtbUnits.Add(selected[i]);
            else autoRtbUnits.Remove(selected[i]);
        }
    }

    internal bool IsAutoRtb(Unit? unit)
    {
        return unit != null && autoRtbUnits.Contains(unit);
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
            guardTargets.Remove(unit);
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);

            GlobalPosition destination = unit is Ship
                ? CommanderDestinationFormation.ApplyOffset(targetPosition, shipSlot++, ShipFormationSpacingMeters, currentFormation)
                : CommanderDestinationFormation.ApplyOffset(targetPosition, groundSlot++, GroundFormationSpacingMeters, currentFormation);

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

        if (awaitingGuardSelection)
        {
            TrySetGuardTarget(screenPosition);
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
            else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                // Alt + RMB on friendly unit -> Guard / Escort
                for (int i = 0; i < selectionService.SelectedUnits.Count; i++)
                {
                    Unit unit = selectionService.SelectedUnits[i];
                    if (!ReferenceEquals(unit, targetUnit) && CommanderGameAccess.ShouldAllowCommanderMove(unit))
                    {
                        stoppedUnits.Remove(unit);
                        focusAttackTargets.Remove(unit);
                        patrolRoutes.Remove(unit);
                        patrolIndices.Remove(unit);
                        guardTargets[unit] = targetUnit;
                        CommanderGameAccess.SetUnitHoldPosition(unit, false);
                    }
                }
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
            guardTargets.Remove(unit);
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);

            GlobalPosition destination = unit is Ship
                ? CommanderDestinationFormation.ApplyOffset(waterDestination, shipSlot++, ShipFormationSpacingMeters, currentFormation)
                : CommanderDestinationFormation.ApplyOffset(groundDestination, groundSlot++, GroundFormationSpacingMeters, currentFormation);

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
            guardTargets.Remove(unit);
            patrolRoutes.Remove(unit);
            patrolIndices.Remove(unit);

            if (waypointQueues.TryGetValue(unit, out Queue<GlobalPosition> queue))
            {
                queue.Clear();
            }

            GlobalPosition targetPos = enemyTarget.transform.GlobalPosition();
            playerDestinations[unit] = targetPos;
            CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(targetPos, true);
        }
    }

    internal void Tick()
    {
        stoppedUnits.RemoveWhere(static unit => unit == null || unit.disabled);
        autoRtbUnits.RemoveWhere(static unit => unit == null || unit.disabled);

        // Prune dead collections
        PruneDeadReferences();

        // Update Guard / Escort positions
        foreach (KeyValuePair<Unit, Unit> pair in guardTargets)
        {
            if (pair.Key != null && !pair.Key.disabled && pair.Value != null && !pair.Value.disabled)
            {
                float dist = Vector3.Distance(pair.Key.transform.position, pair.Value.transform.position);
                if (dist > GuardFollowThreshold)
                {
                    GlobalPosition guardPos = pair.Value.transform.GlobalPosition();
                    playerDestinations[pair.Key] = guardPos;
                    CommanderGameAccess.GetUnitCommand(pair.Key)?.SetDestination(guardPos, true);
                }
            }
        }

        // Periodic Auto-RTB check (every 3 seconds)
        if (Time.unscaledTime >= nextRtbCheckTime)
        {
            nextRtbCheckTime = Time.unscaledTime + 3f;
            CheckAutoRtb();
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
                    GlobalPosition nextPatrolPoint = route[nextIndex];
                    playerDestinations[entry.Key] = nextPatrolPoint;
                    CommanderGameAccess.GetUnitCommand(entry.Key)?.SetDestination(nextPatrolPoint, true);
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

    private void CheckAutoRtb()
    {
        if (autoRtbUnits.Count == 0) return;

        foreach (Unit unit in autoRtbUnits)
        {
            if (unit == null || unit.disabled) continue;

            bool needsService = false;

            // Check damage
            IRepairable[] repairables = unit.GetComponentsInChildren<IRepairable>(true);
            for (int r = 0; r < repairables.Length; r++)
            {
                if (repairables[r] != null && repairables[r].NeedsRepair())
                {
                    needsService = true;
                    break;
                }
            }

            // Check ammo
            if (!needsService && unit.weaponStations != null && unit.weaponStations.Count > 0)
            {
                float currentAmmo = 0f;
                float maxAmmo = 0f;
                for (int s = 0; s < unit.weaponStations.Count; s++)
                {
                    WeaponStation station = unit.weaponStations[s];
                    if (station?.Weapons == null) continue;
                    for (int w = 0; w < station.Weapons.Count; w++)
                    {
                        Weapon weapon = station.Weapons[w];
                        if (weapon != null)
                        {
                            currentAmmo += weapon.ammo;
                            maxAmmo += Mathf.Max(1, weapon.GetFullAmmo());
                        }
                    }
                }
                if (maxAmmo > 0f && (currentAmmo / maxAmmo) <= 0.2f)
                {
                    needsService = true;
                }
            }

            if (needsService)
            {
                Unit? nearestLogistics = FindNearestLogistics(unit);
                if (nearestLogistics != null)
                {
                    GlobalPosition dest = nearestLogistics.transform.GlobalPosition();
                    playerDestinations[unit] = dest;
                    CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(dest, true);
                }
            }
        }
    }

    private static Unit? FindNearestLogistics(Unit unit)
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq?.factionUnits == null) return null;

        Unit? nearest = null;
        float minDist = float.MaxValue;
        Vector3 pos = unit.transform.position;

        foreach (PersistentID id in hq.factionUnits)
        {
            if (id.TryGetUnit(out Unit u) && u != null && !u.disabled && !ReferenceEquals(u, unit))
            {
                if (u.GetComponentInChildren<Rearmer>(true) != null || u.GetComponentInChildren<Repairer>(true) != null)
                {
                    float d = Vector3.Distance(pos, u.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        nearest = u;
                    }
                }
            }
        }
        return nearest;
    }

    internal bool TryGetFocusAttackTarget(Unit unit, out Unit target)
    {
        return focusAttackTargets.TryGetValue(unit, out target!) && target != null && !target.disabled;
    }

    internal bool TryGetGuardTarget(Unit unit, out Unit target)
    {
        return guardTargets.TryGetValue(unit, out target!) && target != null && !target.disabled;
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

    internal bool TryGetPatrolRoute(Unit unit, List<GlobalPosition> buffer)
    {
        buffer.Clear();
        if (!patrolRoutes.TryGetValue(unit, out List<GlobalPosition> route) || route.Count < 2)
        {
            return false;
        }

        for (int i = 0; i < route.Count; i++)
        {
            buffer.Add(route[i]);
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
            guardTargets.Remove(unit);
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
            guardTargets.Remove(unit);
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

    internal void PruneDeadReferences()
    {
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

        List<Unit>? deadGuardKeys = null;
        foreach (KeyValuePair<Unit, Unit> pair in guardTargets)
        {
            if (pair.Key == null || pair.Key.disabled || pair.Value == null || pair.Value.disabled)
            {
                deadGuardKeys ??= new List<Unit>();
                deadGuardKeys.Add(pair.Key);
            }
        }
        if (deadGuardKeys != null)
        {
            for (int i = 0; i < deadGuardKeys.Count; i++) guardTargets.Remove(deadGuardKeys[i]);
        }

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
    }

    internal void ResetSession()
    {
        stoppedUnits.Clear();
        playerDestinations.Clear();
        waypointQueues.Clear();
        focusAttackTargets.Clear();
        guardTargets.Clear();
        patrolRoutes.Clear();
        patrolIndices.Clear();
        autoRtbUnits.Clear();
        awaitingPatrolSelection = false;
        awaitingGuardSelection = false;
        awaitingBarrageSelection = false;
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
