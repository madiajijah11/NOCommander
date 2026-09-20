using System;
using System.Collections.Generic;
using System.Linq;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderCheatService
{
    internal enum EntityCategory
    {
        All = 0,
        Building = 1,
        Land = 2,
        Air = 3,
        Naval = 4
    }

    private readonly CommanderSelectionService selectionService;
    private readonly List<UnitDefinition> allDefinitions = new();
    private readonly List<UnitDefinition> filteredDefinitions = new();
    private readonly List<UnitDefinition> buildingDefinitions = new();
    private readonly List<UnitDefinition> landDefinitions = new();
    private readonly List<UnitDefinition> airDefinitions = new();
    private readonly List<UnitDefinition> navalDefinitions = new();

    private bool catalogInitialized;
    private float statusUntil;
    private string statusText = string.Empty;

    // 3D Placement & Hologram Preview
    private bool awaitingPlacement;
    private UnitDefinition? pendingSpawnDefinition;
    private bool spawnAsEnemy;
    private float placementHeading;
    private GameObject? ghostPreviewObject;

    internal static CommanderCheatService? Instance { get; private set; }

    internal bool GodModeEnabled { get; set; }
    internal bool FreeSpawningEnabled { get; set; }
    internal bool InfiniteAmmoEnabled { get; set; }
    internal bool FreezeAiEnabled { get; set; }

    internal bool AwaitingPlacement => awaitingPlacement && pendingSpawnDefinition != null;
    internal UnitDefinition? PendingSpawnDefinition => pendingSpawnDefinition;
    internal bool SpawnAsEnemy => spawnAsEnemy;
    internal float PlacementHeading => placementHeading;
    internal string StatusText => Time.unscaledTime <= statusUntil ? statusText : string.Empty;

    internal CommanderCheatService(CommanderSelectionService selectionService)
    {
        this.selectionService = selectionService;
        Instance = this;
    }

    internal static EntityCategory ResolveCategory(UnitDefinition def)
    {
        if (def == null || def.unitPrefab == null)
        {
            return EntityCategory.Building;
        }

        // 1. Check strong definition types first
        if (def is AircraftDefinition)
        {
            return EntityCategory.Air;
        }
        if (def is ShipDefinition)
        {
            return EntityCategory.Naval;
        }

        // 2. Check components on prefab (both root & children)
        if (def.unitPrefab.GetComponentInChildren<Aircraft>(true) != null)
        {
            return EntityCategory.Air;
        }
        if (def.unitPrefab.GetComponentInChildren<Ship>(true) != null)
        {
            return EntityCategory.Naval;
        }
        if (def is VehicleDefinition || def.unitPrefab.GetComponentInChildren<GroundVehicle>(true) != null)
        {
            return EntityCategory.Land;
        }

        // 3. Name-based heuristics for edge cases
        string name = !string.IsNullOrEmpty(def.unitName) ? def.unitName.ToLowerInvariant() : string.Empty;
        string prefabName = def.unitPrefab.name != null ? def.unitPrefab.name.ToLowerInvariant() : string.Empty;

        if (name.Contains("ship") || name.Contains("corvette") || name.Contains("destroyer") || name.Contains("frigate") || name.Contains("carrier") || name.Contains("barge")
            || prefabName.Contains("ship") || prefabName.Contains("corvette") || prefabName.Contains("destroyer") || prefabName.Contains("frigate") || prefabName.Contains("carrier"))
        {
            return EntityCategory.Naval;
        }

        if (name.Contains("plane") || name.Contains("jet") || name.Contains("heli") || name.Contains("aircraft") || name.Contains("drone") || name.Contains("gunship")
            || prefabName.Contains("plane") || prefabName.Contains("jet") || prefabName.Contains("heli") || prefabName.Contains("aircraft") || prefabName.Contains("drone"))
        {
            return EntityCategory.Air;
        }

        if (name.Contains("tank") || name.Contains("truck") || name.Contains("tractor") || name.Contains("vehicle") || name.Contains("car") || name.Contains("ifv") || name.Contains("apc") || name.Contains("trailer") || name.Contains("jacknife") || name.Contains("spaag")
            || prefabName.Contains("tank") || prefabName.Contains("truck") || prefabName.Contains("tractor") || prefabName.Contains("vehicle") || prefabName.Contains("car") || prefabName.Contains("ifv") || prefabName.Contains("apc") || prefabName.Contains("trailer") || prefabName.Contains("jacknife"))
        {
            return EntityCategory.Land;
        }

        // 4. Everything else is building / structure / installation
        return EntityCategory.Building;
    }

    internal static string GetCategoryLabel(UnitDefinition def)
    {
        string name = (!string.IsNullOrEmpty(def.unitName) ? def.unitName : def.name).ToLowerInvariant();
        EntityCategory cat = ResolveCategory(def);

        switch (cat)
        {
            case EntityCategory.Air:
                if (name.Contains("heli") || name.Contains("cricket") || name.Contains("tarantula")) return "AIR | HELICOPTER";
                if (name.Contains("medusa") || name.Contains("vtol") || name.Contains("cargo")) return "AIR | CARGO & VTOL";
                if (name.Contains("darkreach") || name.Contains("bomber") || name.Contains("strike")) return "AIR | HEAVY BOMBER";
                if (name.Contains("compass") || name.Contains("cas") || name.Contains("ground attack")) return "AIR | CLOSE AIR SUPPORT";
                if (name.Contains("revoker") || name.Contains("ifrit") || name.Contains("fighter") || name.Contains("intercept")) return "AIR | AIR SUPERIORITY";
                return "AIR | MULTIROLE AIRCRAFT";

            case EntityCategory.Naval:
                if (name.Contains("carrier") || name.Contains("flagship")) return "NAVAL | AIRCRAFT CARRIER";
                if (name.Contains("destroyer") || name.Contains("frigate")) return "NAVAL | GUIDED MISSILE WARSHIP";
                if (name.Contains("corvette") || name.Contains("patrol") || name.Contains("boat")) return "NAVAL | CORVETTE / PATROL";
                if (name.Contains("barge") || name.Contains("cargo") || name.Contains("supply")) return "NAVAL | SUPPLY & LOGISTICS";
                return "NAVAL | SURFACE WARSHIP";

            case EntityCategory.Land:
                if (name.Contains("tank") || name.Contains("mbt") || name.Contains("heavy")) return "LAND | MAIN BATTLE TANK";
                if (name.Contains("spaag") || name.Contains("aaa") || name.Contains("sam") || name.Contains("air defense")) return "LAND | AIR DEFENSE (AAA/SAM)";
                if (name.Contains("mrls") || name.Contains("artillery") || name.Contains("mortar") || name.Contains("howitzer")) return "LAND | ROCKET ARTILLERY";
                if (name.Contains("ifv") || name.Contains("apc") || name.Contains("scout") || name.Contains("light")) return "LAND | ARMORED FIGHTING VEHICLE";
                if (name.Contains("truck") || name.Contains("tractor") || name.Contains("rearm") || name.Contains("repair") || name.Contains("jacknife") || name.Contains("logistics") || name.Contains("trailer")) return "LAND | LOGISTICS & SUPPORT";
                return "LAND | COMBAT VEHICLE";

            case EntityCategory.Building:
            default:
                if (name.Contains("factory") || name.Contains("plant") || name.Contains("refinery") || name.Contains("industrial") || name.Contains("power")) return "BUILDING | INDUSTRY & REVENUE";
                if (name.Contains("hangar") || name.Contains("depot") || name.Contains("dock") || name.Contains("shipyard") || name.Contains("runway")) return "BUILDING | MILITARY SPAWNER";
                if (name.Contains("radar") || name.Contains("sam") || name.Contains("tower") || name.Contains("strato") || name.Contains("bunker") || name.Contains("gun")) return "BUILDING | AIR DEFENSE & RADAR";
                if (name.Contains("fob") || name.Contains("storage") || name.Contains("warehouse") || name.Contains("fuel") || name.Contains("ammo")) return "BUILDING | FIELD LOGISTICS";
                return "BUILDING | MILITARY STRUCTURE";
        }
    }

    internal void EnsureCatalogLoaded()
    {
        if (catalogInitialized && allDefinitions.Count > 0)
        {
            return;
        }

        allDefinitions.Clear();
        buildingDefinitions.Clear();
        landDefinitions.Clear();
        airDefinitions.Clear();
        navalDefinitions.Clear();

        UnitDefinition[] available = Resources.FindObjectsOfTypeAll<UnitDefinition>();
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < available.Length; i++)
        {
            UnitDefinition def = available[i];
            if (def == null || def.unitPrefab == null || string.IsNullOrWhiteSpace(def.unitName))
            {
                continue;
            }

            if (!seenNames.Add(def.unitName))
            {
                continue;
            }

            allDefinitions.Add(def);

            EntityCategory cat = ResolveCategory(def);
            switch (cat)
            {
                case EntityCategory.Air:
                    airDefinitions.Add(def);
                    break;
                case EntityCategory.Naval:
                    navalDefinitions.Add(def);
                    break;
                case EntityCategory.Land:
                    landDefinitions.Add(def);
                    break;
                case EntityCategory.Building:
                default:
                    buildingDefinitions.Add(def);
                    break;
            }
        }

        allDefinitions.Sort(static (a, b) => string.Compare(a.unitName, b.unitName, StringComparison.OrdinalIgnoreCase));
        buildingDefinitions.Sort(static (a, b) => string.Compare(a.unitName, b.unitName, StringComparison.OrdinalIgnoreCase));
        landDefinitions.Sort(static (a, b) => string.Compare(a.unitName, b.unitName, StringComparison.OrdinalIgnoreCase));
        airDefinitions.Sort(static (a, b) => string.Compare(a.unitName, b.unitName, StringComparison.OrdinalIgnoreCase));
        navalDefinitions.Sort(static (a, b) => string.Compare(a.unitName, b.unitName, StringComparison.OrdinalIgnoreCase));

        catalogInitialized = true;
    }

    internal IReadOnlyList<UnitDefinition> GetDefinitionsByCategory(int categoryIndex, string searchFilter)
    {
        EnsureCatalogLoaded();

        IReadOnlyList<UnitDefinition> source = categoryIndex switch
        {
            1 => buildingDefinitions,
            2 => landDefinitions,
            3 => airDefinitions,
            4 => navalDefinitions,
            _ => allDefinitions
        };

        if (string.IsNullOrWhiteSpace(searchFilter))
        {
            return source;
        }

        filteredDefinitions.Clear();
        for (int i = 0; i < source.Count; i++)
        {
            string catLabel = GetCategoryLabel(source[i]);
            if (source[i].unitName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                || catLabel.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                filteredDefinitions.Add(source[i]);
            }
        }

        return filteredDefinitions;
    }

    internal void BeginPlacement(UnitDefinition definition, bool asEnemy)
    {
        if (definition == null || definition.unitPrefab == null)
        {
            SetStatus("Invalid unit definition.");
            return;
        }

        DestroyGhostPreview();
        awaitingPlacement = true;
        pendingSpawnDefinition = definition;
        spawnAsEnemy = asEnemy;
        placementHeading = 0f;
        SetStatus("Select position in 3D world to spawn " + definition.unitName + " [Scroll to Rotate].");
    }

    internal void CancelPlacement()
    {
        DestroyGhostPreview();
        awaitingPlacement = false;
        pendingSpawnDefinition = null;
        SetStatus("Spawn placement cancelled.");
    }

    internal void UpdatePlacementPreview(Vector2 screenPosition)
    {
        if (!awaitingPlacement || pendingSpawnDefinition == null)
        {
            DestroyGhostPreview();
            return;
        }

        // Rotate with mouse scroll wheel or [ / ] keys
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            placementHeading = (placementHeading + scroll * 15f) % 360f;
            if (placementHeading < 0f) placementHeading += 360f;
        }

        if (Input.GetKey(KeyCode.LeftBracket))
        {
            placementHeading = (placementHeading - 90f * Time.unscaledDeltaTime) % 360f;
            if (placementHeading < 0f) placementHeading += 360f;
        }
        if (Input.GetKey(KeyCode.RightBracket))
        {
            placementHeading = (placementHeading + 90f * Time.unscaledDeltaTime) % 360f;
        }

        UnitDefinition def = pendingSpawnDefinition;
        bool isShip = ResolveCategory(def) == EntityCategory.Naval;

        bool hit = isShip
            ? CommanderGameAccess.TryRaycastWaterPosition(screenPosition, out GlobalPosition targetPos)
            : CommanderGameAccess.TryRaycastWorldPosition(screenPosition, out targetPos);

        if (!hit)
        {
            if (ghostPreviewObject != null)
            {
                ghostPreviewObject.SetActive(false);
            }
            return;
        }

        EnsureGhostPreviewCreated(def);

        if (ghostPreviewObject != null)
        {
            ghostPreviewObject.SetActive(true);
            Vector3 localPos = targetPos.ToLocalPosition() + def.spawnOffset;
            ghostPreviewObject.transform.position = localPos;
            ghostPreviewObject.transform.rotation = Quaternion.Euler(0f, placementHeading, 0f);
        }
    }

    private void EnsureGhostPreviewCreated(UnitDefinition def)
    {
        if (ghostPreviewObject != null || def.unitPrefab == null)
        {
            return;
        }

        try
        {
            ghostPreviewObject = UnityEngine.Object.Instantiate(def.unitPrefab);
            ghostPreviewObject.name = "NOC_GhostHologramPreview";

            // Strip/disable non-visual components
            MonoBehaviour[] scripts = ghostPreviewObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++)
            {
                scripts[i].enabled = false;
            }

            Collider[] colliders = ghostPreviewObject.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] rbs = ghostPreviewObject.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                rbs[i].isKinematic = true;
                rbs[i].detectCollisions = false;
            }

            Color tint = spawnAsEnemy ? new Color(1f, 0.3f, 0.25f, 0.75f) : new Color(0.25f, 0.9f, 0.95f, 0.75f);
            Renderer[] renderers = ghostPreviewObject.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] mats = renderers[r].materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && mats[m].HasProperty("_Color"))
                    {
                        mats[m].color = tint;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            CommanderPlugin.Log.LogWarning("Could not instantiate ghost preview: " + ex.Message);
        }
    }

    private void DestroyGhostPreview()
    {
        if (ghostPreviewObject != null)
        {
            UnityEngine.Object.Destroy(ghostPreviewObject);
            ghostPreviewObject = null;
        }
    }

    internal bool TrySpawnAtWorldPoint(Vector2 screenPosition)
    {
        if (!awaitingPlacement || pendingSpawnDefinition == null)
        {
            return false;
        }

        Spawner? spawner = NetworkSceneSingleton<Spawner>.i;
        if (spawner == null)
        {
            SetStatus("Spawner is not available.");
            CancelPlacement();
            return false;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        FactionHQ? targetHq = localHq;

        if (spawnAsEnemy)
        {
            targetHq = FindEnemyHq(localHq) ?? localHq;
        }

        if (targetHq == null)
        {
            SetStatus("Target faction HQ is not available.");
            CancelPlacement();
            return false;
        }

        UnitDefinition def = pendingSpawnDefinition;
        bool isShip = ResolveCategory(def) == EntityCategory.Naval;

        GlobalPosition targetPos;
        if (isShip)
        {
            if (!CommanderGameAccess.TryRaycastWaterPosition(screenPosition, out targetPos))
            {
                SetStatus("Target must be on water.");
                return false;
            }
        }
        else
        {
            if (!CommanderGameAccess.TryRaycastWorldPosition(screenPosition, out targetPos))
            {
                SetStatus("Target must be on terrain.");
                return false;
            }
        }

        Vector3 localPos = targetPos.ToLocalPosition() + def.spawnOffset;
        Quaternion rotation = Quaternion.Euler(0f, placementHeading, 0f);

        try
        {
            Unit spawnedUnit = spawner.SpawnFromUnitDefinitionInEditor(
                def,
                localPos.ToGlobalPosition(),
                rotation,
                targetHq,
                "NOC_Cheat_" + def.unitName + "_" + Time.frameCount);

            if (spawnedUnit != null)
            {
                SetStatus("Spawned " + def.unitName + " (" + (spawnAsEnemy ? "ENEMY" : "FRIENDLY") + ") at HDG " + Mathf.RoundToInt(placementHeading) + "°.");
                selectionService.SelectUnit(spawnedUnit, additive: false);
            }
            else
            {
                SetStatus("Spawner failed to instantiate " + def.unitName + ".");
            }
        }
        catch (Exception ex)
        {
            CommanderPlugin.Log.LogError("Cheat unit spawn failed: " + ex);
            SetStatus("Failed to spawn " + def.unitName + ": " + ex.Message);
        }

        DestroyGhostPreview();
        awaitingPlacement = false;
        pendingSpawnDefinition = null;
        return true;
    }

    private static FactionHQ? FindEnemyHq(FactionHQ? localHq)
    {
        FactionHQ[] allHqs = UnityEngine.Object.FindObjectsOfType<FactionHQ>();
        for (int i = 0; i < allHqs.Length; i++)
        {
            if (allHqs[i] != null && allHqs[i] != localHq)
            {
                return allHqs[i];
            }
        }
        return null;
    }

    internal void AddFunds(float amount)
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null)
        {
            SetStatus("No active faction HQ found.");
            return;
        }

        hq.AddFunds(amount);
        SetStatus("Added $" + amount.ToString("N0") + " to faction funds.");
    }

    internal void SetMaxFunds()
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null)
        {
            SetStatus("No active faction HQ found.");
            return;
        }

        hq.AddFunds(10000000f);
        SetStatus("Faction funds increased by $10,000,000.");
    }

    internal void HealSelection()
    {
        IReadOnlyList<Unit> selected = selectionService.SelectedUnits;
        if (selected.Count == 0)
        {
            SetStatus("Select units to heal.");
            return;
        }

        int healedCount = 0;
        for (int i = 0; i < selected.Count; i++)
        {
            Unit unit = selected[i];
            if (unit == null || unit.disabled)
            {
                continue;
            }

            IRepairable[] repairables = unit.GetComponentsInChildren<IRepairable>(true);
            for (int r = 0; r < repairables.Length; r++)
            {
                if (repairables[r] != null)
                {
                    try
                    {
                        repairables[r].Repair(unit, 999999f);
                    }
                    catch
                    {
                    }
                }
            }

            healedCount++;
        }

        SetStatus("Healed " + healedCount + " unit(s) to 100% HP.");
    }

    internal void RestockAmmoSelection()
    {
        IReadOnlyList<Unit> selected = selectionService.SelectedUnits;
        if (selected.Count == 0)
        {
            SetStatus("Select units to restock.");
            return;
        }

        int restockedCount = 0;
        for (int i = 0; i < selected.Count; i++)
        {
            Unit unit = selected[i];
            if (unit == null || unit.disabled)
            {
                continue;
            }

            if (unit.weaponStations != null)
            {
                for (int s = 0; s < unit.weaponStations.Count; s++)
                {
                    WeaponStation station = unit.weaponStations[s];
                    if (station?.Weapons == null) continue;

                    for (int w = 0; w < station.Weapons.Count; w++)
                    {
                        Weapon weapon = station.Weapons[w];
                        if (weapon != null)
                        {
                            weapon.ammo = weapon.GetFullAmmo();
                        }
                    }

                    station.AccountAmmo();
                    station.Updated();
                }
            }

            Rearmer[] rearmers = unit.GetComponentsInChildren<Rearmer>(true);
            for (int r = 0; r < rearmers.Length; r++)
            {
                if (rearmers[r] != null)
                {
                    rearmers[r].SetCapacity(rearmers[r].GetMaxCapacity());
                }
            }

            restockedCount++;
        }

        SetStatus("Restocked ammo and ordnance on " + restockedCount + " unit(s).");
    }

    internal void DestroySelection()
    {
        IReadOnlyList<Unit> selected = selectionService.SelectedUnits;
        if (selected.Count == 0)
        {
            SetStatus("Select target units to destroy.");
            return;
        }

        List<Unit> targets = new(selected);
        selectionService.DeselectAll();
        int destroyedCount = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            Unit unit = targets[i];
            if (unit == null || unit.disabled)
            {
                continue;
            }

            try
            {
                unit.Damage(0, new DamageInfo(0f, 999999f, 999999f, 0f));
            }
            catch
            {
                if (!unit.disabled)
                {
                    unit.DisableUnit();
                }
            }

            destroyedCount++;
        }

        SetStatus("Eliminated " + destroyedCount + " target(s).");
    }

    internal void RevealAllUnits()
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null || hq.trackingDatabase == null)
        {
            SetStatus("No active faction HQ found.");
            return;
        }

        Unit[] allUnits = UnityEngine.Object.FindObjectsOfType<Unit>();
        int revealedCount = 0;

        for (int i = 0; i < allUnits.Length; i++)
        {
            Unit unit = allUnits[i];
            if (unit == null || unit.disabled)
            {
                continue;
            }

            if (!CommanderGameAccess.IsFriendlyUnit(unit, hq))
            {
                TrackingInfo tracking = hq.GetTrackingData(unit.persistentID);
                if (tracking != null)
                {
                    tracking.lastSpottedTime = Time.timeSinceLevelLoad;
                    revealedCount++;
                }
            }
        }

        SetStatus("Updated radar tracking for " + revealedCount + " enemy units.");
    }

    internal void ResetSession()
    {
        DestroyGhostPreview();
        awaitingPlacement = false;
        pendingSpawnDefinition = null;
        placementHeading = 0f;
        statusText = string.Empty;
        catalogInitialized = false;
    }

    private void SetStatus(string text)
    {
        statusText = text;
        statusUntil = Time.unscaledTime + 5f;
    }
}
