using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

/// <summary>
/// Host-only objective defense. When a friendly objective comes under known hostile
/// contact, nearby friendly surface units are pulled to a standoff ring facing the
/// threat instead of being left to idle at their current waypoints.
/// </summary>
/// <remarks>
/// Host-authoritative by construction: every decision here ends in
/// <see cref="CommanderGameAccess.TrySetDestination"/>, which refuses to write a unit
/// command from a multiplayer client. Client peers only see the resulting unit moves.
/// Explicit player orders always win - a unit that already carries a player destination
/// is never second-guessed here.
/// </remarks>
internal sealed class CommanderObjectiveDefenseService
{
    private const float MinIntervalSeconds = 1f;
    private const float RepathDistanceMeters = 25f;
    private const float MinThreatBearingSqr = 0.01f;
    private const float StandoffFractionOfCaptureRange = 0.7f;

    private readonly List<Unit> friendlyUnits = new();
    private readonly List<Unit> staleAssignments = new();
    private readonly List<Unit> siteDefenders = new();
    private readonly Dictionary<Unit, DefenseAssignment> assignments = new();
    private float nextCheckAt;
    private float nextWarnAt;
    private string status = string.Empty;
    private float statusUntil;

    private readonly struct DefenseAssignment
    {
        internal DefenseAssignment(GlobalPosition position, float issuedAt)
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
            "objective.defense",
            Mathf.Max(MinIntervalSeconds, CommanderSettings.ObjectiveDefenseIntervalSeconds),
            1f);
        status = string.Empty;
    }

    internal void Deactivate()
    {
        ResetSession();
    }

    internal void ResetSession()
    {
        friendlyUnits.Clear();
        staleAssignments.Clear();
        siteDefenders.Clear();
        assignments.Clear();
        nextCheckAt = 0f;
        status = string.Empty;
    }

    internal void Tick()
    {
        if (!CommanderSettings.ObjectiveDefenseEnabled
            || !CommanderFeatureGate.AdvancedFeaturesEnabled
            || Time.unscaledTime < nextCheckAt)
        {
            return;
        }

        nextCheckAt = Time.unscaledTime + Mathf.Max(MinIntervalSeconds, CommanderSettings.ObjectiveDefenseIntervalSeconds);

        // Autopilot orders are orders: only the authority that owns the unit may issue them.
        if (GameManager.gameState == GameState.Multiplayer && !CommanderHostAuthority.IsHostAuthority())
        {
            return;
        }

        PruneDeadReferences();

        CommanderRadarService? radar = CommanderRadarService.Instance;
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (radar == null || localHq == null)
        {
            return;
        }

        CommanderGameAccess.CollectFriendlySurfaceUnits(friendlyUnits);
        if (friendlyUnits.Count == 0)
        {
            return;
        }

        float threatRadius = CommanderSettings.ObjectiveDefenseThreatRadiusMeters;
        bool anyEngaged = false;
        foreach (Airbase airbase in localHq.GetAirbases())
        {
            if (airbase == null || airbase.disabled || !ReferenceEquals(airbase.CurrentHQ, localHq))
            {
                continue;
            }

            Transform? center = airbase.center != null ? airbase.center : airbase.transform;
            if (center == null)
            {
                continue;
            }

            Vector3 objectivePosition = center.position;
            if (!radar.TryGetNearestThreat(objectivePosition.ToGlobalPosition(), out GlobalPosition threatPosition, threatRadius))
            {
                ReleaseDefendersNear(objectivePosition);
                continue;
            }

            if (DefendSite(airbase, objectivePosition, threatPosition.ToLocalPosition()))
            {
                anyEngaged = true;
            }
        }

        if (anyEngaged)
        {
            SetStatus("Objective defense holding against a detected threat.");
        }
    }

    private bool DefendSite(Airbase airbase, Vector3 objectivePosition, Vector3 threatPosition)
    {
        // CaptureRange is an explicit ICapturable implementation on Airbase, not a public member.
        float captureRange = Mathf.Max(1f, ((ICapturable)airbase).CaptureRange);
        float radius = Mathf.Min(CommanderSettings.ObjectiveDefenseRadiusMeters, captureRange * 3f);
        int maxDefenders = Mathf.Max(0, CommanderSettings.ObjectiveDefenseMaxUnitsPerSite);

        siteDefenders.Clear();
        for (int i = 0; i < friendlyUnits.Count && siteDefenders.Count < maxDefenders; i++)
        {
            Unit candidate = friendlyUnits[i];
            if (candidate == null || candidate.disabled)
            {
                continue;
            }

            // Never override an explicit player order.
            if (CommanderMoveService.Instance?.HasActivePlayerDestination(candidate) == true)
            {
                continue;
            }

            if (CommanderGameAccess.HorizontalDistance(candidate.transform.position, objectivePosition) > radius)
            {
                continue;
            }

            siteDefenders.Add(candidate);
        }

        if (siteDefenders.Count == 0)
        {
            return false;
        }

        Vector3 bearing = objectivePosition - threatPosition;
        bearing.y = 0f;
        if (bearing.sqrMagnitude <= MinThreatBearingSqr)
        {
            return false;
        }

        Vector3 standoffDirection = bearing.normalized;
        float standoffMeters = captureRange * StandoffFractionOfCaptureRange;
        bool issued = false;
        for (int i = 0; i < siteDefenders.Count; i++)
        {
            Unit defender = siteDefenders[i];
            float lane = (i - (siteDefenders.Count - 1) * 0.5f) * 60f;
            Vector3 laneOffset = new Vector3(-standoffDirection.z, 0f, standoffDirection.x) * lane;
            Vector3 holdPosition = objectivePosition + standoffDirection * standoffMeters + laneOffset;

            if (!TryClaimAssignment(defender, holdPosition.ToGlobalPosition()))
            {
                continue;
            }

            CommanderGameAccess.SetUnitHoldPosition(defender, true);
            if (CommanderGameAccess.TrySetDestination(defender, holdPosition.ToGlobalPosition()))
            {
                issued = true;
            }
        }

        if (issued)
        {
            WarnOnce($"{airbase.name} is under contact. Friendly units are holding a defense line.");
        }

        return issued;
    }

    private bool TryClaimAssignment(Unit defender, GlobalPosition desired)
    {
        if (assignments.TryGetValue(defender, out DefenseAssignment existing))
        {
            float sinceIssue = Time.unscaledTime - existing.IssuedAt;
            bool drift = FastMath.Distance(existing.Position, desired) > RepathDistanceMeters;
            if (!drift && sinceIssue < CommanderSettings.ObjectiveDefenseReissueSeconds)
            {
                return false;
            }
        }

        assignments[defender] = new DefenseAssignment(desired, Time.unscaledTime);
        return true;
    }

    private void ReleaseDefendersNear(Vector3 objectivePosition)
    {
        // Site is quiet again: let the assignments age out so the unit is free to be
        // reassigned. No immediate order is issued, the unit keeps doing what it was doing.
        foreach (KeyValuePair<Unit, DefenseAssignment> entry in assignments)
        {
            Unit unit = entry.Key;
            if (unit == null || unit.disabled)
            {
                continue;
            }

            if (CommanderGameAccess.HorizontalDistance(unit.transform.position, objectivePosition) > CommanderSettings.ObjectiveDefenseReleaseMeters)
            {
                staleAssignments.Add(unit);
            }
        }

        for (int i = 0; i < staleAssignments.Count; i++)
        {
            assignments.Remove(staleAssignments[i]);
        }

        staleAssignments.Clear();
    }

    private void PruneDeadReferences()
    {
        foreach (KeyValuePair<Unit, DefenseAssignment> entry in assignments)
        {
            if (entry.Key == null || entry.Key.disabled)
            {
                staleAssignments.Add(entry.Key!);
            }
        }

        for (int i = 0; i < staleAssignments.Count; i++)
        {
            assignments.Remove(staleAssignments[i]);
        }

        staleAssignments.Clear();
    }

    private void WarnOnce(string message)
    {
        if (Time.unscaledTime < nextWarnAt)
        {
            return;
        }

        nextWarnAt = Time.unscaledTime + CommanderSettings.ObjectiveDefenseWarnIntervalSeconds;
        CommanderAlertService.PostTickerEvent($"[DEFENSE] {message}", new Color(1f, 0.7f, 0.25f, 0.95f));
        SetStatus(message);
    }

    private void SetStatus(string text)
    {
        status = text;
        statusUntil = Time.unscaledTime + 4f;
    }
}