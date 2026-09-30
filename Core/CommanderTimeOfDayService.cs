using System;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

/// <summary>
/// Host-only day/night compression. Verified against BepInEx LogOutput.log: the game
/// stores the world clock as decimal hours (0 - 24) and its own formatter renders
/// "HH:MM", so the full range is 24 and no calibration is needed.
/// </summary>
internal sealed class CommanderTimeOfDayService
{
    /// <summary>Hours in a full day, matching the game's own timeOfDay range.</summary>
    internal const float RangeHours = 24f;

    private static readonly float[] PresetHours = { 6f, 12f, 18f, 0f, 9f };
    private static readonly string[] PresetNames = { "DAWN", "NOON", "DUSK", "NIGHT", "MORNING" };

    private static readonly MethodInfo? SetTimeOfDayMethod = AccessTools.Method(typeof(LevelInfo), "SetTimeOfDay");
    private static readonly MethodInfo? FormatTimeOfDayMethod =
        AccessTools.Method(typeof(UnitConverter), "TimeOfDay", new[] { typeof(float), typeof(bool) });

    private int presetIndex;
    private float cycleStartTime;
    private float cycleStartValue;

    internal static CommanderTimeOfDayService? Instance { get; private set; }

    internal CommanderTimeOfDayService()
    {
        Instance = this;
    }

    internal void Activate()
    {
        nextSyncAt = CommanderScheduler.Stagger("tod.sync", CommanderSettings.TimeOfDaySyncIntervalSeconds, 1f);
        cycleStartTime = 0f;
        cycleStartValue = 0f;
        presetIndex = 0;
    }

    private float nextSyncAt;

    internal void Deactivate() { }

    internal void ResetSession()
    {
        cycleStartTime = 0f;
        cycleStartValue = 0f;
        presetIndex = 0;
        nextSyncAt = 0f;
    }

    internal void Tick()
    {
        if (!CommanderSettings.TimeOfDaySyncEnabled
            || !CommanderFeatureGate.AdvancedFeaturesEnabled
            || SetTimeOfDayMethod == null
            || Time.unscaledTime < nextSyncAt)
        {
            return;
        }

        nextSyncAt = Time.unscaledTime + Mathf.Max(1f, CommanderSettings.TimeOfDaySyncIntervalSeconds);

        LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
        if (level == null || !CommanderHostAuthority.IsHostAuthority())
        {
            return;
        }

        if (cycleStartTime <= 0f)
        {
            cycleStartTime = Time.unscaledTime;
            cycleStartValue = level.NetworktimeOfDay;
            CommanderPlugin.Log.LogInfo(
                $"[TimeOfDay] cycle engaged at {Describe(cycleStartValue)} "
                + $"(raw {cycleStartValue:0.00}), {CommanderSettings.TimeOfDayCycleMinutes:0} min per 24h.");
        }

        float cycleSeconds = Mathf.Max(60f, CommanderSettings.TimeOfDayCycleMinutes * 60f);
        float elapsed = Time.unscaledTime - cycleStartTime;
        float compressed = cycleStartValue + (elapsed / cycleSeconds) * RangeHours;
        float wrapped = compressed % RangeHours;
        if (wrapped < 0f)
        {
            wrapped += RangeHours;
        }

        try
        {
            SetTimeOfDayMethod.Invoke(level, new object[] { wrapped });
        }
        catch (Exception exception)
        {
            CommanderPlugin.Log.LogError($"Time of day sync failed: {exception.Message}");
            nextSyncAt = Time.unscaledTime + 30f;
        }
    }

    internal bool CyclePreset()
    {
        presetIndex = (presetIndex + 1) % PresetHours.Length;
        return JumpToPreset(presetIndex);
    }

    internal bool JumpToPreset(int index)
    {
        if (SetTimeOfDayMethod == null
            || !CommanderHostAuthority.IsHostAuthority()
            || index < 0
            || index >= PresetHours.Length)
        {
            return false;
        }

        LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
        if (level == null)
        {
            return false;
        }

        float value = PresetHours[index];
        try
        {
            SetTimeOfDayMethod.Invoke(level, new object[] { value });
        }
        catch (Exception exception)
        {
            CommanderPlugin.Log.LogError($"Time of day preset failed: {exception.Message}");
            return false;
        }

        cycleStartTime = Time.unscaledTime;
        cycleStartValue = value;
        CommanderPlugin.Log.LogInfo(
            $"[TimeOfDay] preset {PresetNames[index]} -> {Describe(value)} (raw {value:0.00}).");
        return true;
    }

    /// <summary>Renders a raw time value through the game's own clock formatter.</summary>
    private static string Describe(float value)
    {
        if (FormatTimeOfDayMethod == null)
        {
            return "n/a";
        }

        try
        {
            return FormatTimeOfDayMethod.Invoke(null, new object[] { value, false }) as string ?? "n/a";
        }
        catch (Exception)
        {
            return "n/a";
        }
    }

    internal static string[] PresetLabelNames => PresetNames;

    internal float CycleMinutes => CommanderSettings.TimeOfDayCycleMinutes;

    internal void SetEnabled(bool enabled)
    {
        CommanderSettings.TimeOfDaySyncEnabled = enabled;
    }

    internal void SetCycleMinutes(float minutes)
    {
        CommanderSettings.TimeOfDayCycleMinutes = Mathf.Clamp(minutes, 1f, 1440f);
    }

    internal string ClockText
    {
        get
        {
            LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
            return level == null ? string.Empty : Describe(level.NetworktimeOfDay);
        }
    }
}
