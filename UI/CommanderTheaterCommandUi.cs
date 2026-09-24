using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderTheaterCommandUi
{
    private const int WindowId = 0x434F4D54;

    private readonly CommanderTheaterSectorService sectorService;
    private readonly CommanderBattlegroupService battlegroupService;
    private readonly CommanderAirCommandService airCommandService;
    private readonly CommanderTacticalMapService tacticalMapService;

    private bool visible;
    private Rect windowRect;
    private Vector2 sectorScroll;

    internal bool Visible
    {
        get => visible;
        set => visible = value;
    }

    internal CommanderTheaterCommandUi(
        CommanderTheaterSectorService sectorService,
        CommanderBattlegroupService battlegroupService,
        CommanderAirCommandService airCommandService,
        CommanderTacticalMapService tacticalMapService)
    {
        this.sectorService = sectorService;
        this.battlegroupService = battlegroupService;
        this.airCommandService = airCommandService;
        this.tacticalMapService = tacticalMapService;
    }

    internal void Draw()
    {
        if (!visible)
        {
            return;
        }

        float width = Mathf.Min(560f, CommanderUiScale.Width - 24f);
        float height = Mathf.Min(620f, CommanderUiScale.Height - 120f);
        if (windowRect.width < 100f)
        {
            windowRect = new Rect(
                Mathf.Max(12f, CommanderUiScale.Width - width - 24f),
                70f,
                width,
                height);
        }

        windowRect = GUI.Window(
            WindowId,
            windowRect,
            DrawWindow,
            "THEATER SECTOR DIRECTIVES (HIGH-COMMAND)",
            CommanderUiTheme.Window);
    }

    private void DrawWindow(int id)
    {
        if (GUI.Button(new Rect(windowRect.width - 34f, 3f, 26f, 22f), "X", CommanderUiTheme.Button))
        {
            visible = false;
        }

        float y = 38f;
        GUI.Label(new Rect(16f, y, windowRect.width - 32f, 22f), "THEATER SECTORS & FRONTLINES", CommanderUiTheme.Header);
        y += 26f;

        IReadOnlyList<TheaterSector> sectors = sectorService.Sectors;
        if (sectors.Count == 0)
        {
            GUI.Label(new Rect(16f, y, windowRect.width - 32f, 24f), "Scanning theater for strategic sectors...", CommanderUiTheme.MutedLabel);
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 40f, 34f));
            return;
        }

        // Sector List ScrollView
        float scrollHeight = 220f;
        sectorScroll = GUI.BeginScrollView(
            new Rect(12f, y, windowRect.width - 24f, scrollHeight),
            sectorScroll,
            new Rect(0f, 0f, windowRect.width - 48f, sectors.Count * 62f));

        float itemY = 0f;
        for (int i = 0; i < sectors.Count; i++)
        {
            TheaterSector sec = sectors[i];
            bool isSelected = sec.Id == sectorService.SelectedSectorId;

            GUI.Box(new Rect(0f, itemY, windowRect.width - 48f, 56f), string.Empty, isSelected ? CommanderUiTheme.SelectedButton : CommanderUiTheme.Panel);

            string stateBadge = sec.SecurityState switch
            {
                SectorSecurityState.Secure => "[ 🟢 SECURE ]",
                SectorSecurityState.Contested => "[ 🟡 CONTESTED ]",
                SectorSecurityState.Hostile => "[ 🔴 HOSTILE ]",
                _ => "[ UNKNOWN ]"
            };

            GUI.Label(new Rect(10f, itemY + 6f, 200f, 20f), $"{sec.Name} {stateBadge}", CommanderUiTheme.Header);
            GUI.Label(new Rect(10f, itemY + 28f, windowRect.width - 180f, 20f),
                $"Directive: {sec.ActiveDirective} | Forces: {sec.FriendlyStrengthEstimate} Friend / {sec.EnemyStrengthEstimate} Enemy",
                CommanderUiTheme.MutedLabel);

            if (GUI.Button(new Rect(windowRect.width - 140f, itemY + 12f, 82f, 32f), isSelected ? "FOCUSED" : "SELECT", isSelected ? CommanderUiTheme.PrimaryButton : CommanderUiTheme.Button))
            {
                sectorService.SelectSector(sec.Id);
                tacticalMapService.JumpCameraToPosition(sec.CenterPosition);
            }

            itemY += 62f;
        }

        GUI.EndScrollView();
        y += scrollHeight + 14f;

        // Directive Action Card for Selected Sector
        TheaterSector? selected = sectorService.SelectedSector;
        if (selected != null)
        {
            GUI.Box(new Rect(12f, y, windowRect.width - 24f, 180f), string.Empty, CommanderUiTheme.Panel);
            GUI.Label(new Rect(24f, y + 8f, windowRect.width - 48f, 22f), $"DIRECTIVE FOR: {selected.Name.ToUpperInvariant()}", CommanderUiTheme.Header);

            float btnY = y + 36f;
            float btnW = (windowRect.width - 64f) * 0.5f;

            if (GUI.Button(new Rect(24f, btnY, btnW, 36f), "⚔️ ADVANCE & SECURE",
                selected.ActiveDirective == SectorDirective.AdvanceAndSecure ? CommanderUiTheme.PrimaryButton : CommanderUiTheme.Button))
            {
                sectorService.SetSectorDirective(selected.Id, SectorDirective.AdvanceAndSecure);
            }

            if (GUI.Button(new Rect(32f + btnW, btnY, btnW, 36f), "🛡️ HOLD & DEFEND",
                selected.ActiveDirective == SectorDirective.HoldAndDefend ? CommanderUiTheme.PrimaryButton : CommanderUiTheme.Button))
            {
                sectorService.SetSectorDirective(selected.Id, SectorDirective.HoldAndDefend);
            }

            btnY += 42f;
            if (GUI.Button(new Rect(24f, btnY, btnW, 36f), "📡 RECON & HARASS",
                selected.ActiveDirective == SectorDirective.ReconAndHarass ? CommanderUiTheme.PrimaryButton : CommanderUiTheme.Button))
            {
                sectorService.SetSectorDirective(selected.Id, SectorDirective.ReconAndHarass);
            }

            if (GUI.Button(new Rect(32f + btnW, btnY, btnW, 36f), "↩️ TACTICAL FALLBACK",
                selected.ActiveDirective == SectorDirective.TacticalFallback ? CommanderUiTheme.DangerButton : CommanderUiTheme.Button))
            {
                sectorService.SetSectorDirective(selected.Id, SectorDirective.TacticalFallback);
            }

            btnY += 46f;
            // Quick Air Tasking Order to Sector
            if (GUI.Button(new Rect(24f, btnY, windowRect.width - 48f, 34f), "🚀 DISPATCH AIR TASKING ORDER TO SECTOR", CommanderUiTheme.PrimaryButton))
            {
                airCommandService.SelectMode(CommanderAirCommandService.AirCommandMode.AirGuard);
                airCommandService.CompleteAreaSelection(selected.CenterPosition);
            }

            y += 194f;
        }

        // Battlegroup Status Summary
        IReadOnlyList<TaskForce> taskForces = battlegroupService.TaskForces;
        if (taskForces.Count > 0)
        {
            GUI.Label(new Rect(16f, y, windowRect.width - 32f, 20f), "AUTONOMOUS COMBINED-ARMS BATTLEGROUPS", CommanderUiTheme.MutedLabel);
            y += 22f;
            for (int t = 0; t < taskForces.Count; t++)
            {
                TaskForce tf = taskForces[t];
                GUI.Label(new Rect(16f, y, windowRect.width - 32f, 20f),
                    $"• {tf.Name}: {tf.VanguardUnits.Count} MBT/IFV | {tf.AirDefenseUnits.Count} AA Screen | {tf.SupportUnits.Count} Support",
                    CommanderUiTheme.Label);
                y += 20f;
            }
        }

        GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 40f, 34f));
    }
}
