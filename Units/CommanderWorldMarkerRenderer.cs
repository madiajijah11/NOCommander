using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderWorldMarkerRenderer
{
    private const int MaxOverheadUnitsToRender = 8;
    private const float HealthRefreshInterval = 0.5f;

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

    private readonly Dictionary<Unit, IRepairable[]> cachedRepairables = new();
    private readonly Dictionary<Unit, float> cachedHealthPcts = new();
    private readonly Dictionary<Unit, float> cachedAmmoPcts = new();
    private float nextHealthRefreshTime;

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

        // Refresh cached health data at low frequency (0.5s), NEVER per-frame
        if (Time.unscaledTime >= nextHealthRefreshTime)
        {
            nextHealthRefreshTime = Time.unscaledTime + HealthRefreshInterval;
            RefreshSelectedHealthCache();
        }

        drawnAttackTargets.Clear();
        int selectedCount = selectionService.SelectedUnits.Count;
        for (int i = 0; i < selectedCount; i++)
        {
            Unit unit = selectionService.SelectedUnits[i];

            if (i < MaxOverheadUnitsToRender)
            {
                DrawOverheadBars(camera, unit);
            }

            if (moveService.TryGetFocusAttackTarget(unit, out Unit target) && target != null && !target.disabled)
            {
                if (drawnAttackTargets.Add(target))
                {
                    DrawLargeMarker(camera, target.transform.GlobalPosition(), "ATTACK", new Color(1f, 0.25f, 0.2f, 0.95f));
                }
            }
            else if (moveService.TryGetGuardTarget(unit, out Unit guardTarget) && guardTarget != null && !guardTarget.disabled)
            {
                DrawMarker(camera, guardTarget.transform.GlobalPosition(), "GUARD", new Color(0.25f, 0.95f, 0.5f, 0.9f));
            }
            else if (moveService.TryGetPatrolRoute(unit, patrolRouteScratch))
            {
                for (int p = 0; p < patrolRouteScratch.Count; p++)
                {
                    DrawMarker(camera, patrolRouteScratch[p], $"PATROL {p + 1}", new Color(0.35f, 0.88f, 0.95f, 0.9f));
                }
            }
            else if (moveService.TryGetPlayerDestination(unit, out GlobalPosition destination))
            {
                DrawMarker(camera, destination, "MOVE", new Color(0.2f, 0.85f, 0.82f, 0.9f));
            }

            if (moveService.TryGetQueuedWaypoints(unit, queuedWaypointsScratch))
            {
                for (int wp = 0; wp < queuedWaypointsScratch.Count; wp++)
                {
                    DrawMarker(camera, queuedWaypointsScratch[wp], $"WAYPOINT {wp + 1}", new Color(0.95f, 0.85f, 0.3f, 0.85f));
                }
            }
        }

        if (spawnService.SelectedDepot != null && spawnService.TryGetSelectedRallyPoint(out GlobalPosition rallyPoint))
        {
            DrawMarker(camera, rallyPoint, "RALLY", new Color(0.95f, 0.78f, 0.22f, 0.9f));
        }

        if (CommanderCheatService.Instance?.AwaitingPlacement == true && CommanderCheatService.Instance.PendingSpawnDefinition != null)
        {
            string label = $"SPAWN: {CommanderCheatService.Instance.PendingSpawnDefinition.unitName} ({(CommanderCheatService.Instance.SpawnAsEnemy ? "ENEMY" : "FRIENDLY")})";
            Color col = CommanderCheatService.Instance.SpawnAsEnemy ? new Color(1f, 0.25f, 0.2f, 0.95f) : new Color(0.2f, 0.85f, 0.9f, 0.95f);
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

        if (supplyHeliService.AwaitingTargetSelection)
        {
            DrawCursorMarker("LZ", new Color(0.35f, 0.9f, 0.42f, 0.95f));
        }

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
        for (int i = 0; i < supplyRoute.Count; i++)
        {
            string label = i == 0
                ? "AIRBASE"
                : i == supplyRoute.Count - 1 ? "SAM SITE" : $"ROUTE {i}";
            DrawMarker(camera, supplyRoute[i], label, new Color(0.2f, 0.78f, 1f, 0.92f));
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

    private void RefreshSelectedHealthCache()
    {
        IReadOnlyList<Unit> selected = selectionService.SelectedUnits;
        int count = Mathf.Min(selected.Count, MaxOverheadUnitsToRender);

        for (int i = 0; i < count; i++)
        {
            Unit unit = selected[i];
            if (unit == null || unit.disabled)
            {
                continue;
            }

            if (!cachedRepairables.TryGetValue(unit, out IRepairable[] repairables))
            {
                repairables = unit.GetComponentsInChildren<IRepairable>(true);
                cachedRepairables[unit] = repairables;
            }

            if (repairables.Length > 0)
            {
                int damagedCount = 0;
                for (int r = 0; r < repairables.Length; r++)
                {
                    if (repairables[r] != null && repairables[r].NeedsRepair())
                    {
                        damagedCount++;
                    }
                }
                cachedHealthPcts[unit] = Mathf.Clamp01(1f - ((float)damagedCount / repairables.Length));
            }
            else
            {
                cachedHealthPcts[unit] = 1f;
            }

            if (unit.weaponStations != null && unit.weaponStations.Count > 0)
            {
                float currentAmmo = 0f;
                float maxAmmo = 0f;
                for (int s = 0; s < unit.weaponStations.Count; s++)
                {
                    WeaponStation station = unit.weaponStations[s];
                    if (station?.Weapons == null) continue;
                    for (int w = 0; w < station.Weapons.Count; w++)
                    {
                        Weapon weapon = station.Weapons[w];
                        if (weapon != null)
                        {
                            currentAmmo += weapon.ammo;
                            maxAmmo += Mathf.Max(1, weapon.GetFullAmmo());
                        }
                    }
                }
                cachedAmmoPcts[unit] = maxAmmo > 0f ? Mathf.Clamp01(currentAmmo / maxAmmo) : -1f;
            }
            else
            {
                cachedAmmoPcts[unit] = -1f;
            }
        }

        // Prune dead keys
        if (cachedRepairables.Count > 30)
        {
            List<Unit>? deadKeys = null;
            foreach (KeyValuePair<Unit, IRepairable[]> pair in cachedRepairables)
            {
                if (pair.Key == null || pair.Key.disabled)
                {
                    deadKeys ??= new List<Unit>();
                    deadKeys.Add(pair.Key);
                }
            }
            if (deadKeys != null)
            {
                for (int k = 0; k < deadKeys.Count; k++)
                {
                    cachedRepairables.Remove(deadKeys[k]);
                    cachedHealthPcts.Remove(deadKeys[k]);
                    cachedAmmoPcts.Remove(deadKeys[k]);
                }
            }
        }
    }

    private void DrawOverheadBars(Camera camera, Unit unit)
    {
        if (unit == null || unit.disabled)
        {
            return;
        }

        if (!CommanderGameAccess.TryGetWorldMarkerState(unit, camera, out Vector3 screenPos, out float scale))
        {
            return;
        }

        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screenPos);
        float barWidth = Mathf.Clamp(50f * scale, 32f, 64f);
        float barHeight = Mathf.Clamp(5f * scale, 3f, 6f);
        float barX = guiPoint.x - barWidth * 0.5f;
        float barY = guiPoint.y - 26f * scale;

        float healthPct = cachedHealthPcts.TryGetValue(unit, out float hp) ? hp : 1f;
        float ammoPct = cachedAmmoPcts.TryGetValue(unit, out float ap) ? ap : -1f;

        // Background Bar
        Rect bgRect = new(barX - 1f, barY - 1f, barWidth + 2f, barHeight + 2f);
        Color oldColor = GUI.color;
        GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.85f);
        GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

        // Health Fill Bar
        Color hpColor = healthPct > 0.6f
            ? new Color(0.2f, 0.85f, 0.35f, 0.95f)
            : healthPct > 0.3f
                ? new Color(0.95f, 0.78f, 0.15f, 0.95f)
                : new Color(0.95f, 0.25f, 0.2f, 0.95f);

        GUI.color = hpColor;
        GUI.DrawTexture(new Rect(barX, barY, barWidth * healthPct, barHeight), Texture2D.whiteTexture);

        // Ammo Bar if combat unit
        if (ammoPct >= 0f)
        {
            float ammoY = barY + barHeight + 2f;
            float ammoHeight = Mathf.Clamp(3f * scale, 2f, 4f);

            GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.85f);
            GUI.DrawTexture(new Rect(barX - 1f, ammoY - 1f, barWidth + 2f, ammoHeight + 2f), Texture2D.whiteTexture);

            GUI.color = new Color(0.25f, 0.75f, 0.95f, 0.9f);
            GUI.DrawTexture(new Rect(barX, ammoY, barWidth * ammoPct, ammoHeight), Texture2D.whiteTexture);
        }

        GUI.color = oldColor;
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
            _ => "SITE"
        };
    }

    private static Color GetSamColor(CommanderSamSiteAnalyzerService.SiteUnitRole role)
    {
        return role switch
        {
            CommanderSamSiteAnalyzerService.SiteUnitRole.Radar => new Color(1f, 0.78f, 0.12f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.Platform => new Color(0.72f, 0.7f, 1f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.ControlTower => new Color(0.6f, 0.78f, 1f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.Gun23mm => new Color(1f, 0.46f, 0.12f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.Irm => new Color(1f, 0.2f, 0.08f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.StratoLauncher => new Color(0.95f, 0.12f, 0.12f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.Ammo => new Color(0.35f, 1f, 0.35f, 0.95f),
            CommanderSamSiteAnalyzerService.SiteUnitRole.FireControl => new Color(0.1f, 0.9f, 1f, 0.95f),
            _ => Color.white
        };
    }

    private static void DrawCursorMarker(string label, Color color)
    {
        Vector2 guiPoint = CommanderUiScale.ScreenToGui(Input.mousePosition);
        GUIStyle style = CommanderUiTheme.Panel;
        float width = GetMarkerWidth(label, style, 60f);
        float height = GetMarkerHeight(label, style, width, 26f);
        Rect marker = new(guiPoint.x + 14f, guiPoint.y + 14f, width, height);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.Box(marker, label, style);
        CommanderUiTheme.DrawFrame(marker, 1f);
        GUI.color = previous;
    }

    private static void DrawMarker(Camera camera, GlobalPosition position, string label, Color color)
    {
        Vector3 world = position.ToLocalPosition();
        Vector3 screen = camera.WorldToScreenPoint(world);
        if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height)
        {
            return;
        }

        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screen);
        GUIStyle style = CommanderUiTheme.Panel;
        float width = GetMarkerWidth(label, style, 60f);
        float height = GetMarkerHeight(label, style, width, 26f);
        Rect marker = new(guiPoint.x - width * 0.5f, guiPoint.y - height * 0.5f, width, height);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.Box(marker, label, style);
        CommanderUiTheme.DrawFrame(marker, 1f);
        GUI.color = previous;
    }

    private static void DrawLargeMarker(Camera camera, GlobalPosition position, string label, Color color)
    {
        Vector3 screen = camera.WorldToScreenPoint(position.ToLocalPosition());
        if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height)
        {
            return;
        }

        Vector2 guiPoint = CommanderUiScale.ScreenToGui(screen);
        GUIStyle style = CommanderUiTheme.PrimaryButton;
        float width = GetMarkerWidth(label, style, 116f);
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
