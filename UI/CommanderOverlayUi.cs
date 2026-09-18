using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderOverlayUi
{
    internal static CommanderOverlayUi? Instance { get; private set; }

    private const int WindowId = 0x434F4D4D;
    private const int ReserveWindowId = 0x434F4D52;
    private const int PinnedWindowId = 0x434F4D50;
    private const int RadarWindowId = 0x434F4D44;
    private const int SettingsWindowId = 0x434F4D53;

    private readonly CommanderSelectionService selectionService;
    private readonly CommanderMoveService moveService;
    private readonly CommanderSpawnService spawnService;
    private readonly CommanderRadarService radarService;
    private readonly CommanderMobileEmplacementService mobileEmplacementService;
    private readonly CommanderRepairService repairService;
    private readonly CommanderDirectPathService directPathService;
    private readonly CommanderSupplyHeliService supplyHeliService;
    private readonly CommanderAirCommandService airCommandService;
    private readonly CommanderNavalPurchaseService navalPurchaseService;
    private readonly CommanderSamSiteAnalyzerService samSiteAnalyzerService;
    private readonly CommanderSamSiteService samSiteService;
    private readonly CommanderSupplyHeliUi supplyHeliUi;
    private readonly CommanderAirCommandUi airCommandUi;
    private readonly CommanderNavalPurchaseUi navalPurchaseUi;
    private readonly CommanderSamSiteAnalyzerUi samSiteAnalyzerUi;
    private readonly CommanderDepotUi depotUi;
    private readonly CommanderWorldMarkerRenderer worldMarkerRenderer;
    private readonly Action unlockAdvancedFeatures;
    private readonly Action exitCommander;

    private bool panelVisible;
    private bool reserveWindowVisible;
    private bool panelHelpVisible;
    private bool reserveHelpVisible;
    private bool pinnedHelpVisible;
    private bool radarHelpVisible;
    private bool selectionHelpVisible;
    private bool settingsVisible;
    private bool settingsHelpVisible;
    private bool advancedUnlockConfirmation;
    private int settingsTab;
    private string? bindingCapture;
    private bool pinnedWindowVisible = true;
    private int pinnedTab = 1;
    private bool siteAirbaseDropdownOpen;
    private bool siteThresholdDropdownOpen;
    private bool showSupplyMissions = true;
    private bool showAirCommandMissions = true;
    private readonly Dictionary<Canvas, bool> screenshotCanvasStates = new();
    private int screenshotUiStage;
    private bool reopenTacticalMapAfterScreenshot;
    private bool showCommandButton = CommanderSettings.ShowCommandButton;
    private bool showFactionMoney = CommanderSettings.ShowFactionMoney;
    private bool showTacticalMap = CommanderSettings.ShowTacticalMap;
    private bool showSelectionBar = CommanderSettings.ShowSelectionBar;
    private bool showPinnedUnits = CommanderSettings.ShowPinnedUnits;
    private bool showUnitSystems = CommanderSettings.ShowUnitSystems;
    private bool showDepotUi = CommanderSettings.ShowDepotUi;
    private bool showSupplyUi = CommanderSettings.ShowSupplyUi;
    private bool showAirCommandUi = CommanderSettings.ShowAirCommandUi;
    private bool showNavalUi = CommanderSettings.ShowNavalUi;
    private bool showSamAnalyzerUi = CommanderSettings.ShowSamAnalyzerUi;
    private bool showWorldMarkers = CommanderSettings.ShowWorldMarkers;
    private int reserveTab;
    private int reserveMultiplier = 1;
    private bool positionsInitialized;
    private Rect launcherRect;
    private Rect moneyRect;
    private Rect panelRect;
    private Rect reserveWindowRect;
    private Rect selectionBarRect;
    private Rect pinnedWindowRect;
    private Rect radarWindowRect;
    private Rect selectionHelpRect;
    private Rect settingsWindowRect;
    private Rect pinnedLauncherRect;
    private Vector2 reserveScroll;
    private readonly List<CommanderControlGroupsService.ControlGroupInfo> activeGroupsScratch = new();
    private Vector2 pinnedScroll;
    private GUIStyle? ghostCommandStyle;
    private Unit? siteUiTarget;

    internal CommanderOverlayUi(
        CommanderSelectionService selectionService,
        CommanderMoveService moveService,
        CommanderSpawnService spawnService,
        CommanderRadarService radarService,
        CommanderMobileEmplacementService mobileEmplacementService,
        CommanderRepairService repairService,
        CommanderDirectPathService directPathService,
        CommanderSupplyHeliService supplyHeliService,
        CommanderAirCommandService airCommandService,
        CommanderNavalPurchaseService navalPurchaseService,
        CommanderSamSiteAnalyzerService samSiteAnalyzerService,
        CommanderSamSiteService samSiteService,
        Action unlockAdvancedFeatures,
        Action exitCommander)
    {
        Instance = this;
        this.selectionService = selectionService;
        this.moveService = moveService;
        this.spawnService = spawnService;
        this.radarService = radarService;
        this.mobileEmplacementService = mobileEmplacementService;
        this.repairService = repairService;
        this.directPathService = directPathService;
        this.supplyHeliService = supplyHeliService;
        this.airCommandService = airCommandService;
        this.navalPurchaseService = navalPurchaseService;
        this.samSiteAnalyzerService = samSiteAnalyzerService;
        this.samSiteService = samSiteService;
        this.unlockAdvancedFeatures = unlockAdvancedFeatures;
        this.exitCommander = exitCommander;
        supplyHeliUi = new CommanderSupplyHeliUi(supplyHeliService);
        airCommandUi = new CommanderAirCommandUi(airCommandService);
        navalPurchaseUi = new CommanderNavalPurchaseUi(navalPurchaseService);
        samSiteAnalyzerUi = new CommanderSamSiteAnalyzerUi(
            samSiteAnalyzerService,
            samSiteService,
            supplyHeliService);
        depotUi = new CommanderDepotUi(spawnService);
        worldMarkerRenderer = new CommanderWorldMarkerRenderer(
            selectionService,
            moveService,
            spawnService,
            supplyHeliService,
            samSiteAnalyzerService,
            samSiteService);
    }

    internal void Activate()
    {
        panelVisible = false;
        reserveWindowVisible = false;
        panelHelpVisible = false;
        reserveHelpVisible = false;
        supplyHeliUi.Hide();
        airCommandUi.Hide();
        navalPurchaseUi.Hide();
        samSiteAnalyzerUi.Hide();
        depotUi.Reset();
        ResetScreenshotUi();
        settingsVisible = false;
        bindingCapture = null;
        advancedUnlockConfirmation = false;
    }

    internal void Deactivate()
    {
        ResetScreenshotUi();
        panelVisible = false;
        reserveWindowVisible = false;
        settingsVisible = false;
        bindingCapture = null;
        advancedUnlockConfirmation = false;
        supplyHeliUi.Hide();
        airCommandUi.Hide();
        navalPurchaseUi.Hide();
        samSiteAnalyzerUi.Hide();
        depotUi.Reset();
    }

    internal void Tick()
    {
        CommanderUiTheme.Ensure();
        if (screenshotUiStage == 2)
        {
            MaintainAllUiHidden();
        }
        if (!showTacticalMap && CommanderTacticalMapService.Instance?.IsOpen == true)
        {
            CommanderTacticalMapService.Instance.Close();
        }
        float centerY = CommanderUiScale.Height * 0.5f;
        launcherRect = new Rect(10f, centerY - 42f, 52f, 84f);
        moneyRect = new Rect((CommanderUiScale.Width - 250f) * 0.5f, 10f, 250f, 38f);

        if (!positionsInitialized)
        {
            float panelHeight = Mathf.Min(760f, CommanderUiScale.Height - 24f);
            panelRect = new Rect(74f, Mathf.Max(12f, centerY - panelHeight * 0.5f), 400f, panelHeight);
            float reserveWidth = Mathf.Min(590f, CommanderUiScale.Width - 24f);
            float reserveHeight = Mathf.Min(610f, CommanderUiScale.Height - 24f);
            reserveWindowRect = new Rect(
                Mathf.Max(12f, CommanderUiScale.Width - reserveWidth - 12f),
                Mathf.Max(58f, CommanderUiScale.Height - reserveHeight - 12f),
                reserveWidth,
                reserveHeight);
            pinnedWindowRect = new Rect(
                Mathf.Max(12f, CommanderUiScale.Width - 354f),
                Mathf.Clamp(CommanderUiScale.Height * 0.66f - 170f, 58f, CommanderUiScale.Height - 352f),
                342f,
                340f);
            radarWindowRect = new Rect(
                Mathf.Max(12f, CommanderUiScale.Width - 442f),
                Mathf.Clamp(CommanderUiScale.Height * 0.66f - 530f, 58f, CommanderUiScale.Height - 620f),
                430f,
                608f);
            float settingsWidth = Mathf.Min(680f, CommanderUiScale.Width - 24f);
            float settingsHeight = Mathf.Min(660f, CommanderUiScale.Height - 24f);
            settingsWindowRect = new Rect(
                Mathf.Max(12f, (CommanderUiScale.Width - settingsWidth) * 0.5f),
                Mathf.Max(12f, (CommanderUiScale.Height - settingsHeight) * 0.5f),
                settingsWidth,
                settingsHeight);
            positionsInitialized = true;
        }
        else
        {
            panelRect.height = Mathf.Min(760f, CommanderUiScale.Height - 24f);
            reserveWindowRect.width = Mathf.Min(590f, CommanderUiScale.Width - 24f);
            reserveWindowRect.height = Mathf.Min(610f, CommanderUiScale.Height - 24f);
            settingsWindowRect.width = Mathf.Min(680f, CommanderUiScale.Width - 24f);
            settingsWindowRect.height = Mathf.Min(660f, CommanderUiScale.Height - 24f);
        }
        panelRect = CommanderUiTheme.ClampWindow(panelRect);
        reserveWindowRect = CommanderUiTheme.ClampWindow(reserveWindowRect);
        pinnedWindowRect = CommanderUiTheme.ClampWindow(pinnedWindowRect);
        settingsWindowRect = CommanderUiTheme.ClampWindow(settingsWindowRect);
        pinnedLauncherRect = new Rect(
            Mathf.Min(CommanderUiScale.Width - 70f, pinnedWindowRect.xMax + 6f),
            pinnedWindowRect.y,
            62f,
            28f);
        bool samSiteFocused = samSiteService.IsConstructionCore(selectionService.FocusedSelection);
        radarWindowRect.width = Mathf.Min(
            samSiteFocused ? 430f : 380f,
            CommanderUiScale.Width - 24f);
        radarWindowRect.height = Mathf.Min(
            samSiteFocused ? 734f : 450f,
            CommanderUiScale.Height - 24f);
        radarWindowRect = CommanderUiTheme.ClampWindow(radarWindowRect);
        selectionBarRect = new Rect(
            Mathf.Max(12f, (CommanderUiScale.Width - 880f) * 0.5f),
            CommanderUiScale.Height - 158f,
            Mathf.Min(880f, CommanderUiScale.Width - 24f),
            74f);
        selectionHelpRect = new Rect(selectionBarRect.x, selectionBarRect.y - 92f, selectionBarRect.width, 84f);
        if (CommanderFeatureGate.AdvancedFeaturesEnabled)
        {
            supplyHeliUi.Tick();
            airCommandUi.Tick();
            depotUi.Tick();
        }
    }

    internal bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPoint);
        bool advanced = CommanderFeatureGate.AdvancedFeaturesEnabled;
        if (screenshotUiHidden)
        {
            return false;
        }
        if (airCommandUi.Visible)
        {
            return (advanced && showAirCommandUi && airCommandUi.ContainsScreenPoint(screenPoint))
                || (settingsVisible && settingsWindowRect.Contains(guiPoint));
        }
        return launcherRect.Contains(guiPoint)
            || (advanced && showFactionMoney && moneyRect.Contains(guiPoint))
            || (panelVisible && panelRect.Contains(guiPoint))
            || (advanced && reserveWindowVisible && reserveWindowRect.Contains(guiPoint))
            || (showSelectionBar && selectionService.SelectedUnits.Count > 0 && selectionBarRect.Contains(guiPoint))
            || (selectionHelpVisible && selectionHelpRect.Contains(guiPoint))
            || (showPinnedUnits && HasPinEntries && (pinnedLauncherRect.Contains(guiPoint) || (pinnedWindowVisible && pinnedWindowRect.Contains(guiPoint))))
            || (advanced && showUnitSystems && TryGetUnitSystemsTarget(out _, out _) && radarWindowRect.Contains(guiPoint))
            || (advanced && showDepotUi && depotUi.ContainsScreenPoint(screenPoint))
            || (advanced && showSupplyUi && supplyHeliUi.ContainsScreenPoint(screenPoint))
            || (advanced && showAirCommandUi && airCommandUi.ContainsScreenPoint(screenPoint))
            || (advanced && showNavalUi && navalPurchaseUi.ContainsScreenPoint(screenPoint))
            || (advanced && showSamAnalyzerUi && samSiteAnalyzerUi.ContainsScreenPoint(screenPoint))
            || (settingsVisible && settingsWindowRect.Contains(guiPoint));
    }

    internal void DrawInactiveLauncher(Action activateCommander)
    {
        CommanderUiTheme.Ensure();
        float centerY = CommanderUiScale.Height * 0.5f;
        launcherRect = new Rect(10f, centerY - 42f, 52f, 84f);
        EventType activationEvent = Event.current.type;
        if (GUI.Button(launcherRect, "CMD", CommanderUiTheme.PrimaryButton)
            && activationEvent == EventType.MouseUp)
        {
            GUI.FocusControl(null);
            activateCommander();
            panelVisible = true;
        }
    }

    internal void Draw()
    {
        if (screenshotUiHidden)
        {
            return;
        }
        CommanderUiTheme.Ensure();
        bool advanced = CommanderFeatureGate.AdvancedFeaturesEnabled;
        if (showWorldMarkers)
        {
            worldMarkerRenderer.Draw(supplyHeliUi.Visible && supplyHeliUi.ShowLz);
        }
        if (advanced && airCommandUi.Visible)
        {
            if (showAirCommandUi) airCommandUi.Draw();
            DrawSettingsWindowIfVisible();
            return;
        }
        GUIStyle commandStyle = showCommandButton
            ? (panelVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.PrimaryButton)
            : GetGhostCommandStyle();
        if (GUI.Button(launcherRect, "CMD", commandStyle))
        {
            panelVisible = !panelVisible;
        }

        if (advanced && showFactionMoney)
        {
            GUI.Box(moneyRect, $"FACTION FUNDS   {spawnService.GetFactionFundsLabel()}", CommanderUiTheme.Money);
        }

        if (panelVisible)
        {
            panelRect = GUI.Window(WindowId, panelRect, DrawPanelWindow, "COMMANDER", CommanderUiTheme.Window);
        }
        if (advanced && reserveWindowVisible)
        {
            reserveWindowRect = GUI.Window(ReserveWindowId, reserveWindowRect, DrawReserveWindow, "FACTION RESERVE", CommanderUiTheme.Window);
        }

        if (showPinnedUnits && HasPinEntries)
        {
            if (GUI.Button(pinnedLauncherRect, pinnedWindowVisible ? "PINS <" : "PINS >", CommanderUiTheme.Button))
            {
                pinnedWindowVisible = !pinnedWindowVisible;
            }
            if (pinnedWindowVisible)
            {
                pinnedWindowRect = GUI.Window(PinnedWindowId, pinnedWindowRect, DrawPinnedWindow, "UNIT LIST", CommanderUiTheme.Window);
            }
        }
        if (advanced && showUnitSystems && TryGetUnitSystemsTarget(out _, out _))
        {
            string title = samSiteService.IsConstructionCore(selectionService.FocusedSelection)
                ? "SAM SITE LOGISTICS"
                : "UNIT SYSTEMS";
            radarWindowRect = GUI.Window(
                RadarWindowId,
                radarWindowRect,
                DrawRadarWindow,
                title,
                CommanderUiTheme.Window);
        }

        if (advanced && showDepotUi) depotUi.Draw();
        if (advanced && showSupplyUi) supplyHeliUi.Draw();
        if (advanced && showAirCommandUi) airCommandUi.Draw();
        if (advanced && showNavalUi) navalPurchaseUi.Draw();
        if (advanced && showSamAnalyzerUi) samSiteAnalyzerUi.Draw();
        if (showSelectionBar) DrawSelectionBar();
        DrawSettingsWindowIfVisible();
        DrawBoxSelectionIfActive();
    }

    private static void DrawBoxSelectionIfActive()
    {
        if (CommanderInputController.Instance?.IsBoxDragging != true)
        {
            return;
        }

        Rect boxRect = CommanderInputController.Instance.GetBoxSelectionGuiRect();
        if (boxRect.width <= 1f || boxRect.height <= 1f)
        {
            return;
        }

        CommanderUiTheme.DrawFrame(boxRect, 1.5f);
        Color oldColor = GUI.color;
        GUI.color = new Color(0.28f, 0.58f, 0.57f, 0.12f);
        GUI.DrawTexture(boxRect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private bool screenshotUiHidden => screenshotUiStage != 0;
    internal bool CommanderUiHidden => screenshotUiHidden;
    internal bool ShowTacticalMapUi => CommanderFeatureGate.AdvancedFeaturesEnabled
        && showTacticalMap
        && !screenshotUiHidden;
    internal void ToggleScreenshotUi()
    {
        if (screenshotUiStage == 0)
        {
            screenshotUiStage = 1;
            reopenTacticalMapAfterScreenshot = CommanderTacticalMapService.Instance?.IsOpen == true;
            if (reopenTacticalMapAfterScreenshot)
            {
                CommanderTacticalMapService.Instance?.Close();
            }
            return;
        }

        if (screenshotUiStage == 1)
        {
            screenshotUiStage = 2;
            MaintainAllUiHidden();
            return;
        }

        RestoreBaseUi();
        screenshotUiStage = 0;
        if (reopenTacticalMapAfterScreenshot && showTacticalMap)
        {
            CommanderTacticalMapService.Instance?.Open();
        }
        reopenTacticalMapAfterScreenshot = false;
    }

    private void MaintainAllUiHidden()
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (!screenshotCanvasStates.ContainsKey(canvas))
            {
                screenshotCanvasStates.Add(canvas, canvas.enabled);
            }
            canvas.enabled = false;
        }
    }

    private void RestoreBaseUi()
    {
        foreach (KeyValuePair<Canvas, bool> entry in screenshotCanvasStates)
        {
            if (entry.Key != null)
            {
                entry.Key.enabled = entry.Value;
            }
        }
        screenshotCanvasStates.Clear();
    }

    private void ResetScreenshotUi()
    {
        RestoreBaseUi();
        screenshotUiStage = 0;
        reopenTacticalMapAfterScreenshot = false;
    }
    private bool HasPinEntries => selectionService.PinnedUnits.Count > 0
        || selectionService.MissionUnits.Count > 0
        || selectionService.SamSiteUnits.Count > 0;

    private void DrawPanelWindow(int windowId)
    {
        if (CommanderUiTheme.DrawHelpButton(panelRect.width, ref panelHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, panelRect.width - 24f, 92f),
                "LMB selects; Shift+LMB adds; empty LMB clears. RMB orders friendly ground units and ships. Commander camera controls are configured under Settings > Controls. M opens the fullscreen map.");
        }
        if (GUI.Button(new Rect(panelRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.Button))
        {
            panelVisible = false;
        }

        float y = panelHelpVisible ? 136f : 38f;
        bool advanced = CommanderFeatureGate.AdvancedFeaturesEnabled;
        Rect unlockRect = default;
        const string unlockTooltip = "Features behind this toggle are designed for large strategic missions such as Escalation and Terminal Control. Enabling them in other missions may break the mission.";
        if (!advanced)
        {
            string mission = string.IsNullOrWhiteSpace(CommanderFeatureGate.MissionName)
                ? "UNKNOWN MISSION"
                : CommanderFeatureGate.MissionName.ToUpperInvariant();
            GUI.Label(new Rect(12f, y, panelRect.width - 24f, 20f), $"CORE MODE   |   {mission}", CommanderUiTheme.MutedLabel);
            y += 24f;
            unlockRect = new Rect(12f, y, panelRect.width - 24f, 38f);
            string unlockLabel = advancedUnlockConfirmation
                ? "ARE YOU SURE? UNLOCK ALL FEATURES"
                : "UNLOCK ALL FEATURES";
            if (GUI.Button(unlockRect, new GUIContent(unlockLabel, unlockTooltip), CommanderUiTheme.DangerButton))
            {
                if (advancedUnlockConfirmation)
                {
                    unlockAdvancedFeatures();
                    advancedUnlockConfirmation = false;
                }
                else
                {
                    advancedUnlockConfirmation = true;
                }
            }
            y += 48f;
        }

        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && advanced;
        GUI.Label(new Rect(12f, y, panelRect.width - 24f, 18f), "GROUND UNITS", CommanderUiTheme.MutedLabel);
        y += 20f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 34f), "SELECT NEAREST DEPOT", CommanderUiTheme.PrimaryButton))
        {
            spawnService.SelectNearestDepot();
        }
        y += 38f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 34f), "FACTION RESERVE", CommanderUiTheme.PrimaryButton))
        {
            reserveWindowVisible = !reserveWindowVisible;
        }
        y += 44f;

        GUI.Label(new Rect(12f, y, panelRect.width - 24f, 18f), "AIR UNITS", CommanderUiTheme.MutedLabel);
        y += 20f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 34f), "SUPPLY HELI", CommanderUiTheme.PrimaryButton))
        {
            supplyHeliUi.Toggle();
        }
        y += 38f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 34f), "AIR COMMAND", CommanderUiTheme.PrimaryButton))
        {
            if (airCommandUi.Visible)
            {
                airCommandUi.Hide();
            }
            else
            {
                panelVisible = false;
                reserveWindowVisible = false;
                supplyHeliUi.Hide();
                depotUi.Reset();
                airCommandUi.Show();
            }
        }
        y += 44f;

        GUI.Label(new Rect(12f, y, panelRect.width - 24f, 18f), "NAVAL", CommanderUiTheme.MutedLabel);
        y += 20f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 34f), "NAVAL PURCHASE", CommanderUiTheme.PrimaryButton))
        {
            navalPurchaseUi.Toggle();
        }
        y += 40f;

        string helper = supplyHeliService.AwaitingTargetSelection
            ? "Select the cargo destination in the 3D world. The game's Cancel binding cancels."
            : airCommandService.AwaitingAreaSelection
                ? "Select the Air Command mission area on the tactical map or in the 3D world."
                : navalPurchaseService.AwaitingRallySelection
                    ? "Select a water rally point on the fullscreen map."
                : mobileEmplacementService.AwaitingDestination
                    ? "Select the trailer destination in the 3D world. The game's Cancel binding cancels."
            : spawnService.AwaitingRallyPointSelection
                ? "Select the rally point on the tactical map or in the 3D world."
                : string.Empty;
        float settingsY = panelRect.height - 102f;
        float experimentalY = settingsY - 84f;
        if (!string.IsNullOrEmpty(helper) && experimentalY - y >= 36f)
        {
            GUI.Label(new Rect(14f, y, panelRect.width - 28f, 36f), helper, CommanderUiTheme.MutedLabel);
        }
        GUI.Label(new Rect(12f, experimentalY, panelRect.width - 24f, 18f), "EXPERIMENTAL", CommanderUiTheme.MutedLabel);
        if (GUI.Button(new Rect(12f, experimentalY + 20f, panelRect.width - 24f, 34f), "SAM SITE ANALYZER", CommanderUiTheme.Button))
        {
            samSiteAnalyzerUi.Toggle();
        }
        GUI.enabled = oldEnabled;
        if (GUI.Button(new Rect(12f, settingsY, panelRect.width - 24f, 34f), "SETTINGS", CommanderUiTheme.Button))
        {
            settingsVisible = !settingsVisible;
            bindingCapture = null;
        }

        if (GUI.Button(new Rect(12f, panelRect.height - 54f, panelRect.width - 24f, 38f), "EXIT COMMANDER MODE", CommanderUiTheme.DangerButton))
        {
            GUI.FocusControl(null);
            exitCommander();
        }

        if (!advanced && unlockRect.Contains(Event.current.mousePosition))
        {
            Rect tooltipRect = new(12f, unlockRect.yMax + 4f, panelRect.width - 24f, 64f);
            GUI.Box(tooltipRect, string.Empty, CommanderUiTheme.Panel);
            GUI.Label(
                new Rect(tooltipRect.x + 8f, tooltipRect.y + 5f, tooltipRect.width - 16f, tooltipRect.height - 10f),
                unlockTooltip,
                CommanderUiTheme.Label);
        }

        GUI.DragWindow(new Rect(0f, 0f, panelRect.width - 72f, 28f));
    }

    private void DrawSettingsWindowIfVisible()
    {
        if (settingsVisible)
        {
            settingsWindowRect = GUI.Window(
                SettingsWindowId,
                settingsWindowRect,
                DrawSettingsWindow,
                "COMMANDER SETTINGS",
                CommanderUiTheme.Window);
        }
    }

    private void DrawSettingsWindow(int windowId)
    {
        CaptureBindingInput();
        if (CommanderUiTheme.DrawHelpButton(settingsWindowRect.width, ref settingsHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, settingsWindowRect.width - 24f, 74f),
                "Settings are saved in the BepInEx configuration. Commander camera bindings are read only while Commander mode is active and do not alter aircraft controls.");
        }
        if (GUI.Button(new Rect(settingsWindowRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.Button))
        {
            settingsVisible = false;
            bindingCapture = null;
        }

        float y = settingsHelpVisible ? 118f : 38f;
        float tabWidth = (settingsWindowRect.width - 42f) / 4f;
        DrawSettingsTab(new Rect(12f, y, tabWidth, 32f), "GAMEPLAY", 0);
        DrawSettingsTab(new Rect(12f + tabWidth + 6f, y, tabWidth, 32f), "UI / HIDE", 1);
        DrawSettingsTab(new Rect(12f + (tabWidth + 6f) * 2f, y, tabWidth, 32f), "CONTROLS", 2);
        DrawSettingsTab(new Rect(12f + (tabWidth + 6f) * 3f, y, tabWidth, 32f), "CHEATS", 3);
        y += 44f;

        if (settingsTab == 0)
        {
            DrawGameplaySettings(y);
        }
        else if (settingsTab == 1)
        {
            DrawUiSettings(y);
        }
        else if (settingsTab == 2)
        {
            DrawControlSettings(y);
        }
        else
        {
            DrawCheatSettings(y);
        }

        GUI.DragWindow(new Rect(0f, 0f, settingsWindowRect.width - 72f, 28f));
    }

    private void DrawSettingsTab(Rect rect, string label, int tab)
    {
        if (GUI.Button(rect, label, settingsTab == tab ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            settingsTab = tab;
            bindingCapture = null;
        }
    }

    private void DrawGameplaySettings(float y)
    {
        GUI.Box(new Rect(12f, y, settingsWindowRect.width - 24f, 92f), string.Empty, CommanderUiTheme.Panel);
        GUI.Label(new Rect(24f, y + 10f, settingsWindowRect.width - 48f, 22f), "SPAWN RESTRICTIONS", CommanderUiTheme.Header);
        CommanderSettings.LimitToFactoryVehicles = GUI.Toggle(
            new Rect(24f, y + 42f, settingsWindowRect.width - 48f, 30f),
            CommanderSettings.LimitToFactoryVehicles,
            "Limit to vehicles from factories",
            CommanderUiTheme.Toggle);

        y += 104f;
        GUI.Box(new Rect(12f, y, settingsWindowRect.width - 24f, 246f), string.Empty, CommanderUiTheme.Panel);
        GUI.Label(new Rect(24f, y + 10f, settingsWindowRect.width - 48f, 22f), "SMART AI BEHAVIORS & COUNTERS", CommanderUiTheme.Header);
        CommanderSettings.SmartAiEnabled = GUI.Toggle(
            new Rect(24f, y + 40f, settingsWindowRect.width - 48f, 26f),
            CommanderSettings.SmartAiEnabled,
            "Enable Smart AI System",
            CommanderUiTheme.Toggle);
        CommanderSettings.AiReactiveScatter = GUI.Toggle(
            new Rect(24f, y + 70f, settingsWindowRect.width - 48f, 26f),
            CommanderSettings.AiReactiveScatter,
            "AI Reactive Evasion (Scatter on incoming bombs/missiles)",
            CommanderUiTheme.Toggle);
        CommanderSettings.AiAdaptiveProduction = GUI.Toggle(
            new Rect(24f, y + 100f, settingsWindowRect.width - 48f, 26f),
            CommanderSettings.AiAdaptiveProduction,
            "AI Adaptive Counter-Production (Counters air/armor spam)",
            CommanderUiTheme.Toggle);
        CommanderSettings.AiAutoDeployAir = GUI.Toggle(
            new Rect(24f, y + 130f, settingsWindowRect.width - 48f, 26f),
            CommanderSettings.AiAutoDeployAir,
            "AI Auto-Sortie Reserve Aircraft (When un-held stock exists)",
            CommanderUiTheme.Toggle);
        CommanderSettings.AiAutoDeployNaval = GUI.Toggle(
            new Rect(24f, y + 160f, settingsWindowRect.width - 48f, 26f),
            CommanderSettings.AiAutoDeployNaval,
            "AI Auto-Deploy Reserve Warships (When un-held stock exists)",
            CommanderUiTheme.Toggle);
    }

    private void DrawUiSettings(float y)
    {
        GUI.Box(new Rect(12f, y, settingsWindowRect.width - 24f, 306f), string.Empty, CommanderUiTheme.Panel);
        float left = 28f;
        float right = settingsWindowRect.width * 0.5f + 10f;
        float width = settingsWindowRect.width * 0.5f - 40f;
        showCommandButton = GUI.Toggle(new Rect(left, y + 16f, width, 28f), showCommandButton, "Command button", CommanderUiTheme.Toggle);
        showFactionMoney = GUI.Toggle(new Rect(right, y + 16f, width, 28f), showFactionMoney, "Faction funds", CommanderUiTheme.Toggle);
        showTacticalMap = GUI.Toggle(new Rect(left, y + 50f, width, 28f), showTacticalMap, "Tactical map", CommanderUiTheme.Toggle);
        showSelectionBar = GUI.Toggle(new Rect(right, y + 50f, width, 28f), showSelectionBar, "Selection bar", CommanderUiTheme.Toggle);
        showPinnedUnits = GUI.Toggle(new Rect(left, y + 84f, width, 28f), showPinnedUnits, "Unit / mission list", CommanderUiTheme.Toggle);
        showUnitSystems = GUI.Toggle(new Rect(right, y + 84f, width, 28f), showUnitSystems, "Unit systems", CommanderUiTheme.Toggle);
        showDepotUi = GUI.Toggle(new Rect(left, y + 118f, width, 28f), showDepotUi, "Depot UI", CommanderUiTheme.Toggle);
        showSupplyUi = GUI.Toggle(new Rect(right, y + 118f, width, 28f), showSupplyUi, "Supply UI", CommanderUiTheme.Toggle);
        showAirCommandUi = GUI.Toggle(new Rect(left, y + 152f, width, 28f), showAirCommandUi, "Air Command UI", CommanderUiTheme.Toggle);
        showNavalUi = GUI.Toggle(new Rect(right, y + 152f, width, 28f), showNavalUi, "Naval UI", CommanderUiTheme.Toggle);
        showWorldMarkers = GUI.Toggle(new Rect(left, y + 186f, width, 28f), showWorldMarkers, "World markers", CommanderUiTheme.Toggle);
        showSamAnalyzerUi = GUI.Toggle(new Rect(right, y + 186f, width, 28f), showSamAnalyzerUi, "SAM analyzer UI", CommanderUiTheme.Toggle);

        SaveUiVisibilitySettings();
        GUI.Label(
            new Rect(28f, y + 226f, settingsWindowRect.width - 56f, 20f),
            $"Automatic UI scale for {Screen.width} x {Screen.height}: {CommanderSettings.UiScale:0.##}x",
            CommanderUiTheme.MutedLabel);
        GUI.Label(
            new Rect(28f, y + 250f, settingsWindowRect.width - 56f, 20f),
            $"{CommanderSettings.ToggleUi} cycles visible, Commander UI hidden, and all UI hidden.",
            CommanderUiTheme.MutedLabel);
        if (GUI.Button(new Rect(28f, y + 274f, settingsWindowRect.width - 56f, 30f), "RESET UI LAYOUT", CommanderUiTheme.Button))
        {
            ResetUiLayout();
        }
    }

    private void SaveUiVisibilitySettings()
    {
        CommanderSettings.ShowCommandButton = showCommandButton;
        CommanderSettings.ShowFactionMoney = showFactionMoney;
        CommanderSettings.ShowTacticalMap = showTacticalMap;
        CommanderSettings.ShowSelectionBar = showSelectionBar;
        CommanderSettings.ShowPinnedUnits = showPinnedUnits;
        CommanderSettings.ShowUnitSystems = showUnitSystems;
        CommanderSettings.ShowDepotUi = showDepotUi;
        CommanderSettings.ShowSupplyUi = showSupplyUi;
        CommanderSettings.ShowAirCommandUi = showAirCommandUi;
        CommanderSettings.ShowNavalUi = showNavalUi;
        CommanderSettings.ShowSamAnalyzerUi = showSamAnalyzerUi;
        CommanderSettings.ShowWorldMarkers = showWorldMarkers;
    }

    private void DrawControlSettings(float y)
    {
        GUI.Box(new Rect(12f, y, settingsWindowRect.width - 24f, 480f), string.Empty, CommanderUiTheme.Panel);
        GUI.Label(
            new Rect(24f, y + 8f, settingsWindowRect.width - 48f, 32f),
            "Bindings are active only in Commander mode. Click one, then press a keyboard or mouse button. Escape cancels.",
            CommanderUiTheme.MutedLabel);

        float columnWidth = (settingsWindowRect.width - 66f) * 0.5f;
        float left = 24f;
        float right = 42f + columnWidth;
        GUI.Label(new Rect(left, y + 42f, columnWidth, 22f), "CAMERA", CommanderUiTheme.Header);
        GUI.Label(new Rect(right, y + 42f, columnWidth, 22f), "COMMANDER ACTIONS", CommanderUiTheme.Header);
        float rowY = y + 68f;
        DrawBinding(new Rect(left, rowY, columnWidth, 32f), "Forward", "forward");
        DrawBinding(new Rect(left, rowY + 36f, columnWidth, 32f), "Backward", "backward");
        DrawBinding(new Rect(left, rowY + 72f, columnWidth, 32f), "Move left", "left");
        DrawBinding(new Rect(left, rowY + 108f, columnWidth, 32f), "Move right", "right");
        DrawBinding(new Rect(left, rowY + 144f, columnWidth, 32f), "Move up", "up");
        DrawBinding(new Rect(left, rowY + 180f, columnWidth, 32f), "Move down", "down");
        DrawBinding(new Rect(left, rowY + 216f, columnWidth, 32f), "Free look", "look");
        DrawBinding(new Rect(left, rowY + 252f, columnWidth, 32f), "Speed boost", "boost");
        Rect centerFollowRect = new(left, rowY + 288f, columnWidth, 32f);
        DrawBinding(centerFollowRect, "Center / follow", "center_follow");

        DrawBinding(new Rect(right, rowY, columnWidth, 32f), "Select / place", "primary");
        DrawBinding(new Rect(right, rowY + 36f, columnWidth, 32f), "Move / order", "secondary");
        DrawBinding(new Rect(right, rowY + 72f, columnWidth, 32f), "Add selection", "add_selection");
        DrawBinding(new Rect(right, rowY + 108f, columnWidth, 32f), "Repeat deploy", "repeat_deploy");
        DrawBinding(new Rect(right, rowY + 144f, columnWidth, 32f), "Delete modifier", "delete_modifier");
        DrawBinding(new Rect(right, rowY + 180f, columnWidth, 32f), "UI cycle", "toggle_ui");

        if (centerFollowRect.Contains(Event.current.mousePosition))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(right, rowY + 288f, columnWidth, 68f),
                "Press briefly to center on the selected unit. Hold to center and follow it.");
        }

        if (GUI.Button(new Rect(left, y + 432f, columnWidth, 32f), "RESET CAMERA", CommanderUiTheme.Button))
        {
            ResetCameraBindings();
            bindingCapture = null;
        }
        if (GUI.Button(new Rect(right, y + 432f, columnWidth, 32f), "RESET ACTIONS", CommanderUiTheme.Button))
        {
            ResetActionBindings();
            bindingCapture = null;
        }
    }

    private void DrawCheatSettings(float y)
    {
        CommanderCheatService? cheats = CommanderCheatService.Instance;
        float windowWidth = settingsWindowRect.width - 24f;
        GUI.Box(new Rect(12f, y, windowWidth, 480f), string.Empty, CommanderUiTheme.Panel);

        float currentY = y + 10f;
        if (cheats != null && !string.IsNullOrEmpty(cheats.StatusText))
        {
            GUI.Box(new Rect(24f, currentY, windowWidth - 24f, 26f), string.Empty, CommanderUiTheme.Panel);
            GUI.Label(new Rect(32f, currentY + 2f, windowWidth - 40f, 22f), cheats.StatusText, CommanderUiTheme.Header);
            currentY += 32f;
        }

        // 1. Economy Cheats
        GUI.Label(new Rect(24f, currentY, windowWidth - 24f, 22f), "FACTION FUNDS & ECONOMY", CommanderUiTheme.Header);
        currentY += 26f;
        float btnWidth = (windowWidth - 36f) / 3f;
        if (GUI.Button(new Rect(24f, currentY, btnWidth, 32f), "+ $100,000", CommanderUiTheme.Button))
        {
            cheats?.AddFunds(100000f);
        }
        if (GUI.Button(new Rect(24f + btnWidth + 6f, currentY, btnWidth, 32f), "+ $1,000,000", CommanderUiTheme.Button))
        {
            cheats?.AddFunds(1000000f);
        }
        if (GUI.Button(new Rect(24f + (btnWidth + 6f) * 2f, currentY, btnWidth, 32f), "MAX FUNDS", CommanderUiTheme.PrimaryButton))
        {
            cheats?.SetMaxFunds();
        }
        currentY += 44f;

        // 2. Unit & Combat Cheats
        GUI.Label(new Rect(24f, currentY, windowWidth - 24f, 22f), "COMBAT & INVULNERABILITY", CommanderUiTheme.Header);
        currentY += 26f;
        if (cheats != null)
        {
            cheats.GodModeEnabled = GUI.Toggle(
                new Rect(24f, currentY, windowWidth - 24f, 28f),
                cheats.GodModeEnabled,
                "GOD MODE (All Friendly Units Invulnerable)",
                CommanderUiTheme.Toggle);
        }
        currentY += 32f;

        float actionWidth = (windowWidth - 30f) * 0.5f;
        if (GUI.Button(new Rect(24f, currentY, actionWidth, 34f), "HEAL SELECTION (100% HP)", CommanderUiTheme.Button))
        {
            cheats?.HealSelection();
        }
        if (GUI.Button(new Rect(30f + actionWidth, currentY, actionWidth, 34f), "RESTOCK AMMO SELECTION", CommanderUiTheme.Button))
        {
            cheats?.RestockAmmoSelection();
        }
        currentY += 40f;

        if (GUI.Button(new Rect(24f, currentY, windowWidth - 24f, 34f), "DESTROY TARGET (KILL SELECTION)", CommanderUiTheme.DangerButton))
        {
            cheats?.DestroySelection();
        }
        currentY += 48f;

        // 3. Map & Sensors Cheats
        GUI.Label(new Rect(24f, currentY, windowWidth - 24f, 22f), "GLOBAL RECON & RADAR", CommanderUiTheme.Header);
        currentY += 26f;
        if (GUI.Button(new Rect(24f, currentY, windowWidth - 24f, 34f), "REVEAL ALL ENEMY UNITS ON MAP", CommanderUiTheme.Button))
        {
            cheats?.RevealAllUnits();
        }
    }


    private void DrawBinding(Rect rect, string label, string binding)
    {
        float labelWidth = Mathf.Min(94f, rect.width * 0.36f);
        GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label, CommanderUiTheme.Label);
        string buttonText = bindingCapture == binding ? "PRESS KEY..." : GetBinding(binding).ToString();
        if (GUI.Button(
            new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth - 34f, rect.height),
            buttonText,
            bindingCapture == binding ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            bindingCapture = binding;
        }
        if (GUI.Button(new Rect(rect.xMax - 28f, rect.y, 28f, rect.height), "X", CommanderUiTheme.Button))
        {
            SetBinding(binding, new KeyboardShortcut(KeyCode.None));
            bindingCapture = null;
        }
    }

    private void CaptureBindingInput()
    {
        if (bindingCapture == null)
        {
            return;
        }

        Event current = Event.current;
        KeyCode key;
        if (current.type == EventType.KeyDown)
        {
            if (current.keyCode == KeyCode.Escape)
            {
                bindingCapture = null;
                current.Use();
                return;
            }
            key = current.keyCode;
            if (key == KeyCode.None)
            {
                return;
            }
        }
        else if (current.type == EventType.MouseDown)
        {
            key = (KeyCode)((int)KeyCode.Mouse0 + current.button);
        }
        else
        {
            return;
        }

        List<KeyCode> modifiers = new();
        if (current.shift && key != KeyCode.LeftShift && key != KeyCode.RightShift) modifiers.Add(KeyCode.LeftShift);
        if (current.control && key != KeyCode.LeftControl && key != KeyCode.RightControl) modifiers.Add(KeyCode.LeftControl);
        if (current.alt && key != KeyCode.LeftAlt && key != KeyCode.RightAlt) modifiers.Add(KeyCode.LeftAlt);
        SetBinding(bindingCapture, new KeyboardShortcut(key, modifiers.ToArray()));
        bindingCapture = null;
        current.Use();
    }

    private static KeyboardShortcut GetBinding(string binding)
    {
        return binding switch
        {
            "forward" => CommanderSettings.CameraForward,
            "backward" => CommanderSettings.CameraBackward,
            "left" => CommanderSettings.CameraLeft,
            "right" => CommanderSettings.CameraRight,
            "up" => CommanderSettings.CameraUp,
            "down" => CommanderSettings.CameraDown,
            "look" => CommanderSettings.CameraFreeLook,
            "boost" => CommanderSettings.CameraBoost,
            "primary" => CommanderSettings.PrimaryAction,
            "secondary" => CommanderSettings.SecondaryAction,
            "add_selection" => CommanderSettings.AddToSelection,
            "repeat_deploy" => CommanderSettings.RepeatDeployment,
            "delete_modifier" => CommanderSettings.DeleteUnitModifier,
            "center_follow" => CommanderSettings.CameraCenterFollow,
            "toggle_ui" => CommanderSettings.ToggleUi,
            _ => new KeyboardShortcut(KeyCode.None)
        };
    }

    private static void SetBinding(string binding, KeyboardShortcut shortcut)
    {
        switch (binding)
        {
            case "forward": CommanderSettings.CameraForward = shortcut; break;
            case "backward": CommanderSettings.CameraBackward = shortcut; break;
            case "left": CommanderSettings.CameraLeft = shortcut; break;
            case "right": CommanderSettings.CameraRight = shortcut; break;
            case "up": CommanderSettings.CameraUp = shortcut; break;
            case "down": CommanderSettings.CameraDown = shortcut; break;
            case "look": CommanderSettings.CameraFreeLook = shortcut; break;
            case "boost": CommanderSettings.CameraBoost = shortcut; break;
            case "primary": CommanderSettings.PrimaryAction = shortcut; break;
            case "secondary": CommanderSettings.SecondaryAction = shortcut; break;
            case "add_selection": CommanderSettings.AddToSelection = shortcut; break;
            case "repeat_deploy": CommanderSettings.RepeatDeployment = shortcut; break;
            case "delete_modifier": CommanderSettings.DeleteUnitModifier = shortcut; break;
            case "center_follow": CommanderSettings.CameraCenterFollow = shortcut; break;
            case "toggle_ui": CommanderSettings.ToggleUi = shortcut; break;
        }
    }

    private static void ResetCameraBindings()
    {
        CommanderSettings.CameraForward = new KeyboardShortcut(KeyCode.W);
        CommanderSettings.CameraBackward = new KeyboardShortcut(KeyCode.S);
        CommanderSettings.CameraLeft = new KeyboardShortcut(KeyCode.A);
        CommanderSettings.CameraRight = new KeyboardShortcut(KeyCode.D);
        CommanderSettings.CameraUp = new KeyboardShortcut(KeyCode.Q);
        CommanderSettings.CameraDown = new KeyboardShortcut(KeyCode.E);
        CommanderSettings.CameraFreeLook = new KeyboardShortcut(KeyCode.Mouse2);
        CommanderSettings.CameraBoost = new KeyboardShortcut(KeyCode.LeftShift);
        CommanderSettings.CameraCenterFollow = new KeyboardShortcut(KeyCode.Space);
    }

    private static void ResetActionBindings()
    {
        CommanderSettings.PrimaryAction = new KeyboardShortcut(KeyCode.Mouse0);
        CommanderSettings.SecondaryAction = new KeyboardShortcut(KeyCode.Mouse1);
        CommanderSettings.AddToSelection = new KeyboardShortcut(KeyCode.LeftShift);
        CommanderSettings.RepeatDeployment = new KeyboardShortcut(KeyCode.LeftShift);
        CommanderSettings.DeleteUnitModifier = new KeyboardShortcut(KeyCode.LeftAlt);
        CommanderSettings.ToggleUi = new KeyboardShortcut(KeyCode.H);
    }

    private void ResetUiLayout()
    {
        positionsInitialized = false;
        supplyHeliUi.ResetPosition();
        airCommandUi.ResetPosition();
        navalPurchaseUi.ResetPosition();
        samSiteAnalyzerUi.ResetPosition();
        depotUi.ResetPosition();
        CommanderTacticalMapService.Instance?.ResetLayoutPosition();
    }

    private GUIStyle GetGhostCommandStyle()
    {
        if (ghostCommandStyle != null)
        {
            return ghostCommandStyle;
        }

        ghostCommandStyle = new GUIStyle(CommanderUiTheme.Button);
        ghostCommandStyle.normal.background = null;
        ghostCommandStyle.hover.background = null;
        ghostCommandStyle.active.background = null;
        Color dim = ghostCommandStyle.normal.textColor;
        dim.a = 0.5f;
        ghostCommandStyle.normal.textColor = dim;
        ghostCommandStyle.hover.textColor = dim;
        ghostCommandStyle.active.textColor = dim;
        return ghostCommandStyle;
    }

    private void DrawSelectionBar()
    {
        int count = selectionService.SelectedUnits.Count;
        if (count == 0)
        {
            return;
        }

        GUI.Box(selectionBarRect, string.Empty, CommanderUiTheme.Panel);
        GUI.Label(new Rect(selectionBarRect.x + 12f, selectionBarRect.y + 3f, 190f, 24f), "UNIT SELECTION", CommanderUiTheme.MutedLabel);
        if (GUI.Button(new Rect(selectionBarRect.xMax - 34f, selectionBarRect.y + 3f, 26f, 22f), "?", CommanderUiTheme.HelpButton))
        {
            selectionHelpVisible = !selectionHelpVisible;
        }
        Unit? focused = selectionService.FocusedSelection;
        string label = count == 1 && focused != null
            ? CommanderGameAccess.GetUnitLabel(focused)
            : $"{count} UNITS SELECTED";
        GUI.Label(new Rect(selectionBarRect.x + 14f, selectionBarRect.y + 37f, selectionBarRect.width - 408f, 24f), label, CommanderUiTheme.Header);

        float buttonX = selectionBarRect.xMax - 544f;
        if (selectionHelpVisible)
        {
            CommanderUiTheme.DrawHelpOverlay(selectionHelpRect,
                "STOP holds units. AI returns them to Basegame. PATROL ('P') loops waypoints. SCATTER ('X') evades area damage. ROAD toggles road network. STANCE ('F') toggles Hold Fire. PIN stores selection (Alt for DEL).");
        }
        bool oldEnabled = GUI.enabled;
        bool advanced = CommanderFeatureGate.AdvancedFeaturesEnabled;
        GUI.enabled = oldEnabled && moveService.HasCommandableSelection;
        if (GUI.Button(new Rect(buttonX, selectionBarRect.y + 32f, 58f, 34f), "STOP", CommanderUiTheme.DangerButton))
        {
            moveService.StopSelectedUnits();
        }
        GUI.enabled = oldEnabled && advanced && moveService.HasCommandableSelection;
        if (GUI.Button(new Rect(buttonX + 62f, selectionBarRect.y + 32f, 48f, 34f), "AI", CommanderUiTheme.PrimaryButton))
        {
            moveService.ResumeAiForSelectedUnits();
        }
        GUI.enabled = oldEnabled && advanced && moveService.HasCommandableSelection;
        bool isPatrolling = moveService.AwaitingPatrolSelection;
        if (GUI.Button(new Rect(buttonX + 114f, selectionBarRect.y + 32f, 68f, 34f), "PATROL",
            isPatrolling ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            if (isPatrolling) moveService.CancelPatrolOrder();
            else moveService.BeginPatrolOrder();
        }
        GUI.enabled = oldEnabled && advanced && moveService.HasCommandableSelection;
        if (GUI.Button(new Rect(buttonX + 186f, selectionBarRect.y + 32f, 72f, 34f), "SCATTER", CommanderUiTheme.Button))
        {
            moveService.ScatterSelectedUnits(55f);
        }
        GUI.enabled = oldEnabled;
        bool canToggleRoad = advanced
            && count == 1
            && focused != null
            && directPathService.CanConfigure(focused)
            && !CommanderMobileEmplacementService.IsReservedHauler(focused)
            && !CommanderSamSiteService.IsReservedConstructionJacknife(focused);
        bool roadEnabled = !directPathService.IsEnabled(focused);
        GUI.enabled = oldEnabled && canToggleRoad;
        if (GUI.Button(new Rect(buttonX + 262f, selectionBarRect.y + 32f, 80f, 34f),
            roadEnabled ? "ROAD ON" : "ROAD OFF",
            roadEnabled ? CommanderUiTheme.Button : CommanderUiTheme.DangerButton))
        {
            directPathService.ToggleFocusedUnit();
        }
        GUI.enabled = oldEnabled;
        bool isHoldFire = CommanderStanceService.Instance?.IsHoldFire(focused) == true;
        if (GUI.Button(new Rect(buttonX + 346f, selectionBarRect.y + 32f, 96f, 34f),
            isHoldFire ? "HOLD FIRE" : "FREE FIRE",
            isHoldFire ? CommanderUiTheme.DangerButton : CommanderUiTheme.Button))
        {
            CommanderStanceService.Instance?.ToggleHoldFireForSelection();
        }
        GUI.enabled = oldEnabled;
        bool deleteMode = CommanderSettings.DeleteUnitModifier.IsPressed();
        string pinLabel = deleteMode ? "DEL" : (selectionService.IsCurrentSelectionPinned ? "UNPIN" : "PIN");
        GUI.enabled = oldEnabled
            && advanced
            && (!deleteMode || selectionService.CanDeleteSelection);
        if (GUI.Button(new Rect(buttonX + 446f, selectionBarRect.y + 32f, 90f, 34f), pinLabel,
            deleteMode ? CommanderUiTheme.DangerButton : CommanderUiTheme.Button))
        {
            if (deleteMode)
            {
                selectionService.DeleteSelectedUnits();
            }
            else
            {
                selectionService.TogglePinSelected();
            }
        }
        GUI.enabled = oldEnabled;
    }

    private void DrawPinnedWindow(int windowId)
    {
        bool hasManualPins = selectionService.PinnedUnits.Count > 0;
        bool hasMissions = selectionService.MissionUnits.Count > 0;
        bool hasSamSites = selectionService.SamSiteUnits.Count > 0;
        CommanderControlGroupsService.Instance?.GetAllActiveGroups(activeGroupsScratch);
        bool hasGroups = activeGroupsScratch.Count > 0;

        if ((pinnedTab == 0 && !hasManualPins)
            || (pinnedTab == 1 && !hasMissions)
            || (pinnedTab == 2 && !hasSamSites)
            || (pinnedTab == 3 && !hasGroups))
        {
            pinnedTab = hasGroups ? 3 : (hasMissions ? 1 : hasSamSites ? 2 : 0);
        }

        CommanderUiTheme.DrawHelpButton(pinnedWindowRect.width, ref pinnedHelpVisible);
        float y = pinnedHelpVisible ? 106f : 36f;
        if (pinnedHelpVisible)
        {
            CommanderUiTheme.DrawHelpOverlay(new Rect(12f, 34f, pinnedWindowRect.width - 24f, 62f),
                "GROUPS (1-9) displays control groups. PINS has manual pins. MISSIONS tracks aircraft. Click group/unit to select; double-tap to focus camera.");
        }

        int tabCount = (hasGroups ? 1 : 0) + (hasManualPins ? 1 : 0) + (hasMissions ? 1 : 0) + (hasSamSites ? 1 : 0);
        float tabWidth = (pinnedWindowRect.width - 24f - Mathf.Max(0, tabCount - 1) * 6f) / Mathf.Max(1, tabCount);
        float tabX = 12f;

        if (hasGroups && GUI.Button(new Rect(tabX, y, tabWidth, 30f), "GROUPS",
            pinnedTab == 3 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            pinnedTab = 3;
            pinnedScroll = Vector2.zero;
        }
        if (hasGroups) tabX += tabWidth + 6f;

        if (hasManualPins && GUI.Button(new Rect(tabX, y, tabWidth, 30f), "PINS",
            pinnedTab == 0 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            pinnedTab = 0;
            pinnedScroll = Vector2.zero;
        }
        if (hasManualPins) tabX += tabWidth + 6f;
        if (hasMissions && GUI.Button(new Rect(tabX, y, tabWidth, 30f), "MISSIONS",
            pinnedTab == 1 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            pinnedTab = 1;
            pinnedScroll = Vector2.zero;
        }
        if (hasMissions) tabX += tabWidth + 6f;
        if (hasSamSites && GUI.Button(new Rect(tabX, y, tabWidth, 30f), "SAM SITES",
            pinnedTab == 2 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            pinnedTab = 2;
            pinnedScroll = Vector2.zero;
        }
        y += 38f;

        if (pinnedTab == 1)
        {
            float filterWidth = (pinnedWindowRect.width - 30f) * 0.5f;
            showSupplyMissions = GUI.Toggle(new Rect(12f, y, filterWidth, 26f),
                showSupplyMissions, "SUPPLY", CommanderUiTheme.Toggle);
            showAirCommandMissions = GUI.Toggle(new Rect(18f + filterWidth, y, filterWidth, 26f),
                showAirCommandMissions, "AIR COMMAND", CommanderUiTheme.Toggle);
            y += 32f;
        }

        Rect view = new(10f, y, pinnedWindowRect.width - 20f, pinnedWindowRect.height - y - 12f);

        if (pinnedTab == 3)
        {
            float rowHeight = 48f;
            Rect inner = new(0f, 0f, view.width - 18f, Mathf.Max(view.height, activeGroupsScratch.Count * rowHeight + 4f));
            pinnedScroll = GUI.BeginScrollView(view, pinnedScroll, inner);
            for (int i = 0; i < activeGroupsScratch.Count; i++)
            {
                CommanderControlGroupsService.ControlGroupInfo grp = activeGroupsScratch[i];
                float rowY = 2f + i * rowHeight;
                if (GUI.Button(new Rect(4f, rowY, inner.width - 8f, rowHeight - 6f), string.Empty, CommanderUiTheme.Button))
                {
                    CommanderControlGroupsService.Instance?.SelectGroup(grp.Index, additive: false);
                }
                GUI.Label(new Rect(12f, rowY + 4f, 36f, 22f), $"[{grp.Index}]", CommanderUiTheme.Header);
                GUI.Label(new Rect(50f, rowY + 4f, inner.width - 60f, 22f), grp.Summary, CommanderUiTheme.Label);
                GUI.Label(new Rect(50f, rowY + 24f, inner.width - 60f, 18f), $"{grp.Count} Unit{(grp.Count == 1 ? string.Empty : "s")} | Click to select, Double-tap to focus", CommanderUiTheme.MutedLabel);
            }
            GUI.EndScrollView();
        }
        else
        {
            List<Unit> visibleUnits = new();
            IReadOnlyList<Unit> source = pinnedTab == 1
                ? selectionService.MissionUnits
                : pinnedTab == 2
                    ? selectionService.SamSiteUnits
                    : selectionService.PinnedUnits;
            for (int i = 0; i < source.Count; i++)
            {
                Unit unit = source[i];
                if (pinnedTab != 1)
                {
                    visibleUnits.Add(unit);
                    continue;
                }
                CommanderSelectionService.MissionPinInfo info = selectionService.GetMissionInfo(unit);
                if ((showSupplyMissions && info.Source == "SUPPLY")
                    || (showAirCommandMissions && info.Source == "AIR COMMAND"))
                {
                    visibleUnits.Add(unit);
                }
            }

            float rowHeight = pinnedTab == 1 ? 58f : 40f;
            Rect inner = new(0f, 0f, view.width - 18f, Mathf.Max(view.height, visibleUnits.Count * rowHeight + 4f));
            pinnedScroll = GUI.BeginScrollView(view, pinnedScroll, inner);
            for (int i = 0; i < visibleUnits.Count; i++)
            {
                Unit unit = visibleUnits[i];
                float rowY = 2f + i * rowHeight;
                if (GUI.Button(new Rect(4f, rowY, inner.width - 44f, rowHeight - 6f), string.Empty,
                    pinnedTab == 1 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
                {
                    selectionService.SelectPinnedUnit(unit);
                }
                string unitLabel = pinnedTab == 2
                    ? selectionService.GetSamSiteLabel(unit)
                    : CommanderGameAccess.GetUnitLabel(unit);
                GUI.Label(new Rect(12f, rowY + 5f, inner.width - 64f, 22f), unitLabel, CommanderUiTheme.Header);
                if (pinnedTab == 1)
                {
                    CommanderSelectionService.MissionPinInfo info = selectionService.GetMissionInfo(unit);
                    GUI.Label(new Rect(12f, rowY + 28f, inner.width - 64f, 20f),
                        $"{info.Source}  |  {info.Mission}", CommanderUiTheme.MutedLabel);
                }
                if (GUI.Button(new Rect(inner.width - 36f, rowY, 32f, rowHeight - 6f), "X", CommanderUiTheme.DangerButton))
                {
                    selectionService.RemovePinnedUnit(unit);
                    break;
                }
            }
            GUI.EndScrollView();
        }
        GUI.DragWindow(new Rect(0f, 0f, pinnedWindowRect.width - 44f, 28f));
    }

    private void DrawRadarWindow(int windowId)
    {
        if (!TryGetUnitSystemsTarget(out Unit focusedUnit, out CommanderRadarService.RadarState? state))
        {
            return;
        }

        CommanderUiTheme.DrawHelpButton(radarWindowRect.width, ref radarHelpVisible);
        if (radarHelpVisible)
        {
            CommanderUiTheme.DrawHelpOverlay(new Rect(10f, 32f, radarWindowRect.width - 20f, 108f),
                samSiteService.IsConstructionCore(focusedUnit)
                    ? "Build defenses from stored supply. Logistics routes from nearby airbases are planned once from terrain and faction influence, then reused. Show Route displays the selected cached route. Automatic deliveries prefer safer viable routes."
                    : state?.IsCommandTruck == true
                    ? "Counts cover the fire-control network around this command truck. Radar controls affect only the selected unit's local emitter. Enemy-unit controls are disabled."
                    : mobileEmplacementService.IsMoveableTrailer(focusedUnit)
                        ? "Relocate this static trailer with an idle HLT/MSV Tractor or Flatbed within 300 m. The hauler is reserved during loading, travel and deployment."
                    : repairService.IsRepairUnit(focusedUnit)
                        ? "Basegame repair targeting weighs damage, structure value and distance. NEAREST REPAIR instead targets the closest damaged friendly repairable structure on each Basegame repair scan."
                    : focusedUnit is Ship
                        ? "Request a paid Basegame UH-90K naval-supply run for this ship. Purchased airframes are refunded after a successful return. Enemy ships cannot request supply."
                    : "Switch the selected unit's local radar emissions. Aircraft use the Basegame networked radar toggle; enemy-unit controls are disabled.");
        }
        float y = radarHelpVisible ? 146f : 38f;
        bool friendly = CommanderGameAccess.IsFriendlyUnit(focusedUnit, CommanderGameAccess.GetLocalHq());
        if (!friendly)
        {
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 24f), "ENEMY UNIT  |  CONTROLS UNAVAILABLE", CommanderUiTheme.MutedLabel);
            y += 30f;
        }
        if (state?.IsCommandTruck == true)
        {
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 22f),
                $"NEARBY  {state.NearbyRadarCount} RADAR   /   {state.NearbyLauncherCount} LAUNCHERS", CommanderUiTheme.Header);
            y += 30f;
        }
        bool oldEnabled = GUI.enabled;
        if (samSiteService.IsConstructionCore(focusedUnit))
        {
            y = DrawSamSiteLogistics(focusedUnit, friendly, oldEnabled, y);
        }

        if (state != null)
        {
            GUI.enabled = oldEnabled && friendly && state.HasRadar;
            if (GUI.Button(new Rect(12f, y, 126f, 34f),
                state.HasRadar ? (state.IsRadarOnline ? "RDR ONLINE" : "RDR OFFLINE") : "NO LOCAL RDR",
                state.IsRadarOnline ? CommanderUiTheme.SelectedButton : CommanderUiTheme.DangerButton))
            {
                radarService.ToggleRadar();
            }
            GUI.enabled = oldEnabled;
            GUI.Label(new Rect(148f, y, radarWindowRect.width - 160f, 34f), radarService.StatusText, CommanderUiTheme.MutedLabel);
            y += 42f;
        }

        if (friendly && (state?.HasRadar == true || samSiteService.IsConstructionCore(focusedUnit)))
        {
            GlobalPosition coveragePosition = focusedUnit.GlobalPosition();
            if (samSiteService.TryGetConstructionRadarPosition(
                focusedUnit,
                out GlobalPosition siteRadarPosition))
            {
                coveragePosition = siteRadarPosition;
            }
            bool matches = samSiteAnalyzerService.CoverageMatches(focusedUnit);
            bool building = matches && samSiteAnalyzerService.CoverageOverlayBuilding;
            string coverageLabel = building
                ? $"GENERATING  {samSiteAnalyzerService.CoverageOverlayProgress:P0}"
                : matches && samSiteAnalyzerService.CoverageOverlayReady
                    ? "SHOW RADAR COVERAGE"
                    : "GENERATE RADAR COVERAGE";
            GUI.enabled = oldEnabled && !building;
            if (GUI.Button(
                new Rect(12f, y, radarWindowRect.width - 24f, 36f),
                coverageLabel,
                matches && samSiteAnalyzerService.CoverageOverlayReady
                    ? CommanderUiTheme.SelectedButton
                    : CommanderUiTheme.PrimaryButton))
            {
                if (matches && samSiteAnalyzerService.CoverageOverlayReady)
                {
                    CommanderTacticalMapService.Instance?.ShowCoverageFullscreen();
                }
                else
                {
                    samSiteAnalyzerService.GenerateCoverageOverlay(focusedUnit, coveragePosition);
                }
            }
            GUI.enabled = oldEnabled;
            y += 42f;
            if (matches)
            {
                float altitude = samSiteAnalyzerService.CoverageTargetAltitude;
                GUI.Label(
                    new Rect(12f, y, radarWindowRect.width - 24f, 24f),
                    $"TARGET ALTITUDE  {altitude:0} m AGL",
                    CommanderUiTheme.MutedLabel);
                float selectedAltitude = GUI.HorizontalSlider(
                    new Rect(12f, y + 26f, radarWindowRect.width - 24f, 22f),
                    altitude,
                    0f,
                    2000f);
                samSiteAnalyzerService.SetCoverageTargetAltitude(selectedAltitude);
                y += 52f;
            }
            if (building)
            {
                GUI.HorizontalSlider(
                    new Rect(12f, y, radarWindowRect.width - 24f, 16f),
                    samSiteAnalyzerService.CoverageOverlayProgress,
                    0f,
                    1f);
                y += 20f;
            }
        }

        if (repairService.IsRepairUnit(focusedUnit))
        {
            GUI.enabled = oldEnabled && friendly;
            bool nearest = repairService.UsesNearestTarget(focusedUnit);
            if (GUI.Button(
                new Rect(12f, y, radarWindowRect.width - 24f, 36f),
                nearest ? "REPAIR: NEAREST" : "REPAIR: PRIORITY",
                nearest ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
            {
                repairService.ToggleNearestTarget(focusedUnit);
            }
            GUI.enabled = oldEnabled;
            y += 40f;
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 38f), repairService.StatusText, CommanderUiTheme.MutedLabel);
            y += 42f;
        }

        if (focusedUnit is Ship ship)
        {
            GUI.enabled = oldEnabled && friendly;
            if (GUI.Button(new Rect(12f, y, radarWindowRect.width - 24f, 36f),
                supplyHeliService.GetNavalSupplyButtonLabel(ship), CommanderUiTheme.PrimaryButton))
            {
                supplyHeliService.RequestNavalSupply(ship);
            }
            GUI.enabled = oldEnabled;
            y += 40f;
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 38f), supplyHeliService.StatusText, CommanderUiTheme.MutedLabel);
        }
        else if (mobileEmplacementService.IsMoveableTrailer(focusedUnit))
        {
            const string relocationRequirement = "Idle Tractor or Flatbed required within 300 m.";
            bool relocating = mobileEmplacementService.IsRelocating(focusedUnit);
            bool haulerAvailable = mobileEmplacementService.HasAvailableHauler(focusedUnit);
            Rect relocateButtonRect = new(12f, y, radarWindowRect.width - 24f, 36f);
            GUI.enabled = oldEnabled && friendly && !relocating && haulerAvailable;
            if (GUI.Button(relocateButtonRect,
                new GUIContent(relocating ? "RELOCATION ACTIVE" : "RELOCATE TRAILER", relocationRequirement),
                CommanderUiTheme.PrimaryButton))
            {
                mobileEmplacementService.BeginRelocation();
            }
            GUI.enabled = oldEnabled;
            y += 42f;
            GUI.Label(new Rect(12f, y, radarWindowRect.width - 24f, 42f), mobileEmplacementService.StatusText, CommanderUiTheme.MutedLabel);
            if (relocateButtonRect.Contains(Event.current.mousePosition))
            {
                Rect tooltipRect = new(12f, Mathf.Max(34f, relocateButtonRect.y - 48f), radarWindowRect.width - 24f, 42f);
                GUI.Box(tooltipRect, string.Empty, CommanderUiTheme.Panel);
                GUI.Label(new Rect(tooltipRect.x + 8f, tooltipRect.y + 5f, tooltipRect.width - 16f, tooltipRect.height - 10f),
                    relocationRequirement, CommanderUiTheme.Label);
            }
        }
        GUI.DragWindow(new Rect(0f, 0f, radarWindowRect.width - 44f, 28f));
    }

    private float DrawSamSiteLogistics(
        Unit focusedUnit,
        bool friendly,
        bool oldEnabled,
        float y)
    {
        if (!ReferenceEquals(siteUiTarget, focusedUnit))
        {
            siteUiTarget = focusedUnit;
            siteAirbaseDropdownOpen = false;
            siteThresholdDropdownOpen = false;
        }

        float contentWidth = radarWindowRect.width - 24f;
        GUI.Label(
            new Rect(12f, y, contentWidth, 26f),
            $"{samSiteService.GetConstructionSiteSupply(focusedUnit)}"
            + $"     QUEUE  {samSiteService.GetConstructionQueueCount(focusedUnit)}",
            CommanderUiTheme.Header);
        y += 34f;

        float buttonWidth = (contentWidth - 8f) / 3f;
        GUI.enabled = oldEnabled
            && friendly
            && samSiteService.CanQueueConstruction(
                focusedUnit,
                CommanderSamSiteService.SiteBuildType.SamBattery);
        if (GUI.Button(
            new Rect(12f, y, buttonWidth, 36f),
            "SAM 40K",
            CommanderUiTheme.PrimaryButton))
        {
            samSiteService.QueueConstruction(
                focusedUnit,
                CommanderSamSiteService.SiteBuildType.SamBattery);
        }
        GUI.enabled = oldEnabled
            && friendly
            && samSiteService.CanQueueConstruction(
                focusedUnit,
                CommanderSamSiteService.SiteBuildType.Irm);
        if (GUI.Button(
            new Rect(16f + buttonWidth, y, buttonWidth, 36f),
            "IR 2K",
            CommanderUiTheme.Button))
        {
            samSiteService.QueueConstruction(
                focusedUnit,
                CommanderSamSiteService.SiteBuildType.Irm);
        }
        GUI.enabled = oldEnabled
            && friendly
            && samSiteService.CanQueueConstruction(
                focusedUnit,
                CommanderSamSiteService.SiteBuildType.Gun23mm);
        if (GUI.Button(
            new Rect(20f + buttonWidth * 2f, y, buttonWidth, 36f),
            "23MM 2K",
            CommanderUiTheme.Button))
        {
            samSiteService.QueueConstruction(
                focusedUnit,
                CommanderSamSiteService.SiteBuildType.Gun23mm);
        }
        GUI.enabled = oldEnabled;
        y += 48f;

        IReadOnlyList<CommanderSupplyHeliService.SamSiteAirbaseOption> airbases =
            samSiteService.GetConstructionSiteAirbases(focusedUnit);
        Airbase? selectedAirbase = samSiteService.GetConstructionSiteAirbase(focusedUnit);
        CommanderSupplyHeliService.SamSiteAirbaseOption? selectedOption = null;
        for (int i = 0; i < airbases.Count; i++)
        {
            if (ReferenceEquals(airbases[i].Airbase, selectedAirbase))
            {
                selectedOption = airbases[i];
                break;
            }
        }

        GUI.Label(new Rect(12f, y, contentWidth, 20f), "LOGISTICS AIRBASE", CommanderUiTheme.MutedLabel);
        y += 22f;
        string selectedLabel = selectedOption != null
            ? $"{selectedOption.Label}  ({selectedOption.Distance / 1000f:0.0} km)"
            : "NO COMPATIBLE AIRBASE";
        if (GUI.Button(
            new Rect(12f, y, contentWidth, 34f),
            selectedLabel,
            siteAirbaseDropdownOpen ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            siteAirbaseDropdownOpen = !siteAirbaseDropdownOpen;
            siteThresholdDropdownOpen = false;
        }
        y += 38f;

        if (siteAirbaseDropdownOpen)
        {
            for (int i = 0; i < airbases.Count; i++)
            {
                CommanderSupplyHeliService.SamSiteAirbaseOption option = airbases[i];
                string capability = option.SupportsSupply && option.SupportsJacknife
                    ? "SUPPLY + JACKNIFE"
                    : option.SupportsSupply ? "SUPPLY" : "JACKNIFE";
                string safety = option.Safe ? "SAFE" : "FORWARD";
                if (GUI.Button(
                    new Rect(12f, y, contentWidth, 30f),
                    $"{option.Label}  |  {option.Distance / 1000f:0.0} km  |  {capability}  |  {safety} {option.Risk:P0}",
                    ReferenceEquals(option.Airbase, selectedAirbase)
                        ? CommanderUiTheme.SelectedButton
                        : CommanderUiTheme.Button))
                {
                    samSiteService.SelectConstructionSiteAirbase(focusedUnit, i);
                    siteAirbaseDropdownOpen = false;
                }
                y += 32f;
            }
        }

        float halfWidth = (contentWidth - 6f) * 0.5f;
        bool automaticSupply = samSiteService.GetAutomaticSupplyEnabled(focusedUnit);
        GUI.enabled = oldEnabled && friendly;
        if (GUI.Button(
            new Rect(12f, y, halfWidth, 34f),
            automaticSupply ? "AUTO SUPPLY: ON" : "AUTO SUPPLY: OFF",
            automaticSupply ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            samSiteService.ToggleAutomaticSupply(focusedUnit);
        }
        float threshold = samSiteService.GetAutomaticSupplyThreshold(focusedUnit);
        if (GUI.Button(
            new Rect(18f + halfWidth, y, halfWidth, 34f),
            $"BELOW {threshold:0}",
            siteThresholdDropdownOpen ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            siteThresholdDropdownOpen = !siteThresholdDropdownOpen;
            siteAirbaseDropdownOpen = false;
        }
        GUI.enabled = oldEnabled;
        y += 38f;

        bool customRoute = samSiteService.GetConstructionCustomRouteEnabled(focusedUnit);
        bool routeVisible = samSiteService.IsConstructionSupplyRouteVisible(focusedUnit);
        GUI.enabled = oldEnabled && friendly;
        if (GUI.Button(
            new Rect(12f, y, halfWidth, 32f),
            customRoute ? "CUSTOM ROUTE: ON" : "CUSTOM ROUTE: OFF",
            customRoute ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            samSiteService.ToggleConstructionCustomRoute(focusedUnit);
        }
        GUI.enabled = oldEnabled
            && friendly
            && customRoute
            && samSiteService.CanShowConstructionSupplyRoute(focusedUnit);
        if (GUI.Button(
            new Rect(18f + halfWidth, y, halfWidth, 32f),
            routeVisible ? "HIDE SUPPLY ROUTE" : "SHOW SUPPLY ROUTE",
            routeVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            samSiteService.ToggleConstructionSupplyRoute(focusedUnit);
        }
        GUI.enabled = oldEnabled;
        y += 36f;

        if (siteThresholdDropdownOpen)
        {
            float optionWidth = contentWidth / CommanderSamSiteService.SupplyThresholdOptions.Length;
            for (int i = 0; i < CommanderSamSiteService.SupplyThresholdOptions.Length; i++)
            {
                float option = CommanderSamSiteService.SupplyThresholdOptions[i];
                if (GUI.Button(
                    new Rect(12f + optionWidth * i, y, optionWidth - 2f, 30f),
                    option >= 1000f ? $"{option / 1000f:0}K" : $"{option:0}",
                    Mathf.Approximately(option, threshold)
                        ? CommanderUiTheme.SelectedButton
                        : CommanderUiTheme.Button))
                {
                    samSiteService.SetAutomaticSupplyThreshold(focusedUnit, option);
                    siteThresholdDropdownOpen = false;
                }
            }
            y += 34f;
        }

        GUI.enabled = oldEnabled
            && friendly
            && samSiteService.CanRequestConstructionSupply(focusedUnit);
        if (GUI.Button(
            new Rect(12f, y, contentWidth, 36f),
            "REQUEST SUPPLY",
            CommanderUiTheme.PrimaryButton))
        {
            samSiteService.RequestConstructionSupply(focusedUnit);
        }
        GUI.enabled = oldEnabled;
        y += 46f;

        int incomingJacknifes = samSiteService.GetIncomingConstructionJacknifes(focusedUnit);
        string incomingLabel = incomingJacknifes > 0 ? $"  +{incomingJacknifes} INBOUND" : string.Empty;
        GUI.Label(
            new Rect(12f, y, halfWidth, 34f),
            $"JACKNIFE  {samSiteService.GetConstructionSiteJacknifes(focusedUnit)}/2{incomingLabel}",
            CommanderUiTheme.Header);
        GUI.enabled = oldEnabled
            && friendly
            && samSiteService.CanRequestConstructionJacknife(focusedUnit);
        if (GUI.Button(
            new Rect(18f + halfWidth, y, halfWidth, 34f),
            "REQUEST JACKNIFE",
            CommanderUiTheme.Button))
        {
            samSiteService.RequestConstructionJacknife(focusedUnit);
        }
        GUI.enabled = oldEnabled;
        y += 44f;

        GUI.Label(
            new Rect(12f, y, contentWidth, 42f),
            samSiteService.GetConstructionJacknifeStatus(focusedUnit),
            CommanderUiTheme.Label);
        y += 44f;

        GUI.Label(
            new Rect(12f, y, contentWidth, 70f),
            samSiteService.GetConstructionSiteStatus(focusedUnit),
            CommanderUiTheme.MutedLabel);
        return y + 74f;
    }

    private bool TryGetUnitSystemsTarget(out Unit unit, out CommanderRadarService.RadarState? state)
    {
        unit = selectionService.FocusedSelection!;
        state = null;
        if (unit == null || unit.disabled)
        {
            return false;
        }

        if (radarService.TryGetFocusedState(out CommanderRadarService.RadarState radarState)
            && ReferenceEquals(radarState.Unit, unit))
        {
            state = radarState;
        }

        return state != null
            || unit is Ship
            || repairService.IsRepairUnit(unit)
            || mobileEmplacementService.IsMoveableTrailer(unit)
            || samSiteService.IsConstructionCore(unit);
    }

    private void DrawReserveWindow(int windowId)
    {
        if (CommanderUiTheme.DrawHelpButton(reserveWindowRect.width, ref reserveHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, reserveWindowRect.width - 24f, 84f),
                "FACTION RESERVE | Manage reserve stockpiles across LAND, AIR, and NAVAL branches. Multipliers (x1, x5, x10, MAX) apply to +BUY, SELL, and DEPLOY actions. HOLD intercepts automatic deployment so units stay in reserve for manual command.");
        }
        if (GUI.Button(new Rect(reserveWindowRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.DangerButton))
        {
            reserveWindowVisible = false;
            return;
        }

        float y = reserveHelpVisible ? 128f : 38f;
        GUI.Label(new Rect(12f, y, reserveWindowRect.width - 24f, 28f),
            $"FUNDS: {spawnService.GetFactionFundsLabel()}    |    LAND RESERVE: {spawnService.GetProductionReserveTotal()} UNITS", CommanderUiTheme.Header);
        y += 32f;

        // Domain Tabs
        float tabWidth = (reserveWindowRect.width - 36f) / 4f;
        if (GUI.Button(new Rect(12f, y, tabWidth, 30f), "LAND",
            reserveTab == 0 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            reserveTab = 0;
            reserveScroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(16f + tabWidth, y, tabWidth, 30f), "AIR",
            reserveTab == 1 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            reserveTab = 1;
            reserveScroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(20f + tabWidth * 2f, y, tabWidth, 30f), "NAVAL",
            reserveTab == 2 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            reserveTab = 2;
            reserveScroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(24f + tabWidth * 3f, y, tabWidth, 30f), "CATEGORIES",
            reserveTab == 3 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            reserveTab = 3;
            reserveScroll = Vector2.zero;
        }
        y += 36f;

        // Batch Multipliers
        float multWidth = (reserveWindowRect.width - 24f - 130f) / 4f;
        GUI.Label(new Rect(14f, y + 3f, 120f, 22f), "BATCH MULTIPLIER:", CommanderUiTheme.MutedLabel);
        if (GUI.Button(new Rect(136f, y, multWidth, 24f), "x1", reserveMultiplier == 1 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button)) reserveMultiplier = 1;
        if (GUI.Button(new Rect(140f + multWidth, y, multWidth, 24f), "x5", reserveMultiplier == 5 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button)) reserveMultiplier = 5;
        if (GUI.Button(new Rect(144f + multWidth * 2f, y, multWidth, 24f), "x10", reserveMultiplier == 10 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button)) reserveMultiplier = 10;
        if (GUI.Button(new Rect(148f + multWidth * 3f, y, multWidth, 24f), "MAX", reserveMultiplier == 0 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button)) reserveMultiplier = 0;
        y += 32f;

        int activeQty = reserveMultiplier == 0 ? int.MaxValue : reserveMultiplier;
        string buySuffix = reserveMultiplier == 0 ? "MAX" : ("x" + reserveMultiplier);
        string sellSuffix = reserveMultiplier == 0 ? "ALL" : ("x" + reserveMultiplier);

        Rect view = new(12f, y, reserveWindowRect.width - 24f, reserveWindowRect.height - y - 14f);

        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        if (factionSvc == null)
        {
            GUI.Label(view, "Faction Service is unavailable.", CommanderUiTheme.Label);
            GUI.DragWindow(new Rect(0f, 0f, reserveWindowRect.width - 72f, 28f));
            return;
        }

        if (reserveTab == 0)
        {
            // LAND TAB
            IReadOnlyList<VehicleDefinition> definitions = factionSvc.LandDefinitions;
            Rect inner = new(0f, 0f, view.width - 20f, Mathf.Max(view.height, definitions.Count * 48f + 6f));
            reserveScroll = GUI.BeginScrollView(view, reserveScroll, inner);
            for (int i = 0; i < definitions.Count; i++)
            {
                VehicleDefinition def = definitions[i];
                string cat = CommanderGameAccess.GetVehicleCategoryLabel(def);
                bool catHeld = factionSvc.IsCategoryHeld(cat);
                bool indHeld = factionSvc.IsDefinitionHeld(def);
                int count = factionSvc.GetReserveCount(def);
                float cost = factionSvc.GetPurchaseCost(def);
                string costStr = UnitConverter.ValueReading(cost) ?? ("$" + cost.ToString("N0"));

                Rect row = new(4f, 3f + i * 48f, inner.width - 8f, 44f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Panel);

                // Hold toggle
                if (catHeld)
                {
                    GUI.Label(new Rect(row.x + 6f, row.y + 12f, 70f, 20f), "CAT HOLD", CommanderUiTheme.MutedLabel);
                }
                else
                {
                    bool newHeld = GUI.Toggle(new Rect(row.x + 6f, row.y + 11f, 60f, 22f), indHeld, "HOLD", CommanderUiTheme.Toggle);
                    if (newHeld != indHeld) factionSvc.ToggleDefinition(def);
                }

                // Name & Cost
                GUI.Label(new Rect(row.x + 76f, row.y + 4f, row.width - 380f, 20f), CommanderGameAccess.GetVehicleLabel(def), CommanderUiTheme.Label);
                GUI.Label(new Rect(row.x + 76f, row.y + 22f, row.width - 380f, 18f), $"{cat} | {costStr}", CommanderUiTheme.MutedLabel);

                // Stock count
                GUI.Label(new Rect(row.xMax - 290f, row.y + 10f, 65f, 24f), $"QTY: {count}", CommanderUiTheme.Header);

                // +BUY Button
                if (GUI.Button(new Rect(row.xMax - 220f, row.y + 6f, 68f, 32f), $"+BUY {buySuffix}", CommanderUiTheme.Button))
                {
                    factionSvc.TryBuyStockToReserve(def, activeQty, out _);
                }

                // SELL Button
                bool canSell = count > 0;
                bool oldE = GUI.enabled;
                GUI.enabled = oldE && canSell;
                if (GUI.Button(new Rect(row.xMax - 148f, row.y + 6f, 68f, 32f), $"SELL {sellSuffix}", CommanderUiTheme.DangerButton))
                {
                    factionSvc.TryScrapStockFromReserve(def, activeQty, out _);
                }
                GUI.enabled = oldE;

                // DEPLOY Button
                int deployCount = reserveMultiplier == 0 ? Mathf.Max(1, count) : reserveMultiplier;
                string deployLabel = count >= deployCount ? $"FREE x{deployCount}" : $"QUEUE x{deployCount}";
                if (GUI.Button(new Rect(row.xMax - 76f, row.y + 6f, 72f, 32f), deployLabel, count >= deployCount ? CommanderUiTheme.PrimaryButton : CommanderUiTheme.Button))
                {
                    for (int q = 0; q < deployCount; q++)
                    {
                        spawnService.AddVehicleToSpawnList(def);
                    }
                }
            }
            GUI.EndScrollView();
        }
        else if (reserveTab == 1)
        {
            // AIR TAB
            IReadOnlyList<AircraftDefinition> definitions = factionSvc.AirDefinitions;
            Rect inner = new(0f, 0f, view.width - 20f, Mathf.Max(view.height, definitions.Count * 48f + 6f));
            reserveScroll = GUI.BeginScrollView(view, reserveScroll, inner);
            for (int i = 0; i < definitions.Count; i++)
            {
                AircraftDefinition def = definitions[i];
                int count = factionSvc.GetReserveCount(def);
                float cost = factionSvc.GetPurchaseCost(def);
                string costStr = UnitConverter.ValueReading(cost) ?? ("$" + cost.ToString("N0"));
                bool indHeld = factionSvc.IsDefinitionHeld(def);

                Rect row = new(4f, 3f + i * 48f, inner.width - 8f, 44f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Panel);

                bool newHeld = GUI.Toggle(new Rect(row.x + 6f, row.y + 11f, 60f, 22f), indHeld, "HOLD", CommanderUiTheme.Toggle);
                if (newHeld != indHeld) factionSvc.ToggleDefinition(def);

                GUI.Label(new Rect(row.x + 76f, row.y + 4f, row.width - 320f, 20f), def.unitName, CommanderUiTheme.Label);
                GUI.Label(new Rect(row.x + 76f, row.y + 22f, row.width - 320f, 18f), $"AIRCRAFT | {costStr}", CommanderUiTheme.MutedLabel);

                GUI.Label(new Rect(row.xMax - 220f, row.y + 10f, 65f, 24f), $"QTY: {count}", CommanderUiTheme.Header);

                if (GUI.Button(new Rect(row.xMax - 148f, row.y + 6f, 68f, 32f), $"+BUY {buySuffix}", CommanderUiTheme.Button))
                {
                    factionSvc.TryBuyStockToReserve(def, activeQty, out _);
                }

                bool canSell = count > 0;
                bool oldE = GUI.enabled;
                GUI.enabled = oldE && canSell;
                if (GUI.Button(new Rect(row.xMax - 76f, row.y + 6f, 72f, 32f), $"SELL {sellSuffix}", CommanderUiTheme.DangerButton))
                {
                    factionSvc.TryScrapStockFromReserve(def, activeQty, out _);
                }
                GUI.enabled = oldE;
            }
            GUI.EndScrollView();
        }
        else if (reserveTab == 2)
        {
            // NAVAL TAB
            IReadOnlyList<ShipDefinition> definitions = factionSvc.NavalDefinitions;
            Rect inner = new(0f, 0f, view.width - 20f, Mathf.Max(view.height, definitions.Count * 48f + 6f));
            reserveScroll = GUI.BeginScrollView(view, reserveScroll, inner);
            for (int i = 0; i < definitions.Count; i++)
            {
                ShipDefinition def = definitions[i];
                int count = factionSvc.GetReserveCount(def);
                float cost = factionSvc.GetPurchaseCost(def);
                string costStr = UnitConverter.ValueReading(cost) ?? ("$" + cost.ToString("N0"));
                bool indHeld = factionSvc.IsDefinitionHeld(def);

                Rect row = new(4f, 3f + i * 48f, inner.width - 8f, 44f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Panel);

                bool newHeld = GUI.Toggle(new Rect(row.x + 6f, row.y + 11f, 60f, 22f), indHeld, "HOLD", CommanderUiTheme.Toggle);
                if (newHeld != indHeld) factionSvc.ToggleDefinition(def);

                GUI.Label(new Rect(row.x + 76f, row.y + 4f, row.width - 320f, 20f), def.unitName, CommanderUiTheme.Label);
                GUI.Label(new Rect(row.x + 76f, row.y + 22f, row.width - 320f, 18f), $"WARSHIP | {costStr}", CommanderUiTheme.MutedLabel);

                GUI.Label(new Rect(row.xMax - 220f, row.y + 10f, 65f, 24f), $"QTY: {count}", CommanderUiTheme.Header);

                if (GUI.Button(new Rect(row.xMax - 148f, row.y + 6f, 68f, 32f), $"+BUY {buySuffix}", CommanderUiTheme.Button))
                {
                    factionSvc.TryBuyStockToReserve(def, activeQty, out _);
                }

                bool canSell = count > 0;
                bool oldE = GUI.enabled;
                GUI.enabled = oldE && canSell;
                if (GUI.Button(new Rect(row.xMax - 76f, row.y + 6f, 72f, 32f), $"SELL {sellSuffix}", CommanderUiTheme.DangerButton))
                {
                    factionSvc.TryScrapStockFromReserve(def, activeQty, out _);
                }
                GUI.enabled = oldE;
            }
            GUI.EndScrollView();
        }
        else
        {
            // CATEGORIES TAB
            IReadOnlyList<string> categories = spawnService.GetProductionCategories();
            float bulkY = view.y;
            float bulkWidth = (view.width - 10f) * 0.5f;

            Rect catView = new(view.x, view.y + 38f, view.width, view.height - 38f);
            if (GUI.Button(new Rect(view.x, bulkY, bulkWidth, 32f), "HOLD ALL", CommanderUiTheme.DangerButton))
            {
                factionSvc.SetAllCategoriesHeld(true);
            }
            if (GUI.Button(new Rect(view.x + bulkWidth + 10f, bulkY, bulkWidth, 32f), "RELEASE ALL", CommanderUiTheme.Button))
            {
                factionSvc.SetAllCategoriesHeld(false);
            }

            Rect inner = new(0f, 0f, catView.width - 20f, Mathf.Max(catView.height, categories.Count * 46f + 6f));
            reserveScroll = GUI.BeginScrollView(catView, reserveScroll, inner);
            for (int i = 0; i < categories.Count; i++)
            {
                string category = categories[i];
                bool held = factionSvc.IsCategoryHeld(category);
                Rect row = new(4f, 3f + i * 46f, inner.width - 8f, 42f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Panel);
                bool updatedHeld = GUI.Toggle(new Rect(row.x + 10f, row.y + 10f, 64f, 22f), held, "HOLD", CommanderUiTheme.Toggle);
                if (updatedHeld != held)
                {
                    factionSvc.ToggleCategory(category);
                }
                GUI.Label(new Rect(row.x + 94f, row.y + 8f, row.width - 230f, 26f), category, CommanderUiTheme.Header);
                GUI.Label(new Rect(row.xMax - 126f, row.y + 8f, 118f, 26f),
                    $"RESERVE {spawnService.GetProductionCategoryReserveCount(category)}", CommanderUiTheme.MutedLabel);
            }
            GUI.EndScrollView();

            if (categories.Count == 0)
            {
                GUI.Label(catView, "No friendly vehicle factories are currently active.", CommanderUiTheme.Label);
            }
        }

        GUI.DragWindow(new Rect(0f, 0f, reserveWindowRect.width - 72f, 28f));
    }
}
