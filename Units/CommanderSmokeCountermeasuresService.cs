using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderSmokeCountermeasuresService
{
    private const float CooldownSeconds = 45f;
    private readonly Dictionary<Unit, float> lastSmokeDeployTimes = new();
    private readonly List<ActiveSmokeScreen> activeSmokes = new();

    internal static CommanderSmokeCountermeasuresService? Instance { get; private set; }

    internal sealed class ActiveSmokeScreen
    {
        internal Vector3 Position;
        internal float ExpiryTime;
        internal float Radius;
    }

    internal IReadOnlyList<ActiveSmokeScreen> ActiveSmokes => activeSmokes;

    internal CommanderSmokeCountermeasuresService()
    {
        Instance = this;
    }

    internal bool CanDeploySmoke(Unit? unit)
    {
        if (unit == null || unit.disabled || unit is not GroundVehicle) return false;
        if (lastSmokeDeployTimes.TryGetValue(unit, out float lastTime) && Time.unscaledTime - lastTime < CooldownSeconds)
        {
            return false;
        }
        return true;
    }

    internal bool TryDeploySmokeForSelection(CommanderSelectionService selectionService, CommanderMoveService moveService)
    {
        IReadOnlyList<Unit> selected = selectionService.SelectedUnits;
        if (selected == null || selected.Count == 0) return false;

        bool anyDeployed = false;
        float now = Time.unscaledTime;

        for (int i = 0; i < selected.Count; i++)
        {
            Unit unit = selected[i];
            if (!CanDeploySmoke(unit)) continue;

            lastSmokeDeployTimes[unit] = now;
            anyDeployed = true;

            // Spawn Active Smoke Screen zone
            activeSmokes.Add(new ActiveSmokeScreen
            {
                Position = unit.transform.position,
                ExpiryTime = now + 18f,
                Radius = 45f
            });

            // Defensive Reverse Maneuver (Evasive Action)
            Vector3 backPos = unit.transform.position - unit.transform.forward * 25f;
            moveService.IssueDirectMoveOrder(backPos.ToGlobalPosition(), queueWaypoint: false);

            CommanderAlertService.PostTickerEvent($"[SMOKE] {unit.unitName} popped emergency smoke screen!", new Color(0.85f, 0.85f, 0.9f, 0.95f));
        }

        return anyDeployed;
    }

    internal bool IsPointObscuredBySmoke(Vector3 point)
    {
        for (int i = 0; i < activeSmokes.Count; i++)
        {
            ActiveSmokeScreen s = activeSmokes[i];
            if (Vector3.Distance(point, s.Position) <= s.Radius)
            {
                return true;
            }
        }
        return false;
    }

    internal void Tick()
    {
        float now = Time.unscaledTime;
        for (int i = activeSmokes.Count - 1; i >= 0; i--)
        {
            if (now >= activeSmokes[i].ExpiryTime)
            {
                activeSmokes.RemoveAt(i);
            }
        }

        // Prune dead units from cooldowns
        List<Unit>? deadCooldowns = null;
        foreach (KeyValuePair<Unit, float> pair in lastSmokeDeployTimes)
        {
            if (pair.Key == null || pair.Key.disabled)
            {
                deadCooldowns ??= new List<Unit>();
                deadCooldowns.Add(pair.Key);
            }
        }
        if (deadCooldowns != null)
        {
            for (int i = 0; i < deadCooldowns.Count; i++) lastSmokeDeployTimes.Remove(deadCooldowns[i]);
        }
    }

    internal void ResetSession()
    {
        lastSmokeDeployTimes.Clear();
        activeSmokes.Clear();
    }
}
