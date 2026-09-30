using System;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderTimeOfDayService
{
    private static readonly (string Name, float Fraction)[] Presets =
    {
        ("DAWN", 0.25f),
        ("NOON", 0.5f),
        ("DUSK", 0.75f),
        ("NIGHT", 0f),
        ("MORNING", 0.375f),
    };

    private static readonly MethodInfo? SetTimeOfDayMethod = AccessTools.Method(typeof(LevelInfo), "SetTimeOfDay");
    private static readonly MethodInfo? FormatTimeOfDayMethod =
        AccessTools.Method(typeof(UnitConverter), "TimeOfDay", new[] { typeof(float), typeof(bool) });

    private int presetIndex;

    internal static CommanderTimeOfDayService? Instance { get; private set; }

    internal CommanderTimeOfDayService()
    {
        Instance = this;
    }

    private float nextSyncAt;
    private float lastObservedValue = -1f;
    private float observedMax;
    private float range;
    private bool rangeLocked;
    private bool calibrationLogged;
    private float cycleStartTime;
    private float cycleStartValue;

    internal void Activate()
    {
        nextSyncAt = CommanderScheduler.Stagger("tod.sync", CommanderSettings.TimeOfDaySyncIntervalSeconds, 1f);
        lastObservedValue = -1f;
        observedMax = 0f;
        range = 0f;
        rangeLocked = false;
        calibrationLogged = false;
        cycleStartTime = 0f;
        cycleStartValue = 0f;
        presetIndex = 0;
    }

    internal void Deactivate() { }

    internal void ResetSession()
    {
        lastObservedValue = -1f;
        observedMax = 0f;
        range = 0f;
        rangeLocked = false;
        calibrationLogged = false;
        cycleStartTime = 0f;
        cycleStartValue = 0f;
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
        if (level == null)
        {
            return;
        }

        float current = level.NetworktimeOfDay;
        if (current > observedMax)
        {
            observedMax = current;
        }

        if (lastObservedValue >= 0f && current < lastObservedValue && observedMax > 1f)
        {
            range = observedMax;
            rangeLocked = true;
        }
        lastObservedValue = current;

        if (!CommanderHostAuthority.IsHostAuthority())
        {
            return;
        }

        if (!rangeLocked)
        {
            if (!calibrationLogged)
            {
                calibrationLogged = true;
                string? formatted = FormatTimeOfDayMethod != null
                    ? FormatTimeOfDayMethod.Invoke(null, new object[] { current, false }) as string
                    : null;
                CommanderPlugin.Log.LogInfo(
                    $"[TimeOfDay] calibrating: value={current}, label={formatted ?? "n/a"}. "
                    + "Compression engages after the first wrap.");
            }
            return;
        }

        float cycleSeconds = Mathf.Max(60f, CommanderSettings.TimeOfDayCycleMinutes * 60f);
        if (cycleStartTime <= 0f)
        {
            cycleStartTime = Time.unscaledTime;
            cycleStartValue = current;
        }

        float elapsed = Time.unscaledTime - cycleStartTime;
        float compressed = cycleStartValue + (elapsed / cycleSeconds) * range;
        float wrapped = compressed % range;
        if (wrapped < 0f)
        {
            wrapped += range;
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

    /// <summary>
    /// Jumps the world clock to a lighting preset. Presets are fractions of the observed
    /// range, so they work whether the game stores time in hours or in seconds.
    /// </summary>
    internal bool CyclePreset()
    {
        if (SetTimeOfDayMethod == null
            || !CommanderHostAuthority.IsHostAuthority()
            || !rangeLocked
            || range <= 1f)
        {
            return false;
        }

        presetIndex = (presetIndex + 1) % Presets.Length;
        return ApplyPreset();
    }

    internal string PresetName => rangeLocked ? Presets[presetIndex].Name : "UNSET";

    private bool ApplyPreset()
    {
        LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
        if (level == null)
        {
            return false;
        }

        float value = Presets[presetIndex].Fraction * range;
        try
        {
            SetTimeOfDayMethod!.Invoke(level, new object[] { value });
        }
        catch (Exception exception)
        {
            CommanderPlugin.Log.LogError($"Time of day preset failed: {exception.Message}");
            return false;
        }

        cycleStartTime = Time.unscaledTime;
        cycleStartValue = value;
        lastObservedValue = value;
        return true;
    }

    internal string ClockText
    {
        get
        {
            LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
            if (level == null || FormatTimeOfDayMethod == null)
            {
                return string.Empty;
            }

            return FormatTimeOfDayMethod.Invoke(null, new object[] { level.NetworktimeOfDay, false }) as string ?? string.Empty;
        }
    }
}
