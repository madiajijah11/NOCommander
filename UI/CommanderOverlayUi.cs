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
    private const int CheatWindowId = 0x434F4D43;
    private const int OobWindowId = 0x434F4D4F;
    private const int FactoryWindowId = 0x434F4D46;
    private const int BuildingWindowId = 0x434F4D42;
    private const int MoneyWindowId = 0x434F4D24;

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
    private bool cheatWindowVisible;
    private bool cheatHelpVisible;
    private int cheatTab;
    private int cheatCategoryIndex;
    private string cheatSearchFilter = string.Empty;
    private bool cheatSpawnAsEnemy;
    private Vector2 cheatScroll;
    private bool oobWindowVisible;
    private bool oobHelpVisible;
    private Vector2 oobScroll;
    private Rect oobWindowRect;
    private bool factoryWindowVisible;
    private bool factoryHelpVisible;
    private Vector2 factoryScroll;
    private Rect factoryWindowRect;
    private bool buildingWindowVisible;
    private bool buildingHelpVisible;
    private int buildingMainTab;
    private int buildingCategoryTab;
    private string buildingSearchFilter = string.Empty;
    private Vector2 buildingScroll;
    private Vector2 projectScroll;
    private string reserveCategoryFilter = "ALL";
    private Rect buildingWindowRect;
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
    private Rect cheatWindowRect;
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

        if (!positionsInitialized)
        {
            moneyRect = new Rect(70f, 10f, 320f, 32f);
            float panelHeight = Mathf.Min(480f, CommanderUiScale.Height - 24f);
            panelRect = new Rect(68f, Mathf.Max(12f, centerY - panelHeight * 0.5f), 340f, panelHeight);
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
            float settingsWidth = Mathf.Min(700f, CommanderUiScale.Width - 24f);
            float settingsHeight = Mathf.Min(540f, CommanderUiScale.Height - 24f);
            settingsWindowRect = new Rect(
                Mathf.Max(12f, (CommanderUiScale.Width - settingsWidth) * 0.5f),
                Mathf.Max(12f, (CommanderUiScale.Height - settingsHeight) * 0.5f),
                settingsWidth,
                settingsHeight);
            float cheatWidth = Mathf.Min(760f, CommanderUiScale.Width - 24f);
            float cheatHeight = Mathf.Min(700f, CommanderUiScale.Height - 24f);
            cheatWindowRect = new Rect(
                Mathf.Max(12f, (CommanderUiScale.Width - cheatWidth) * 0.5f),
                Mathf.Max(12f, (CommanderUiScale.Height - cheatHeight) * 0.5f),
                cheatWidth,
                cheatHeight);
            float oobWidth = Mathf.Min(660f, CommanderUiScale.Width - 24f);
            float oobHeight = Mathf.Min(430f, CommanderUiScale.Height - 24f);
            oobWindowRect = new Rect(
                Mathf.Max(12f, (CommanderUiScale.Width - oobWidth) * 0.5f),
                Mathf.Max(12f, (CommanderUiScale.Height - oobHeight) * 0.5f),
                oobWidth,
                oobHeight);
            float factoryWidth = Mathf.Min(740f, CommanderUiScale.Width - 24f);
            float factoryHeight = Mathf.Min(600f, CommanderUiScale.Height - 24f);
            factoryWindowRect = new Rect(
                Mathf.Max(12f, (CommanderUiScale.Width - factoryWidth) * 0.5f),
                Mathf.Max(12f, (CommanderUiScale.Height - factoryHeight) * 0.5f),
                factoryWidth,
                factoryHeight);
            float bldWidth = Mathf.Min(740f, CommanderUiScale.Width - 24f);
            float bldHeight = Mathf.Min(640f, CommanderUiScale.Height - 24f);
            buildingWindowRect = new Rect(
                Mathf.Max(12f, (CommanderUiScale.Width - bldWidth) * 0.5f),
                Mathf.Max(12f, (CommanderUiScale.Height - bldHeight) * 0.5f),
                bldWidth,
                bldHeight);
            positionsInitialized = true;
        }
        else
        {
            panelRect.width = 340f;
            panelRect.height = Mathf.Min(480f, CommanderUiScale.Height - 24f);
            reserveWindowRect.width = Mathf.Min(590f, CommanderUiScale.Width - 24f);
            reserveWindowRect.height = Mathf.Min(610f, CommanderUiScale.Height - 24f);
            settingsWindowRect.width = Mathf.Min(700f, CommanderUiScale.Width - 24f);
            settingsWindowRect.height = Mathf.Min(540f, CommanderUiScale.Height - 24f);
            cheatWindowRect.width = Mathf.Min(760f, CommanderUiScale.Width - 24f);
            cheatWindowRect.height = Mathf.Min(700f, CommanderUiScale.Height - 24f);
            oobWindowRect.width = Mathf.Min(660f, CommanderUiScale.Width - 24f);
            oobWindowRect.height = Mathf.Min(430f, CommanderUiScale.Height - 24f);
            factoryWindowRect.width = Mathf.Min(740f, CommanderUiScale.Width - 24f);
            factoryWindowRect.height = Mathf.Min(600f, CommanderUiScale.Height - 24f);
            buildingWindowRect.width = Mathf.Min(740f, CommanderUiScale.Width - 24f);
            buildingWindowRect.height = Mathf.Min(640f, CommanderUiScale.Height - 24f);
            moneyRect.width = 220f;
            moneyRect.height = 32f;
        }
        moneyRect = CommanderUiTheme.ClampWindow(moneyRect, 6f);
        panelRect = CommanderUiTheme.ClampWindow(panelRect);
        reserveWindowRect = CommanderUiTheme.ClampWindow(reserveWindowRect);
        pinnedWindowRect = CommanderUiTheme.ClampWindow(pinnedWindowRect);
        settingsWindowRect = CommanderUiTheme.ClampWindow(settingsWindowRect);
        cheatWindowRect = CommanderUiTheme.ClampWindow(cheatWindowRect);
        oobWindowRect = CommanderUiTheme.ClampWindow(oobWindowRect);
        factoryWindowRect = CommanderUiTheme.ClampWindow(factoryWindowRect);
        buildingWindowRect = CommanderUiTheme.ClampWindow(buildingWindowRect);
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

        int selectedCount = selectionService.SelectedUnits.Count;
        float barH = selectedCount == 1 ? 92f : 66f;
        selectionBarRect = new Rect(
            Mathf.Max(12f, (CommanderUiScale.Width - 1040f) * 0.5f),
            CommanderUiScale.Height - barH - 10f,
            Mathf.Min(1040f, CommanderUiScale.Width - 24f),
            barH);
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
            || (cheatWindowVisible && cheatWindowRect.Contains(guiPoint))
            || (oobWindowVisible && oobWindowRect.Contains(guiPoint))
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
        if (showCommandButton)
        {
            DrawModernLeftDock();
        }

        if (advanced && showFactionMoney)
        {
            moneyRect = GUI.Window(MoneyWindowId, moneyRect, DrawMoneyWindow, string.Empty, CommanderUiTheme.Panel);
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
        if (cheatWindowVisible)
        {
            cheatWindowRect = GUI.Window(CheatWindowId, cheatWindowRect, DrawCheatWindow, "CHEAT / SANDBOX", CommanderUiTheme.Window);
        }
        if (oobWindowVisible)
        {
            oobWindowRect = GUI.Window(OobWindowId, oobWindowRect, DrawOobWindow, "ORDER OF BATTLE (ARMY STATUS)", CommanderUiTheme.Window);
        }
        if (advanced && factoryWindowVisible)
        {
            factoryWindowRect = GUI.Window(FactoryWindowId, factoryWindowRect, DrawFactoryWindow, "FACTORIES & PRODUCTION LINES", CommanderUiTheme.Window);
        }
        if (advanced && buildingWindowVisible)
        {
            buildingWindowRect = GUI.Window(BuildingWindowId, buildingWindowRect, DrawBuildingEconomyWindow, "BUILDINGS & INFRASTRUCTURE ECONOMY", CommanderUiTheme.Window);
        }
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
        CommanderUiTheme.DrawHeaderStripe(new Rect(0f, 0f, panelRect.width, panelRect.height));
        CommanderUiTheme.DrawMutedFrame(new Rect(0f, 0f, panelRect.width, panelRect.height));
        if (CommanderUiTheme.DrawHelpButton(panelRect.width, ref panelHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, panelRect.width - 24f, 92f),
                "LMB selects; Shift+LMB adds; empty LMB clears. RMB orders friendly ground units and ships. Commander camera controls are configured under Settings > Controls. M opens the fullscreen map.");
        }
        if (CommanderUiTheme.DrawCloseButton(panelRect.width))
        {
            panelVisible = false;
        }

        float y = panelHelpVisible ? 136f : 36f;
        bool advanced = CommanderFeatureGate.AdvancedFeaturesEnabled;
        Rect unlockRect = default;
        const string unlockTooltip = "Features behind this toggle are designed for large strategic missions such as Escalation and Terminal Control. Enabling them in other missions may break the mission.";
        if (!advanced)
        {
            string mission = string.IsNullOrWhiteSpace(CommanderFeatureGate.MissionName)
                ? "UNKNOWN MISSION"
                : CommanderFeatureGate.MissionName.ToUpperInvariant();
            GUI.Label(new Rect(12f, y, panelRect.width - 24f, 20f), $"CORE MODE   |   {mission}", CommanderUiTheme.MutedLabel);
            y += 22f;
            unlockRect = new Rect(12f, y, panelRect.width - 24f, 34f);
            string unlockLabel = advancedUnlockConfirmation
                ? "ARE YOU SURE? UNLOCK ALL"
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
            y += 40f;
        }

        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && advanced;

        // [01] GROUND COMBAT & INDUSTRY
        GUI.Label(new Rect(12f, y, panelRect.width - 24f, 18f), "GROUND COMBAT & INDUSTRY", CommanderUiTheme.MutedLabel);
        y += 20f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 32f), "SELECT NEAREST DEPOT", CommanderUiTheme.PrimaryButton))
        {
            spawnService.SelectNearestDepot();
        }
        y += 36f;
        float halfW = (panelRect.width - 30f) * 0.5f;
        if (GUI.Button(new Rect(12f, y, halfW, 32f), "FACTORIES", factoryWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            factoryWindowVisible = !factoryWindowVisible;
            if (factoryWindowVisible && CommanderFactoryProductionService.Instance?.SelectedFactory != null)
            {
                JumpToFactory(CommanderFactoryProductionService.Instance.SelectedFactory);
            }
        }
        if (GUI.Button(new Rect(18f + halfW, y, halfW, 32f), "RESERVE", reserveWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            reserveWindowVisible = !reserveWindowVisible;
        }
        y += 40f;

        // [02] AIR & NAVAL TACTICAL SUPPORT
        GUI.Label(new Rect(12f, y, panelRect.width - 24f, 18f), "AIR & NAVAL TACTICAL SUPPORT", CommanderUiTheme.MutedLabel);
        y += 20f;
        float halfBtn = (panelRect.width - 30f) * 0.5f;
        if (GUI.Button(new Rect(12f, y, halfBtn, 32f), "✈️ AIR CAP", CommanderUiTheme.PrimaryButton))
        {
            airCommandService.QuickCallInMission(CommanderAirCommandService.AirCommandMode.AirGuard);
        }
        if (GUI.Button(new Rect(18f + halfBtn, y, halfBtn, 32f), "💣 STRIKE / CAS", CommanderUiTheme.PrimaryButton))
        {
            airCommandService.QuickCallInMission(CommanderAirCommandService.AirCommandMode.Cas);
        }
        y += 36f;
        if (GUI.Button(new Rect(12f, y, halfBtn, 32f), "📡 SEAD ARAD", CommanderUiTheme.PrimaryButton))
        {
            airCommandService.QuickCallInMission(CommanderAirCommandService.AirCommandMode.Arad);
        }
        if (GUI.Button(new Rect(18f + halfBtn, y, halfBtn, 32f), "🚁 CARGO DROP", CommanderUiTheme.PrimaryButton))
        {
            supplyHeliService.QuickCallInCargoAirdrop();
        }
        y += 36f;
        if (GUI.Button(new Rect(12f, y, halfBtn, 30f), "⚓ NAVAL FLEET", navalPurchaseUi.Visible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            navalPurchaseUi.Toggle();
        }
        if (GUI.Button(new Rect(18f + halfBtn, y, halfBtn, 30f), "⚙️ ADVANCED AIR", airCommandUi.Visible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            if (airCommandUi.Visible) airCommandUi.Hide();
            else { panelVisible = false; airCommandUi.Show(); }
        }
        y += 38f;

        // [03] INFRASTRUCTURE & ECONOMY
        GUI.Label(new Rect(12f, y, panelRect.width - 24f, 18f), "INFRASTRUCTURE & ECONOMY", CommanderUiTheme.MutedLabel);
        y += 20f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 32f), "BUILDINGS & ECONOMY", buildingWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.PrimaryButton))
        {
            buildingWindowVisible = !buildingWindowVisible;
        }
        y += 40f;

        // [04] TACTICAL TOOLS & SANDBOX
        GUI.Label(new Rect(12f, y, panelRect.width - 24f, 18f), "TACTICAL TOOLS & SANDBOX", CommanderUiTheme.MutedLabel);
        y += 20f;
        float thirdW = (panelRect.width - 36f) / 3f;
        if (GUI.Button(new Rect(12f, y, thirdW, 32f), "SAM SITES", samSiteAnalyzerUi.Visible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            samSiteAnalyzerUi.Toggle();
        }
        if (GUI.Button(new Rect(18f + thirdW, y, thirdW, 32f), "ARMY OOB", oobWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            oobWindowVisible = !oobWindowVisible;
        }
        if (GUI.Button(new Rect(24f + thirdW * 2f, y, thirdW, 32f), "SANDBOX", cheatWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.PrimaryButton))
        {
            cheatWindowVisible = !cheatWindowVisible;
        }
        y += 40f;

        string helper = supplyHeliService.AwaitingTargetSelection
            ? "Select cargo destination in 3D world. Cancel binding cancels."
            : airCommandService.AwaitingAreaSelection
                ? "Select Air Command mission area in 3D world."
                : navalPurchaseService.AwaitingRallySelection
                    ? "Select water rally point on fullscreen map."
                : mobileEmplacementService.AwaitingDestination
                    ? "Select trailer destination in 3D world."
            : spawnService.AwaitingRallyPointSelection
                ? "Select rally point on map or in 3D world."
                : string.Empty;

        if (!string.IsNullOrEmpty(helper))
        {
            GUI.Label(new Rect(14f, y, panelRect.width - 28f, 22f), helper, CommanderUiTheme.MutedLabel);
            y += 26f;
        }

        GUI.enabled = oldEnabled;

        // [05] SETTINGS & EXIT
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 32f), "SETTINGS", settingsVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            settingsVisible = !settingsVisible;
            bindingCapture = null;
        }
        y += 36f;
        if (GUI.Button(new Rect(12f, y, panelRect.width - 24f, 34f), "EXIT COMMANDER MODE", CommanderUiTheme.DangerButton))
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
        CommanderUiTheme.DrawHeaderStripe(new Rect(0f, 0f, settingsWindowRect.width, settingsWindowRect.height));
        CommanderUiTheme.DrawMutedFrame(new Rect(0f, 0f, settingsWindowRect.width, settingsWindowRect.height));
        CaptureBindingInput();
        if (CommanderUiTheme.DrawHelpButton(settingsWindowRect.width, ref settingsHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, settingsWindowRect.width - 24f, 74f),
                "Settings are saved in the BepInEx configuration. Commander camera bindings are read only while Commander mode is active and do not alter aircraft controls.");
        }
        if (CommanderUiTheme.DrawCloseButton(settingsWindowRect.width))
        {
            settingsVisible = false;
            bindingCapture = null;
        }

        float y = settingsHelpVisible ? 118f : 38f;
        float tabWidth = (settingsWindowRect.width - 36f) / 3f;
        DrawSettingsTab(new Rect(12f, y, tabWidth, 32f), "GAMEPLAY", 0);
        DrawSettingsTab(new Rect(12f + tabWidth + 6f, y, tabWidth, 32f), "UI / HIDE", 1);
        DrawSettingsTab(new Rect(12f + (tabWidth + 6f) * 2f, y, tabWidth, 32f), "CONTROLS", 2);
        y += 44f;

        if (settingsTab == 0)
        {
            DrawGameplaySettings(y);
        }
        else if (settingsTab == 1)
        {
            DrawUiSettings(y);
        }
        else
        {
            DrawControlSettings(y);
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
        GUI.Box(new Rect(12f, y, settingsWindowRect.width - 24f, 430f), string.Empty, CommanderUiTheme.Panel);
        CommanderUiTheme.DrawMutedFrame(new Rect(12f, y, settingsWindowRect.width - 24f, 430f));
        GUI.Label(
            new Rect(24f, y + 6f, settingsWindowRect.width - 48f, 22f),
            "Bindings are active in Commander mode. Click one, then press a key/mouse button. Escape cancels.",
            CommanderUiTheme.MutedLabel);

        float columnWidth = (settingsWindowRect.width - 66f) * 0.5f;
        float left = 24f;
        float right = 42f + columnWidth;
        float rowY = y + 32f;

        GUI.Label(new Rect(left, rowY, columnWidth, 22f), "CAMERA CONTROLS", CommanderUiTheme.Header);
        GUI.Label(new Rect(right, rowY, columnWidth, 22f), "TACTICAL ACTIONS", CommanderUiTheme.Header);
        rowY += 24f;

        // Left Column: Camera (9 rows)
        DrawBinding(new Rect(left, rowY, columnWidth, 28f), "Forward", "forward");
        DrawBinding(new Rect(left, rowY + 32f, columnWidth, 28f), "Backward", "backward");
        DrawBinding(new Rect(left, rowY + 64f, columnWidth, 28f), "Strafe left", "left");
        DrawBinding(new Rect(left, rowY + 96f, columnWidth, 28f), "Strafe right", "right");
        DrawBinding(new Rect(left, rowY + 128f, columnWidth, 28f), "Elevate up", "up");
        DrawBinding(new Rect(left, rowY + 160f, columnWidth, 28f), "Elevate down", "down");
        DrawBinding(new Rect(left, rowY + 192f, columnWidth, 28f), "Free look", "look");
        DrawBinding(new Rect(left, rowY + 224f, columnWidth, 28f), "Speed boost", "boost");
        Rect centerFollowRect = new(left, rowY + 256f, columnWidth, 28f);
        DrawBinding(centerFollowRect, "Center / follow", "center_follow");

        // Right Column: Actions & Controls (9 rows)
        DrawBinding(new Rect(right, rowY, columnWidth, 28f), "Select / place", "primary");
        DrawBinding(new Rect(right, rowY + 32f, columnWidth, 28f), "Move / order", "secondary");
        DrawBinding(new Rect(right, rowY + 64f, columnWidth, 28f), "Add selection", "add_selection");
        DrawBinding(new Rect(right, rowY + 96f, columnWidth, 28f), "Rotate left", "rot_left");
        DrawBinding(new Rect(right, rowY + 128f, columnWidth, 28f), "Rotate right", "rot_right");
        DrawBinding(new Rect(right, rowY + 160f, columnWidth, 28f), "Tilt up", "pitch_up");
        DrawBinding(new Rect(right, rowY + 192f, columnWidth, 28f), "Tilt down", "pitch_down");
        DrawBinding(new Rect(right, rowY + 224f, columnWidth, 28f), "Delete modifier", "delete_modifier");
        DrawBinding(new Rect(right, rowY + 256f, columnWidth, 28f), "UI cycle", "toggle_ui");

        // Reset Buttons Row (Cleanly spaced at bottom)
        float resetY = rowY + 310f;
        if (GUI.Button(new Rect(left, resetY, columnWidth, 32f), "RESET CAMERA", CommanderUiTheme.Button))
        {
            ResetCameraBindings();
            bindingCapture = null;
        }
        if (GUI.Button(new Rect(right, resetY, columnWidth, 32f), "RESET ACTIONS", CommanderUiTheme.Button))
        {
            ResetActionBindings();
            bindingCapture = null;
        }
    }

    private void DrawFactoryWindow(int windowId)
    {
        CommanderUiTheme.DrawHeaderStripe(new Rect(0f, 0f, factoryWindowRect.width, factoryWindowRect.height));
        CommanderUiTheme.DrawMutedFrame(new Rect(0f, 0f, factoryWindowRect.width, factoryWindowRect.height));
        if (CommanderUiTheme.DrawHelpButton(factoryWindowRect.width, ref factoryHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, factoryWindowRect.width - 24f, 84f),
                "FACTORIES & PRODUCTION LINES | Select an active friendly factory to reconfigure its production line. Selected factories will manufacture the chosen vehicle type in their standard production cycle.");
        }
        if (CommanderUiTheme.DrawCloseButton(factoryWindowRect.width))
        {
            factoryWindowVisible = false;
            return;
        }

        float y = factoryHelpVisible ? 128f : 38f;
        CommanderFactoryProductionService? factorySvc = CommanderFactoryProductionService.Instance;
        if (factorySvc == null || factorySvc.FriendlyFactories.Count == 0)
        {
            GUI.Label(new Rect(16f, y, factoryWindowRect.width - 32f, 30f), "No active friendly vehicle factories found on this map.", CommanderUiTheme.Label);
            GUI.DragWindow(new Rect(0f, 0f, factoryWindowRect.width - 72f, 28f));
            return;
        }

        // Factory Carousel Selector
        IReadOnlyList<Factory> factories = factorySvc.FriendlyFactories;
        int factoryCount = factories.Count;
        Factory? currentFactory = factorySvc.SelectedFactory;

        Rect selectorBox = new(12f, y, factoryWindowRect.width - 24f, 40f);
        GUI.Box(selectorBox, string.Empty, CommanderUiTheme.Panel);
        CommanderUiTheme.DrawMutedFrame(selectorBox);

        if (GUI.Button(new Rect(selectorBox.x + 4f, selectorBox.y + 4f, 74f, 32f), "◀ PREV", CommanderUiTheme.Button))
        {
            int prevIdx = (factorySvc.SelectedFactoryIndex - 1 + factoryCount) % factoryCount;
            factorySvc.SelectFactory(prevIdx);
            JumpToFactory(factorySvc.SelectedFactory);
        }

        string curName = currentFactory?.attachedUnit != null ? currentFactory.attachedUnit.unitName : ("Factory " + (factorySvc.SelectedFactoryIndex + 1));
        string headerLabel = $"FACTORY ({factorySvc.SelectedFactoryIndex + 1} / {factoryCount}): {curName.ToUpperInvariant()}";
        GUI.Label(new Rect(selectorBox.x + 84f, selectorBox.y + 8f, selectorBox.width - 250f, 24f), headerLabel, CommanderUiTheme.Header);

        if (GUI.Button(new Rect(selectorBox.xMax - 162f, selectorBox.y + 4f, 78f, 32f), "JUMP TO", CommanderUiTheme.PrimaryButton))
        {
            JumpToFactory(currentFactory);
        }

        if (GUI.Button(new Rect(selectorBox.xMax - 80f, selectorBox.y + 4f, 76f, 32f), "NEXT ▶", CommanderUiTheme.Button))
        {
            int nextIdx = (factorySvc.SelectedFactoryIndex + 1) % factoryCount;
            factorySvc.SelectFactory(nextIdx);
            JumpToFactory(factorySvc.SelectedFactory);
        }
        y += 48f;

        if (currentFactory == null || currentFactory.attachedUnit == null)
        {
            GUI.DragWindow(new Rect(0f, 0f, factoryWindowRect.width - 72f, 28f));
            return;
        }

        // Status Card
        Rect statusBox = new(12f, y, factoryWindowRect.width - 24f, 70f);
        GUI.Box(statusBox, string.Empty, CommanderUiTheme.Panel);
        CommanderUiTheme.DrawMutedFrame(statusBox);

        string currentProd = currentFactory.ProductionUnit != null ? currentFactory.ProductionUnit.unitName : "None";
        float progress = factorySvc.GetProductionProgress(currentFactory);
        float remainingSec = Mathf.Max(0f, currentFactory.productionInterval * (1f - progress));

        GUI.Label(new Rect(statusBox.x + 12f, statusBox.y + 8f, statusBox.width - 24f, 22f),
            $"ACTIVE LINE: {currentProd.ToUpperInvariant()}   |   CYCLE: {currentFactory.productionInterval:0}s   |   ETA: {remainingSec:0}s", CommanderUiTheme.Header);

        Rect progBg = new(statusBox.x + 12f, statusBox.y + 36f, statusBox.width - 24f, 18f);
        GUI.Box(progBg, string.Empty, CommanderUiTheme.Card);
        Color prevCol = GUI.color;
        GUI.color = CommanderUiTheme.Accent;
        GUI.DrawTexture(new Rect(progBg.x, progBg.y, progBg.width * progress, progBg.height), Texture2D.whiteTexture);
        GUI.color = prevCol;
        GUI.Label(new Rect(progBg.x + 8f, progBg.y, progBg.width - 16f, progBg.height), $"{Mathf.RoundToInt(progress * 100f)}%", CommanderUiTheme.Label);

        y += 78f;

        GUI.Label(new Rect(12f, y, factoryWindowRect.width - 24f, 20f), "REASSIGN PRODUCTION LINE (VEHICLES):", CommanderUiTheme.SubHeader);
        y += 24f;

        // Vehicle Grid
        IReadOnlyList<VehicleDefinition> availableDefs = factorySvc.AvailableVehicleDefinitions;
        Rect scrollRect = new(12f, y, factoryWindowRect.width - 24f, factoryWindowRect.height - y - 14f);
        Rect innerRect = new(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, availableDefs.Count * 48f + 8f));

        factoryScroll = GUI.BeginScrollView(scrollRect, factoryScroll, innerRect);
        for (int v = 0; v < availableDefs.Count; v++)
        {
            VehicleDefinition def = availableDefs[v];
            Rect row = new(4f, 4f + v * 48f, innerRect.width - 8f, 44f);
            GUI.Box(row, string.Empty, CommanderUiTheme.Card);

            bool isCurrent = ReferenceEquals(currentFactory.ProductionUnit, def);
            if (isCurrent)
            {
                CommanderUiTheme.DrawFrame(row, 1.5f);
            }

            string catLabel = CommanderGameAccess.GetVehicleCategoryLabel(def);
            string costStr = UnitConverter.ValueReading(def.value) ?? ("$" + def.value.ToString("N0"));
            GUI.Label(new Rect(row.x + 12f, row.y + 4f, row.width - 170f, 20f), def.unitName, CommanderUiTheme.Label);
            GUI.Label(new Rect(row.x + 12f, row.y + 22f, row.width - 170f, 18f), $"CATEGORY: {catLabel.ToUpperInvariant()}   |   VALUE: {costStr}", CommanderUiTheme.MutedLabel);

            GUI.enabled = !isCurrent;
            if (GUI.Button(new Rect(row.xMax - 140f, row.y + 6f, 130f, 32f), isCurrent ? "PRODUCING" : "SET LINE", isCurrent ? CommanderUiTheme.SelectedButton : CommanderUiTheme.PrimaryButton))
            {
                factorySvc.SetProductionUnit(currentFactory, def);
            }
            GUI.enabled = true;
        }
        GUI.EndScrollView();

        GUI.DragWindow(new Rect(0f, 0f, factoryWindowRect.width - 72f, 28f));
    }

    private void JumpToFactory(Factory? factory)
    {
        if (factory?.attachedUnit != null && !factory.attachedUnit.disabled)
        {
            selectionService.SelectUnit(factory.attachedUnit, additive: false);
            CommanderTacticalMapService.Instance?.JumpCameraToPosition(factory.attachedUnit.transform.GlobalPosition());
        }
    }

    private void DrawBuildingEconomyWindow(int windowId)
    {
        CommanderUiTheme.DrawHeaderStripe(new Rect(0f, 0f, buildingWindowRect.width, buildingWindowRect.height));
        CommanderUiTheme.DrawMutedFrame(new Rect(0f, 0f, buildingWindowRect.width, buildingWindowRect.height));
        if (CommanderUiTheme.DrawHelpButton(buildingWindowRect.width, ref buildingHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, buildingWindowRect.width - 24f, 84f),
                "BUILDINGS & ECONOMY | Manage faction income and infrastructure assets. Invest in Economic Capital Projects or upgrade facilities to accelerate passive income per minute. Monitor sector control and jump directly to any asset.");
        }
        if (CommanderUiTheme.DrawCloseButton(buildingWindowRect.width))
        {
            buildingWindowVisible = false;
            return;
        }

        float y = buildingHelpVisible ? 128f : 38f;
        CommanderBuildingEconomyService? bldSvc = CommanderBuildingEconomyService.Instance;
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        if (bldSvc == null)
        {
            GUI.DragWindow(new Rect(0f, 0f, buildingWindowRect.width - 72f, 28f));
            return;
        }

        // Financial & Sector Metrics Card
        Rect statsCard = new(12f, y, buildingWindowRect.width - 24f, 54f);
        GUI.Box(statsCard, string.Empty, CommanderUiTheme.Panel);
        CommanderUiTheme.DrawMutedFrame(statsCard);

        float fourth = statsCard.width / 4f;
        GUI.Label(new Rect(statsCard.x + 8f, statsCard.y + 6f, fourth - 12f, 18f), "TOTAL INCOME", CommanderUiTheme.MutedLabel);
        string incomeStr = UnitConverter.ValueReading(bldSvc.EstimatedIncomePerMinute) ?? ("$" + bldSvc.EstimatedIncomePerMinute.ToString("N0"));
        GUI.Label(new Rect(statsCard.x + 8f, statsCard.y + 24f, fourth - 12f, 22f), "+" + incomeStr + " / min", CommanderUiTheme.Header);

        GUI.Label(new Rect(statsCard.x + fourth + 8f, statsCard.y + 6f, fourth - 12f, 18f), "PROJECTS BOOST", CommanderUiTheme.MutedLabel);
        string projStr = UnitConverter.ValueReading(bldSvc.ProjectsIncomePerMinute) ?? ("$" + bldSvc.ProjectsIncomePerMinute.ToString("N0"));
        GUI.Label(new Rect(statsCard.x + fourth + 8f, statsCard.y + 24f, fourth - 12f, 22f), "+" + projStr + " / min", CommanderUiTheme.Header);

        GUI.Label(new Rect(statsCard.x + fourth * 2f + 8f, statsCard.y + 6f, fourth - 12f, 18f), "FACILITY BOOST", CommanderUiTheme.MutedLabel);
        string facStr = UnitConverter.ValueReading(bldSvc.FacilityUpgradesIncomePerMinute) ?? ("$" + bldSvc.FacilityUpgradesIncomePerMinute.ToString("N0"));
        GUI.Label(new Rect(statsCard.x + fourth * 2f + 8f, statsCard.y + 24f, fourth - 12f, 22f), "+" + facStr + " / min", CommanderUiTheme.Header);

        GUI.Label(new Rect(statsCard.x + fourth * 3f + 8f, statsCard.y + 6f, fourth - 12f, 18f), "SECTORS", CommanderUiTheme.MutedLabel);
        GUI.Label(new Rect(statsCard.x + fourth * 3f + 8f, statsCard.y + 24f, fourth - 12f, 22f), bldSvc.ControlledSectorsCount + " / " + bldSvc.TotalSectorsCount, CommanderUiTheme.Header);

        y += 62f;

        // Main Navigation (Assets vs Capital Investment Projects)
        float mainTabW = (buildingWindowRect.width - 28f) * 0.5f;
        if (GUI.Button(new Rect(12f, y, mainTabW, 32f), "🏢 INFRASTRUCTURE ASSETS (" + bldSvc.AllEntries.Count + ")", buildingMainTab == 0 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            buildingMainTab = 0;
        }
        if (GUI.Button(new Rect(16f + mainTabW, y, mainTabW, 32f), "📈 ECONOMIC CAPITAL PROJECTS", buildingMainTab == 1 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            buildingMainTab = 1;
        }
        y += 38f;

        if (buildingMainTab == 1)
        {
            // ECONOMIC CAPITAL PROJECTS TAB
            IReadOnlyList<CommanderBuildingEconomyService.EconomicProject> projects = bldSvc.Projects;
            Rect scrollRect = new(12f, y, buildingWindowRect.width - 24f, buildingWindowRect.height - y - 14f);
            Rect innerRect = new(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, projects.Count * 96f + 8f));

            projectScroll = GUI.BeginScrollView(scrollRect, projectScroll, innerRect);
            for (int p = 0; p < projects.Count; p++)
            {
                CommanderBuildingEconomyService.EconomicProject proj = projects[p];
                Rect row = new(4f, 4f + p * 96f, innerRect.width - 8f, 90f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Card);
                CommanderUiTheme.DrawMutedFrame(row);

                bool isMax = proj.CurrentLevel >= proj.MaxLevel;
                string costStr = UnitConverter.ValueReading(proj.CurrentCost) ?? ("$" + proj.CurrentCost.ToString("N0"));
                string yieldStr = UnitConverter.ValueReading(proj.IncomeBoostPerMin) ?? ("$" + proj.IncomeBoostPerMin.ToString("N0"));
                string totalYield = UnitConverter.ValueReading(proj.CurrentLevel * proj.IncomeBoostPerMin) ?? ("$" + (proj.CurrentLevel * proj.IncomeBoostPerMin).ToString("N0"));

                GUI.Label(new Rect(row.x + 12f, row.y + 8f, row.width - 220f, 22f), $"{proj.Title}  (LEVEL {proj.CurrentLevel}/{proj.MaxLevel})", CommanderUiTheme.Header);
                GUI.Label(new Rect(row.x + 12f, row.y + 32f, row.width - 220f, 32f), proj.Description, CommanderUiTheme.MutedLabel);
                GUI.Label(new Rect(row.x + 12f, row.y + 64f, row.width - 220f, 20f), $"ACTIVE YIELD: +{totalYield}/min   |   NEXT BOOST: +{yieldStr}/min", CommanderUiTheme.SubHeader);

                string btnText = isMax ? "MAX LEVEL" : $"INVEST {costStr}";
                bool canAfford = !isMax && factionSvc != null && factionSvc.FactionFunds >= proj.CurrentCost;
                bool oldE = GUI.enabled;
                GUI.enabled = oldE && !isMax && canAfford;

                if (GUI.Button(new Rect(row.xMax - 190f, row.y + 24f, 178f, 42f), btnText, isMax ? CommanderUiTheme.SelectedButton : (canAfford ? CommanderUiTheme.PrimaryButton : CommanderUiTheme.Button)))
                {
                    bldSvc.TryInvestInProject(proj.Id, out _);
                }
                GUI.enabled = oldE;
            }
            GUI.EndScrollView();
        }
        else
        {
            // Category Filter Tabs
            string[] tabNames = { "ALL (" + bldSvc.AllEntries.Count + ")", "ECONOMY (" + bldSvc.EconomyEntries.Count + ")", "SPAWNERS (" + bldSvc.MilitaryEntries.Count + ")", "DEFENSE (" + bldSvc.DefenseEntries.Count + ")", "LOGISTICS (" + bldSvc.LogisticsEntries.Count + ")" };
            float tabW = (buildingWindowRect.width - 24f) / tabNames.Length;
            for (int t = 0; t < tabNames.Length; t++)
            {
                if (GUI.Button(new Rect(12f + t * tabW, y, tabW - 4f, 30f), tabNames[t],
                    buildingCategoryTab == t ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
                {
                    buildingCategoryTab = t;
                    buildingScroll = Vector2.zero;
                }
            }
            y += 36f;

            // Search Bar
            GUI.Label(new Rect(14f, y + 4f, 70f, 22f), "SEARCH:", CommanderUiTheme.MutedLabel);
            buildingSearchFilter = GUI.TextField(new Rect(86f, y, buildingWindowRect.width - 100f, 26f), buildingSearchFilter, CommanderUiTheme.Panel);
            y += 34f;

            // Building List ScrollView
            IReadOnlyList<CommanderBuildingEconomyService.BuildingEntry> entries = bldSvc.GetEntriesByCategory(
                (CommanderBuildingEconomyService.BuildingCategory)buildingCategoryTab,
                buildingSearchFilter);

            Rect scrollRect = new(12f, y, buildingWindowRect.width - 24f, buildingWindowRect.height - y - 14f);
            Rect innerRect = new(0f, 0f, scrollRect.width - 20f, Mathf.Max(scrollRect.height, entries.Count * 54f + 8f));

            buildingScroll = GUI.BeginScrollView(scrollRect, buildingScroll, innerRect);
            for (int i = 0; i < entries.Count; i++)
            {
                CommanderBuildingEconomyService.BuildingEntry entry = entries[i];
                Rect row = new(4f, 4f + i * 54f, innerRect.width - 8f, 48f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Card);

                Color badgeCol = entry.Category switch
                {
                    CommanderBuildingEconomyService.BuildingCategory.Economy => new Color(0.95f, 0.8f, 0.2f, 0.95f),
                    CommanderBuildingEconomyService.BuildingCategory.MilitarySpawning => new Color(0.2f, 0.85f, 1f, 0.95f),
                    CommanderBuildingEconomyService.BuildingCategory.Defense => new Color(1f, 0.35f, 0.2f, 0.95f),
                    CommanderBuildingEconomyService.BuildingCategory.Logistics => new Color(0.3f, 0.95f, 0.5f, 0.95f),
                    _ => Color.white
                };

                string lvlTag = entry.UpgradeLevel > 1 ? $" [LV.{entry.UpgradeLevel}]" : string.Empty;
                GUI.Label(new Rect(row.x + 12f, row.y + 4f, row.width - 240f, 20f), entry.Name + lvlTag, CommanderUiTheme.Label);
                Color prev = GUI.color;
                GUI.color = badgeCol;
                GUI.Label(new Rect(row.x + 12f, row.y + 24f, row.width - 240f, 18f), "[" + entry.RoleLabel + "]   " + (entry.IsOperational ? "OPERATIONAL" : "OFFLINE / DAMAGED"), CommanderUiTheme.MutedLabel);
                GUI.color = prev;

                // Facility Upgrade Button
                if (entry.Building != null && entry.Category == CommanderBuildingEconomyService.BuildingCategory.Economy)
                {
                    bool isMaxLvl = entry.UpgradeLevel >= 3;
                    float upCost = 60000f * entry.UpgradeLevel;
                    string upCostStr = UnitConverter.ValueReading(upCost) ?? ("$" + upCost.ToString("N0"));
                    string upLabel = isMaxLvl ? "MAX LVL" : $"UPGRADE {upCostStr}";
                    bool canUp = !isMaxLvl && factionSvc != null && factionSvc.FactionFunds >= upCost;

                    bool oldE = GUI.enabled;
                    GUI.enabled = oldE && canUp;
                    if (GUI.Button(new Rect(row.xMax - 220f, row.y + 8f, 110f, 32f), upLabel, canUp ? CommanderUiTheme.PrimaryButton : CommanderUiTheme.Button))
                    {
                        bldSvc.TryUpgradeBuildingFacility(entry.Building, out _);
                    }
                    GUI.enabled = oldE;
                }

                // Jump To Button
                if (GUI.Button(new Rect(row.xMax - 104f, row.y + 8f, 96f, 32f), "JUMP TO", CommanderUiTheme.Button))
                {
                    if (entry.AttachedUnit != null && !entry.AttachedUnit.disabled)
                    {
                        selectionService.SelectUnit(entry.AttachedUnit, additive: false);
                        CommanderTacticalMapService.Instance?.JumpCameraToPosition(entry.AttachedUnit.transform.GlobalPosition());
                    }
                    else
                    {
                        CommanderTacticalMapService.Instance?.JumpCameraToPosition(entry.WorldPosition.ToGlobalPosition());
                    }
                }
            }
            GUI.EndScrollView();
        }

        GUI.DragWindow(new Rect(0f, 0f, buildingWindowRect.width - 72f, 28f));
    }

    private void DrawCheatWindow(int windowId)
    {
        CommanderUiTheme.DrawHeaderStripe(new Rect(0f, 0f, cheatWindowRect.width, cheatWindowRect.height));
        CommanderUiTheme.DrawMutedFrame(new Rect(0f, 0f, cheatWindowRect.width, cheatWindowRect.height));
        CommanderCheatService? cheats = CommanderCheatService.Instance;
        if (CommanderUiTheme.DrawHelpButton(cheatWindowRect.width, ref cheatHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, cheatWindowRect.width - 24f, 84f),
                "SANDBOX & CHEAT MENU | Spawn any vehicle, warship, aircraft, or building directly in the 3D world. Manage economy, enable God Mode, repair/restock selection, or reveal all enemy radar positions.");
        }
        if (CommanderUiTheme.DrawCloseButton(cheatWindowRect.width))
        {
            cheatWindowVisible = false;
            cheats?.CancelPlacement();
            return;
        }

        float y = cheatHelpVisible ? 128f : 38f;

        // Status Header
        if (cheats != null && !string.IsNullOrEmpty(cheats.StatusText))
        {
            GUI.Box(new Rect(12f, y, cheatWindowRect.width - 24f, 28f), string.Empty, CommanderUiTheme.Panel);
            GUI.Label(new Rect(20f, y + 2f, cheatWindowRect.width - 40f, 24f), cheats.StatusText, CommanderUiTheme.Header);
            y += 34f;
        }

        // Tab Navigation
        float tabWidth = (cheatWindowRect.width - 42f) / 4f;
        if (GUI.Button(new Rect(12f, y, tabWidth, 32f), "SPAWN ENTITIES", cheatTab == 0 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            cheatTab = 0;
            cheatScroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(16f + tabWidth, y, tabWidth, 32f), "ECONOMY", cheatTab == 1 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            cheatTab = 1;
            cheatScroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(20f + tabWidth * 2f, y, tabWidth, 32f), "COMBAT & GOD", cheatTab == 2 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            cheatTab = 2;
            cheatScroll = Vector2.zero;
        }
        if (GUI.Button(new Rect(24f + tabWidth * 3f, y, tabWidth, 32f), "MAP & VISION", cheatTab == 3 ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            cheatTab = 3;
            cheatScroll = Vector2.zero;
        }
        y += 40f;

        Rect view = new(12f, y, cheatWindowRect.width - 24f, cheatWindowRect.height - y - 14f);

        if (cheatTab == 0)
        {
            // TAB 0: SPAWN UNITS & BUILDINGS
            float catWidth = (view.width - 32f) / 5f;
            string[] catNames = { "ALL", "BUILDINGS", "LAND", "AIR", "NAVAL" };
            for (int c = 0; c < 5; c++)
            {
                if (GUI.Button(new Rect(view.x + c * (catWidth + 6f), view.y, catWidth, 26f), catNames[c],
                    cheatCategoryIndex == c ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
                {
                    cheatCategoryIndex = c;
                    cheatScroll = Vector2.zero;
                }
            }

            // Search Bar & Faction Toggle Row
            float searchY = view.y + 32f;
            GUI.Label(new Rect(view.x, searchY + 4f, 60f, 22f), "SEARCH:", CommanderUiTheme.MutedLabel);
            cheatSearchFilter = GUI.TextField(new Rect(view.x + 65f, searchY + 2f, view.width - 260f, 24f), cheatSearchFilter, CommanderUiTheme.Panel);

            // Faction Spawn toggle
            cheatSpawnAsEnemy = GUI.Toggle(new Rect(view.xMax - 180f, searchY + 2f, 180f, 24f), cheatSpawnAsEnemy,
                cheatSpawnAsEnemy ? "SPAWN AS: ENEMY" : "SPAWN AS: FRIENDLY", CommanderUiTheme.Toggle);

            // List of Unit & Building Definitions
            Rect listRect = new(view.x, searchY + 32f, view.width, view.height - 66f);
            if (cheats != null)
            {
                IReadOnlyList<UnitDefinition> definitions = cheats.GetDefinitionsByCategory(cheatCategoryIndex, cheatSearchFilter);
                Rect inner = new(0f, 0f, listRect.width - 20f, Mathf.Max(listRect.height, definitions.Count * 46f + 6f));
                cheatScroll = GUI.BeginScrollView(listRect, cheatScroll, inner);

                for (int i = 0; i < definitions.Count; i++)
                {
                    UnitDefinition def = definitions[i];
                    Rect row = new(4f, 3f + i * 46f, inner.width - 8f, 42f);
                    GUI.Box(row, string.Empty, CommanderUiTheme.Panel);

                    string typeStr = CommanderCheatService.GetCategoryLabel(def);

                    GUI.Label(new Rect(row.x + 10f, row.y + 3f, row.width - 170f, 20f), def.unitName, CommanderUiTheme.Label);
                    GUI.Label(new Rect(row.x + 10f, row.y + 21f, row.width - 170f, 18f), "TYPE: " + typeStr, CommanderUiTheme.MutedLabel);

                    if (GUI.Button(new Rect(row.xMax - 150f, row.y + 6f, 140f, 30f), "PLACE IN 3D", CommanderUiTheme.PrimaryButton))
                    {
                        cheats.BeginPlacement(def, cheatSpawnAsEnemy);
                    }
                }

                GUI.EndScrollView();

                if (definitions.Count == 0)
                {
                    GUI.Label(listRect, "No matching unit or building definitions found.", CommanderUiTheme.MutedLabel);
                }
            }
        }
        else if (cheatTab == 1)
        {
            // TAB 1: ECONOMY
            GUI.Box(view, string.Empty, CommanderUiTheme.Panel);
            float itemY = view.y + 16f;

            GUI.Label(new Rect(view.x + 16f, itemY, view.width - 32f, 24f), "FACTION TREASURY CHEATS", CommanderUiTheme.Header);
            itemY += 32f;

            float btnW = (view.width - 48f) / 3f;
            if (GUI.Button(new Rect(view.x + 16f, itemY, btnW, 36f), "+ $100,000", CommanderUiTheme.Button)) cheats?.AddFunds(100000f);
            if (GUI.Button(new Rect(view.x + 24f + btnW, itemY, btnW, 36f), "+ $1,000,000", CommanderUiTheme.Button)) cheats?.AddFunds(1000000f);
            if (GUI.Button(new Rect(view.x + 32f + btnW * 2f, itemY, btnW, 36f), "+ $10,000,000", CommanderUiTheme.PrimaryButton)) cheats?.SetMaxFunds();
            itemY += 50f;

            if (GUI.Button(new Rect(view.x + 16f, itemY, view.width - 32f, 36f), "SET MAX FUNDS ($10,000,000)", CommanderUiTheme.SelectedButton))
            {
                cheats?.SetMaxFunds();
            }
        }
        else if (cheatTab == 2)
        {
            // TAB 2: COMBAT & GODMODE
            GUI.Box(view, string.Empty, CommanderUiTheme.Panel);
            float itemY = view.y + 16f;

            GUI.Label(new Rect(view.x + 16f, itemY, view.width - 32f, 24f), "INVULNERABILITY & SELECTION CHEATS", CommanderUiTheme.Header);
            itemY += 32f;

            if (cheats != null)
            {
                cheats.GodModeEnabled = GUI.Toggle(
                    new Rect(view.x + 16f, itemY, view.width - 32f, 28f),
                    cheats.GodModeEnabled,
                    "GOD MODE (All Friendly Units & Buildings Invulnerable)",
                    CommanderUiTheme.Toggle);
            }
            itemY += 38f;

            float halfW = (view.width - 40f) * 0.5f;
            if (GUI.Button(new Rect(view.x + 16f, itemY, halfW, 36f), "HEAL SELECTION (100% HP)", CommanderUiTheme.Button)) cheats?.HealSelection();
            if (GUI.Button(new Rect(view.x + 24f + halfW, itemY, halfW, 36f), "RESTOCK AMMO SELECTION", CommanderUiTheme.Button)) cheats?.RestockAmmoSelection();
            itemY += 46f;

            if (GUI.Button(new Rect(view.x + 16f, itemY, view.width - 32f, 36f), "DESTROY / KILL TARGET (Instant Elimination)", CommanderUiTheme.DangerButton))
            {
                cheats?.DestroySelection();
            }
        }
        else
        {
            // TAB 3: MAP & VISION
            GUI.Box(view, string.Empty, CommanderUiTheme.Panel);
            float itemY = view.y + 16f;

            GUI.Label(new Rect(view.x + 16f, itemY, view.width - 32f, 24f), "TACTICAL VISION & RADAR REVEAL", CommanderUiTheme.Header);
            itemY += 32f;

            if (GUI.Button(new Rect(view.x + 16f, itemY, view.width - 32f, 38f), "REVEAL ALL ENEMY UNITS (Full Radar Vision)", CommanderUiTheme.PrimaryButton))
            {
                cheats?.RevealAllUnits();
            }
        }

        GUI.DragWindow(new Rect(0f, 0f, cheatWindowRect.width - 72f, 28f));
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
            "rot_left" => CommanderSettings.CameraRotateLeft,
            "rot_right" => CommanderSettings.CameraRotateRight,
            "pitch_up" => CommanderSettings.CameraPitchUp,
            "pitch_down" => CommanderSettings.CameraPitchDown,
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
            case "rot_left": CommanderSettings.CameraRotateLeft = shortcut; break;
            case "rot_right": CommanderSettings.CameraRotateRight = shortcut; break;
            case "pitch_up": CommanderSettings.CameraPitchUp = shortcut; break;
            case "pitch_down": CommanderSettings.CameraPitchDown = shortcut; break;
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
        CommanderSettings.CameraRotateLeft = new KeyboardShortcut(KeyCode.LeftArrow);
        CommanderSettings.CameraRotateRight = new KeyboardShortcut(KeyCode.RightArrow);
        CommanderSettings.CameraPitchUp = new KeyboardShortcut(KeyCode.UpArrow);
        CommanderSettings.CameraPitchDown = new KeyboardShortcut(KeyCode.DownArrow);
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
        CommanderUiTheme.DrawHeaderStripe(selectionBarRect);
        CommanderUiTheme.DrawMutedFrame(selectionBarRect);

        Unit? focused = selectionService.FocusedSelection;
        bool advanced = CommanderFeatureGate.AdvancedFeaturesEnabled;
        bool oldEnabled = GUI.enabled;

        float buttonY = selectionBarRect.y + (count == 1 ? 54f : 28f);

        if (count == 1 && focused != null && !focused.disabled)
        {
            // 1. Single Unit Header & Telemetry Combined
            string catLabel = focused.definition != null
                ? CommanderCheatService.GetCategoryLabel(focused.definition)
                : (focused is Aircraft ? "AIR" : (focused is Ship ? "NAVAL" : "LAND"));

            float hpPct = 1f;
            IRepairable[] rep = focused.GetComponentsInChildren<IRepairable>(true);
            if (rep.Length > 0)
            {
                int damaged = 0;
                for (int r = 0; r < rep.Length; r++)
                {
                    if (rep[r] != null && rep[r].NeedsRepair()) damaged++;
                }
                hpPct = Mathf.Clamp01(1f - ((float)damaged / rep.Length));
            }

            string statusTag = hpPct <= 0.35f
                ? "CRITICAL"
                : (hpPct < 0.85f ? "DAMAGED" : "READY");
            Color statusCol = hpPct <= 0.35f
                ? new Color(1f, 0.25f, 0.2f, 0.95f)
                : (hpPct < 0.85f ? new Color(0.95f, 0.78f, 0.15f, 0.95f) : new Color(0.2f, 0.85f, 0.35f, 0.95f));

            float speedKmh = focused.rb != null ? focused.rb.velocity.magnitude * 3.6f : 0f;
            int heading = Mathf.RoundToInt(focused.transform.eulerAngles.y) % 360;
            float altM = focused.transform.position.y;
            if (focused is Aircraft air) altM = air.radarAlt;
            int fuelPct = focused is Aircraft ac ? Mathf.RoundToInt(ac.GetFuelLevel() * 100f) : 100;
            bool isHold = CommanderStanceService.Instance?.IsHoldFire(focused) == true;

            string compassDir = heading >= 337 || heading < 23 ? "N"
                : (heading < 68 ? "NE"
                : (heading < 113 ? "E"
                : (heading < 158 ? "SE"
                : (heading < 203 ? "S"
                : (heading < 248 ? "SW"
                : (heading < 293 ? "W" : "NW"))))));

            string line1 = $"[{catLabel}] {focused.unitName.ToUpperInvariant()}   •   SPD: {Mathf.RoundToInt(speedKmh)} km/h   •   HDG: {heading:000}° ({compassDir})   •   ALT: {Mathf.RoundToInt(altM)}m   •   FUEL: {fuelPct}%   •   STANCE: {(isHold ? "HOLD" : "FREE")}";
            GUI.Label(new Rect(selectionBarRect.x + 12f, selectionBarRect.y + 4f, selectionBarRect.width - 160f, 20f), line1, CommanderUiTheme.Header);

            Color prev = GUI.color;
            GUI.color = statusCol;
            GUI.Label(new Rect(selectionBarRect.xMax - 140f, selectionBarRect.y + 4f, 100f, 20f), $"[{statusTag}]", CommanderUiTheme.Header);
            GUI.color = prev;

            // 2. Armament Breakdown
            List<string> weaponSummaries = new();
            if (focused.weaponStations != null)
            {
                for (int s = 0; s < focused.weaponStations.Count; s++)
                {
                    WeaponStation st = focused.weaponStations[s];
                    if (st?.Weapons == null) continue;
                    for (int w = 0; w < st.Weapons.Count; w++)
                    {
                        Weapon wp = st.Weapons[w];
                        if (wp != null && !string.IsNullOrWhiteSpace(wp.name))
                        {
                            weaponSummaries.Add(wp.name + " [" + wp.ammo + "/" + wp.GetFullAmmo() + "]");
                        }
                    }
                }
            }

            string wpText = weaponSummaries.Count > 0
                ? "ARMAMENT:  " + string.Join("   •   ", weaponSummaries)
                : "ARMAMENT:  UNARMED LOGISTICS / SUPPORT PLATFORM";

            GUI.Label(new Rect(selectionBarRect.x + 12f, selectionBarRect.y + 26f, selectionBarRect.width - 50f, 22f), wpText, CommanderUiTheme.Label);
        }
        else
        {
            // Multi-Unit Header
            string formName = moveService.CurrentFormation.ToString().ToUpperInvariant();
            GUI.Label(new Rect(selectionBarRect.x + 12f, selectionBarRect.y + 4f, selectionBarRect.width - 240f, 20f),
                $"{count} UNITS SELECTED (TACTICAL BATTLE GROUP)", CommanderUiTheme.Header);

            GUI.Label(new Rect(selectionBarRect.xMax - 220f, selectionBarRect.y + 4f, 180f, 20f),
                $"[FORM: {formName}]", CommanderUiTheme.SubHeader);
        }

        if (GUI.Button(new Rect(selectionBarRect.xMax - 30f, selectionBarRect.y + 3f, 22f, 20f), "?", CommanderUiTheme.HelpButton))
        {
            selectionHelpVisible = !selectionHelpVisible;
        }

        if (selectionHelpVisible)
        {
            CommanderUiTheme.DrawHelpOverlay(selectionHelpRect,
                "STOP holds units. AI returns to basegame. A-MOVE ('T') moves & engages targets. PATROL ('P') loops waypoints. GUARD ('G') escorts target. SCATTER ('X') evades area damage. FORM ('V') cycles military formations. RTB auto-returns for repair/rearm. STANCE ('F') toggles Hold Fire. PIN stores selection.");
        }

        // 3. Command Button Grid (Row of 11 Aligned Tactical Buttons)
        float totalWidth = selectionBarRect.width - 24f;
        float btnWidth = (totalWidth - 10f * 5f) / 11f;
        float bx = selectionBarRect.x + 12f;

        // 1. STOP
        GUI.enabled = oldEnabled && moveService.HasCommandableSelection;
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), "STOP", CommanderUiTheme.DangerButton))
        {
            moveService.StopSelectedUnits();
        }
        bx += btnWidth + 5f;

        // 2. AI
        GUI.enabled = oldEnabled && advanced && moveService.HasCommandableSelection;
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), "AI", CommanderUiTheme.PrimaryButton))
        {
            moveService.ResumeAiForSelectedUnits();
        }
        bx += btnWidth + 5f;

        // 3. A-MOVE
        GUI.enabled = oldEnabled && advanced && moveService.HasCommandableSelection;
        bool isAttackMoving = moveService.AwaitingAttackMoveSelection;
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), "A-MOVE",
            isAttackMoving ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            if (isAttackMoving) moveService.CancelAttackMoveOrder();
            else moveService.BeginAttackMoveOrder();
        }
        bx += btnWidth + 5f;

        // 4. PATROL
        bool isPatrolling = moveService.AwaitingPatrolSelection;
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), "PATROL",
            isPatrolling ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            if (isPatrolling) moveService.CancelPatrolOrder();
            else moveService.BeginPatrolOrder();
        }
        bx += btnWidth + 5f;

        // 5. GUARD / ESCORT
        bool isGuarding = moveService.AwaitingGuardSelection;
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), "GUARD",
            isGuarding ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            if (isGuarding) moveService.CancelGuardOrder();
            else moveService.BeginGuardOrder();
        }
        bx += btnWidth + 5f;

        // 6. SCATTER
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), "SCATTER", CommanderUiTheme.Button))
        {
            moveService.ScatterSelectedUnits(55f);
        }
        bx += btnWidth + 5f;

        // 7. FORMATION CYCLE
        string formShort = moveService.CurrentFormation.ToString().ToUpperInvariant();
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), "FORM: " + formShort, CommanderUiTheme.Button))
        {
            moveService.CycleFormation();
        }
        bx += btnWidth + 5f;

        // 8. AUTO-RTB
        bool isRtb = moveService.IsAutoRtb(focused);
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), isRtb ? "RTB ON" : "RTB",
            isRtb ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            moveService.ToggleAutoRtbForSelection();
        }
        bx += btnWidth + 5f;

        // 9. FOB DEPLOY or ROAD ON/OFF
        GUI.enabled = oldEnabled;
        bool canFob = advanced && count == 1 && focused != null && CommanderForwardOutpostService.Instance?.CanDeployFob(focused) == true;
        if (canFob)
        {
            bool isFob = CommanderForwardOutpostService.Instance?.IsFobDeployed(focused) == true;
            if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f),
                isFob ? "PACK FOB" : "DEPLOY FOB",
                isFob ? CommanderUiTheme.SelectedButton : CommanderUiTheme.PrimaryButton))
            {
                CommanderForwardOutpostService.Instance?.ToggleFobForSelection();
            }
        }
        else
        {
            bool canToggleRoad = advanced
                && count == 1
                && focused != null
                && directPathService.CanConfigure(focused)
                && !CommanderMobileEmplacementService.IsReservedHauler(focused)
                && !CommanderSamSiteService.IsReservedConstructionJacknife(focused);
            bool roadEnabled = !directPathService.IsEnabled(focused);
            GUI.enabled = oldEnabled && canToggleRoad;
            if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f),
                roadEnabled ? "ROAD ON" : "ROAD OFF",
                roadEnabled ? CommanderUiTheme.Button : CommanderUiTheme.DangerButton))
            {
                directPathService.ToggleFocusedUnit();
            }
        }
        bx += btnWidth + 5f;

        // 10. STANCE (HOLD / FREE FIRE)
        GUI.enabled = oldEnabled;
        bool isHoldFire = CommanderStanceService.Instance?.IsHoldFire(focused) == true;
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f),
            isHoldFire ? "HOLD FIRE" : "FREE FIRE",
            isHoldFire ? CommanderUiTheme.DangerButton : CommanderUiTheme.Button))
        {
            CommanderStanceService.Instance?.ToggleHoldFireForSelection();
        }
        bx += btnWidth + 5f;

        // 11. PIN / DEL
        GUI.enabled = oldEnabled;
        bool deleteMode = CommanderSettings.DeleteUnitModifier.IsPressed();
        string pinLabel = deleteMode ? "DEL" : (selectionService.IsCurrentSelectionPinned ? "UNPIN" : "PIN");
        GUI.enabled = oldEnabled
            && advanced
            && (!deleteMode || selectionService.CanDeleteSelection);
        if (GUI.Button(new Rect(bx, buttonY, btnWidth, 32f), pinLabel,
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
    private void DrawMoneyWindow(int windowId)
    {
        CommanderBuildingEconomyService? bldSvc = CommanderBuildingEconomyService.Instance;
        string fundsStr = spawnService.GetFactionFundsLabel();
        string incStr = bldSvc != null && bldSvc.EstimatedIncomePerMinute > 0f
            ? (" (+" + (UnitConverter.ValueReading(bldSvc.EstimatedIncomePerMinute) ?? ("$" + bldSvc.EstimatedIncomePerMinute.ToString("N0"))) + "/m)")
            : string.Empty;

        GUI.Box(new Rect(0f, 0f, moneyRect.width, moneyRect.height), string.Empty, CommanderUiTheme.Panel);
        CommanderUiTheme.DrawFrame(new Rect(0f, 0f, moneyRect.width, moneyRect.height), 1f);

        string fullText = $"FUNDS: {fundsStr}{incStr}";
        GUI.Label(new Rect(8f, 0f, moneyRect.width - 16f, moneyRect.height), fullText, CommanderUiTheme.Header);
        GUI.DragWindow(new Rect(0f, 0f, moneyRect.width, moneyRect.height));
    }
    private void DrawModernLeftDock()
    {
        bool advanced = CommanderFeatureGate.AdvancedFeaturesEnabled;
        float centerY = CommanderUiScale.Height * 0.5f;
        float dockY = Mathf.Max(50f, centerY - 210f);
        launcherRect = new Rect(10f, dockY, 52f, 410f);

        GUI.Box(launcherRect, string.Empty, CommanderUiTheme.Panel);
        CommanderUiTheme.DrawMutedFrame(launcherRect);

        float by = dockY + 4f;
        float bw = 44f;
        float bh = 36f;
        float bx = 14f;

        // 1. Toggle Drawer
        if (GUI.Button(new Rect(bx, by, bw, bh), panelVisible ? "CMD <" : "CMD >", panelVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.PrimaryButton))
        {
            panelVisible = !panelVisible;
        }
        by += bh + 4f;

        // 2. Select Nearest Depot
        GUI.enabled = advanced;
        if (GUI.Button(new Rect(bx, by, bw, bh), "DEP", CommanderUiTheme.Button))
        {
            spawnService.SelectNearestDepot();
        }
        by += bh + 4f;

        // 3. Factory Carousel
        if (GUI.Button(new Rect(bx, by, bw, bh), "FACT", factoryWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            factoryWindowVisible = !factoryWindowVisible;
            if (factoryWindowVisible && CommanderFactoryProductionService.Instance?.SelectedFactory != null)
            {
                JumpToFactory(CommanderFactoryProductionService.Instance.SelectedFactory);
            }
        }
        by += bh + 4f;

        // 4. Reserve
        if (GUI.Button(new Rect(bx, by, bw, bh), "RESV", reserveWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            reserveWindowVisible = !reserveWindowVisible;
        }
        by += bh + 4f;

        // 5. Air Command
        if (GUI.Button(new Rect(bx, by, bw, bh), "AIR", airCommandUi.Visible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            if (airCommandUi.Visible) airCommandUi.Hide();
            else { panelVisible = false; airCommandUi.Show(); }
        }
        by += bh + 4f;

        // 6. Naval Fleet
        if (GUI.Button(new Rect(bx, by, bw, bh), "NAVY", navalPurchaseUi.Visible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            navalPurchaseUi.Toggle();
        }
        by += bh + 4f;

        // 7. Buildings & Economy
        if (GUI.Button(new Rect(bx, by, bw, bh), "ECON", buildingWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            buildingWindowVisible = !buildingWindowVisible;
        }
        by += bh + 4f;

        // 8. Army OOB Status
        if (GUI.Button(new Rect(bx, by, bw, bh), "OOB", oobWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            oobWindowVisible = !oobWindowVisible;
        }
        by += bh + 4f;

        // 9. Sandbox / Cheats
        if (GUI.Button(new Rect(bx, by, bw, bh), "SAND", cheatWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.PrimaryButton))
        {
            cheatWindowVisible = !cheatWindowVisible;
        }
        by += bh + 4f;

        // 10. Settings
        GUI.enabled = true;
        if (GUI.Button(new Rect(bx, by, bw, bh), "SET", settingsVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            settingsVisible = !settingsVisible;
            bindingCapture = null;
        }
    }

    private void DrawOobWindow(int windowId)
    {
        CommanderUiTheme.DrawHeaderStripe(new Rect(0f, 0f, oobWindowRect.width, oobWindowRect.height));
        CommanderUiTheme.DrawMutedFrame(new Rect(0f, 0f, oobWindowRect.width, oobWindowRect.height));
        if (CommanderUiTheme.DrawHelpButton(oobWindowRect.width, ref oobHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, oobWindowRect.width - 24f, 68f),
                "ORDER OF BATTLE (OOB) | Real-time breakdown of all active friendly forces, naval vessels, aircraft squadrons, factories, and strategic installations.");
        }
        if (CommanderUiTheme.DrawCloseButton(oobWindowRect.width))
        {
            oobWindowVisible = false;
            return;
        }

        float y = oobHelpVisible ? 112f : 38f;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            GUI.Label(new Rect(16f, y, oobWindowRect.width - 32f, 30f), "No active faction HQ.", CommanderUiTheme.Label);
            GUI.DragWindow(new Rect(0f, 0f, oobWindowRect.width - 72f, 28f));
            return;
        }

        int totalGround = 0;
        int totalTanks = 0;
        int totalIfvs = 0;
        int totalSpaag = 0;
        int totalLogistics = 0;
        int totalAircraft = 0;
        int totalWarships = 0;
        int totalSamSites = 0;
        int totalFactories = 0;

        Unit[] allUnits = UnityEngine.Object.FindObjectsOfType<Unit>();
        for (int i = 0; i < allUnits.Length; i++)
        {
            Unit u = allUnits[i];
            if (u == null || u.disabled || !CommanderGameAccess.IsFriendlyUnit(u, localHq)) continue;

            if (u is Aircraft) totalAircraft++;
            else if (u is Ship) totalWarships++;
            else if (u is GroundVehicle gv && gv.definition is VehicleDefinition vdef)
            {
                totalGround++;
                string cat = CommanderGameAccess.GetVehicleCategoryLabel(vdef);
                if (string.Equals(cat, "Tank", StringComparison.OrdinalIgnoreCase)) totalTanks++;
                else if (string.Equals(cat, "AAA", StringComparison.OrdinalIgnoreCase) || string.Equals(cat, "SAM", StringComparison.OrdinalIgnoreCase)) totalSpaag++;
                else if (string.Equals(cat, "Logistics", StringComparison.OrdinalIgnoreCase) || string.Equals(cat, "Support", StringComparison.OrdinalIgnoreCase)) totalLogistics++;
                else totalIfvs++;
            }
            else if (u.GetComponent<Factory>() != null) totalFactories++;
            else if (CommanderSamSiteCoreRegistry.IsTrackedSiteUnit(u)) totalSamSites++;
        }

        GUI.Box(new Rect(12f, y, oobWindowRect.width - 24f, 40f), string.Empty, CommanderUiTheme.Panel);
        GUI.Label(new Rect(20f, y + 8f, oobWindowRect.width - 40f, 24f),
            $"FACTION FUNDS: {spawnService.GetFactionFundsLabel()}    |    TOTAL ACTIVE ARMY: {totalGround + totalAircraft + totalWarships} UNITS",
            CommanderUiTheme.Header);
        y += 46f;

        bool emcon = CommanderRadarService.Instance?.IsGlobalEmconActive == true;
        if (GUI.Button(new Rect(12f, y, oobWindowRect.width - 24f, 32f),
            emcon ? "GLOBAL EMCON ACTIVE: RADARS SILENCED (CLICK TO RESTORE)" : "GLOBAL RADAR SILENCE: EMCON OFF (CLICK TO SILENCE ALL)",
            emcon ? CommanderUiTheme.DangerButton : CommanderUiTheme.Button))
        {
            CommanderRadarService.Instance?.ToggleGlobalEmcon();
        }
        y += 38f;

        Rect view = new(12f, y, oobWindowRect.width - 24f, oobWindowRect.height - y - 14f);
        Rect inner = new(0f, 0f, view.width - 20f, 380f);
        oobScroll = GUI.BeginScrollView(view, oobScroll, inner);

        float itemY = 4f;

        DrawOobSection(new Rect(4f, itemY, inner.width - 8f, 84f), "GROUND COMBAT FORCES",
            $"Total Ground Units: {totalGround}",
            $"• Main Battle Tanks: {totalTanks}    • IFVs & Light Armor: {totalIfvs}",
            $"• Air Defense (AAA/SAM): {totalSpaag}    • Munitions & Logistics: {totalLogistics}");
        itemY += 90f;

        DrawOobSection(new Rect(4f, itemY, inner.width - 8f, 74f), "AEROSPACE & HELICOPTER WING",
            $"Total Aircraft & Helicopters Active: {totalAircraft}",
            $"• In-Flight Missions: {selectionService.MissionUnits.Count}",
            "• Airbase Status: Operational");
        itemY += 80f;

        DrawOobSection(new Rect(4f, itemY, inner.width - 8f, 74f), "SURFACE NAVAL FLEET",
            $"Total Active Warships & Carriers: {totalWarships}",
            $"• Naval Combat Patrols: {totalWarships}",
            "• Coastal Support: Active");
        itemY += 80f;

        DrawOobSection(new Rect(4f, itemY, inner.width - 8f, 74f), "STRATEGIC INFRASTRUCTURE",
            $"• Production Factories: {totalFactories}",
            $"• SAM Defense Sites: {totalSamSites}",
            $"• Active Vehicle Depots: {(spawnService.SelectedDepot != null ? 1 : 0)}");

        GUI.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, oobWindowRect.width - 72f, 28f));
    }

    private static void DrawOobSection(Rect rect, string title, string line1, string line2, string line3)
    {
        GUI.Box(rect, string.Empty, CommanderUiTheme.Panel);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 20f), title, CommanderUiTheme.Header);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 24f, rect.width - 24f, 18f), line1, CommanderUiTheme.Label);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 42f, rect.width - 24f, 18f), line2, CommanderUiTheme.MutedLabel);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 60f, rect.width - 24f, 18f), line3, CommanderUiTheme.MutedLabel);
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
        CommanderUiTheme.DrawHeaderStripe(new Rect(0f, 0f, reserveWindowRect.width, reserveWindowRect.height));
        CommanderUiTheme.DrawMutedFrame(new Rect(0f, 0f, reserveWindowRect.width, reserveWindowRect.height));
        if (CommanderUiTheme.DrawHelpButton(reserveWindowRect.width, ref reserveHelpVisible))
        {
            CommanderUiTheme.DrawHelpOverlay(
                new Rect(12f, 34f, reserveWindowRect.width - 24f, 84f),
                "FACTION RESERVE | Manage reserve stockpiles across LAND, AIR, and NAVAL branches. Multipliers (x1, x5, x10, MAX) apply to +BUY, SELL, and DEPLOY actions. HOLD intercepts automatic deployment so units stay in reserve for manual command.");
        }
        if (CommanderUiTheme.DrawCloseButton(reserveWindowRect.width))
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
            IReadOnlyList<string> allCats = factionSvc.AllLandCategories;
            List<string> chips = new() { "ALL" };
            chips.AddRange(allCats);

            float chipBarY = view.y;
            float chipW = Mathf.Max(60f, (view.width - (chips.Count - 1) * 4f) / chips.Count);
            for (int c = 0; c < chips.Count; c++)
            {
                string chipName = chips[c];
                bool isSelected = string.Equals(reserveCategoryFilter, chipName, StringComparison.OrdinalIgnoreCase);
                if (GUI.Button(new Rect(view.x + c * (chipW + 4f), chipBarY, chipW, 26f), chipName.ToUpperInvariant(),
                    isSelected ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
                {
                    reserveCategoryFilter = chipName;
                    reserveScroll = Vector2.zero;
                }
            }

            Rect landListRect = new(view.x, view.y + 32f, view.width, view.height - 32f);
            IReadOnlyList<VehicleDefinition> definitions = factionSvc.GetFilteredLandDefinitions(reserveCategoryFilter);
            Rect inner = new(0f, 0f, landListRect.width - 20f, Mathf.Max(landListRect.height, definitions.Count * 48f + 6f));
            reserveScroll = GUI.BeginScrollView(landListRect, reserveScroll, inner);
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
            IReadOnlyList<string> categories = factionSvc.AllLandCategories;
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
                int totalReserve = factionSvc.GetCategoryReserveTotal(category);

                Rect row = new(4f, 3f + i * 46f, inner.width - 8f, 42f);
                GUI.Box(row, string.Empty, CommanderUiTheme.Panel);
                bool updatedHeld = GUI.Toggle(new Rect(row.x + 10f, row.y + 10f, 64f, 22f), held, "HOLD", CommanderUiTheme.Toggle);
                if (updatedHeld != held)
                {
                    factionSvc.ToggleCategory(category);
                }
                GUI.Label(new Rect(row.x + 94f, row.y + 8f, row.width - 230f, 26f), category, CommanderUiTheme.Header);
                GUI.Label(new Rect(row.xMax - 140f, row.y + 8f, 130f, 26f),
                    $"RESERVE: {totalReserve} UNITS", CommanderUiTheme.MutedLabel);
            }
            GUI.EndScrollView();

            if (categories.Count == 0)
            {
                GUI.Label(catView, "No vehicle categories found.", CommanderUiTheme.Label);
            }
        }

        GUI.DragWindow(new Rect(0f, 0f, reserveWindowRect.width - 72f, 28f));
    }
}
