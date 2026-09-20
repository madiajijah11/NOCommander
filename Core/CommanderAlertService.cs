using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderAlertService
{
    private const float IncidentExpirySeconds = 15f;

    internal static CommanderAlertService? Instance { get; private set; }

    internal readonly struct TickerEvent
    {
        internal readonly string Text;
        internal readonly Color Color;
        internal readonly float Timestamp;

        internal TickerEvent(string text, Color color, float timestamp)
        {
            Text = text;
            Color = color;
            Timestamp = timestamp;
        }
    }

    private readonly List<TickerEvent> tickerEvents = new();
    internal IReadOnlyList<TickerEvent> TickerEvents => tickerEvents;

    internal Unit? LastIncidentUnit { get; private set; }
    internal Vector3 LastIncidentPosition { get; private set; }
    internal float LastIncidentTime { get; private set; }

    internal bool HasActiveIncident =>
        LastIncidentTime > 0f && (Time.unscaledTime - LastIncidentTime) <= IncidentExpirySeconds;

    internal CommanderAlertService()
    {
        Instance = this;
    }

    internal static void PostTickerEvent(string text, Color color)
    {
        if (Instance == null) return;
        Instance.tickerEvents.Add(new TickerEvent(text, color, Time.unscaledTime));
        if (Instance.tickerEvents.Count > 6)
        {
            Instance.tickerEvents.RemoveAt(0);
        }
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

        // Only alert on meaningful damage
        if (info.pierceDamage.Value == 0 && info.blastDamage.Value == 0 && info.fireDamage.Value == 0 && info.impactDamage.Value == 0)
        {
            return;
        }

        Instance.LastIncidentUnit = unit;
        Instance.LastIncidentPosition = unit.transform.position;
        Instance.LastIncidentTime = Time.unscaledTime;
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
        tickerEvents.Clear();
    }
}

[HarmonyPatch]
internal static class CommanderGodModeAndDamagePatches
{
    private static bool ShouldBlockDamage(Unit? unit)
    {
        return CommanderCheatService.Instance?.GodModeEnabled == true
            && unit != null
            && !unit.disabled
            && CommanderGameAccess.IsFriendlyUnit(unit, CommanderGameAccess.GetLocalHq());
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.Damage))]
    [HarmonyPrefix]
    private static bool UnitDamagePrefix(Unit __instance)
    {
        return !ShouldBlockDamage(__instance);
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.Damage))]
    [HarmonyPostfix]
    private static void UnitDamagePostfix(Unit __instance, DamageInfo damageInfo)
    {
        CommanderAlertService.NotifyUnitDamaged(__instance, damageInfo);
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.RpcDamage))]
    [HarmonyPrefix]
    private static bool UnitRpcDamagePrefix(Unit __instance)
    {
        return !ShouldBlockDamage(__instance);
    }

    [HarmonyPatch(typeof(UnitPart), nameof(UnitPart.TakeDamage))]
    [HarmonyPrefix]
    private static bool UnitPartTakeDamagePrefix(UnitPart __instance)
    {
        return !ShouldBlockDamage(__instance.parentUnit ?? __instance.GetComponentInParent<Unit>());
    }

    [HarmonyPatch(typeof(UnitPart), nameof(UnitPart.ApplyDamage))]
    [HarmonyPrefix]
    private static bool UnitPartApplyDamagePrefix(UnitPart __instance)
    {
        return !ShouldBlockDamage(__instance.parentUnit ?? __instance.GetComponentInParent<Unit>());
    }

    [HarmonyPatch(typeof(UnitPart), nameof(UnitPart.TakeShockwave))]
    [HarmonyPrefix]
    private static bool UnitPartTakeShockwavePrefix(UnitPart __instance)
    {
        return !ShouldBlockDamage(__instance.parentUnit ?? __instance.GetComponentInParent<Unit>());
    }

    [HarmonyPatch(typeof(AeroPart), nameof(AeroPart.ApplyDamage))]
    [HarmonyPrefix]
    private static bool AeroPartApplyDamagePrefix(AeroPart __instance)
    {
        return !ShouldBlockDamage(__instance.parentUnit ?? __instance.GetComponentInParent<Unit>());
    }

    [HarmonyPatch(typeof(AeroPart), nameof(AeroPart.TakeShockwave))]
    [HarmonyPrefix]
    private static bool AeroPartTakeShockwavePrefix(AeroPart __instance)
    {
        return !ShouldBlockDamage(__instance.parentUnit ?? __instance.GetComponentInParent<Unit>());
    }

    [HarmonyPatch(typeof(ShipPart), nameof(ShipPart.ApplyDamage))]
    [HarmonyPrefix]
    private static bool ShipPartApplyDamagePrefix(ShipPart __instance)
    {
        return !ShouldBlockDamage(__instance.parentUnit ?? __instance.GetComponentInParent<Unit>());
    }
}
