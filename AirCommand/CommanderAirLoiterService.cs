using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderAirLoiterService
{
    private const float OrbitRadius = 3500f;
    private const float OrbitAltitude = 3500f;

    private readonly Dictionary<Aircraft, LoiterOrbitState> activeOrbits = new();
    private readonly List<Aircraft> staleOrbits = new();

    internal static CommanderAirLoiterService? Instance { get; private set; }

    internal sealed class LoiterOrbitState
    {
        internal Vector3 CenterPoint;
        internal float CurrentAngle;
        internal float TargetAltitude;
        internal float FuelCheckTime;
    }

    internal CommanderAirLoiterService()
    {
        Instance = this;
    }

    internal bool IsInLoiterOrbit(Aircraft? aircraft)
    {
        return aircraft != null && !aircraft.disabled && activeOrbits.ContainsKey(aircraft);
    }

    internal void OrderLoiterOrbit(Aircraft aircraft, Vector3 centerPoint, float altitude = OrbitAltitude)
    {
        if (aircraft == null || aircraft.disabled) return;

        LoiterOrbitState state = new()
        {
            CenterPoint = centerPoint,
            TargetAltitude = altitude,
            CurrentAngle = 0f,
            FuelCheckTime = Time.unscaledTime
        };

        activeOrbits[aircraft] = state;
        CommanderAlertService.PostTickerEvent($"[AIR] {aircraft.unitName} entered on-call holding orbit", new Color(0.2f, 0.85f, 1f, 0.95f));
    }

    internal bool TryDispatchNearestOnCallAircraft(Vector3 targetPos, out Aircraft? dispatched)
    {
        dispatched = null;
        if (activeOrbits.Count == 0) return false;

        float minDistance = float.MaxValue;
        foreach (KeyValuePair<Aircraft, LoiterOrbitState> pair in activeOrbits)
        {
            Aircraft ac = pair.Key;
            if (ac == null || ac.disabled) continue;

            float dist = Vector3.Distance(ac.transform.position, targetPos);
            if (dist < minDistance)
            {
                minDistance = dist;
                dispatched = ac;
            }
        }

        if (dispatched != null)
        {
            activeOrbits.Remove(dispatched);
            CommanderAlertService.PostTickerEvent($"[AIR] {dispatched.unitName} broke holding orbit -> Engaging target!", new Color(1f, 0.45f, 0.2f, 0.95f));
            return true;
        }

        return false;
    }

    internal void Tick()
    {
        staleOrbits.Clear();
        float dt = Time.deltaTime;

        foreach (KeyValuePair<Aircraft, LoiterOrbitState> pair in activeOrbits)
        {
            Aircraft ac = pair.Key;
            LoiterOrbitState state = pair.Value;

            if (ac == null || ac.disabled)
            {
                staleOrbits.Add(ac);
                continue;
            }

            // Advance orbit angle
            state.CurrentAngle += dt * 0.15f;
            if (state.CurrentAngle > Mathf.PI * 2f) state.CurrentAngle -= Mathf.PI * 2f;

            Vector3 orbitTarget = state.CenterPoint + new Vector3(Mathf.Cos(state.CurrentAngle) * OrbitRadius, state.TargetAltitude, Mathf.Sin(state.CurrentAngle) * OrbitRadius);

            // Steer aircraft waypoint along orbit
            CommanderGameAccess.GetUnitCommand(ac)?.SetDestination(orbitTarget.ToGlobalPosition(), false);
        }

        for (int i = 0; i < staleOrbits.Count; i++)
        {
            activeOrbits.Remove(staleOrbits[i]);
        }
    }

    internal void ResetSession()
    {
        activeOrbits.Clear();
        staleOrbits.Clear();
    }
}
