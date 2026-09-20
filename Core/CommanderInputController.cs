using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderInputController
{
    private const float DragThresholdPixels = 8f;

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
        return new Rect(
            Mathf.Min(p1.x, p2.x),
            Mathf.Min(p1.y, p2.y),
            Mathf.Abs(p1.x - p2.x),
            Mathf.Abs(p1.y - p2.y));
    }

    internal void Tick()
    {
        Vector2 mousePosition = Input.mousePosition;
        CommanderCheatService.Instance?.UpdatePlacementPreview(mousePosition);

        HandleKeyboardShortcuts();

        // 1. Right-Click / Secondary Action Global Cancellation
        if (CommanderShortcutInput.IsDown(CommanderSettings.SecondaryAction) || Input.GetMouseButtonDown(1))
        {
            if (CancelAnyTargetingMode())
            {
                CancelDrag();
                return;
            }
        }

        if (CommanderNavalPurchaseService.Instance?.AwaitingRallySelection == true)
        {
            return;
        }

        if (spawnService.IsMapInteractionActive())
        {
            return;
        }

        // 2. Tactical Minimap Cursor & Click Routing
        DynamicMap? dynamicMap = SceneSingleton<DynamicMap>.i;
        bool cursorInMinimap = tacticalMapService.IsOpen && dynamicMap != null && dynamicMap.IsCursorInMapRectangle();

        if (cursorInMinimap && dynamicMap != null)
        {
            CancelDrag();
            if (CommanderShortcutInput.IsDown(CommanderSettings.PrimaryAction) || Input.GetMouseButtonDown(0))
            {
                if (dynamicMap.TryGetCursorCoordinates(out GlobalPosition mapPos))
                {
                    if (airCommandService.AwaitingAreaSelection)
                    {
                        airCommandService.CompleteAreaSelection(mapPos);
                        return;
                    }
                    if (supplyHeliService.AwaitingTargetSelection)
                    {
                        supplyHeliService.TrySpawnAtPosition(mapPos);
                        return;
                    }
                    if (moveService.AwaitingAttackMoveSelection)
                    {
                        moveService.IssueDirectMoveOrder(mapPos, false);
                        moveService.CancelAttackMoveOrder();
                        return;
                    }
                }
            }
            if (CommanderShortcutInput.IsDown(CommanderSettings.SecondaryAction) || Input.GetMouseButtonDown(1))
            {
                if (dynamicMap.TryGetCursorCoordinates(out GlobalPosition mapPos))
                {
                    bool queueWaypoint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    moveService.IssueDirectMoveOrder(mapPos, queueWaypoint: queueWaypoint);
                    return;
                }
            }
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

        // 3. Primary Click & Box Selection Dragging in 3D World
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

        // 4. Secondary Click (Move / Attack Order in 3D World)
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
        // Cancel targeting via Backspace, C, Delete, or Rewired Cancel
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.C) || CommanderGameInput.CancelDown)
        {
            if (CancelAnyTargetingMode())
            {
                return;
            }
        }

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

        // T key: Attack Move Order Toggle
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (moveService.AwaitingAttackMoveSelection)
            {
                moveService.CancelAttackMoveOrder();
            }
            else
            {
                moveService.BeginAttackMoveOrder();
            }
        }

        // B key: Artillery Barrage Order Toggle
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

        // V key: Cycle Formations (Ring, Line, Column, Wedge, Box)
        if (CommanderShortcutInput.IsDown(CommanderSettings.ToggleFormation))
        {
            moveService.CycleFormation();
        }

        // Ctrl + R: Global Radar Silence / EMCON Toggle
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

        if (moveService.AwaitingAttackMoveSelection)
        {
            moveService.TrySetAttackMoveDestination(mousePosition);
            return;
        }

        if (moveService.AwaitingGuardSelection)
        {
            moveService.TrySetGuardTarget(mousePosition);
            return;
        }

        if (moveService.AwaitingPatrolSelection)
        {
            moveService.TrySetPatrolDestination(mousePosition);
            return;
        }

        if (moveService.AwaitingBarrageSelection)
        {
            moveService.TrySetBarrageTarget(mousePosition);
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

        bool queueWaypoint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        moveService.TryIssueMoveOrder(mousePosition, queueWaypoint: queueWaypoint);
    }

    internal bool CancelAnyTargetingMode()
    {
        bool cancelled = false;

        if (airCommandService.AwaitingAreaSelection)
        {
            airCommandService.CancelAreaSelection();
            cancelled = true;
        }
        if (supplyHeliService.AwaitingTargetSelection)
        {
            supplyHeliService.CancelTargetSelection();
            cancelled = true;
        }
        if (moveService.AwaitingAttackMoveSelection)
        {
            moveService.CancelAttackMoveOrder();
            cancelled = true;
        }
        if (moveService.AwaitingGuardSelection)
        {
            moveService.CancelGuardOrder();
            cancelled = true;
        }
        if (moveService.AwaitingPatrolSelection)
        {
            moveService.CancelPatrolOrder();
            cancelled = true;
        }
        if (moveService.AwaitingBarrageSelection)
        {
            moveService.CancelBarrageOrder();
            cancelled = true;
        }
        if (spawnService.AwaitingRallyPointSelection)
        {
            spawnService.CancelRallySelection();
            cancelled = true;
        }
        if (mobileEmplacementService.AwaitingDestination)
        {
            mobileEmplacementService.CancelDestination();
            cancelled = true;
        }
        if (CommanderCheatService.Instance?.AwaitingPlacement == true)
        {
            CommanderCheatService.Instance.CancelPlacement();
            cancelled = true;
        }

        return cancelled;
    }
}
