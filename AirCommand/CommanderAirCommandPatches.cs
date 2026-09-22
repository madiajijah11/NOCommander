using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

[HarmonyPatch]
internal static class CommanderAirCommandPatches
{
    private static readonly FieldInfo? StateAircraftField = AccessTools.Field(typeof(PilotBaseState), "aircraft");
    private static readonly FieldInfo? DestinationField = AccessTools.Field(typeof(PilotBaseState), "destination");
    private static readonly FieldInfo? TimeWithoutTargetField = AccessTools.Field(typeof(AIPilotCombatModes), "timeWithoutTarget");
    private static readonly FieldInfo? TargetHeightField = AccessTools.Field(typeof(AIPilotCombatModes), "targetHeight");
    private static readonly FieldInfo? TakeoffAirbaseField = AccessTools.Field(typeof(AIPilotTakeoffState), "airbase");
    private static readonly FieldInfo? LandingAirbaseField = AccessTools.Field(typeof(AIPilotLandingState), "airbase");
    private static readonly FieldInfo? AirbaseAttachedUnitField = AccessTools.Field(typeof(Airbase), "attachedUnit");
    private static readonly FieldInfo? TailHookDeployedField = AccessTools.Field(typeof(TailHook), "deployed");

    [HarmonyPatch(typeof(CombatAI), nameof(CombatAI.ChooseHQTarget))]
    [HarmonyPrefix]
    private static bool ChooseHqTargetPrefix(
        Unit searcher,
        List<WeaponStation> stationList,
        ref CombatAI.TargetSearchResults __result)
    {
        if (!CommanderAirCommandService.TryChooseMissionTarget(searcher, stationList, out CombatAI.TargetSearchResults result))
        {
            return true;
        }

        __result = result;
        return false;
    }

    [HarmonyPatch(typeof(CombatAI), nameof(CombatAI.LookForMissileTargets))]
    [HarmonyPrefix]
    private static bool LookForMissileTargetsPrefix(
        Aircraft aircraft,
        WeaponStation weaponStation,
        ref int __result)
    {
        if (!CommanderAirCommandService.TryBuildAradSaturationTargets(aircraft, weaponStation, out int targetCount))
        {
            return true;
        }

        __result = targetCount;
        return false;
    }

    [HarmonyPatch(typeof(AIPilotCombatModes), "NoTarget")]
    [HarmonyPostfix]
    private static void NoTargetPostfix(AIPilotCombatModes __instance)
    {
        if (!CommanderAirCommandService.TryGetMissionHoldPoint(__instance, out GlobalPosition point))
        {
            return;
        }

        DestinationField?.SetValue(__instance, point);
        CommanderAirCommandService.RecordTargetlessTick(__instance);
    }

    [HarmonyPatch(typeof(AIPilotCombatModes), "ManageAltitude")]
    [HarmonyPostfix]
    private static void ManageAltitudePostfix(AIPilotCombatModes __instance)
    {
        CommanderAirCommandService.ApplyMissionTargetAltitude(__instance, TargetHeightField);
    }

    [HarmonyPatch(typeof(AIPilotCombatModes), "RunAttackMode")]
    [HarmonyPostfix]
    private static void RunAttackModePostfix(AIPilotCombatModes __instance)
    {
        CommanderAirCommandService.ConstrainMissionDestination(__instance, DestinationField);
    }

    [HarmonyPatch(typeof(FactionHQ), nameof(FactionHQ.RegisterFactionUnit))]
    [HarmonyPostfix]
    private static void RegisterFactionUnitPostfix(FactionHQ __instance, Unit unit)
    {
        CommanderAirCommandService.NotifyFactionUnitRegistered(__instance, unit);
    }

    [HarmonyPatch(typeof(Aircraft), nameof(Aircraft.ReturnToInventory))]
    [HarmonyPostfix]
    private static void ReturnToInventoryPostfix(Aircraft __instance)
    {
        CommanderAirCommandService.NotifyAircraftReturned(__instance);
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.DisableUnit))]
    [HarmonyPostfix]
    private static void DisableUnitPostfix(Unit __instance)
    {
        CommanderAirCommandService.NotifyUnitDisabled(__instance);
    }

    // ==========================================
    // 🛡️ AUTONOMOUS MISSILE DEFENSE (FLARES, CHAFF, ECM)
    // ==========================================

    [HarmonyPatch(typeof(AIPilotCombatModes), "AICombat_OnMissileAlert")]
    [HarmonyPostfix]
    private static void OnMissileAlertPostfix(AIPilotCombatModes __instance)
    {
        Aircraft? aircraft = GetStateAircraft(__instance);
        if (aircraft == null || aircraft.disabled)
        {
            return;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(aircraft, localHq))
        {
            return;
        }

        // 1. Pop Flares & Chaff Salvo
        if (aircraft.countermeasureManager != null)
        {
            aircraft.countermeasureManager.PopFlares();
        }
        else
        {
            aircraft.Countermeasures(true, 0);
        }

        // 2. Trigger Active Radar Jammer / ECM Pod if equipped
        RadarJammer[] jammers = aircraft.GetComponentsInChildren<RadarJammer>(true);
        for (int j = 0; j < jammers.Length; j++)
        {
            if (jammers[j] != null && jammers[j].enabled)
            {
                jammers[j].Fire();
            }
        }
    }

    internal static Aircraft? GetStateAircraft(AIPilotCombatModes state)
    {
        return StateAircraftField?.GetValue(state) as Aircraft;
    }

    private static bool IsCarrierAirbase(Airbase? airbase)
    {
        if (airbase == null) return false;
        Unit? attached = AirbaseAttachedUnitField?.GetValue(airbase) as Unit;
        return attached is Ship || airbase.GetComponentInParent<Ship>() != null;
    }

    // ==========================================
    // 🌟 CARRIER & RUNWAY TAKEOFF SAFETY SYSTEM
    // ==========================================

    [HarmonyPatch(typeof(AIPilotTakeoffState), nameof(AIPilotTakeoffState.FixedUpdateState))]
    [HarmonyPrefix]
    private static void PilotTakeoffFixedUpdatePrefix(AIPilotTakeoffState __instance)
    {
        Aircraft? aircraft = StateAircraftField?.GetValue(__instance) as Aircraft;
        if (aircraft == null || aircraft.disabled || aircraft.rb == null)
        {
            return;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(aircraft, localHq))
        {
            return;
        }

        Airbase? airbase = TakeoffAirbaseField?.GetValue(__instance) as Airbase;
        bool isCarrier = IsCarrierAirbase(airbase);

        // Carrier bow drop prevention (aircraft leaving short carrier deck over water)
        if (isCarrier || (aircraft.transform.position.y < Datum.LocalSeaY + 35f && aircraft.speed > 30f))
        {
            if (aircraft.radarAlt < 18f && aircraft.radarAlt > 1f)
            {
                Vector3 vel = aircraft.rb.velocity;
                if (vel.y < 3.0f)
                {
                    aircraft.rb.velocity = new Vector3(vel.x, Mathf.Max(vel.y, 4.5f), vel.z);
                }
            }
        }
    }

    [HarmonyPatch(typeof(AIHeloTakeoffState), nameof(AIHeloTakeoffState.FixedUpdateState))]
    [HarmonyPrefix]
    private static void HeloTakeoffFixedUpdatePrefix(AIHeloTakeoffState __instance)
    {
        Aircraft? aircraft = StateAircraftField?.GetValue(__instance) as Aircraft;
        if (aircraft == null || aircraft.disabled || aircraft.rb == null)
        {
            return;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(aircraft, localHq))
        {
            return;
        }

        // Helo takeoff climb assist to clear carrier island / ship superstructure
        if (aircraft.radarAlt < 22f && aircraft.radarAlt > 0.5f)
        {
            Vector3 vel = aircraft.rb.velocity;
            if (vel.y < 2.0f)
            {
                aircraft.rb.velocity = new Vector3(vel.x, Mathf.Max(vel.y, 3.0f), vel.z);
            }
        }
    }

    // ==========================================
    // 🌟 CARRIER & RUNWAY LANDING SAFETY SYSTEM
    // ==========================================

    [HarmonyPatch(typeof(AIPilotLandingState), nameof(AIPilotLandingState.FixedUpdateState))]
    [HarmonyPrefix]
    private static void PilotLandingFixedUpdatePrefix(AIPilotLandingState __instance)
    {
        Aircraft? aircraft = StateAircraftField?.GetValue(__instance) as Aircraft;
        if (aircraft == null || aircraft.disabled || aircraft.rb == null)
        {
            return;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(aircraft, localHq))
        {
            return;
        }

        Airbase? airbase = LandingAirbaseField?.GetValue(__instance) as Airbase;
        bool isCarrier = IsCarrierAirbase(airbase);

        // 1. Ensure landing gear is deployed on approach
        if (!aircraft.gearDeployed)
        {
            aircraft.SetGear(true);
        }

        // 2. Deploy TailHook for carrier landings
        if (isCarrier)
        {
            TailHook? hook = aircraft.GetComponentInChildren<TailHook>(true);
            if (hook != null)
            {
                TailHookDeployedField?.SetValue(hook, true);
            }
        }

        // 3. Anti-crash touchdown cushion
        if (aircraft.radarAlt < 8f && aircraft.radarAlt > 0.1f)
        {
            Vector3 vel = aircraft.rb.velocity;
            if (vel.y < -3.0f)
            {
                aircraft.rb.velocity = new Vector3(vel.x, -2.0f, vel.z);
            }

            // Gently dampen angular wobble near touchdown to prevent wingtip strike
            Vector3 angVel = aircraft.rb.angularVelocity;
            aircraft.rb.angularVelocity = new Vector3(angVel.x * 0.9f, angVel.y * 0.9f, angVel.z * 0.8f);

            // Carrier deck arresting deceleration: prevent rolling off the bow into water
            if (isCarrier && aircraft.speed > 5f)
            {
                aircraft.rb.velocity = new Vector3(vel.x * 0.92f, vel.y, vel.z * 0.92f);
            }
        }
    }

    [HarmonyPatch(typeof(AIHeloLandingState), nameof(AIHeloLandingState.FixedUpdateState))]
    [HarmonyPrefix]
    private static void HeloLandingFixedUpdatePrefix(AIHeloLandingState __instance)
    {
        Aircraft? aircraft = StateAircraftField?.GetValue(__instance) as Aircraft;
        if (aircraft == null || aircraft.disabled || aircraft.rb == null)
        {
            return;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(aircraft, localHq))
        {
            return;
        }

        // 1. Ensure landing gear is deployed
        if (!aircraft.gearDeployed)
        {
            aircraft.SetGear(true);
        }

        // 2. Soft touchdown cushion for helicopters
        if (aircraft.radarAlt < 6f && aircraft.radarAlt > 0.1f)
        {
            Vector3 vel = aircraft.rb.velocity;
            if (vel.y < -2.5f)
            {
                aircraft.rb.velocity = new Vector3(vel.x, -1.5f, vel.z);
            }

            Vector3 angVel = aircraft.rb.angularVelocity;
            aircraft.rb.angularVelocity = new Vector3(angVel.x * 0.85f, angVel.y * 0.85f, angVel.z * 0.85f);
        }
    }
}
