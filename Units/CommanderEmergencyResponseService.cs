using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

/// <summary>
/// Host-only emergency response service. When hostile artillery or heavy ordnance
/// is pinpointed by counter-battery radar, nearby unassigned friendly combat units
/// are dispatched to contain and suppress the firing origin.
/// </summary>
/// <remarks>
/// Player-assigned destinations always take precedence. Autonomous movement is issued
/// exclusively via <see cref="CommanderGameAccess.TrySetDestination"/>, which is host-gated.
/// Doctrine policies are respected: standoff/SAM units hold at safe range; frontline units
/// advance to perimeter.
/// </remarks>
internal sealed class CommanderEmergencyResponseService
{
    private const float MinIntervalSeconds = 1f;
    private const float RepathDistanceMeters = 30f;
    private const float StandoffPerimeterMeters = 350f;
    private const float FrontlinePerimeterMeters = 150f;
    private const float AssignmentReissueSeconds = 15f;
    private const float MinBearingSqr = 0.01f;

    private readonly List<Unit> friendlyUnits = new();
    private readonly List<Unit> responders = new();
    private readonly List<Unit> staleUnits = new();
    private readonly Dictionary<Unit, EmergencyAssignment> assignments = new();
    private float nextCheckAt;
    private float nextWarnAt;
    private string status = string.Empty;
    private float statusUntil;

    private readonly struct EmergencyAssignment
    {
        internal readonly GlobalPosition Position;
        internal readonly float IssuedAt;

        internal EmergencyAssignment(GlobalPosition position, float issuedAt)
        {
            Position = position;
            IssuedAt = issuedAt;
        }
    }

    internal static CommanderEmergencyResponseService? Instance { get; private set; }
    internal string StatusText => Time.unscaledTime <= statusUntil ? status : string.Empty;

    internal CommanderEmergencyResponseService()
    {
        Instance = this;
    }

    internal void Activate()
    {
        nextCheckAt = CommanderScheduler.Stagger("emergency.response", CommanderSettings.EmergencyResponseIntervalSeconds, 1f);
    }

    internal void Deactivate()
    {
        ResetSession();
    }

    internal void ResetSession()
    {
        friendlyUnits.Clear();
        responders.Clear();
        staleUnits.Clear();
        assignments.Clear();
        nextCheckAt = 0f;
        nextWarnAt = 0f;
        status = string.Empty;
        statusUntil = 0f;
    }

    internal void Tick()
    {
        if (!CommanderSettings.EmergencyResponseEnabled
            || !CommanderFeatureGate.AdvancedFeaturesEnabled
            || Time.unscaledTime < nextCheckAt)
        {
            return;
        }

        nextCheckAt = Time.unscaledTime + Mathf.Max(MinIntervalSeconds, CommanderSettings.EmergencyResponseIntervalSeconds);

        if (GameManager.gameState == GameState.Multiplayer && !CommanderHostAuthority.IsHostAuthority())
        {
            return;
        }

        IReadOnlyList<CommanderCounterBatteryRadarService.CounterBatteryPing>? pings =
            CommanderCounterBatteryRadarService.Instance?.ActivePings;
        if (pings == null || pings.Count == 0)
        {
            if (assignments.Count > 0)
            {
                assignments.Clear();
            }
            return;
        }

        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null)
        {
            return;
        }

        PruneDeadReferences();

        friendlyUnits.Clear();
        CommanderGameAccess.CollectFriendlySurfaceUnits(friendlyUnits);
        if (friendlyUnits.Count == 0)
        {
            return;
        }

        float maxRadius = CommanderSettings.EmergencyResponseRadiusMeters;
        int minUnits = Mathf.Max(1, CommanderSettings.EmergencyResponseMinUnits);

        // Process the most recent active ping
        for (int p = pings.Count - 1; p >= 0; p--)
        {
            CommanderCounterBatteryRadarService.CounterBatteryPing ping = pings[p];
            if (ping.SourceUnit == null || ping.SourceUnit.disabled)
            {
                continue;
            }

            Vector3 pingPos = ping.Position;
            responders.Clear();

            for (int i = 0; i < friendlyUnits.Count; i++)
            {
                Unit candidate = friendlyUnits[i];
                if (candidate == null || candidate.disabled)
                {
                    continue;
                }

                // Never override live player orders
                if (CommanderMoveService.Instance?.HasActivePlayerDestination(candidate) == true)
                {
                    continue;
                }

                // Respect doctrine: only combat units respond (not Logistics or Recon)
                CommanderDoctrineRole role = CommanderDoctrineService.Classify(candidate);
                if (role == CommanderDoctrineRole.Logistics || role == CommanderDoctrineRole.Recon)
                {
                    continue;
                }

                float dist = CommanderGameAccess.HorizontalDistance(candidate.transform.position, pingPos);
                if (dist <= maxRadius)
                {
                    responders.Add(candidate);
                }
            }

            if (responders.Count < minUnits)
            {
                continue;
            }

            // Dispatch responders to perimeter based on doctrine
            bool dispatchedAny = false;
            for (int i = 0; i < responders.Count; i++)
            {
                Unit unit = responders[i];
                CommanderDoctrinePolicy policy = CommanderDoctrineService.GetPolicy(CommanderDoctrineService.Classify(unit));

                Vector3 unitPos = unit.transform.position;
                Vector3 toThreat = pingPos - unitPos;
                toThreat.y = 0f;

                float perimeter = policy.PreferredStandoffMeters > 0f
                    ? Mathf.Max(StandoffPerimeterMeters, policy.PreferredStandoffMeters)
                    : FrontlinePerimeterMeters;

                Vector3 targetPos;
                if (toThreat.sqrMagnitude > MinBearingSqr)
                {
                    Vector3 threatDir = toThreat.normalized;
                    targetPos = pingPos - threatDir * perimeter;
                }
                else
                {
                    targetPos = pingPos;
                }

                GlobalPosition desired = targetPos.ToGlobalPosition();
                if (TryClaimAssignment(unit, desired))
                {
                    CommanderGameAccess.SetUnitHoldPosition(unit, true);
                    CommanderGameAccess.TrySetDestination(unit, desired);
                    dispatchedAny = true;
                }
            }

            if (dispatchedAny)
            {
                SetStatus($"EMERGENCY RESPONSE | {responders.Count} units dispatched to suppress counter-battery origin");
                WarnOnce($"Emergency response team dispatched to contain hostile artillery at ({Mathf.RoundToInt(pingPos.x)}, {Mathf.RoundToInt(pingPos.z)}).");
                break; // One active emergency response operation per cycle is enough
            }
        }
    }

    private bool TryClaimAssignment(Unit unit, GlobalPosition desired)
    {
        float now = Time.unscaledTime;
        if (assignments.TryGetValue(unit, out EmergencyAssignment existing))
        {
            float drifted = FastMath.Distance(existing.Position, desired);
            if (drifted < RepathDistanceMeters && now - existing.IssuedAt < AssignmentReissueSeconds)
            {
                return false;
            }
        }

        assignments[unit] = new EmergencyAssignment(desired, now);
        return true;
    }

    private void PruneDeadReferences()
    {
        staleUnits.Clear();
        foreach (KeyValuePair<Unit, EmergencyAssignment> entry in assignments)
        {
            if (entry.Key == null || entry.Key.disabled)
            {
                staleUnits.Add(entry.Key!);
            }
        }

        for (int i = 0; i < staleUnits.Count; i++)
        {
            assignments.Remove(staleUnits[i]);
        }
        staleUnits.Clear();
    }

    private void WarnOnce(string message)
    {
        if (Time.unscaledTime < nextWarnAt)
        {
            return;
        }

        nextWarnAt = Time.unscaledTime + Mathf.Max(MinIntervalSeconds, CommanderSettings.EmergencyResponseWarnIntervalSeconds);
        CommanderAlertService.PostTickerEvent($"[EMERGENCY] {message}", new Color(1f, 0.45f, 0.2f, 0.95f));
    }

    private void SetStatus(string text)
    {
        status = text;
        statusUntil = Time.unscaledTime + 3f;
    }
}
