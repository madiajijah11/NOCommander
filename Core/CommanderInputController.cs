using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderInputController
{
    private const float DragThresholdPixels = 12f;

    private readonly CommanderOverlayUi overlayUi;
    private readonly CommanderSelectionService selectionService;
    private readonly CommanderSpawnService spawnService;
    private readonly CommanderMarkerService markerService;
    private readonly CommanderMoveService moveService;
    private readonly CommanderTacticalMapService tacticalMapService;
    private readonly CommanderSupplyHeliService supplyHeliService;
    private readonly CommanderMobileEmplacementService mobileEmplacementService;
    private readonly CommanderAirCommandService airCommandService;
    private readonly CommanderControlGroupsService? controlGroupsService;
    private readonly CommanderAlertService? alertService;
    private readonly CommanderStanceService? stanceService;
    private CommanderPovCrewUi? povCrewUi;

    private bool isMouseDown;
    private bool isBoxDragging;
    private Vector2 dragStartPos;

    internal static CommanderInputController? Instance { get; private set; }

    internal bool IsBoxDragging => isBoxDragging;

    internal CommanderInputController(
        CommanderOverlayUi overlayUi,
        CommanderSelectionService selectionService,
        CommanderSpawnService spawnService,
        CommanderMarkerService markerService,
        CommanderMoveService moveService,
        CommanderTacticalMapService tacticalMapService,
        CommanderSupplyHeliService supplyHeliService,
        CommanderMobileEmplacementService mobileEmplacementService,
        CommanderAirCommandService airCommandService,
        CommanderControlGroupsService? controlGroupsService = null,
        CommanderAlertService? alertService = null,
        CommanderStanceService? stanceService = null)
    {
        this.overlayUi = overlayUi;
        this.selectionService = selectionService;
        this.spawnService = spawnService;
        this.markerService = markerService;
        this.moveService = moveService;
        this.tacticalMapService = tacticalMapService;
        this.supplyHeliService = supplyHeliService;
        this.mobileEmplacementService = mobileEmplacementService;
        this.airCommandService = airCommandService;
        this.controlGroupsService = controlGroupsService;
        this.alertService = alertService;
        this.stanceService = stanceService;
        Instance = this;
    }

    internal void SetPovCrewUi(CommanderPovCrewUi ui)
    {
        povCrewUi = ui;
    }

    internal Rect GetBoxSelectionGuiRect()
    {
        Vector2 p1 = CommanderUiScale.ScreenToGui(dragStartPos);
        Vector2 p2 = CommanderUiScale.ScreenToGui(Input.mousePosition);
        return Rect.MinMaxRect(
            Mathf.Min(p1.x, p2.x),
            Mathf.Min(p1.y, p2.y),
            Mathf.Max(p1.x, p2.x),
            Mathf.Max(p1.y, p2.y));
    }

    internal void Tick()
    {
        Vector2 mousePosition = Input.mousePosition;
        CommanderCheatService.Instance?.UpdatePlacementPreview(mousePosition);

        HandleKeyboardShortcuts();

        if (CommanderNavalPurchaseService.Instance?.AwaitingRallySelection == true)
        {
            return;
        }

        if (spawnService.IsMapInteractionActive())
        {
            return;
        }

        if (povCrewUi?.ContainsScreenPoint(mousePosition) == true)
        {
            CancelDrag();
            return;
        }
        if (tacticalMapService.ContainsScreenPoint(mousePosition))
        {
            CancelDrag();
            return;
        }

        DynamicMap? dynamicMap = SceneSingleton<DynamicMap>.i;
        if (tacticalMapService.IsOpen && dynamicMap != null && dynamicMap.IsCursorInMapRectangle())
        {
            CancelDrag();
            if (CommanderShortcutInput.IsDown(CommanderSettings.SecondaryAction))
            {
                if (dynamicMap.TryGetCursorCoordinates(out GlobalPosition mapPos))
                {
                    bool queueWaypoint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    moveService.IssueDirectMoveOrder(mapPos, queueWaypoint: queueWaypoint);
                }
            }
            return;
        }

        // Primary Click & Box Selection Dragging
        if (CommanderShortcutInput.IsDown(CommanderSettings.PrimaryAction))
        {
            if (overlayUi.ContainsScreenPoint(mousePosition)
                || supplyHeliService.AwaitingTargetSelection
                || airCommandService.AwaitingAreaSelection
                || mobileEmplacementService.AwaitingDestination
                || spawnService.AwaitingRallyPointSelection)
            {
                HandlePrimaryClick(mousePosition);
            }
            else
            {
                isMouseDown = true;
                dragStartPos = mousePosition;
                isBoxDragging = false;
            }
        }

        if (isMouseDown && CommanderShortcutInput.IsPressed(CommanderSettings.PrimaryAction))
        {
            if (!isBoxDragging && Vector2.Distance(dragStartPos, mousePosition) >= DragThresholdPixels)
            {
                isBoxDragging = true;
            }
        }

        if (isMouseDown && CommanderShortcutInput.IsUp(CommanderSettings.PrimaryAction))
        {
            if (isBoxDragging)
            {
                Rect guiRect = GetBoxSelectionGuiRect();
                selectionService.SelectUnitsInScreenRect(guiRect, additive: CommanderSettings.AddToSelection.IsPressed());
            }
            else
            {
                HandlePrimaryClick(mousePosition);
            }

            isMouseDown = false;
            isBoxDragging = false;
        }

        // Secondary Click (Move / Attack Order)
        if (CommanderShortcutInput.IsDown(CommanderSettings.SecondaryAction))
        {
            HandleSecondaryClick(mousePosition);
        }
    }

    private void CancelDrag()
    {
        isMouseDown = false;
        isBoxDragging = false;
    }

    private void HandleKeyboardShortcuts()
    {
        // Spacebar alert jump when no unit is focused
        if (Input.GetKeyDown(KeyCode.Space) && selectionService.FocusedSelection == null)
        {
            alertService?.TryJumpToIncident(selectionService, tacticalMapService);
        }

        // Toggle Hold Fire stance (default key F)
        if (CommanderShortcutInput.IsDown(CommanderSettings.ToggleHoldFire))
        {
            stanceService?.ToggleHoldFireForSelection();
        }

        // Select All Combat Army (default key ~ / BackQuote, configurable)
        if (CommanderShortcutInput.IsDown(CommanderSettings.SelectAllArmy))
        {
            controlGroupsService?.SelectAllArmy(combatOnly: true);
        }

        // X key: Scatter / Evade Order
        if (Input.GetKeyDown(KeyCode.X))
        {
            moveService.ScatterSelectedUnits(55f);
        }

        // P key: Patrol Mode Toggle
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (moveService.AwaitingPatrolSelection)
            {
                moveService.CancelPatrolOrder();
            }
            else
            {
                moveService.BeginPatrolOrder();
            }
        }

        // G key: Guard / Escort Order Toggle
        if (CommanderShortcutInput.IsDown(CommanderSettings.GuardOrder))
        {
            if (moveService.AwaitingGuardSelection)
            {
                moveService.CancelGuardOrder();
            }
            else
            {
                moveService.BeginGuardOrder();
            }
        }

        // B key: Artillery / Barrage Order Toggle
        if (CommanderShortcutInput.IsDown(CommanderSettings.ArtilleryBarrage))
        {
            if (moveService.AwaitingBarrageSelection)
            {
                moveService.CancelBarrageOrder();
            }
            else
            {
                moveService.BeginBarrageOrder();
            }
        }

        // V key: Cycle Formations (Ring, Line, Column, Wedge, Box, Echelon)
        if (CommanderShortcutInput.IsDown(CommanderSettings.ToggleFormation))
        {
            moveService.CycleFormation();
        }

        // Ctrl + R: Global EMCON / Radar Silence Toggle
        if (CommanderShortcutInput.IsDown(CommanderSettings.GlobalRadarSilence))
        {
            CommanderRadarService.Instance?.ToggleGlobalEmcon();
        }

        // Control Groups (0-9)
        if (controlGroupsService != null)
        {
            bool isCtrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool isShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            for (int i = 0; i <= 9; i++)
            {
                KeyCode key = i == 0 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
                if (Input.GetKeyDown(key))
                {
                    if (isCtrl)
                    {
                        controlGroupsService.AssignGroup(i, selectionService.SelectedUnits);
                    }
                    else
                    {
                        controlGroupsService.SelectGroup(i, additive: isShift);
                    }
                    break;
                }
            }
        }
    }

    private void HandlePrimaryClick(Vector2 mousePosition)
    {
        if (overlayUi.ContainsScreenPoint(mousePosition))
        {
            return;
        }

        if (CommanderCheatService.Instance?.AwaitingPlacement == true)
        {
            CommanderCheatService.Instance.TrySpawnAtWorldPoint(mousePosition);
            return;
        }

        if (supplyHeliService.AwaitingTargetSelection)
        {
            supplyHeliService.TrySpawnAtWorldPoint(mousePosition);
            return;
        }

        if (airCommandService.AwaitingAreaSelection)
        {
            airCommandService.TrySetAreaFromWorld(mousePosition);
            return;
        }

        if (mobileEmplacementService.AwaitingDestination)
        {
            mobileEmplacementService.TrySetDestinationFromWorld(mousePosition);
            return;
        }

        if (spawnService.AwaitingRallyPointSelection)
        {
            spawnService.TrySetRallyPointFromWorld(mousePosition);
            return;
        }

        bool additive = CommanderSettings.AddToSelection.IsPressed();

        if (markerService.TryGetMarkerUnitAt(mousePosition, out Unit markerUnit))
        {
            selectionService.SelectUnit(markerUnit, additive);
            CommanderCameraFollowService.Instance?.CenterOnSelectionIfFollowing();
            return;
        }

        if (CommanderGameAccess.TryRaycastSelectableUnit(mousePosition, out Unit worldUnit))
        {
            selectionService.SelectUnit(worldUnit, additive);
            CommanderCameraFollowService.Instance?.CenterOnSelectionIfFollowing();
            return;
        }

        if (!additive)
        {
            selectionService.DeselectAll();
        }
    }

    private void HandleSecondaryClick(Vector2 mousePosition)
    {
        if (overlayUi.ContainsScreenPoint(mousePosition))
        {
            return;
        }

        if (CommanderCheatService.Instance?.AwaitingPlacement == true)
        {
            CommanderCheatService.Instance.CancelPlacement();
            return;
        }

        if (CommanderCheatService.Instance?.AwaitingPlacement == true)
        {
            CommanderCheatService.Instance.CancelPlacement();
            return;
        }

        bool queueWaypoint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        moveService.TryIssueMoveOrder(mousePosition, queueWaypoint: queueWaypoint);
    }
}
