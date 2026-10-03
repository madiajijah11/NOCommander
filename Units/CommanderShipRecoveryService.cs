using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

/// <summary>
/// Autonomous Ship Recovery & Reverse Maneuver Service.
/// Fixes the game flaw where AI-controlled ships get beached or stranded on the shoreline
/// and cannot back out because basegame ShipAI has zero reverse gear logic.
/// Detects low speed under forward throttle near shoreline obstacles, engages reverse propulsion
/// with opposing rudder until clear of the shore, and resumes normal navigation.
/// </summary>
internal sealed class CommanderShipRecoveryService
{
    internal static CommanderShipRecoveryService? Instance { get; private set; }

    private static readonly FieldInfo? ShipField = AccessTools.Field(typeof(ShipAI), "ship");
    private static readonly FieldInfo? InputsField = AccessTools.Field(typeof(ShipAI), "inputs");
    private static readonly FieldInfo? ShoreObstacleField = AccessTools.Field(typeof(ShipAI), "shoreObstacle");

    private enum RecoveryStage
    {
        None,
        Reversing,
        TurningClear
    }

    private sealed class ShipRecoveryState
    {
        public RecoveryStage Stage = RecoveryStage.None;
        public float StageEndTime;
        public float ReverseSteerSign = 1f;
        public float StuckTimer;
        public Vector3 LastSamplePos;
        public float LastSampleTime;
    }

    private readonly Dictionary<ShipAI, ShipRecoveryState> recoveryStates = new();
    private readonly List<ShipAI> tempPruneList = new();

    private const float StuckSpeedThreshold = 1.2f; // m/s
    private const float StuckDetectionSeconds = 2.5f; // time stuck before trigger
    private const float ReverseDurationSeconds = 6.0f; // reverse for 6s
    private const float TurnClearDurationSeconds = 3.5f; // turn into open water for 3.5s

    internal CommanderShipRecoveryService()
    {
        Instance = this;
    }

    internal void ResetSession()
    {
        recoveryStates.Clear();
        tempPruneList.Clear();
    }

    internal void PruneDeadReferences()
    {
        if (recoveryStates.Count == 0) return;

        tempPruneList.Clear();
        foreach (KeyValuePair<ShipAI, ShipRecoveryState> kvp in recoveryStates)
        {
            if (kvp.Key == null)
            {
                continue;
            }

            Ship? ship = GetShip(kvp.Key);
            if (ship == null || ship.disabled)
            {
                tempPruneList.Add(kvp.Key);
            }
        }

        for (int i = 0; i < tempPruneList.Count; i++)
        {
            recoveryStates.Remove(tempPruneList[i]);
        }
        tempPruneList.Clear();
    }

    private static Ship? GetShip(ShipAI shipAI)
    {
        if (shipAI == null) return null;
        if (ShipField != null)
        {
            return ShipField.GetValue(shipAI) as Ship;
        }
        return shipAI.GetComponent<Ship>();
    }

    private static ShipInputs? GetInputs(ShipAI shipAI)
    {
        if (shipAI == null) return null;
        if (InputsField != null)
        {
            return InputsField.GetValue(shipAI) as ShipInputs;
        }
        Ship? ship = GetShip(shipAI);
        return ship != null ? ship.GetInputs() : null;
    }

    private static bool TryGetShoreObstacle(ShipAI shipAI, out Obstacle obstacle)
    {
        obstacle = default;
        if (shipAI == null || ShoreObstacleField == null) return false;
        object? val = ShoreObstacleField.GetValue(shipAI);
        if (val is Obstacle obs)
        {
            obstacle = obs;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Evaluates if a ship is stranded and overrides ShipInputs if reversing or clearing.
    /// Returns true if inputs were overridden.
    /// </summary>
    internal bool TryHandleShipRecovery(ShipAI shipAI)
    {
        if (shipAI == null) return false;
        Ship? ship = GetShip(shipAI);
        if (ship == null || ship.disabled) return false;
        if (!CommanderSettings.ShipRecoveryEnabled) return false;

        // Multiplayer authority check: only Host executes recovery navigation
        if (GameManager.gameState == GameState.Multiplayer && !CommanderHostAuthority.IsHostAuthority())
        {
            return false;
        }

        ShipInputs? inputs = GetInputs(shipAI);
        if (inputs == null) return false;

        if (!recoveryStates.TryGetValue(shipAI, out ShipRecoveryState? state) || state == null)
        {
            state = new ShipRecoveryState();
            recoveryStates[shipAI] = state;
        }

        float now = Time.time;
        Transform shipTransform = ship.transform;
        Vector3 currentPos = shipTransform.position;
        Rigidbody? rb = ship.rb;

        // 1. If currently in recovery maneuver
        if (state.Stage == RecoveryStage.Reversing)
        {
            if (now >= state.StageEndTime)
            {
                // Transition to TurningClear stage
                state.Stage = RecoveryStage.TurningClear;
                state.StageEndTime = now + TurnClearDurationSeconds;
            }
            else
            {
                // Active Reverse: Full reverse throttle (-1.0f) with opposite rudder to back away
                inputs.throttle = -1.0f;
                inputs.steering = state.ReverseSteerSign;
                return true;
            }
        }

        if (state.Stage == RecoveryStage.TurningClear)
        {
            if (now >= state.StageEndTime)
            {
                // Recovery complete! Resume normal AI steering
                state.Stage = RecoveryStage.None;
                state.StuckTimer = 0f;
                state.LastSamplePos = currentPos;
                state.LastSampleTime = now;
            }
            else
            {
                // Forward half-throttle turning hard towards open water
                inputs.throttle = 0.5f;
                inputs.steering = -state.ReverseSteerSign;
                return true;
            }
        }

        // 2. Not in recovery: Check if ship is stranded / beached
        // Needs a valid destination (navigating state)
        if (shipAI.state != ShipAI.ShipAIState.navigating && shipAI.state != ShipAI.ShipAIState.holding)
        {
            state.StuckTimer = 0f;
            return false;
        }

        // Check if shore obstacle is active or close
        bool nearShore = false;
        Vector3 shoreDir = Vector3.forward;
        if (TryGetShoreObstacle(shipAI, out Obstacle shoreObstacle) && shoreObstacle.Transform != null)
        {
            Vector3 toShore = shoreObstacle.Transform.position - currentPos;
            toShore.y = 0f;
            float distToShore = toShore.magnitude;
            if (distToShore <= shoreObstacle.Radius + 120f)
            {
                nearShore = true;
                shoreDir = toShore.normalized;
            }
        }

        // Check velocity
        float currentSpeed = rb != null ? rb.velocity.magnitude : 0f;

        if (state.LastSampleTime <= 0f)
        {
            state.LastSamplePos = currentPos;
            state.LastSampleTime = now;
            return false;
        }

        float dt = now - state.LastSampleTime;
        if (dt >= 1.0f)
        {
            float distMoved = Vector3.Distance(currentPos, state.LastSamplePos);
            float effectiveSpeed = distMoved / dt;

            state.LastSamplePos = currentPos;
            state.LastSampleTime = now;

            // If effective speed is very low, and ship has forward throttle or facing shore
            if (effectiveSpeed < StuckSpeedThreshold && currentSpeed < StuckSpeedThreshold)
            {
                if (nearShore || inputs.throttle > 0.05f)
                {
                    state.StuckTimer += dt;
                }
            }
            else
            {
                state.StuckTimer = Mathf.Max(0f, state.StuckTimer - dt * 0.5f);
            }
        }

        // 3. Trigger recovery if stuck threshold exceeded
        if (state.StuckTimer >= StuckDetectionSeconds)
        {
            state.Stage = RecoveryStage.Reversing;
            state.StageEndTime = now + ReverseDurationSeconds;

            // Pick reverse rudder direction away from shore obstacle
            float dotShore = Vector3.Dot(shipTransform.right, shoreDir);
            state.ReverseSteerSign = dotShore > 0f ? 1.0f : -1.0f;

            inputs.throttle = -1.0f;
            inputs.steering = state.ReverseSteerSign;

            FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
            if (localHq != null && CommanderGameAccess.IsFriendlyUnit(ship, localHq))
            {
                CommanderAlertService.PostTickerEvent($"[NAVAL] {ship.unitName}: Executing reverse maneuver from shore", new Color(0.3f, 0.8f, 1f, 0.9f));
            }

            return true;
        }

        return false;
    }
}

/// <summary>
/// Harmony postfix on ShipAI.Steer to enforce reverse ungrounding when beached.
/// </summary>
[HarmonyPatch(typeof(ShipAI), "Steer")]
internal static class CommanderShipRecoveryPatch
{
    [HarmonyPostfix]
    private static void Postfix(ShipAI __instance)
    {
        if (__instance == null || !CommanderSettings.ShipRecoveryEnabled) return;

        CommanderShipRecoveryService.Instance?.TryHandleShipRecovery(__instance);
    }
}
