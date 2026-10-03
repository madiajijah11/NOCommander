using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal static class CommanderSettings
{
    private static ConfigFile? config;
    private static readonly Dictionary<string, ConfigEntryBase> entries = new();

    internal static float UiScale { get; set; } = 1.5f;
    internal static bool ModEnabled { get => Get("General", "Enabled", true); set => Set("General", "Enabled", value); }
    internal static bool LimitToFactoryVehicles { get => Get("Gameplay", "LimitToFactoryVehicles", false); set => Set("Gameplay", "LimitToFactoryVehicles", value); }
    internal static bool AutoRtbEnabled { get => Get("Gameplay", "AutoRtbEnabled", true); set => Set("Gameplay", "AutoRtbEnabled", value); }
    internal static bool AutoServiceEnabled { get => Get("Gameplay", "AutoServiceEnabled", true); set => Set("Gameplay", "AutoServiceEnabled", value); }
    internal static bool DoctrineEnabled { get => Get("Gameplay", "DoctrineEnabled", true); set => Set("Gameplay", "DoctrineEnabled", value); }
    internal static float DoctrineRefreshIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "DoctrineRefreshIntervalSeconds", 2f)); set => Set("Gameplay", "DoctrineRefreshIntervalSeconds", Mathf.Max(1f, value)); }
    internal static bool ThreatAwareMovementEnabled { get => Get("Gameplay", "ThreatAwareMovementEnabled", true); set => Set("Gameplay", "ThreatAwareMovementEnabled", value); }
    internal static float ThreatDangerRadiusMeters { get => Mathf.Max(0f, Get("Gameplay", "ThreatDangerRadiusMeters", 200f)); set => Set("Gameplay", "ThreatDangerRadiusMeters", Mathf.Max(0f, value)); }
    internal static float AutoRtbFuelPercent { get => Get("Gameplay", "AutoRtbFuelPercent", 0.15f); set => Set("Gameplay", "AutoRtbFuelPercent", value); }
    internal static float AutoRtbAmmoPercent { get => Get("Gameplay", "AutoRtbAmmoPercent", 0.20f); set => Set("Gameplay", "AutoRtbAmmoPercent", value); }
    internal static bool WarActivityEnabled { get => Get("Gameplay", "WarActivityEnabled", true); set => Set("Gameplay", "WarActivityEnabled", value); }
    internal static string WarActivityDoctrine { get => Get("Gameplay", "WarActivityDoctrine", "Balanced"); set => Set("Gameplay", "WarActivityDoctrine", value); }
    internal static float WarActivityIntervalSeconds { get => Get("Gameplay", "WarActivityIntervalSeconds", 8f); set => Set("Gameplay", "WarActivityIntervalSeconds", value); }
    internal static float WarActivityReserveFunds { get => Get("Gameplay", "WarActivityReserveFunds", 25000f); set => Set("Gameplay", "WarActivityReserveFunds", value); }
    internal static int WarActivityMinimumGround { get => Get("Gameplay", "WarActivityMinimumGround", 12); set => Set("Gameplay", "WarActivityMinimumGround", value); }
    internal static int WarActivityMinimumLogistics { get => Get("Gameplay", "WarActivityMinimumLogistics", 2); set => Set("Gameplay", "WarActivityMinimumLogistics", value); }
    internal static int WarActivityMinimumAir { get => Get("Gameplay", "WarActivityMinimumAir", 4); set => Set("Gameplay", "WarActivityMinimumAir", value); }
    internal static float WarActivityAirIntervalSeconds { get => Get("Gameplay", "WarActivityAirIntervalSeconds", 20f); set => Set("Gameplay", "WarActivityAirIntervalSeconds", value); }
    internal static float WarActivityNavalIntervalSeconds { get => Get("Gameplay", "WarActivityNavalIntervalSeconds", 30f); set => Set("Gameplay", "WarActivityNavalIntervalSeconds", value); }
    internal static int WarActivityMinimumNaval { get => Get("Gameplay", "WarActivityMinimumNaval", 1); set => Set("Gameplay", "WarActivityMinimumNaval", value); }
    internal static float WarActivityFrontlineIntervalSeconds { get => Get("Gameplay", "WarActivityFrontlineIntervalSeconds", 15f); set => Set("Gameplay", "WarActivityFrontlineIntervalSeconds", value); }
    internal static float WarActivitySupplyIntervalSeconds { get => Get("Gameplay", "WarActivitySupplyIntervalSeconds", 45f); set => Set("Gameplay", "WarActivitySupplyIntervalSeconds", value); }
    internal static bool ObjectiveDefenseEnabled { get => Get("Gameplay", "ObjectiveDefenseEnabled", true); set => Set("Gameplay", "ObjectiveDefenseEnabled", value); }
    internal static float ObjectiveDefenseIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "ObjectiveDefenseIntervalSeconds", 5f)); set => Set("Gameplay", "ObjectiveDefenseIntervalSeconds", Mathf.Max(1f, value)); }
    internal static float ObjectiveDefenseThreatRadiusMeters { get => Mathf.Max(0f, Get("Gameplay", "ObjectiveDefenseThreatRadiusMeters", 1500f)); set => Set("Gameplay", "ObjectiveDefenseThreatRadiusMeters", Mathf.Max(0f, value)); }
    internal static float ObjectiveDefenseRadiusMeters { get => Mathf.Max(1f, Get("Gameplay", "ObjectiveDefenseRadiusMeters", 600f)); set => Set("Gameplay", "ObjectiveDefenseRadiusMeters", Mathf.Max(1f, value)); }
    internal static int ObjectiveDefenseMaxUnitsPerSite { get => Get("Gameplay", "ObjectiveDefenseMaxUnitsPerSite", 6); set => Set("Gameplay", "ObjectiveDefenseMaxUnitsPerSite", value); }
    internal static float ObjectiveDefenseReissueSeconds { get => Mathf.Max(1f, Get("Gameplay", "ObjectiveDefenseReissueSeconds", 10f)); set => Set("Gameplay", "ObjectiveDefenseReissueSeconds", Mathf.Max(1f, value)); }
    internal static float ObjectiveDefenseReleaseMeters { get => Mathf.Max(1f, Get("Gameplay", "ObjectiveDefenseReleaseMeters", 900f)); set => Set("Gameplay", "ObjectiveDefenseReleaseMeters", Mathf.Max(1f, value)); }
    internal static float ObjectiveDefenseWarnIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "ObjectiveDefenseWarnIntervalSeconds", 20f)); set => Set("Gameplay", "ObjectiveDefenseWarnIntervalSeconds", Mathf.Max(1f, value)); }
    internal static bool BattleGroupOrdersEnabled { get => Get("Gameplay", "BattleGroupOrdersEnabled", true); set => Set("Gameplay", "BattleGroupOrdersEnabled", value); }
    internal static float BattleGroupIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "BattleGroupIntervalSeconds", 5f)); set => Set("Gameplay", "BattleGroupIntervalSeconds", Mathf.Max(1f, value)); }
    internal static int BattleGroupMinUnits { get => Get("Gameplay", "BattleGroupMinUnits", 1); set => Set("Gameplay", "BattleGroupMinUnits", value); }
    internal static float BattleGroupReissueSeconds { get => Mathf.Max(1f, Get("Gameplay", "BattleGroupReissueSeconds", 12f)); set => Set("Gameplay", "BattleGroupReissueSeconds", Mathf.Max(1f, value)); }
    internal static float BattleGroupWarnIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "BattleGroupWarnIntervalSeconds", 30f)); set => Set("Gameplay", "BattleGroupWarnIntervalSeconds", Mathf.Max(1f, value)); }
    internal static bool AirMissionQueueEnabled { get => Get("Gameplay", "AirMissionQueueEnabled", true); set => Set("Gameplay", "AirMissionQueueEnabled", value); }
    internal static float AirMissionQueueIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "AirMissionQueueIntervalSeconds", 5f)); set => Set("Gameplay", "AirMissionQueueIntervalSeconds", Mathf.Max(1f, value)); }
    internal static int AirMissionQueueMaxDepth { get => Get("Gameplay", "AirMissionQueueMaxDepth", 3); set => Set("Gameplay", "AirMissionQueueMaxDepth", value); }
    internal static float ReconFreshnessSeconds { get => Mathf.Max(1f, Get("Gameplay", "ReconFreshnessSeconds", 8f)); set => Set("Gameplay", "ReconFreshnessSeconds", Mathf.Max(1f, value)); }
    internal static bool EmergencyResponseEnabled { get => Get("Gameplay", "EmergencyResponseEnabled", true); set => Set("Gameplay", "EmergencyResponseEnabled", value); }
    internal static float EmergencyResponseIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "EmergencyResponseIntervalSeconds", 10f)); set => Set("Gameplay", "EmergencyResponseIntervalSeconds", Mathf.Max(1f, value)); }
    internal static float EmergencyResponseRadiusMeters { get => Mathf.Max(0f, Get("Gameplay", "EmergencyResponseRadiusMeters", 1200f)); set => Set("Gameplay", "EmergencyResponseRadiusMeters", Mathf.Max(0f, value)); }
    internal static int EmergencyResponseMinUnits { get => Get("Gameplay", "EmergencyResponseMinUnits", 3); set => Set("Gameplay", "EmergencyResponseMinUnits", value); }
    internal static float EmergencyResponseWarnIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "EmergencyResponseWarnIntervalSeconds", 30f)); set => Set("Gameplay", "EmergencyResponseWarnIntervalSeconds", Mathf.Max(1f, value)); }
    internal static bool TimeOfDaySyncEnabled { get => Get("Gameplay", "TimeOfDaySyncEnabled", true); set => Set("Gameplay", "TimeOfDaySyncEnabled", value); }
    internal static float TimeOfDayCycleMinutes { get => Get("Gameplay", "TimeOfDayCycleMinutes", 60f); set => Set("Gameplay", "TimeOfDayCycleMinutes", value); }
    internal static float TimeOfDaySyncIntervalSeconds { get => Get("Gameplay", "TimeOfDaySyncIntervalSeconds", 5f); set => Set("Gameplay", "TimeOfDaySyncIntervalSeconds", value); }
    internal static bool TacticalAudioEnabled { get => Get("Audio", "TacticalAudioEnabled", false); set => Set("Audio", "TacticalAudioEnabled", value); }
    internal static float TacticalAudioVolume { get => Mathf.Clamp01(Get("Audio", "TacticalAudioVolume", 0f)); set => Set("Audio", "TacticalAudioVolume", Mathf.Clamp01(value)); }
    internal static bool RadioChatterEnabled { get => Get("Audio", "RadioChatterEnabled", false); set => Set("Audio", "RadioChatterEnabled", value); }
    internal static float RadioChatterCooldownSeconds { get => Mathf.Max(0.5f, Get("Audio", "RadioChatterCooldownSeconds", 2.5f)); set => Set("Audio", "RadioChatterCooldownSeconds", Mathf.Max(0.5f, value)); }
    internal static bool FrontlineDirectivesEnabled { get => Get("Gameplay", "FrontlineDirectivesEnabled", true); set => Set("Gameplay", "FrontlineDirectivesEnabled", value); }
    internal static float FrontlineDirectiveIntervalSeconds { get => Mathf.Max(1f, Get("Gameplay", "FrontlineDirectiveIntervalSeconds", 6f)); set => Set("Gameplay", "FrontlineDirectiveIntervalSeconds", Mathf.Max(1f, value)); }
    internal static float FrontlinePushDistanceMeters { get => Mathf.Max(100f, Get("Gameplay", "FrontlinePushDistanceMeters", 800f)); set => Set("Gameplay", "FrontlinePushDistanceMeters", Mathf.Max(100f, value)); }
    internal static float FrontlineHoldDistanceMeters { get => Mathf.Max(50f, Get("Gameplay", "FrontlineHoldDistanceMeters", 400f)); set => Set("Gameplay", "FrontlineHoldDistanceMeters", Mathf.Max(50f, value)); }
    internal static bool BattlefieldVisualFxEnabled { get => Get("Visuals", "BattlefieldVisualFxEnabled", true); set => Set("Visuals", "BattlefieldVisualFxEnabled", value); }
    internal static bool ShowFrontlineOverlay { get => Get("Visuals", "ShowFrontlineOverlay", true); set => Set("Visuals", "ShowFrontlineOverlay", value); }
    internal static bool ShipRecoveryEnabled { get => Get("Gameplay", "ShipRecoveryEnabled", true); set => Set("Gameplay", "ShipRecoveryEnabled", value); }
    internal static bool TimeOfDayControlEnabled { get => Get("Gameplay", "TimeOfDayControlEnabled", true); set => Set("Gameplay", "TimeOfDayControlEnabled", value); }
    internal static float TimeOfDayRateMultiplier { get => Get("Gameplay", "TimeOfDayRateMultiplier", 1f); set => Set("Gameplay", "TimeOfDayRateMultiplier", value); }
    internal static bool GameSpeedEnabled { get => Get("Gameplay", "GameSpeedEnabled", true); set => Set("Gameplay", "GameSpeedEnabled", value); }
    internal static float GameSpeedValue { get => Get("Gameplay", "GameSpeedValue", 1f); set => Set("Gameplay", "GameSpeedValue", value); }
    internal static bool ShowCommandButton { get => Get("UI", "ShowCommandButton", true); set => Set("UI", "ShowCommandButton", value); }
    internal static bool ShowFactionMoney { get => Get("UI", "ShowFactionMoney", true); set => Set("UI", "ShowFactionMoney", value); }
    internal static bool ShowTacticalMap { get => Get("UI", "ShowTacticalMap", true); set => Set("UI", "ShowTacticalMap", value); }
    internal static bool ShowSelectionBar { get => Get("UI", "ShowSelectionBar", true); set => Set("UI", "ShowSelectionBar", value); }
    internal static bool ShowPinnedUnits { get => Get("UI", "ShowPinnedUnits", true); set => Set("UI", "ShowPinnedUnits", value); }
    internal static bool ShowUnitSystems { get => Get("UI", "ShowUnitSystems", true); set => Set("UI", "ShowUnitSystems", value); }
    internal static bool ShowDepotUi { get => Get("UI", "ShowDepotUi", true); set => Set("UI", "ShowDepotUi", value); }
    internal static bool ShowSupplyUi { get => Get("UI", "ShowSupplyUi", true); set => Set("UI", "ShowSupplyUi", value); }
    internal static bool ShowAirCommandUi { get => Get("UI", "ShowAirCommandUi", true); set => Set("UI", "ShowAirCommandUi", value); }
    internal static bool ShowNavalUi { get => Get("UI", "ShowNavalUi", true); set => Set("UI", "ShowNavalUi", value); }
    internal static bool ShowSamAnalyzerUi { get => Get("UI", "ShowSamAnalyzerUi", true); set => Set("UI", "ShowSamAnalyzerUi", value); }
    internal static bool ShowWorldMarkers { get => Get("UI", "ShowWorldMarkers", true); set => Set("UI", "ShowWorldMarkers", value); }
    internal static int SamScanQueriesPerFrame { get => Get("SAM Analyzer", "RaycastsPerFrame", 64); set => Set("SAM Analyzer", "RaycastsPerFrame", value); }

    internal static KeyboardShortcut PrimaryAction { get => GetShortcut("PrimaryAction", KeyCode.Mouse0, "Select units and place world targets."); set => Set("Keybinds", "PrimaryAction", value); }
    internal static KeyboardShortcut SecondaryAction { get => GetShortcut("SecondaryAction", KeyCode.Mouse1, "Issue move orders."); set => Set("Keybinds", "SecondaryAction", value); }
    internal static KeyboardShortcut AddToSelection { get => GetShortcut("AddToSelection", KeyCode.LeftShift, "Hold while selecting to add units."); set => Set("Keybinds", "AddToSelection", value); }
    internal static KeyboardShortcut RepeatDeployment { get => GetShortcut("RepeatDeployment", KeyCode.LeftShift, "Hold while placing a supply target to repeat the deployment."); set => Set("Keybinds", "RepeatDeployment", value); }
    internal static KeyboardShortcut DeleteUnitModifier { get => GetShortcut("DeleteUnitModifier", KeyCode.LeftAlt, "Hold to turn PIN into DEL."); set => Set("Keybinds", "DeleteUnitModifier", value); }
    internal static KeyboardShortcut CameraCenterFollow { get => GetShortcut("CameraCenterFollow", KeyCode.Space, "Tap to center; hold to center and follow."); set => Set("Keybinds", "CameraCenterFollow", value); }
    internal static KeyboardShortcut ToggleHoldFire { get => GetShortcut("ToggleHoldFire", KeyCode.F, "Toggle Hold Fire / Free Fire for selected units."); set => Set("Keybinds", "ToggleHoldFire", value); }
    internal static KeyboardShortcut ToggleUi { get => GetShortcut("ToggleUi", KeyCode.H, "Cycle visible, Commander UI hidden, and all UI hidden."); set => Set("Keybinds", "ToggleUi", value); }
    internal static KeyboardShortcut SelectAllArmy { get => GetShortcut("SelectAllArmy", KeyCode.BackQuote, "Select all friendly combat army."); set => Set("Keybinds", "SelectAllArmy", value); }
    internal static KeyboardShortcut ArtilleryBarrage { get => GetShortcut("ArtilleryBarrage", KeyCode.B, "Call in artillery/MRLS barrage on target area."); set => Set("Keybinds", "ArtilleryBarrage", value); }
    internal static KeyboardShortcut GlobalRadarSilence { get => GetShortcut("GlobalRadarSilence", KeyCode.R, new[] { KeyCode.LeftControl }, "Toggle global EMCON / radar silence across all friendly units."); set => Set("Keybinds", "GlobalRadarSilence", value); }
    internal static KeyboardShortcut CameraForward { get => GetShortcut("CameraForward", KeyCode.W, "Move the Commander camera forward."); set => Set("Keybinds", "CameraForward", value); }
    internal static KeyboardShortcut CameraBackward { get => GetShortcut("CameraBackward", KeyCode.S, "Move the Commander camera backward."); set => Set("Keybinds", "CameraBackward", value); }
    internal static KeyboardShortcut CameraLeft { get => GetShortcut("CameraLeft", KeyCode.A, "Move the Commander camera left."); set => Set("Keybinds", "CameraLeft", value); }
    internal static KeyboardShortcut CameraRight { get => GetShortcut("CameraRight", KeyCode.D, "Move the Commander camera right."); set => Set("Keybinds", "CameraRight", value); }
    internal static KeyboardShortcut CameraUp { get => GetShortcut("CameraUp", KeyCode.Q, "Move the Commander camera upward."); set => Set("Keybinds", "CameraUp", value); }
    internal static KeyboardShortcut CameraDown { get => GetShortcut("CameraDown", KeyCode.E, "Move the Commander camera downward."); set => Set("Keybinds", "CameraDown", value); }
    internal static KeyboardShortcut CameraFreeLook { get => GetShortcut("CameraFreeLook", KeyCode.Mouse2, "Hold while moving the mouse to look around in Commander mode."); set => Set("Keybinds", "CameraFreeLook", value); }
    internal static KeyboardShortcut CameraBoost { get => GetShortcut("CameraBoost", KeyCode.LeftShift, "Hold for faster Commander camera movement."); set => Set("Keybinds", "CameraBoost", value); }
    internal static KeyboardShortcut CameraRotateLeft { get => GetShortcut("CameraRotateLeft", KeyCode.LeftArrow, "Rotate the Commander camera view left."); set => Set("Keybinds", "CameraRotateLeft", value); }
    internal static KeyboardShortcut CameraRotateRight { get => GetShortcut("CameraRotateRight", KeyCode.RightArrow, "Rotate the Commander camera view right."); set => Set("Keybinds", "CameraRotateRight", value); }
    internal static KeyboardShortcut CameraPitchUp { get => GetShortcut("CameraPitchUp", KeyCode.UpArrow, "Tilt the Commander camera view upward."); set => Set("Keybinds", "CameraPitchUp", value); }
    internal static KeyboardShortcut CameraPitchDown { get => GetShortcut("CameraPitchDown", KeyCode.DownArrow, "Tilt the Commander camera view downward."); set => Set("Keybinds", "CameraPitchDown", value); }

    internal static string AirCommandMode { get => Get("Air Command", "MissionMode", "AirGuard"); set => Set("Air Command", "MissionMode", value); }
    internal static string AirLoadoutBalance { get => Get("Air Command", "LoadoutBalance", "Primary"); set => Set("Air Command", "LoadoutBalance", value); }
    internal static float AirTargetAltitude { get => Get("Air Command", "TargetAltitude", 0f); set => Set("Air Command", "TargetAltitude", value); }
    internal static bool AirGuardTargetOrdnance { get => Get("Air Command", "AirGuardTargetOrdnance", false); set => Set("Air Command", "AirGuardTargetOrdnance", value); }
    internal static bool AradSaturationAttack { get => Get("Air Command", "AradSaturationAttack", false); set => Set("Air Command", "AradSaturationAttack", value); }
    internal static bool AirIncludeInternalCannons { get => Get("Air Command", "IncludeInternalCannons", true); set => Set("Air Command", "IncludeInternalCannons", value); }
    internal static float AwacsRadiusKm { get => Get("Air Command", "AwacsRadiusKm", 60f); set => Set("Air Command", "AwacsRadiusKm", value); }
    internal static float CasRadiusKm { get => Get("Air Command", "CasRadiusKm", 20f); set => Set("Air Command", "CasRadiusKm", value); }
    internal static float AirGuardRadiusKm { get => Get("Air Command", "AirGuardRadiusKm", 30f); set => Set("Air Command", "AirGuardRadiusKm", value); }
    internal static float AradRadiusKm { get => Get("Air Command", "AradRadiusKm", 50f); set => Set("Air Command", "AradRadiusKm", value); }
    internal static float StrikeRadiusKm { get => Get("Air Command", "StrikeRadiusKm", 80f); set => Set("Air Command", "StrikeRadiusKm", value); }

    internal static void Initialize(ConfigFile configFile)
    {
        config = configFile;
        _ = ModEnabled;
        _ = LimitToFactoryVehicles;
        _ = AutoRtbEnabled;
        _ = AutoServiceEnabled;
        _ = DoctrineEnabled;
        _ = DoctrineRefreshIntervalSeconds;
        _ = ThreatAwareMovementEnabled;
        _ = ThreatDangerRadiusMeters;
        _ = AutoRtbFuelPercent;
        _ = AutoRtbAmmoPercent;
        _ = ObjectiveDefenseEnabled;
        _ = ObjectiveDefenseIntervalSeconds;
        _ = ObjectiveDefenseThreatRadiusMeters;
        _ = ObjectiveDefenseRadiusMeters;
        _ = ObjectiveDefenseMaxUnitsPerSite;
        _ = ObjectiveDefenseReissueSeconds;
        _ = ObjectiveDefenseReleaseMeters;
        _ = ObjectiveDefenseWarnIntervalSeconds;
        _ = BattleGroupOrdersEnabled;
        _ = BattleGroupIntervalSeconds;
        _ = BattleGroupMinUnits;
        _ = BattleGroupReissueSeconds;
        _ = BattleGroupWarnIntervalSeconds;
        _ = AirMissionQueueEnabled;
        _ = AirMissionQueueIntervalSeconds;
        _ = AirMissionQueueMaxDepth;
        _ = ReconFreshnessSeconds;
        _ = EmergencyResponseEnabled;
        _ = EmergencyResponseIntervalSeconds;
        _ = EmergencyResponseRadiusMeters;
        _ = EmergencyResponseMinUnits;
        _ = EmergencyResponseWarnIntervalSeconds;
        _ = TacticalAudioEnabled;
        _ = TacticalAudioVolume;
        _ = RadioChatterEnabled;
        _ = RadioChatterCooldownSeconds;
        _ = FrontlineDirectivesEnabled;
        _ = FrontlineDirectiveIntervalSeconds;
        _ = FrontlinePushDistanceMeters;
        _ = FrontlineHoldDistanceMeters;
        _ = BattlefieldVisualFxEnabled;
        _ = ShowFrontlineOverlay;
        _ = ShipRecoveryEnabled;
        _ = ShowCommandButton;
        _ = PrimaryAction;
        _ = SecondaryAction;
        _ = AddToSelection;
        _ = RepeatDeployment;
        _ = DeleteUnitModifier;
        _ = CameraCenterFollow;
        _ = ToggleUi;
        _ = CameraForward;
        _ = CameraBackward;
        _ = CameraLeft;
        _ = CameraRight;
        _ = CameraUp;
        _ = CameraDown;
        _ = CameraFreeLook;
        _ = CameraBoost;
        _ = SamScanQueriesPerFrame;
        _ = AirCommandMode;
        _ = AwacsRadiusKm;
        _ = CasRadiusKm;
        _ = AirGuardRadiusKm;
        _ = AradRadiusKm;
        _ = StrikeRadiusKm;
    }

    private static KeyboardShortcut GetShortcut(string key, KeyCode defaultKey, string description)
    {
        return GetShortcut(key, defaultKey, new KeyCode[0], description);
    }

    private static KeyboardShortcut GetShortcut(string key, KeyCode defaultKey, KeyCode[] modifiers, string description)
    {
        if (config == null) return new KeyboardShortcut(defaultKey, modifiers);
        string lookup = "Keybinds/" + key;
        if (entries.TryGetValue(lookup, out ConfigEntryBase existing))
        {
            return ((ConfigEntry<KeyboardShortcut>)existing).Value;
        }

        ConfigEntry<KeyboardShortcut> created = config.Bind(
            "Keybinds",
            key,
            new KeyboardShortcut(defaultKey, modifiers),
            new ConfigDescription(description + " Set the main key to None to disable it."));
        entries.Add(lookup, created);
        return created.Value;
    }

    private static T Get<T>(string section, string key, T defaultValue)
    {
        ConfigEntry<T>? entry = GetEntry(section, key, defaultValue);
        return entry == null ? defaultValue : entry.Value;
    }

    private static void Set<T>(string section, string key, T value)
    {
        ConfigEntry<T>? entry = GetEntry(section, key, value);
        if (entry != null) entry.Value = value;
    }

    private static ConfigEntry<T>? GetEntry<T>(string section, string key, T defaultValue)
    {
        if (config == null) return null;
        string lookup = section + "/" + key;
        if (entries.TryGetValue(lookup, out ConfigEntryBase existing)) return (ConfigEntry<T>)existing;
        ConfigEntry<T> created = config.Bind(section, key, defaultValue);
        entries.Add(lookup, created);
        return created;
    }
}
