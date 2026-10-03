using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

public enum FrontlineDirectiveMode
{
    Balanced = 0,
    AggressivePush = 1,
    DefensiveHold = 2,
    TacticalRetreat = 3
}

/// <summary>
/// Autonomous high-command frontline driver (Host-Authoritative).
/// Computes frontline center-of-mass between friendly ground combat units and known enemy contacts,
/// then issues coordinated push, screen, or defensive hold waypoints to unmanaged frontline units.
/// </summary>
internal sealed class CommanderFrontlineDirectiveService
{
    internal static CommanderFrontlineDirectiveService? Instance { get; private set; }

    private const float MinIntervalSeconds = 2f;
    private const float RepathDistanceMeters = 75f;

    private readonly List<Unit> friendlyCombatants = new();
    private readonly List<Unit> tempPruneList = new();
    private readonly Dictionary<Unit, float> lastOrderTimes = new();

    private float nextEvaluationTime;
    private Vector3 frontlineCentroid;
    private Vector3 opposingCentroid;
    private bool hasActiveFrontline;

    internal FrontlineDirectiveMode CurrentMode { get; set; } = FrontlineDirectiveMode.Balanced;
    internal bool HasActiveFrontline => hasActiveFrontline;
    internal Vector3 FrontlineCentroid => frontlineCentroid;

    internal CommanderFrontlineDirectiveService()
    {
        Instance = this;
    }

    internal void Tick()
    {
        if (!CommanderSettings.FrontlineDirectivesEnabled
            || !CommanderFeatureGate.AdvancedFeaturesEnabled
            || Time.unscaledTime < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = Time.unscaledTime + Mathf.Max(MinIntervalSeconds, CommanderSettings.FrontlineDirectiveIntervalSeconds);

        if (GameManager.gameState == GameState.Multiplayer && !CommanderHostAuthority.IsHostAuthority())
        {
            return;
        }

        PruneDeadReferences();
        EvaluateAndDriveFrontline();
    }

    private void EvaluateAndDriveFrontline()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderRadarService? radar = CommanderRadarService.Instance;
        if (localHq == null || radar == null) return;

        friendlyCombatants.Clear();
        CommanderGameAccess.CollectFriendlySurfaceUnits(friendlyCombatants);

        if (friendlyCombatants.Count == 0)
        {
            hasActiveFrontline = false;
            return;
        }

        // Calculate friendly center of mass (ground combat vehicles only)
        Vector3 friendlySum = Vector3.zero;
        int friendlyCount = 0;
        for (int i = 0; i < friendlyCombatants.Count; i++)
        {
            Unit u = friendlyCombatants[i];
            if (u == null || u.disabled || u is not GroundVehicle) continue;
            friendlySum += u.transform.position;
            friendlyCount++;
        }

        if (friendlyCount == 0)
        {
            hasActiveFrontline = false;
            return;
        }

        Vector3 friendlyCenter = friendlySum / friendlyCount;

        // Calculate known enemy threat center of mass from radar
        IReadOnlyList<Unit> threats = radar.ThreatUnits;
        Vector3 enemySum = Vector3.zero;
        int enemyCount = 0;

        for (int i = 0; i < threats.Count; i++)
        {
            Unit t = threats[i];
            if (t == null || t.disabled) continue;
            enemySum += t.transform.position;
            enemyCount++;
        }

        if (enemyCount == 0)
        {
            hasActiveFrontline = false;
            return;
        }

        opposingCentroid = enemySum / enemyCount;
        frontlineCentroid = (friendlyCenter + opposingCentroid) * 0.5f;
        hasActiveFrontline = true;

        DriveUnitsAlongDirective(friendlyCenter, opposingCentroid);
    }

    private void DriveUnitsAlongDirective(Vector3 friendlyCenter, Vector3 enemyCenter)
    {
        Vector3 advanceDirection = (enemyCenter - friendlyCenter).normalized;
        if (advanceDirection.sqrMagnitude < 0.01f)
        {
            advanceDirection = Vector3.forward;
        }

        float pushDistance = CommanderSettings.FrontlinePushDistanceMeters;
        float holdDistance = CommanderSettings.FrontlineHoldDistanceMeters;

        CommanderMoveService? moveService = CommanderMoveService.Instance;
        CommanderDoctrineService? doctrine = CommanderDoctrineService.Instance;

        for (int i = 0; i < friendlyCombatants.Count; i++)
        {
            Unit unit = friendlyCombatants[i];
            if (unit == null || unit.disabled || unit is not GroundVehicle) continue;

            // Never override player's explicit manual orders
            if (moveService != null && moveService.HasActivePlayerDestination(unit))
            {
                continue;
            }

            // Respect doctrine roles
            bool isFrontlineAllowed = true;
            if (doctrine != null && doctrine.TryGetPolicy(unit, out CommanderDoctrinePolicy policy))
            {
                isFrontlineAllowed = policy.AllowFrontlineAdvance;
            }

            if (!isFrontlineAllowed && CurrentMode == FrontlineDirectiveMode.AggressivePush)
            {
                // Support / Standoff echelons hold rear guard rather than spearheading
                continue;
            }

            if (lastOrderTimes.TryGetValue(unit, out float lastTime)
                && Time.unscaledTime - lastTime < CommanderSettings.FrontlineDirectiveIntervalSeconds * 1.5f)
            {
                continue;
            }

            Vector3 currentPos = unit.transform.position;
            Vector3 targetPos = currentPos;

            switch (CurrentMode)
            {
                case FrontlineDirectiveMode.AggressivePush:
                    targetPos = currentPos + advanceDirection * pushDistance;
                    break;

                case FrontlineDirectiveMode.Balanced:
                    // If behind friendly center, advance to form line; if ahead, hold
                    float projection = Vector3.Dot(currentPos - friendlyCenter, advanceDirection);
                    if (projection < 0f)
                    {
                        targetPos = currentPos + advanceDirection * (pushDistance * 0.5f);
                    }
                    else
                    {
                        targetPos = currentPos + advanceDirection * 50f;
                    }
                    break;

                case FrontlineDirectiveMode.DefensiveHold:
                    targetPos = currentPos; // Hold in place
                    break;

                case FrontlineDirectiveMode.TacticalRetreat:
                    targetPos = currentPos - advanceDirection * holdDistance;
                    break;
            }

            if (FastMath.Distance(currentPos, targetPos) > RepathDistanceMeters)
            {
                GlobalPosition targetGlobal = targetPos.ToGlobalPosition();
                CommanderGameAccess.SetUnitHoldPosition(unit, CurrentMode == FrontlineDirectiveMode.DefensiveHold);
                if (CommanderGameAccess.TrySetDestination(unit, targetGlobal))
                {
                    lastOrderTimes[unit] = Time.unscaledTime;
                }
            }
        }
    }

    private void PruneDeadReferences()
    {
        if (lastOrderTimes.Count == 0) return;

        tempPruneList.Clear();
        foreach (KeyValuePair<Unit, float> kv in lastOrderTimes)
        {
            if (kv.Key == null || kv.Key.disabled)
            {
                if (kv.Key != null)
                {
                    tempPruneList.Add(kv.Key);
                }
            }
        }

        for (int i = 0; i < tempPruneList.Count; i++)
        {
            Unit u = tempPruneList[i];
            if (u != null)
            {
                lastOrderTimes.Remove(u);
            }
        }
    }

    internal void ResetSession()
    {
        friendlyCombatants.Clear();
        lastOrderTimes.Clear();
        tempPruneList.Clear();
        hasActiveFrontline = false;
        nextEvaluationTime = 0f;
        CurrentMode = FrontlineDirectiveMode.Balanced;
    }
}
