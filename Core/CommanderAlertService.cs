using System;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderAlertService
{
    private const float IncidentExpirySeconds = 15f;

    internal static CommanderAlertService? Instance { get; private set; }

    internal Unit? LastIncidentUnit { get; private set; }
    internal Vector3 LastIncidentPosition { get; private set; }
    internal float LastIncidentTime { get; private set; }

    internal bool HasActiveIncident =>
        LastIncidentTime > 0f && (Time.unscaledTime - LastIncidentTime) <= IncidentExpirySeconds;

    internal CommanderAlertService()
    {
        Instance = this;
    }

    internal static void NotifyUnitDamaged(Unit unit, DamageInfo info)
    {
        if (Instance == null || unit == null || unit.disabled)
        {
            return;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(unit, localHq))
        {
            return;
        }

        Instance.LastIncidentUnit = unit;
        Instance.LastIncidentPosition = unit.transform.position;
        Instance.LastIncidentTime = Time.unscaledTime;

        // Trigger AI reactive evasion scatter if vehicle is taking explosive splash damage
        CommanderSmartAiService.Instance?.TryTriggerAiScatter(unit, unit.transform.position, 100f);
    }

    internal bool TryJumpToIncident(CommanderSelectionService? selectionService, CommanderTacticalMapService? tacticalMapService)
    {
        if (!HasActiveIncident)
        {
            return false;
        }

        if (LastIncidentUnit != null && !LastIncidentUnit.disabled)
        {
            selectionService?.SelectUnit(LastIncidentUnit, additive: false);
            tacticalMapService?.JumpCameraToPosition(LastIncidentUnit.transform.GlobalPosition());
        }
        else
        {
            tacticalMapService?.JumpCameraToPosition(LastIncidentPosition.ToGlobalPosition());
        }

        return true;
    }

    internal void ResetSession()
    {
        LastIncidentUnit = null;
        LastIncidentPosition = Vector3.zero;
        LastIncidentTime = 0f;
    }
}

[HarmonyPatch(typeof(Unit), nameof(Unit.Damage))]
internal static class CommanderUnitDamagePatch
{
    private static bool Prefix(Unit __instance)
    {
        if (CommanderCheatService.Instance?.GodModeEnabled == true
            && __instance != null
            && !__instance.disabled
            && CommanderGameAccess.IsFriendlyUnit(__instance, CommanderGameAccess.GetLocalHq()))
        {
            return false; // Intercept and block all damage under God Mode
        }
        return true;
    }

    private static void Postfix(Unit __instance, DamageInfo damageInfo)
    {
        CommanderAlertService.NotifyUnitDamaged(__instance, damageInfo);
    }
}
