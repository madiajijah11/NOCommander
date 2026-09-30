using System;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

/// <summary>
/// Host-only control over the world clock.
/// <para>
/// The base game already advances the clock in real time inside
/// <c>LevelInfo.UpdateSimulation</c> using <c>timeOfDay += timeFactor * deltaTime / 3600</c>,
/// with a 24 hour wrap. This service exposes that native <c>timeFactor</c> multiplier instead
/// of overwriting the clock, so the game keeps ownership of the value.
/// </para>
/// </summary>
internal sealed class CommanderTimeOfDayService
{
    private static readonly float[] PresetHours = { 6f, 12f, 18f, 0f, 9f };
    private static readonly string[] PresetNames = { "DAWN", "NOON", "DUSK", "NIGHT", "MORNING" };

    private static readonly MethodInfo? SetTimeOfDayMethod = AccessTools.Method(typeof(LevelInfo), "SetTimeOfDay");
    private static readonly MethodInfo? FormatTimeOfDayMethod =
        AccessTools.Method(typeof(UnitConverter), "TimeOfDay", new[] { typeof(float), typeof(bool) });
    private static readonly FieldInfo? TimeFactorField = AccessTools.Field(typeof(LevelInfo), "timeFactor");

    private float nextSyncAt;
    private float appliedRate = -1f;

    internal static CommanderTimeOfDayService? Instance { get; private set; }

    internal CommanderTimeOfDayService()
    {
        Instance = this;
    }

    internal void Activate()
    {
        nextSyncAt = CommanderScheduler.Stagger("tod.rate", 5f, 1f);
        appliedRate = -1f;
    }

    internal void Deactivate() { }

    internal void ResetSession()
    {
        nextSyncAt = 0f;
        appliedRate = -1f;
    }

    /// <summary>
    /// Rate check only. The field is compared and written at most once per interval, and
    /// only when it actually drifted, so there is no per-frame work and no sync traffic.
    /// </summary>
    internal void Tick()
    {
        if (!CommanderSettings.TimeOfDayControlEnabled
            || !CommanderFeatureGate.AdvancedFeaturesEnabled
            || TimeFactorField == null
            || Time.unscaledTime < nextSyncAt)
        {
            return;
        }

        nextSyncAt = Time.unscaledTime + 5f;

        LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
        if (level == null || !CommanderHostAuthority.IsHostAuthority())
        {
            return;
        }

        float desired = DesiredRate;
        if (appliedRate >= 0f && Mathf.Approximately(appliedRate, desired))
        {
            return;
        }

        ApplyRate(level, desired);
    }

    /// <summary>
    /// Game hours per real hour. 1 matches the base game default, 24 makes one game hour
    /// pass in one real minute.
    /// </summary>
    internal static float DesiredRate
    {
        get
        {
            float rate = CommanderSettings.TimeOfDayRateMultiplier;
            return rate < 0f ? 0f : rate;
        }
    }

    internal void SetRate(float rate)
    {
        CommanderSettings.TimeOfDayRateMultiplier = Mathf.Max(0f, rate);
        appliedRate = -1f;
    }

    internal float CurrentRate
    {
        get
        {
            if (TimeFactorField == null)
            {
                return DesiredRate;
            }

            LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
            if (level == null)
            {
                return DesiredRate;
            }

            return TimeFactorField.GetValue(level) is float value ? value : DesiredRate;
        }
    }

    internal void SetEnabled(bool enabled)
    {
        CommanderSettings.TimeOfDayControlEnabled = enabled;
        if (!enabled)
        {
            ApplyRate(NetworkSceneSingleton<LevelInfo>.i, 1f);
        }
        appliedRate = -1f;
    }

    private void ApplyRate(LevelInfo? level, float rate)
    {
        if (level == null || TimeFactorField == null)
        {
            return;
        }

        try
        {
            TimeFactorField.SetValue(level, rate);
            appliedRate = rate;
            CommanderPlugin.Log.LogInfo($"[TimeOfDay] rate set to {rate:0.##}x (base game default is 1x).");
        }
        catch (Exception exception)
        {
            CommanderPlugin.Log.LogError($"Failed to set time of day rate: {exception.Message}");
            appliedRate = -1f;
        }
    }

    internal static string[] PresetLabelNames => PresetNames;

    internal void SetEnabledPresets(bool enabled)
    {
        presetsEnabled = enabled;
    }

    private bool presetsEnabled = true;
    private int presetIndex;

    internal bool CyclePreset()
    {
        presetIndex = (presetIndex + 1) % PresetHours.Length;
        return JumpToPreset(presetIndex);
    }

    /// <summary>Jumps the clock to a lighting preset. The game keeps advancing from there.</summary>
    internal bool JumpToPreset(int index)
    {
        if (index >= 0 && index < PresetHours.Length)
        {
            presetIndex = index;
        }

        if (SetTimeOfDayMethod == null
            || !presetsEnabled
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

        CommanderPlugin.Log.LogInfo(
            $"[TimeOfDay] preset {PresetNames[index]} -> {Describe(value)} (raw {value:0.00}).");
        return true;
    }

    /// <summary>Renders a raw hour value through the game's own clock formatter.</summary>
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

    internal string ClockText
    {
        get
        {
            LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
            return level == null ? string.Empty : Describe(level.NetworktimeOfDay);
        }
    }
}
