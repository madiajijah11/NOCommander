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

            Vector3 unitWorldPos = unit.transform.position;
            bool hasUnitScreen = CommanderGameAccess.TryGetWorldMarkerState(unit.transform.GlobalPosition(), camera, out Vector3 unitScreenPos, out _);
            Vector2 unitGuiPoint = hasUnitScreen ? CommanderUiScale.ScreenToGui(unitScreenPos) : Vector2.zero;

            // Attack Line
            if (moveService.TryGetFocusAttackTarget(unit, out Unit target) && target != null && !target.disabled)
            {
                if (drawnAttackTargets.Add(target))
                {
                    DrawLargeMarker(camera, target.transform.GlobalPosition(), "ATTACK", new Color(1f, 0.25f, 0.2f, 0.95f));
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
                        DrawMarker(camera, wpPos, $"WAYPOINT {wp + 1}", new Color(0.95f, 0.85f, 0.3f, 0.85f));

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
                    DrawLargeMarker(camera, fob.transform.GlobalPosition(), "FOB LOGISTICS", new Color(0.2f, 0.95f, 0.5f, 0.95f));
                }
            }
        }

        // 4. Cursor Previews & Interaction Cues
        if (CommanderCheatService.Instance?.AwaitingPlacement == true && CommanderCheatService.Instance.PendingSpawnDefinition != null)
        {
            var cheat = CommanderCheatService.Instance;
            string label = "SPAWN: " + cheat.PendingSpawnDefinition.unitName + " (" + (cheat.SpawnAsEnemy ? "ENEMY" : "FRIENDLY") + ")\nHDG: " + Mathf.RoundToInt(cheat.PlacementHeading).ToString("000") + "°  [Scroll to Rotate]";
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
            DrawCursorMarker("LZ", new Color(0.35f, 0.9f, 0.42f, 0.95f));
        }

        // 5. SAM Sites Analysis & Supply Routes
        samSiteAnalyzerService.CopyProposalSites(samSiteProposals);
        for (int i = 0; i < samSiteProposals.Count; i++)
        {
            DrawLargeMarker(
                camera,
                samSiteProposals[i].Position,
                $"SAM SITE {i + 1}",
                new Color(0.1f, 0.82f, 1f, 0.95f));
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

    private static void DrawCursorMarker(string label, Color color)
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 guiPoint = CommanderUiScale.ScreenToGui(mousePos);

        GUIStyle style = CommanderUiTheme.Panel;
        float width = GetMarkerWidth(label, style, 48f);
        float height = GetMarkerHeight(label, style, width, 24f);
        Rect marker = new(guiPoint.x + 16f, guiPoint.y - height * 0.5f, width, height);

        Color previous = GUI.color;
        GUI.color = color;
        GUI.Box(marker, label, style);
        CommanderUiTheme.DrawFrame(marker, 1.5f);
        GUI.color = previous;
    }

    private static void DrawMarker(Camera camera, GlobalPosition position, string label, Color color)
    {
        if (!CommanderGameAccess.TryGetWorldMarkerState(position, camera, out Vector3 screenPoint, out _))
        {
            return;
        }

        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPoint);
        GUIStyle style = CommanderUiTheme.Panel;
        float width = GetMarkerWidth(label, style, 48f);
        float height = GetMarkerHeight(label, style, width, 24f);
        Rect marker = new(guiPoint.x - width * 0.5f, guiPoint.y - height * 0.5f, width, height);

        Color previous = GUI.color;
        GUI.color = color;
        GUI.Box(marker, label, style);
        CommanderUiTheme.DrawFrame(marker, 1.5f);
        GUI.color = previous;
    }

    private static void DrawLargeMarker(Camera camera, GlobalPosition position, string label, Color color)
    {
        if (!CommanderGameAccess.TryGetWorldMarkerState(position, camera, out Vector3 screenPoint, out _))
        {
            return;
        }

        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPoint);
        GUIStyle style = CommanderUiTheme.Header;
        float width = GetMarkerWidth(label, style, 90f);
        float height = GetMarkerHeight(label, style, width, 38f);
        Rect marker = new(guiPoint.x - width * 0.5f, guiPoint.y - height * 0.5f, width, height);

        Color previous = GUI.color;
        GUI.color = color;
        GUI.Box(marker, label, style);
        CommanderUiTheme.DrawFrame(marker, 2f);
        GUI.color = previous;
    }

    private static float GetMarkerWidth(string label, GUIStyle style, float minimumWidth)
    {
        return Mathf.Max(minimumWidth, style.CalcSize(new GUIContent(label)).x + 18f);
    }

    private static float GetMarkerHeight(string label, GUIStyle style, float width, float minimumHeight)
    {
        float contentWidth = Mathf.Max(1f, width - style.padding.horizontal);
        float calculatedHeight = style.CalcHeight(new GUIContent(label), contentWidth);
        return Mathf.Max(minimumHeight, calculatedHeight + 4f);
    }
}
