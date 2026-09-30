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
    private static readonly FieldInfo? IsDayLightField = AccessTools.Field(typeof(LevelInfo), "isDayLight");
    private static readonly FieldInfo? DaylightEventField = AccessTools.Field(typeof(LevelInfo), "onDaylightChange");

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

    /// <summary>
    /// Diagnostic for the missing night lights. Reports what the base game itself believes
    /// the light state is, and how many subsystems subscribed to the daylight change event.
    /// If the subscriber count is zero, nothing is listening for nightfall.
    /// </summary>
    internal string DaylightStateText
    {
        get
        {
            LevelInfo? level = NetworkSceneSingleton<LevelInfo>.i;
            if (level == null)
            {
                return "no level";
            }

            object? isDay = IsDayLightField?.GetValue(level);
            int subscribers = 0;
            if (DaylightEventField?.GetValue(level) is Delegate handler)
            {
                subscribers = handler.GetInvocationList().Length;
            }

            return $"isDayLight={isDay ?? "?"}  subscribers={subscribers}  clock={ClockText}";
        }
    }

    internal void LogDaylightState()
    {
        CommanderPlugin.Log.LogInfo("[TimeOfDay] " + DaylightStateText);
    }

    private static readonly Type? BuildingLightsType = AccessTools.TypeByName("BuildingLights");
    private static readonly FieldInfo? BuildingLightsArrayField =
        BuildingLightsType == null ? null : AccessTools.Field(BuildingLightsType, "lights");
    private static readonly FieldInfo? BuildingLightsToggleField =
        BuildingLightsType == null ? null : AccessTools.Field(BuildingLightsType, "daylightToggle");

    /// <summary>
    /// One-shot scene scan for building lights. Only ever runs from an explicit button press,
    /// never from a tick or GUI frame loop, so the FindObjectsOfType cost is paid once.
    /// </summary>
    internal string BuildingLightReport
    {
        get
        {
            if (BuildingLightsType == null || BuildingLightsArrayField == null)
            {
                return "BuildingLights type not found";
            }

            UnityEngine.Object[] all = UnityEngine.Object.FindObjectsOfType(BuildingLightsType);
            int withLightArray = 0;
            int daylightToggle = 0;
            int lightTotal = 0;
            int lightEnabled = 0;
            int lightZeroIntensity = 0;

            for (int i = 0; i < all.Length; i++)
            {
                object? component = all[i];
                if (component == null)
                {
                    continue;
                }

                if (BuildingLightsToggleField?.GetValue(component) is bool toggle && toggle)
                {
                    daylightToggle++;
                }

                if (BuildingLightsArrayField.GetValue(component) is not Array lights || lights.Length == 0)
                {
                    continue;
                }

                withLightArray++;
                for (int j = 0; j < lights.Length; j++)
                {
                    if (lights.GetValue(j) is not Light light || light == null)
                    {
                        continue;
                    }

                    lightTotal++;
                    if (light.enabled)
                    {
                        lightEnabled++;
                    }
                    if (light.intensity <= 0f)
                    {
                        lightZeroIntensity++;
                    }
                }
            }

            return $"buildings={all.Length} withLightArray={withLightArray} daylightToggle={daylightToggle} "
                + $"lights={lightTotal} enabled={lightEnabled} zeroIntensity={lightZeroIntensity}";
        }
    }

    internal void LogBuildingLights()
    {
        CommanderPlugin.Log.LogInfo("[TimeOfDay] " + DaylightStateText);
        CommanderPlugin.Log.LogInfo("[Lights] " + BuildingLightReport);
    }

    private static readonly Type? NavLightType = AccessTools.TypeByName("NavLight");
    private static readonly Type? NavLightsType = AccessTools.TypeByName("NavLights");
    private static readonly FieldInfo? NavLightIsOnField =
        NavLightType == null ? null : AccessTools.Field(NavLightType, "isOn");
    private static readonly FieldInfo? NavLightRendererField =
        NavLightType == null ? null : AccessTools.Field(NavLightType, "renderer");
    private static readonly FieldInfo? NavLightsArrayField =
        NavLightsType == null ? null : AccessTools.Field(NavLightsType, "navLights");
    private static readonly MethodInfo? NavLightToggleStateMethod =
        NavLightType == null ? null : AccessTools.Method(NavLightType, "ToggleState");

    private string ScanType(string label, Type? type, FieldInfo? stateField, FieldInfo? arrayField)
    {
        if (type == null)
        {
            return $"{label}: type not found";
        }

        UnityEngine.Object[] all = UnityEngine.Object.FindObjectsOfType(type);
        int hosts = 0;
        int stateTrue = 0;
        int stateFalse = 0;

        for (int i = 0; i < all.Length; i++)
        {
            object? component = all[i];
            if (component == null)
            {
                continue;
            }

            bool counted = false;
            if (arrayField != null && arrayField.GetValue(component) is Array items)
            {
                for (int j = 0; j < items.Length; j++)
                {
                    object? item = items.GetValue(j);
                    if (item == null)
                    {
                        continue;
                    }

                    counted = true;
                    if (stateField != null)
                    {
                        bool on = stateField.GetValue(item) is bool value && value;
                        if (on)
                        {
                            stateTrue++;
                        }
                        else
                        {
                            stateFalse++;
                        }
                    }
                }
            }

            if (counted)
            {
                hosts++;
            }
        }

        return stateField == null
            ? $"{label}: instances={all.Length} hostsWithArray={hosts}"
            : $"{label}: instances={all.Length} hostsWithArray={hosts} on={stateTrue} off={stateFalse}";
    }

    /// <summary>One-shot scan of aircraft and ground unit nav lights. Button press only.</summary>
    internal string UnitLightReport
    {
        get
        {
            return ScanType("navLight", NavLightsType, NavLightIsOnField, NavLightsArrayField)
                + "  |  " + ScanType("navLightComp", NavLightType, NavLightIsOnField, null);
        }
    }

    internal void LogUnitLights()
    {
        CommanderPlugin.Log.LogInfo("[Lights] " + UnitLightReport);
    }

    /// <summary>Turns nav lights on for every aircraft in the scene, on the host only.</summary>
    internal int ForceUnitLightsOn()
    {
        if (!CommanderHostAuthority.IsHostAuthority())
        {
            return 0;
        }

        UnityEngine.Object[] all = UnityEngine.Object.FindObjectsOfType(NavLightType);
        int toggled = 0;
        for (int i = 0; i < all.Length; i++)
        {
            object? component = all[i];
            if (component == null || NavLightIsOnField?.GetValue(component) is not bool on || on)
            {
                continue;
            }

            if (NavLightRendererField?.GetValue(component) is Renderer renderer && renderer != null)
            {
                renderer.enabled = true;
            }

            NavLightToggleStateMethod?.Invoke(component, null);
            toggled++;
        }

        if (toggled > 0)
        {
            CommanderPlugin.Log.LogInfo($"[Lights] forced {toggled} nav light(s) on.");
        }
        return toggled;
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
