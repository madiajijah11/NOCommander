using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderWorldMarkerRenderer
{
    private static Texture2D? lineTexture;

    private readonly CommanderSelectionService selectionService;
    private readonly CommanderMoveService moveService;
    private readonly CommanderSpawnService spawnService;
    private readonly CommanderSupplyHeliService supplyHeliService;
    private readonly CommanderSamSiteAnalyzerService samSiteAnalyzerService;
    private readonly CommanderSamSiteService samSiteService;
    private readonly List<GlobalPosition> deliveryTargets = new();
    private readonly List<GlobalPosition> supplyRoute = new();
    private readonly List<CommanderSamSiteAnalyzerService.SiteLayoutMarker> samSiteLayout = new();
    private readonly List<CommanderSamSiteAnalyzerService.SiteCandidate> samSiteProposals = new();
    private readonly List<GlobalPosition> queuedWaypointsScratch = new();
    private readonly List<GlobalPosition> patrolRouteScratch = new();
    private readonly HashSet<Unit> drawnAttackTargets = new();

    internal CommanderWorldMarkerRenderer(
        CommanderSelectionService selectionService,
        CommanderMoveService moveService,
        CommanderSpawnService spawnService,
        CommanderSupplyHeliService supplyHeliService,
        CommanderSamSiteAnalyzerService samSiteAnalyzerService,
        CommanderSamSiteService samSiteService)
    {
        this.selectionService = selectionService;
        this.moveService = moveService;
        this.spawnService = spawnService;
        this.supplyHeliService = supplyHeliService;
        this.samSiteAnalyzerService = samSiteAnalyzerService;
        this.samSiteService = samSiteService;
    }

    internal void Draw(bool supplyWindowVisible)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        Camera? camera = SceneSingleton<CameraStateManager>.i?.mainCamera;
        if (camera == null)
        {
            return;
        }

        drawnAttackTargets.Clear();
        int selectedCount = selectionService.SelectedUnits.Count;

        // 1. Draw Paths & Vector Lines for Selected Units
        for (int i = 0; i < selectedCount; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];
            if (unit == null || unit.disabled) continue;

            bool hasUnitScreen = CommanderGameAccess.TryGetWorldMarkerState(unit.transform.GlobalPosition(), camera, out Vector3 unitScreenPos, out _);
            Vector2 unitGuiPoint = hasUnitScreen ? CommanderUiScale.ScreenToGui(unitScreenPos) : Vector2.zero;

            // Attack Line
            if (moveService.TryGetFocusAttackTarget(unit, out Unit target) && target != null && !target.disabled)
            {
                if (drawnAttackTargets.Add(target))
                {
                    DrawMarker(camera, target.transform.GlobalPosition(), "ATTACK", new Color(1f, 0.25f, 0.2f, 0.95f), large: true);
                }

                if (hasUnitScreen && CommanderGameAccess.TryGetWorldMarkerState(target.transform.GlobalPosition(), camera, out Vector3 tgtScreen, out _))
                {
                    Vector2 tgtGuiPoint = CommanderUiScale.ScreenToGui(tgtScreen);
                    DrawScreenLine(unitGuiPoint, tgtGuiPoint, new Color(1f, 0.2f, 0.15f, 0.75f), 2.5f);
                }
            }
            // Guard / Escort Line
            else if (moveService.TryGetGuardTarget(unit, out Unit guardTarget) && guardTarget != null && !guardTarget.disabled)
            {
                DrawMarker(camera, guardTarget.transform.GlobalPosition(), "GUARD", new Color(0.25f, 0.95f, 0.5f, 0.9f));

                if (hasUnitScreen && CommanderGameAccess.TryGetWorldMarkerState(guardTarget.transform.GlobalPosition(), camera, out Vector3 guardScreen, out _))
                {
                    Vector2 guardGuiPoint = CommanderUiScale.ScreenToGui(guardScreen);
                    DrawScreenLine(unitGuiPoint, guardGuiPoint, new Color(0.25f, 0.95f, 0.5f, 0.7f), 2f);
                }
            }
            // Patrol Loop Lines
            else if (moveService.TryGetPatrolRoute(unit, patrolRouteScratch) && patrolRouteScratch.Count >= 2)
            {
                Vector2 prevPoint = Vector2.zero;
                bool hasPrev = false;

                for (int p = 0; p < patrolRouteScratch.Count; p++)
                {
                    GlobalPosition pt = patrolRouteScratch[p];
                    DrawMarker(camera, pt, $"PATROL {p + 1}", new Color(0.35f, 0.88f, 0.95f, 0.9f));

                    if (CommanderGameAccess.TryGetWorldMarkerState(pt, camera, out Vector3 ptScreen, out _))
                    {
                        Vector2 ptGui = CommanderUiScale.ScreenToGui(ptScreen);
                        if (hasPrev)
                        {
                            DrawScreenLine(prevPoint, ptGui, new Color(0.35f, 0.88f, 0.95f, 0.75f), 2f);
                        }
                        else if (hasUnitScreen)
                        {
                            DrawScreenLine(unitGuiPoint, ptGui, new Color(0.35f, 0.88f, 0.95f, 0.5f), 1.5f);
                        }
                        prevPoint = ptGui;
                        hasPrev = true;
                    }
                }
            }
            // Move Destination & Sequential Waypoints Lines
            else if (moveService.TryGetPlayerDestination(unit, out GlobalPosition destination))
            {
                DrawMarker(camera, destination, "MOVE", new Color(0.2f, 0.85f, 0.82f, 0.9f));

                Vector2 prevWpGui = Vector2.zero;
                bool hasPrevWp = false;

                if (CommanderGameAccess.TryGetWorldMarkerState(destination, camera, out Vector3 destScreen, out _))
                {
                    Vector2 destGuiPoint = CommanderUiScale.ScreenToGui(destScreen);
                    if (hasUnitScreen)
                    {
                        DrawScreenLine(unitGuiPoint, destGuiPoint, new Color(0.2f, 0.85f, 0.82f, 0.75f), 2.5f);
                    }
                    prevWpGui = destGuiPoint;
                    hasPrevWp = true;
                }

                // Chained Waypoints
                if (moveService.TryGetQueuedWaypoints(unit, queuedWaypointsScratch))
                {
                    for (int wp = 0; wp < queuedWaypointsScratch.Count; wp++)
                    {
                        GlobalPosition wpPos = queuedWaypointsScratch[wp];
                        DrawMarker(camera, wpPos, $"WP {wp + 1}", new Color(0.95f, 0.85f, 0.3f, 0.85f));

                        if (CommanderGameAccess.TryGetWorldMarkerState(wpPos, camera, out Vector3 wpScreen, out _))
                        {
                            Vector2 wpGuiPoint = CommanderUiScale.ScreenToGui(wpScreen);
                            if (hasPrevWp)
                            {
                                DrawScreenLine(prevWpGui, wpGuiPoint, new Color(0.95f, 0.85f, 0.3f, 0.75f), 2f);
                            }
                            prevWpGui = wpGuiPoint;
                            hasPrevWp = true;
                        }
                    }
                }
            }
        }

        // 2. Depot Rally Points
        if (spawnService.SelectedDepot != null && spawnService.TryGetSelectedRallyPoint(out GlobalPosition rallyPoint))
        {
            DrawMarker(camera, rallyPoint, "RALLY", new Color(0.95f, 0.78f, 0.22f, 0.9f));

            if (CommanderGameAccess.TryGetWorldMarkerState(spawnService.SelectedDepot.transform.GlobalPosition(), camera, out Vector3 depotScreen, out _)
                && CommanderGameAccess.TryGetWorldMarkerState(rallyPoint, camera, out Vector3 rallyScreen, out _))
            {
                DrawScreenLine(
                    CommanderUiScale.ScreenToGui(depotScreen),
                    CommanderUiScale.ScreenToGui(rallyScreen),
                    new Color(0.95f, 0.78f, 0.22f, 0.6f),
                    2f);
            }
        }

        // 3. Deployed FOB Logistics Markers
        if (CommanderForwardOutpostService.Instance?.DeployedFobs != null)
        {
            foreach (Unit fob in CommanderForwardOutpostService.Instance.DeployedFobs)
            {
                if (fob != null && !fob.disabled)
                {
                    DrawMarker(camera, fob.transform.GlobalPosition(), "FOB LOGISTICS", new Color(0.2f, 0.95f, 0.5f, 0.95f), large: true);
                }
            }
        }

        // 4. Cursor Previews & Interaction Cues
        if (CommanderCheatService.Instance?.AwaitingPlacement == true && CommanderCheatService.Instance.PendingSpawnDefinition != null)
        {
            var cheat = CommanderCheatService.Instance;
            string label = "SPAWN: " + cheat.PendingSpawnDefinition.unitName + " (" + (cheat.SpawnAsEnemy ? "ENEMY" : "FRIENDLY") + ") | HDG: " + Mathf.RoundToInt(cheat.PlacementHeading).ToString("000") + "°";
            Color col = cheat.SpawnAsEnemy ? new Color(1f, 0.3f, 0.25f, 0.95f) : new Color(0.25f, 0.9f, 0.95f, 0.95f);
            DrawCursorMarker(label, col);
        }

        if (moveService.AwaitingGuardSelection)
        {
            DrawCursorMarker("SELECT GUARD TARGET", new Color(0.25f, 0.95f, 0.5f, 0.95f));
        }

        if (moveService.AwaitingBarrageSelection)
        {
            DrawCursorMarker("BARRAGE TARGET AREA", new Color(1f, 0.45f, 0.15f, 0.95f));
        }

        if (moveService.AwaitingAttackMoveSelection)
        {
            DrawCursorMarker("ATTACK MOVE TARGET", new Color(1f, 0.35f, 0.2f, 0.95f));
        }

        if (supplyHeliService.AwaitingTargetSelection)
        {
            DrawCursorMarker("LZ DESTINATION", new Color(0.35f, 0.9f, 0.42f, 0.95f));
        }

        // 5. SAM Sites Analysis & Supply Routes
        samSiteAnalyzerService.CopyProposalSites(samSiteProposals);
        for (int i = 0; i < samSiteProposals.Count; i++)
        {
            DrawMarker(
                camera,
                samSiteProposals[i].Position,
                $"SAM SITE {i + 1}",
                new Color(0.1f, 0.82f, 1f, 0.95f),
                large: true);
        }

        samSiteAnalyzerService.CopyVisibleActiveLayout(samSiteLayout);
        for (int i = 0; i < samSiteLayout.Count; i++)
        {
            CommanderSamSiteAnalyzerService.SiteLayoutMarker marker = samSiteLayout[i];
            if (marker.Role == CommanderSamSiteAnalyzerService.SiteUnitRole.ControlTower)
            {
                continue;
            }
            DrawMarker(camera, marker.Position, GetSamLabel(marker.Role), GetSamColor(marker.Role));
        }

        samSiteService.CopyVisibleSupplyRoute(supplyRoute);
        Vector2 prevSupplyGui = Vector2.zero;
        bool hasPrevSupply = false;

        for (int i = 0; i < supplyRoute.Count; i++)
        {
            string label = i == 0
                ? "AIRBASE"
                : i == supplyRoute.Count - 1 ? "SAM SITE" : $"ROUTE {i}";
            DrawMarker(camera, supplyRoute[i], label, new Color(0.2f, 0.78f, 1f, 0.92f));

            if (CommanderGameAccess.TryGetWorldMarkerState(supplyRoute[i], camera, out Vector3 supScreen, out _))
            {
                Vector2 supGui = CommanderUiScale.ScreenToGui(supScreen);
                if (hasPrevSupply)
                {
                    DrawScreenLine(prevSupplyGui, supGui, new Color(0.2f, 0.78f, 1f, 0.6f), 1.5f);
                }
                prevSupplyGui = supGui;
                hasPrevSupply = true;
            }
        }

        if (!supplyWindowVisible)
        {
            return;
        }

        supplyHeliService.CopyActiveDeliveryTargets(deliveryTargets);
        for (int i = 0; i < deliveryTargets.Count; i++)
        {
            DrawMarker(camera, deliveryTargets[i], "LZ", new Color(0.35f, 0.9f, 0.42f, 0.9f));
        }

        // 6. Active Smoke Screens
        if (CommanderSmokeCountermeasuresService.Instance?.ActiveSmokes != null)
        {
            var smokes = CommanderSmokeCountermeasuresService.Instance.ActiveSmokes;
            for (int s = 0; s < smokes.Count; s++)
            {
                var smoke = smokes[s];
                DrawGroundCircle(camera, smoke.Position, smoke.Radius, new Color(0.9f, 0.9f, 0.95f, 0.35f), 16, 2.5f);
                DrawMarker(camera, smoke.Position.ToGlobalPosition(), "SMOKE SCREEN", new Color(0.85f, 0.85f, 0.9f, 0.8f));
            }
        }

        // 7. Counter-Battery Radar Pings
        if (CommanderCounterBatteryRadarService.Instance?.ActivePings != null)
        {
            var pings = CommanderCounterBatteryRadarService.Instance.ActivePings;
            for (int p = 0; p < pings.Count; p++)
            {
                var ping = pings[p];
                DrawMarker(camera, ping.Position.ToGlobalPosition(), "COUNTER-BATTERY PINPOINT", new Color(1f, 0.2f, 0.15f, 0.95f), large: true);
                DrawGroundCircle(camera, ping.Position, 60f, new Color(1f, 0.2f, 0.15f, 0.6f), 16, 2f);
            }
        }

        // 8. Ground JTAC Close Air Support Pinpoint
        if (CommanderAlliedAiService.HasActiveJtacTarget)
        {
            DrawMarker(camera, CommanderAlliedAiService.JtacTargetPosition, "JTAC CAS TARGET", new Color(1f, 0.65f, 0.1f, 0.95f), large: true);
            DrawGroundCircle(camera, CommanderAlliedAiService.JtacTargetPosition.ToLocalPosition(), 80f, new Color(1f, 0.65f, 0.1f, 0.65f), 16, 2.2f);
        }
    }

    internal static void DrawScreenLine(Vector2 pointA, Vector2 pointB, Color color, float width = 2f)
    {
        Vector2 diff = pointB - pointA;
        float length = diff.magnitude;
        if (length < 1f)
        {
            return;
        }

        lineTexture ??= Texture2D.whiteTexture;

        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        Matrix4x4 matrix = GUI.matrix;
        Color previousColor = GUI.color;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, pointA);
        GUI.DrawTexture(new Rect(pointA.x, pointA.y - width * 0.5f, length, width), lineTexture);
        GUI.matrix = matrix;
        GUI.color = previousColor;
    }

    internal static void DrawTacticalReticle(Vector2 center, Color color, float size = 14f)
    {
        lineTexture ??= Texture2D.whiteTexture;

        Color prev = GUI.color;
        GUI.color = color;

        // Diamond (Rotated Square Outline)
        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, center);
        float half = size * 0.5f;

        GUI.DrawTexture(new Rect(center.x - half, center.y - half, size, 1.5f), lineTexture);
        GUI.DrawTexture(new Rect(center.x - half, center.y + half - 1.5f, size, 1.5f), lineTexture);
        GUI.DrawTexture(new Rect(center.x - half, center.y - half, 1.5f, size), lineTexture);
        GUI.DrawTexture(new Rect(center.x + half - 1.5f, center.y - half, 1.5f, size), lineTexture);
        // Center pip
        GUI.DrawTexture(new Rect(center.x - 1.5f, center.y - 1.5f, 3f, 3f), lineTexture);

        GUI.matrix = matrix;
        GUI.color = prev;
    }

    private static void DrawMarker(Camera camera, GlobalPosition position, string? label, Color color, bool large = false)
    {
        if (!CommanderGameAccess.TryGetWorldMarkerState(position, camera, out Vector3 screenPoint, out _))
        {
            return;
        }

        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPoint);
        float reticleSize = large ? 18f : 13f;
        DrawTacticalReticle(guiPoint, color, reticleSize);

        if (!string.IsNullOrEmpty(label))
        {
            GUIContent content = new(label);
            Vector2 textSize = CommanderUiTheme.MutedLabel.CalcSize(content);
            float badgeW = textSize.x + 10f;
            float badgeH = 17f;
            Rect badge = new(guiPoint.x - badgeW * 0.5f, guiPoint.y - (large ? 16f : 12f) - badgeH, badgeW, badgeH);

            Color prev = GUI.color;
            GUI.color = new Color(0.04f, 0.08f, 0.10f, 0.82f);
            GUI.DrawTexture(badge, Texture2D.whiteTexture);

            GUI.color = color;
            CommanderUiTheme.DrawFrame(badge, 1f);

            GUIStyle labelStyle = large ? CommanderUiTheme.Header : CommanderUiTheme.MutedLabel;
            GUI.Label(new Rect(badge.x + 5f, badge.y - 1f, badge.width - 10f, badge.height), label, labelStyle);
            GUI.color = prev;
        }
    }

    private static void DrawCursorMarker(string label, Color color)
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 guiPoint = CommanderUiScale.ScreenToGui(mousePos);

        GUIContent content = new(label);
        Vector2 textSize = CommanderUiTheme.MutedLabel.CalcSize(content);
        float width = textSize.x + 14f;
        float height = 22f;
        Rect marker = new(guiPoint.x + 14f, guiPoint.y - height * 0.5f, width, height);

        Color prev = GUI.color;
        GUI.color = new Color(0.04f, 0.08f, 0.10f, 0.85f);
        GUI.DrawTexture(marker, Texture2D.whiteTexture);

        GUI.color = color;
        CommanderUiTheme.DrawFrame(marker, 1.2f);
        GUI.Label(new Rect(marker.x + 7f, marker.y, marker.width - 14f, marker.height), label, CommanderUiTheme.MutedLabel);
        GUI.color = prev;
    }

    private static void DrawGroundCircle(Camera camera, Vector3 center, float radius, Color color, int segments = 20, float thickness = 1.8f)
    {
        Vector2 prevGui = Vector2.zero;
        bool hasPrev = false;
        Vector2 firstGui = Vector2.zero;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 worldPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            if (CommanderGameAccess.TryGetWorldMarkerState(worldPoint.ToGlobalPosition(), camera, out Vector3 screenPoint, out _))
            {
                Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPoint);
                if (hasPrev)
                {
                    DrawScreenLine(prevGui, guiPoint, color, thickness);
                }
                else
                {
                    firstGui = guiPoint;
                }
                prevGui = guiPoint;
                hasPrev = true;
            }
            else
            {
                hasPrev = false;
            }
        }

        if (hasPrev && firstGui != Vector2.zero)
        {
            DrawScreenLine(prevGui, firstGui, color, thickness);
        }
    }

    private static string GetSamLabel(CommanderSamSiteAnalyzerService.SiteUnitRole role)
    {
        return role switch
        {
            CommanderSamSiteAnalyzerService.SiteUnitRole.Radar => "RADAR",
            CommanderSamSiteAnalyzerService.SiteUnitRole.Platform => "PLATFORM",
            CommanderSamSiteAnalyzerService.SiteUnitRole.ControlTower => "SITE CORE",
            CommanderSamSiteAnalyzerService.SiteUnitRole.Gun23mm => "23MM",
            CommanderSamSiteAnalyzerService.SiteUnitRole.Irm => "IRM",
            CommanderSamSiteAnalyzerService.SiteUnitRole.StratoLauncher => "STRATOLANCE",
            CommanderSamSiteAnalyzerService.SiteUnitRole.Ammo => "AMMO",
            CommanderSamSiteAnalyzerService.SiteUnitRole.FireControl => "FIRE CTRL",
            _ => "SITE",
        };
    }

    private static Color GetSamColor(CommanderSamSiteAnalyzerService.SiteUnitRole role)
    {
        return role switch
        {
            CommanderSamSiteAnalyzerService.SiteUnitRole.Radar => new Color(0.3f, 0.85f, 1f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.Platform => new Color(0.2f, 0.9f, 0.4f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.ControlTower => new Color(1f, 0.8f, 0.1f, 0.98f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.Gun23mm => new Color(1f, 0.45f, 0.1f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.Irm => new Color(1f, 0.2f, 0.08f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.StratoLauncher => new Color(0.9f, 0.15f, 0.05f, 0.98f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.Ammo => new Color(0.9f, 0.85f, 0.2f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.FireControl => new Color(0.4f, 0.7f, 1f, 0.95f),
            _ => new Color(0.6f, 0.8f, 0.9f, 0.9f),
        };
    }
}
