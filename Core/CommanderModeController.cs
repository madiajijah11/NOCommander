using UnityEngine;
using UnityEngine.SceneManagement;

namespace NuclearOptionCommander;

internal sealed class CommanderModeController : MonoBehaviour
{
    private CommanderCameraController? cameraController;
    private CommanderCursorController? cursorController;
    private CommanderSelectionService? selectionService;
    private CommanderFactionVehicleService? factionVehicleService;
    private CommanderCameraFollowService? cameraFollowService;
    private CommanderPovCrewUi? povCrewUi;
    private CommanderTacticalMapService? tacticalMapService;
    private CommanderRadarService? radarService;
    private CommanderMobileEmplacementService? mobileEmplacementService;
    private CommanderDirectPathService? directPathService;
    private CommanderRepairService? repairService;
    private CommanderSupplyHeliService? supplyHeliService;
    private CommanderAirCommandService? airCommandService;
    private CommanderNavalPurchaseService? navalPurchaseService;
    private CommanderSamSiteAnalyzerService? samSiteAnalyzerService;
    private CommanderSamSiteService? samSiteService;
    private CommanderSpawnService? spawnService;
    private CommanderMarkerService? markerService;
    private CommanderMoveService? moveService;
    private CommanderDoctrineService? doctrineService;
    private CommanderOrderAuthority? orderAuthority;
    private CommanderControlGroupsService? controlGroupsService;
    private CommanderAlertService? alertService;
    private CommanderStanceService? stanceService;
    private CommanderCheatService? cheatService;
    private CommanderFactoryProductionService? factoryProductionService;
    private CommanderWarActivityService? warActivityService;
    private readonly CommanderTimeOfDayService timeOfDayService = new();
    private readonly CommanderGameSpeedService gameSpeedService = new();
    private CommanderForwardOutpostService? forwardOutpostService;
    private CommanderBuildingEconomyService? buildingEconomyService;
    private CommanderTargetDeconflictionService? targetDeconflictionService;
    private CommanderAirLoiterService? airLoiterService;
    private CommanderSmokeCountermeasuresService? smokeService;
    private CommanderCounterBatteryRadarService? counterBatteryService;
    private CommanderObjectiveDefenseService? objectiveDefenseService;
    private CommanderBattleGroupService? battleGroupService;
    private CommanderEmergencyResponseService? emergencyResponseService;
    private CommanderOverlayUi? overlayUi;
    private CommanderInputController? inputController;
    private CommanderPersistentOperations? persistentOperations;
    private float nextInactiveEntryProbeAt;
    private bool aircraftSelectionMenuPresent;

    private readonly System.Diagnostics.Stopwatch perfSw = new();
    private float nextPerfSummaryTime;
    private float measuredMsMarker;
    private float measuredMsMap;
    private float measuredMsGui;

    internal static string LivePerfTelemetry { get; private set; } = string.Empty;

    internal bool IsActive { get; private set; }

    private void Awake()
    {
        CommanderUiScale.ApplyResolutionPreset();
        cameraController = new CommanderCameraController();
        cursorController = new CommanderCursorController();
        selectionService = new CommanderSelectionService();
        cameraFollowService = new CommanderCameraFollowService(selectionService);
        povCrewUi = new CommanderPovCrewUi(cameraFollowService);
        factionVehicleService = new CommanderFactionVehicleService();
        tacticalMapService = new CommanderTacticalMapService(cameraFollowService);
        radarService = new CommanderRadarService(selectionService);
        mobileEmplacementService = new CommanderMobileEmplacementService(selectionService);
        directPathService = new CommanderDirectPathService(selectionService);
        repairService = new CommanderRepairService();
        supplyHeliService = new CommanderSupplyHeliService();
        airCommandService = new CommanderAirCommandService(tacticalMapService);
        navalPurchaseService = new CommanderNavalPurchaseService(tacticalMapService);
        samSiteAnalyzerService = new CommanderSamSiteAnalyzerService();
        samSiteService = new CommanderSamSiteService(
            samSiteAnalyzerService,
            supplyHeliService);
        spawnService = new CommanderSpawnService(selectionService, factionVehicleService, tacticalMapService);
        markerService = new CommanderMarkerService(selectionService);
        orderAuthority = new CommanderOrderAuthority();
        doctrineService = new CommanderDoctrineService(selectionService, CommanderSettings.DoctrineRefreshIntervalSeconds);
        moveService = new CommanderMoveService(selectionService, orderAuthority, doctrineService);
        persistentOperations = new CommanderPersistentOperations(
            spawnService,
            supplyHeliService,
            airCommandService,
            mobileEmplacementService,
            samSiteAnalyzerService,
            samSiteService);
        controlGroupsService = new CommanderControlGroupsService(selectionService);
        alertService = new CommanderAlertService();
        stanceService = new CommanderStanceService(selectionService);
        cheatService = new CommanderCheatService(selectionService);
        factoryProductionService = new CommanderFactoryProductionService();
        warActivityService = new CommanderWarActivityService(factionVehicleService, spawnService, airCommandService, navalPurchaseService, supplyHeliService);
        forwardOutpostService = new CommanderForwardOutpostService(selectionService);
        buildingEconomyService = new CommanderBuildingEconomyService();
        smokeService = new CommanderSmokeCountermeasuresService();
        counterBatteryService = new CommanderCounterBatteryRadarService();
        airLoiterService = new CommanderAirLoiterService();
        objectiveDefenseService = new CommanderObjectiveDefenseService();
        battleGroupService = new CommanderBattleGroupService();
        emergencyResponseService = new CommanderEmergencyResponseService();
        targetDeconflictionService = new CommanderTargetDeconflictionService();
        overlayUi = new CommanderOverlayUi(
            selectionService,
            moveService,
            spawnService,
            radarService,
            mobileEmplacementService,
            repairService,
            directPathService,
            supplyHeliService,
            airCommandService,
            navalPurchaseService,
            samSiteAnalyzerService,
            samSiteService,
            UnlockAdvancedFeatures,
            () => Deactivate());
        inputController = new CommanderInputController(
            overlayUi,
            selectionService,
            spawnService,
            markerService,
            moveService,
            tacticalMapService,
            supplyHeliService,
            mobileEmplacementService,
            airCommandService,
            controlGroupsService,
            alertService,
            stanceService);
        inputController.SetPovCrewUi(povCrewUi);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void Update()
    {
        CommanderUiScale.RefreshResolutionPreset();
        if (CommanderFeatureGate.AdvancedFeaturesEnabled)
        {
            persistentOperations?.Tick();
        }
        if (!IsActive)
        {
            return;
        }

        if (CommanderShortcutInput.IsDown(CommanderSettings.ToggleUi))
        {
            overlayUi?.ToggleScreenshotUi();
        }

        if (IsPlayerInOperationalAircraft())
        {
            Deactivate(restorePreviousCamera: false);
            return;
        }

        cursorController?.Tick();
        selectionService?.Tick();
        doctrineService?.Tick();
        cameraFollowService?.Tick();
        moveService?.Tick();

        perfSw.Restart();
        markerService?.Tick();
        measuredMsMarker = (float)perfSw.Elapsed.TotalMilliseconds;

        perfSw.Restart();
        tacticalMapService?.Tick();
        measuredMsMap = (float)perfSw.Elapsed.TotalMilliseconds;

        if (CommanderFeatureGate.AdvancedFeaturesEnabled)
        {
            radarService?.Tick();
            mobileEmplacementService?.TickActive();
            supplyHeliService?.TickActive();
            airCommandService?.TickActive();
            navalPurchaseService?.TickActive();
            samSiteAnalyzerService?.TickActive();
            spawnService?.TickActive();
            factoryProductionService?.Tick();
            warActivityService?.Tick();
            objectiveDefenseService?.Tick();
            battleGroupService?.Tick();
            emergencyResponseService?.Tick();
            timeOfDayService.Tick();
            forwardOutpostService?.Tick();
            buildingEconomyService?.Tick();
            airLoiterService?.Tick();
            smokeService?.Tick();
            counterBatteryService?.Tick();
        }
        overlayUi?.Tick();
        inputController?.Tick();

        float now = Time.unscaledTime;
        if (now >= nextPerfSummaryTime)
        {
            nextPerfSummaryTime = now + 0.35f;
            LivePerfTelemetry = $"CPU: Mkr:{measuredMsMarker:0.0}ms Map:{measuredMsMap:0.0}ms GUI:{measuredMsGui:0.0}ms";
            if (measuredMsMarker > 10f || measuredMsMap > 10f || measuredMsGui > 15f)
            {
                CommanderPlugin.Log.LogWarning($"[NOCommander SPIKE] {LivePerfTelemetry}");
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsActive)
        {
            cameraFollowService?.FixedTick();
        }
    }

    private void OnGUI()
    {
        Matrix4x4 previousMatrix = CommanderUiScale.Begin();
        try
        {
            if (!IsActive)
            {
                if (ShouldShowCommanderEntry())
                {
                    overlayUi?.DrawInactiveLauncher(Activate);
                }
                return;
            }

            perfSw.Restart();
            overlayUi?.Draw();
            measuredMsGui = (float)perfSw.Elapsed.TotalMilliseconds;
            if (overlayUi?.CommanderUiHidden != true)
            {
                povCrewUi?.Draw();
            }
            if (overlayUi?.ShowTacticalMapUi == true)
            {
                tacticalMapService?.DrawControls();
            }
        }
        finally
        {
            CommanderUiScale.End(previousMatrix);
        }
    }

    private bool ShouldShowCommanderEntry()
    {
        if (IsPlayerInOperationalAircraft()
            || (GameManager.gameState != GameState.SinglePlayer && GameManager.gameState != GameState.Multiplayer))
        {
            return false;
        }

        if (DynamicMap.mapMaximized)
        {
            return true;
        }

        if (Time.unscaledTime >= nextInactiveEntryProbeAt)
        {
            nextInactiveEntryProbeAt = Time.unscaledTime + 0.75f;
            aircraftSelectionMenuPresent = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>() != null;
        }
        return aircraftSelectionMenuPresent;
    }

    private void OnDisable()
    {
        Deactivate();
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Deactivate(restorePreviousCamera: false);
    }

    private void OnApplicationQuit()
    {
        Deactivate();
    }

    internal void Toggle()
    {
        if (IsActive)
        {
            Deactivate();
            return;
        }

        Activate();
    }

    private void Activate()
    {
        if (IsActive)
        {
            return;
        }

        if (IsPlayerInOperationalAircraft())
        {
            CommanderPlugin.Log.LogWarning("Commander mode is only available while the player is outside an aircraft.");
            return;
        }

        AircraftSelectionMenu? aircraftSelectionMenu = UnityEngine.Object.FindObjectOfType<AircraftSelectionMenu>();
        if (aircraftSelectionMenu != null && aircraftSelectionMenu.gameObject.activeInHierarchy)
        {
            aircraftSelectionMenu.ReturnToMap();
        }

        if (cameraController == null || !cameraController.TryActivate())
        {
            CommanderPlugin.Log.LogWarning("Commander mode could not start because the free camera is not available yet.");
            return;
        }

        CommanderFeatureGate.RefreshMission();
        cursorController?.Activate();
        IsActive = true;
        orderAuthority?.BeginSession();
        selectionService?.Activate();
        markerService?.Activate();
        if (CommanderFeatureGate.AdvancedFeaturesEnabled)
        {
            ActivateAdvancedServices();
        }
        overlayUi?.Activate();
        if (CommanderFeatureGate.AdvancedFeaturesEnabled
            && overlayUi?.ShowTacticalMapUi == true)
        {
            tacticalMapService?.Open();
        }
        CommanderPlugin.Log.LogInfo(
            $"Commander mode enabled: mission={CommanderFeatureGate.MissionName}, features={(CommanderFeatureGate.AdvancedFeaturesEnabled ? "full" : "core")}.");
    }

    private void UnlockAdvancedFeatures()
    {
        if (CommanderFeatureGate.AdvancedFeaturesEnabled)
        {
            return;
        }

        CommanderFeatureGate.UnlockAdvancedFeatures();
        if (IsActive)
        {
            ActivateAdvancedServices();
            if (overlayUi?.ShowTacticalMapUi == true)
            {
                tacticalMapService?.Open();
            }
        }
        CommanderPlugin.Log.LogWarning(
            $"Advanced Commander features manually unlocked for mission '{CommanderFeatureGate.MissionName}'.");
    }

    private void ActivateAdvancedServices()
    {
        radarService?.Activate();
        mobileEmplacementService?.Activate();
        supplyHeliService?.Activate();
        airCommandService?.Activate();
        navalPurchaseService?.Activate();
        samSiteAnalyzerService?.Activate();
        spawnService?.Activate();
        warActivityService?.Activate();
        objectiveDefenseService?.Activate();
        battleGroupService?.Activate();
        emergencyResponseService?.Activate();
        timeOfDayService.Activate();
        gameSpeedService.Activate();
    }

    private void Deactivate(bool restorePreviousCamera = true)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        orderAuthority?.ResetSession();
        doctrineService?.ResetSession();
        selectionService?.Deactivate();
        cameraFollowService?.Disable();
        markerService?.Deactivate();
        radarService?.Deactivate();
        mobileEmplacementService?.Deactivate();
        directPathService?.Deactivate();
        supplyHeliService?.Deactivate();
        airCommandService?.Deactivate();
        navalPurchaseService?.Deactivate();
        samSiteAnalyzerService?.Deactivate();
        spawnService?.Deactivate();
        warActivityService?.Deactivate();
        objectiveDefenseService?.Deactivate();
        battleGroupService?.Deactivate();
        emergencyResponseService?.Deactivate();
        timeOfDayService.Deactivate();
        overlayUi?.Deactivate();
        tacticalMapService?.Close();
        cursorController?.Deactivate();
        cameraController?.Deactivate(restorePreviousCamera);
        CommanderPlugin.Log.LogInfo("Commander mode disabled.");
    }

    private static bool IsPlayerInOperationalAircraft()
    {
        return GameManager.GetLocalAircraft(out Aircraft aircraft)
            && aircraft != null
            && !aircraft.disabled;
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        Deactivate(restorePreviousCamera: false);
        CommanderFeatureGate.ResetSession();
        selectionService?.ResetSession();
        doctrineService?.ResetSession();
        orderAuthority?.ResetSession();
        cameraFollowService?.Disable();
        tacticalMapService?.ResetSession();
        radarService?.ResetSession();
        mobileEmplacementService?.ResetSession();
        directPathService?.ResetSession();
        repairService?.ResetSession();
        supplyHeliService?.ResetSession();
        airCommandService?.ResetSession();
        navalPurchaseService?.ResetSession();
        samSiteService?.ResetSession();
        samSiteAnalyzerService?.ResetSession();
        aircraftSelectionMenuPresent = false;
        nextInactiveEntryProbeAt = 0f;
        factionVehicleService?.ResetSession();
        spawnService?.ResetSession();
        controlGroupsService?.ResetSession();
        alertService?.ResetSession();
        stanceService?.ResetSession();
        cheatService?.ResetSession();
        factoryProductionService?.ResetSession();
        warActivityService?.ResetSession();
        objectiveDefenseService?.ResetSession();
        battleGroupService?.ResetSession();
        emergencyResponseService?.ResetSession();
        timeOfDayService.ResetSession();
        gameSpeedService.ResetSession();
        forwardOutpostService?.ResetSession();
        buildingEconomyService?.ResetSession();
        airLoiterService?.ResetSession();
        targetDeconflictionService?.ResetSession();
        smokeService?.ResetSession();
        counterBatteryService?.ResetSession();
        moveService?.ResetSession();
    }

}
