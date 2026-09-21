using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderCounterBatteryRadarService
{
    private const float PingDurationSeconds = 25f;
    private readonly List<CounterBatteryPing> activePings = new();

    internal static CommanderCounterBatteryRadarService? Instance { get; private set; }

    internal sealed class CounterBatteryPing
    {
        internal Vector3 Position;
        internal float ExpiryTime;
        internal string WeaponSource;
        internal Unit? SourceUnit;
    }

    internal IReadOnlyList<CounterBatteryPing> ActivePings => activePings;

    internal CommanderCounterBatteryRadarService()
    {
        Instance = this;
    }

    internal static void NotifyWeaponFired(Weapon weapon, Vector3 muzzlePos)
    {
        if (Instance == null || weapon == null) return;

        Unit? firingUnit = weapon.GetComponentInParent<Unit>();
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (firingUnit == null || localHq == null || CommanderGameAccess.IsFriendlyUnit(firingUnit, localHq))
        {
            return;
        }

        // Only register significant ballistic / rocket / missile launches
        string wpName = weapon.name.ToLowerInvariant();
        bool isArtilleryOrMissile = wpName.Contains("missile") || wpName.Contains("rocket") || wpName.Contains("mortar") || wpName.Contains("cannon") || wpName.Contains("howitzer") || wpName.Contains("sam") || wpName.Contains("battery");

        if (!isArtilleryOrMissile)
        {
            return;
        }

        float now = Time.unscaledTime;
        // Avoid duplicate spam from same location within 3s
        for (int i = 0; i < Instance.activePings.Count; i++)
        {
            if (Vector3.Distance(Instance.activePings[i].Position, muzzlePos) < 50f && (Instance.activePings[i].ExpiryTime - now) > (PingDurationSeconds - 3f))
            {
                return;
            }
        }

        Instance.activePings.Add(new CounterBatteryPing
        {
            Position = muzzlePos,
            ExpiryTime = now + PingDurationSeconds,
            WeaponSource = weapon.name,
            SourceUnit = firingUnit
        });

        CommanderAlertService.PostTickerEvent($"[COUNTER-BATTERY] Hostile firing origin pinpointed at ({Mathf.RoundToInt(muzzlePos.x)}, {Mathf.RoundToInt(muzzlePos.z)})", new Color(1f, 0.25f, 0.2f, 0.95f));
    }

    internal void Tick()
    {
        float now = Time.unscaledTime;
        for (int i = activePings.Count - 1; i >= 0; i--)
        {
            CounterBatteryPing ping = activePings[i];
            if (now >= ping.ExpiryTime || ping.SourceUnit == null)
            {
                activePings.RemoveAt(i);
                continue;
            }
            // Clear stale ref if unit was destroyed between pings
            if (ping.SourceUnit.disabled)
            {
                ping.SourceUnit = null;
            }
        }
    }

    internal void ResetSession()
    {
        activePings.Clear();
    }
}

[HarmonyPatch(typeof(Weapon), "Fire")]
internal static class CommanderWeaponFirePatch
{
    private static void Postfix(Weapon __instance)
    {
        if (__instance != null)
        {
            CommanderCounterBatteryRadarService.NotifyWeaponFired(__instance, __instance.transform.position);
        }
    }
}
