using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

/// <summary>
/// Host-only battle-group cohesion. Control groups the player has already defined are
/// reused as battle groups: a group whose members carry no explicit player order and that
/// has drifted far from the faction base is rallied back so it does not trickle in one
/// vehicle at a time.
/// </summary>
/// <remarks>
/// Deliberately conservative - this never re-tasks a group the player is actively
/// working with. Members carrying a live player destination are left alone, and every
/// issued order goes through <see cref="CommanderGameAccess.TrySetDestination"/>, which
/// is a no-op on a multiplayer client.
/// </remarks>
internal sealed class CommanderBattleGroupService
{
    private const float MinIntervalSeconds = 1f;
    private const float RallyDistanceMeters = 500f;
    private const float RepathDistanceMeters = 30f;
    private const float GroupSpacingMeters = 120f;
    private const float UnitSpacingMeters = 40f;
    private const float MinBearingSqr = 0.01f;

    private readonly List<CommanderControlGroupsService.ControlGroupInfo> groups = new();
    private readonly List<Unit> movableUnits = new();
    private readonly List<int> staleGroups = new();
    private readonly List<int> rallyKeys = new();
    private readonly Dictionary<int, GroupRally> rallies = new();
    private float nextCheckAt;
    private float nextWarnAt;
    private string status = string.Empty;
    private float statusUntil;

    private readonly struct GroupRally
    {
        internal GroupRally(GlobalPosition position, float issuedAt)
        {
            Position = position;
            IssuedAt = issuedAt;
        }

        internal GlobalPosition Position { get; }
        internal float IssuedAt { get; }
    }

    internal string StatusText => Time.unscaledTime <= statusUntil ? status : string.Empty;

    internal void Activate()
    {
        nextCheckAt = CommanderScheduler.Stagger(
            "battlegroup.rally",
            Mathf.Max(MinIntervalSeconds, CommanderSettings.BattleGroupIntervalSeconds),
            1f);
        status = string.Empty;
    }

    internal void Deactivate()
    {
        ResetSession();
    }

    internal void ResetSession()
    {
        groups.Clear();
        movableUnits.Clear();
        staleGroups.Clear();
        rallyKeys.Clear();
        rallies.Clear();
        nextCheckAt = 0f;
        status = string.Empty;
    }

    internal void Tick()
    {
        if (!CommanderSettings.BattleGroupOrdersEnabled
            || !CommanderFeatureGate.AdvancedFeaturesEnabled
            || Time.unscaledTime < nextCheckAt)
        {
            return;
        }

        nextCheckAt = Time.unscaledTime + Mathf.Max(MinIntervalSeconds, CommanderSettings.BattleGroupIntervalSeconds);

        if (GameManager.gameState == GameState.Multiplayer && !CommanderHostAuthority.IsHostAuthority())
        {
            return;
        }

        CommanderControlGroupsService? controlGroups = CommanderControlGroupsService.Instance;
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (controlGroups == null || localHq == null)
        {
            return;
        }

        PruneDeadReferences(controlGroups);

        controlGroups.GetAllActiveGroups(groups);
        if (groups.Count == 0)
        {
            return;
        }

        Vector3 rallyCenter = localHq.transform.position;

        for (int i = 0; i < groups.Count; i++)
        {
            CommanderControlGroupsService.ControlGroupInfo group = groups[i];
            if (group.Count < CommanderSettings.BattleGroupMinUnits)
            {
                continue;
            }

            IReadOnlyList<Unit>? members = controlGroups.GetGroupUnits(group.Index);
            if (members == null || members.Count == 0)
            {
                continue;
            }

            CollectRallyCandidates(members);
            if (movableUnits.Count == 0)
            {
                // Every member is under an explicit player order: hands off.
                continue;
            }

            Vector3 centroid = ComputeCentroid(movableUnits);
            if (CommanderGameAccess.HorizontalDistance(centroid, rallyCenter) < RallyDistanceMeters)
            {
                continue;
            }

            Vector3 approach = rallyCenter - centroid;
            approach.y = 0f;
            if (approach.sqrMagnitude <= MinBearingSqr)
            {
                continue;
            }

            // Fan rally slots apart so groups do not stack on the same point.
            Vector3 lateral = new Vector3(-approach.z, 0f, approach.x).normalized;
            Vector3 rallyPoint = rallyCenter + lateral * (GroupSpacingMeters * group.Index);
            if (!TryClaimRally(group.Index, rallyPoint.ToGlobalPosition()))
            {
                continue;
            }

            if (RallyGroup(movableUnits, rallyPoint))
            {
                WarnOnce($"[BATTLEGROUP] Control group {group.Index} ({group.Count}) rallied to base.");
            }
        }
    }

    private void CollectRallyCandidates(IReadOnlyList<Unit> members)
    {
        movableUnits.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            Unit unit = members[i];
            if (unit == null || unit.disabled || !CommanderGameAccess.ShouldAllowCommanderMove(unit))
            {
                continue;
            }

            // Explicit player orders win: never rally a group the player is moving.
            if (CommanderMoveService.Instance?.HasActivePlayerDestination(unit) == true)
            {
                continue;
            }

            movableUnits.Add(unit);
        }
    }

    private static Vector3 ComputeCentroid(List<Unit> units)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < units.Count; i++)
        {
            sum += units[i].transform.position;
        }

        return sum / units.Count;
    }

    private bool TryClaimRally(int groupIndex, GlobalPosition desired)
    {
        if (rallies.TryGetValue(groupIndex, out GroupRally existing))
        {
            bool drift = FastMath.Distance(existing.Position, desired) > RepathDistanceMeters;
            float sinceIssue = Time.unscaledTime - existing.IssuedAt;
            if (!drift && sinceIssue < CommanderSettings.BattleGroupReissueSeconds)
            {
                return false;
            }
        }

        rallies[groupIndex] = new GroupRally(desired, Time.unscaledTime);
        return true;
    }

    private bool RallyGroup(List<Unit> units, Vector3 rallyPoint)
    {
        Vector3 lateral = new Vector3(-rallyPoint.z, 0f, rallyPoint.x);
        lateral.y = 0f;
        if (lateral.sqrMagnitude > MinBearingSqr)
        {
            lateral.Normalize();
        }

        int issued = 0;
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            float row = (i - (units.Count - 1) * 0.5f) * UnitSpacingMeters;
            Vector3 target = rallyPoint + lateral * row;

            CommanderGameAccess.SetUnitHoldPosition(unit, false);
            if (CommanderGameAccess.TrySetDestination(unit, target.ToGlobalPosition()))
            {
                issued++;
            }
        }

        return issued > 0;
    }

    private void PruneDeadReferences(CommanderControlGroupsService controlGroups)
    {
        // A rally record only matters while its control group still exists.
        staleGroups.Clear();
        rallyKeys.Clear();
        foreach (int key in rallies.Keys)
        {
            rallyKeys.Add(key);
        }

        for (int i = 0; i < rallyKeys.Count; i++)
        {
            if (!controlGroups.HasGroup(rallyKeys[i]))
            {
                staleGroups.Add(rallyKeys[i]);
            }
        }

        for (int i = 0; i < staleGroups.Count; i++)
        {
            rallies.Remove(staleGroups[i]);
        }
    }

    private void WarnOnce(string message)
    {
        if (Time.unscaledTime < nextWarnAt)
        {
            return;
        }

        nextWarnAt = Time.unscaledTime + CommanderSettings.BattleGroupWarnIntervalSeconds;
        CommanderAlertService.PostTickerEvent(message, new Color(0.45f, 0.8f, 1f, 0.95f));
        SetStatus(message);
    }

    private void SetStatus(string text)
    {
        status = text;
        statusUntil = Time.unscaledTime + 4f;
    }
}
