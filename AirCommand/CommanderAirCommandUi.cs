using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderAirCommandUi
{
    private const int WindowId = 0x434F4143;
    private const int MissionWindowId = 0x434F414D;

    private readonly CommanderAirCommandService service;
    private Rect windowRect = new(16f, 16f, 480f, 640f);
    private Vector2 aircraftScroll;
    private Vector2 missionScroll;
    private bool positionInitialized;
    private bool missionWindowVisible;
    private Rect missionWindowRect;
    private readonly List<Aircraft> missionAircraft = new();

    internal static CommanderAirCommandUi? Instance { get; private set; }

    internal CommanderAirCommandUi(CommanderAirCommandService service)
    {
        this.service = service;
        Instance = this;
    }

    internal bool Visible { get; private set; }

    internal void Toggle()
    {
        if (Visible) Hide();
        else Show();
    }

    internal void Show()
    {
        if (Visible) return;
        Visible = true;
        service.SetUiVisible(true);
        service.RefreshOptions();
        aircraftScroll = Vector2.zero;
    }

    internal void Hide()
    {
        if (service.AwaitingAreaSelection) service.CancelAreaSelection();
        Visible = false;
        service.SetUiVisible(false);
        service.ClearMissionAircraftSelection();
        CommanderSelectionService.Instance?.DeselectAll();
        if (CommanderTacticalMapService.Instance?.IsFullscreenOpen == true)
        {
            CommanderTacticalMapService.Instance.CloseFullscreen();
            if (CommanderPlugin.Instance?.IsCommanderModeActive == true && CommanderSettings.ShowTacticalMap)
            {
                CommanderTacticalMapService.Instance.Open();
            }
        }
    }

    internal bool HandleMapKey()
    {
        if (!Visible) return false;
        Hide();
        return true;
    }

    internal void ResetPosition()
    {
        positionInitialized = false;
    }

    internal void Tick()
    {
        float width = Mathf.Min(480f, CommanderUiScale.Width - 60f);
        float height = Mathf.Min(700f, CommanderUiScale.Height - 32f);

        if (!positionInitialized)
        {
            windowRect = new Rect(24f, Mathf.Max(16f, (CommanderUiScale.Height - height) * 0.5f), width, height);
            missionWindowRect = new Rect(windowRect.xMax + 12f, windowRect.y, 320f, Mathf.Min(440f, height));
            positionInitialized = true;
        }
        else
        {
            windowRect.width = width;
            windowRect.height = height;
            windowRect = CommanderUiTheme.ClampWindow(windowRect);
            missionWindowRect.width = 320f;
            missionWindowRect.height = Mathf.Min(440f, windowRect.height);
            missionWindowRect = CommanderUiTheme.ClampWindow(missionWindowRect);
        }

        service.CollectMissionAircraft(missionAircraft);
    }

    internal bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPoint);
        return Visible && (windowRect.Contains(guiPoint) || (missionWindowVisible && missionWindowRect.Contains(guiPoint)));
    }

    internal void Draw()
    {
        if (!Visible) return;

        service.SetAreaSelectionBlockingRects(windowRect, missionWindowVisible ? missionWindowRect : Rect.zero);
        windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "AIR COMMAND DECK", CommanderUiTheme.Window);
        if (missionWindowVisible && missionAircraft.Count > 0)
        {
            missionWindowRect = GUI.Window(MissionWindowId, missionWindowRect, DrawMissionWindow, "ACTIVE MISSIONS", CommanderUiTheme.Window);
        }
    }

    private void DrawWindow(int windowId)
    {
        if (GUI.Button(new Rect(windowRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.DangerButton))
        {
            if (service.AwaitingAreaSelection) service.CancelAreaSelection();
            Hide();
            return;
        }

        float y = 34f;

        // 1. Mission Mode Selector Tabs (Military Doctrine Roles)
        float tabW = (windowRect.width - 32f) / 4f;
        DrawModeTab(CommanderAirCommandService.AirCommandMode.AirGuard, "🛡️ DEFENDER", 12f, y, tabW);
        DrawModeTab(CommanderAirCommandService.AirCommandMode.Cas, "⚔️ ATTACKER", 16f + tabW, y, tabW);
        DrawModeTab(CommanderAirCommandService.AirCommandMode.Arad, "📡 RECON / EW", 20f + tabW * 2f, y, tabW);
        DrawModeTab(CommanderAirCommandService.AirCommandMode.StrategicStrike, "📦 SUPPORT", 24f + tabW * 3f, y, tabW);
        y += 42f;

        // 2. Mission Settings Row (Radius Stepper & Active Missions Toggle)
        Rect settingsRow = new(12f, y, windowRect.width - 24f, 36f);
        GUI.Box(settingsRow, string.Empty, CommanderUiTheme.Panel);
        GUI.Label(new Rect(settingsRow.x + 8f, settingsRow.y + 7f, 90f, 22f), "RADIUS:", CommanderUiTheme.MutedLabel);
        if (GUI.Button(new Rect(settingsRow.x + 80f, settingsRow.y + 4f, 26f, 26f), "-", CommanderUiTheme.Button)) service.StepMissionRadius(-5f);
        GUI.Label(new Rect(settingsRow.x + 112f, settingsRow.y + 7f, 60f, 22f), $"{service.SelectedMissionRadiusKm:0} km", CommanderUiTheme.Header);
        if (GUI.Button(new Rect(settingsRow.x + 172f, settingsRow.y + 4f, 26f, 26f), "+", CommanderUiTheme.Button)) service.StepMissionRadius(5f);

        string missionBtnLabel = $"MISSIONS ({missionAircraft.Count})";
        if (GUI.Button(new Rect(settingsRow.xMax - 140f, settingsRow.y + 4f, 132f, 26f), missionBtnLabel,
            missionWindowVisible ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            missionWindowVisible = !missionWindowVisible;
        }
        y += 44f;

        // 3. Ready Aircraft Cards List (Quick 1-Click Deploy)
        GUI.Label(new Rect(12f, y, windowRect.width - 24f, 20f), "SELECT AIRCRAFT & DEPLOY", CommanderUiTheme.MutedLabel);
        y += 22f;

        float listViewHeight = windowRect.height - y - 68f;
        Rect viewRect = new(12f, y, windowRect.width - 24f, listViewHeight);
        float cardHeight = 98f;
        float innerHeight = Mathf.Max(viewRect.height, service.Options.Count * (cardHeight + 6f) + 6f);
        aircraftScroll = GUI.BeginScrollView(viewRect, aircraftScroll, new Rect(0f, 0f, viewRect.width - 18f, innerHeight));

        for (int i = 0; i < service.Options.Count; i++)
        {
            CommanderAirCommandService.AirMissionOption option = service.Options[i];
            Rect card = new(2f, 2f + i * (cardHeight + 6f), viewRect.width - 22f, cardHeight);
            bool isSelected = (i == service.SelectedOptionIndex);

            GUI.Box(card, string.Empty, isSelected ? CommanderUiTheme.Panel : CommanderUiTheme.Panel);

            // Plane Icon & Name + Cost / Reserve Status
            FactionHQ? hq = CommanderGameAccess.GetLocalHq();
            int reserve = hq?.GetUnitSupply(option.Definition) ?? 0;
            string costText = reserve > 0 ? $"[{reserve} IN RESERVE]" : $"[{UnitConverter.ValueReading(option.Definition.value)}]";
            GUI.Label(new Rect(card.x + 10f, card.y + 6f, card.width - 130f, 22f), $"✈️ {option.Definition.unitName}  {costText}", CommanderUiTheme.Header);

            // Loadout Preset with Cycle Button
            string presetName = string.IsNullOrEmpty(option.LoadoutName) ? "Combat Preset" : option.LoadoutName;
            if (option.AvailablePresets.Count > 1)
            {
                if (GUI.Button(new Rect(card.x + 10f, card.y + 30f, card.width - 135f, 22f), $"⚙️ PRESET: {presetName.ToUpperInvariant()} ↺", CommanderUiTheme.Button))
                {
                    option.CyclePreset();
                }
            }
            else
            {
                GUI.Label(new Rect(card.x + 10f, card.y + 30f, card.width - 135f, 20f), $"PRESET: {presetName}", CommanderUiTheme.MutedLabel);
            }

            // Real Armament Breakdown (Weapons & Count)
            string weaponSummary = option.GetLoadoutWeaponSummary();
            GUI.Label(new Rect(card.x + 10f, card.y + 54f, card.width - 135f, 38f), weaponSummary, CommanderUiTheme.MutedLabel);

            // Quick Deploy Button
            bool canAfford = reserve > 0 || (hq != null && hq.factionFunds >= option.Definition.value);
            bool old = GUI.enabled;
            GUI.enabled = old && canAfford && !service.AwaitingAreaSelection;

            Rect deployBtnRect = new(card.xMax - 115f, card.y + 20f, 105f, 58f);
            if (GUI.Button(deployBtnRect, "DEPLOY ➔", isSelected ? CommanderUiTheme.PrimaryButton : CommanderUiTheme.SelectedButton))
            {
                service.SelectOption(i);
                service.BeginAreaSelection();
            }
            GUI.enabled = old;
        }

        if (service.Options.Count == 0)
        {
            GUI.Label(new Rect(20f, 30f, viewRect.width - 40f, 40f), "No compatible combat aircraft available for this mission mode.", CommanderUiTheme.MutedLabel);
        }

        GUI.EndScrollView();
        y += listViewHeight + 8f;

        // 4. Status Bar & Cancel Button
        if (service.AwaitingAreaSelection)
        {
            if (GUI.Button(new Rect(12f, y, windowRect.width - 24f, 38f), "CANCEL TARGET SELECTION", CommanderUiTheme.DangerButton))
            {
                service.CancelAreaSelection();
            }
        }
        else
        {
            string status = string.IsNullOrEmpty(service.StatusText) ? "Ready for tasking. Click DEPLOY on any aircraft above." : service.StatusText;
            GUI.Label(new Rect(12f, y, windowRect.width - 24f, 36f), status, CommanderUiTheme.MutedLabel);
        }

        GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 44f, 34f));
    }

    private void DrawModeTab(CommanderAirCommandService.AirCommandMode mode, string label, float x, float y, float width)
    {
        bool isSelected = service.SelectedMode == mode;
        if (GUI.Button(new Rect(x, y, width - 4f, 34f), label, isSelected ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Button))
        {
            service.SelectMode(mode);
            aircraftScroll = Vector2.zero;
        }
    }

    private void DrawMissionWindow(int windowId)
    {
        if (GUI.Button(new Rect(missionWindowRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.Button))
        {
            missionWindowVisible = false;
            return;
        }

        if (missionAircraft.Count == 0)
        {
            GUI.Label(new Rect(16f, 40f, missionWindowRect.width - 32f, 30f), "No active air missions.", CommanderUiTheme.MutedLabel);
            GUI.DragWindow(new Rect(0f, 0f, missionWindowRect.width - 44f, 34f));
            return;
        }

        Rect view = new(10f, 36f, missionWindowRect.width - 20f, missionWindowRect.height - 48f);
        float innerH = Mathf.Max(view.height, missionAircraft.Count * 62f + 6f);
        missionScroll = GUI.BeginScrollView(view, missionScroll, new Rect(0f, 0f, view.width - 18f, innerH));

        for (int i = 0; i < missionAircraft.Count; i++)
        {
            Aircraft ac = missionAircraft[i];
            if (ac == null || ac.disabled) continue;

            Rect item = new(2f, 2f + i * 62f, view.width - 22f, 56f);
            GUI.Box(item, string.Empty, CommanderUiTheme.Panel);

            GUI.Label(new Rect(item.x + 8f, item.y + 6f, item.width - 80f, 20f), ac.unitName, CommanderUiTheme.Header);
            GUI.Label(new Rect(item.x + 8f, item.y + 28f, item.width - 80f, 18f), $"Speed: {ac.speed:F0} m/s | Alt: {ac.radarAlt:F0} m", CommanderUiTheme.MutedLabel);

            if (GUI.Button(new Rect(item.xMax - 68f, item.y + 10f, 60f, 36f), "RTB", CommanderUiTheme.DangerButton))
            {
                service.RequestReturnToBase(ac);
            }
        }

        GUI.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, missionWindowRect.width - 44f, 34f));
    }
}
