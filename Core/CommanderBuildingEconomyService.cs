using System;
using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderBuildingEconomyService
{
    internal enum BuildingCategory
    {
        All = 0,
        Economy = 1,
        MilitarySpawning = 2,
        Defense = 3,
        Logistics = 4
    }

    internal sealed class BuildingEntry
    {
        internal Building? Building { get; }
        internal Unit? AttachedUnit { get; }
        internal string Name { get; }
        internal BuildingCategory Category { get; }
        internal string RoleLabel { get; }
        internal Vector3 WorldPosition { get; }
        internal bool IsOperational { get; }

        internal BuildingEntry(Building? building, Unit? unit, string name, BuildingCategory category, string roleLabel, Vector3 pos, bool operational)
        {
            Building = building;
            AttachedUnit = unit;
            Name = name;
            Category = category;
            RoleLabel = roleLabel;
            WorldPosition = pos;
            IsOperational = operational;
        }
    }

    private readonly List<BuildingEntry> allEntries = new();
    private readonly List<BuildingEntry> economyEntries = new();
    private readonly List<BuildingEntry> militaryEntries = new();
    private readonly List<BuildingEntry> defenseEntries = new();
    private readonly List<BuildingEntry> logisticsEntries = new();
    private readonly List<BuildingEntry> filteredEntries = new();

    private float nextRefreshTime;

    internal static CommanderBuildingEconomyService? Instance { get; private set; }

    internal IReadOnlyList<BuildingEntry> AllEntries => allEntries;
    internal IReadOnlyList<BuildingEntry> EconomyEntries => economyEntries;
    internal IReadOnlyList<BuildingEntry> MilitaryEntries => militaryEntries;
    internal IReadOnlyList<BuildingEntry> DefenseEntries => defenseEntries;
    internal IReadOnlyList<BuildingEntry> LogisticsEntries => logisticsEntries;

    internal float EstimatedIncomePerMinute { get; private set; }
    internal int ControlledSectorsCount { get; private set; }
    internal int TotalSectorsCount { get; private set; }

    internal CommanderBuildingEconomyService()
    {
        Instance = this;
    }

    internal void Tick()
    {
        float now = Time.unscaledTime;
        if (now >= nextRefreshTime)
        {
            nextRefreshTime = now + 3f;
            RefreshBuildingDatabase();
        }
    }

    internal IReadOnlyList<BuildingEntry> GetEntriesByCategory(BuildingCategory category, string searchFilter)
    {
        IReadOnlyList<BuildingEntry> source = category switch
        {
            BuildingCategory.Economy => economyEntries,
            BuildingCategory.MilitarySpawning => militaryEntries,
            BuildingCategory.Defense => defenseEntries,
            BuildingCategory.Logistics => logisticsEntries,
            _ => allEntries
        };

        if (string.IsNullOrWhiteSpace(searchFilter))
        {
            return source;
        }

        filteredEntries.Clear();
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].Name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0
                || source[i].RoleLabel.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                filteredEntries.Add(source[i]);
            }
        }

        return filteredEntries;
    }

    internal void RefreshBuildingDatabase()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            allEntries.Clear();
            economyEntries.Clear();
            militaryEntries.Clear();
            defenseEntries.Clear();
            logisticsEntries.Clear();
            return;
        }

        allEntries.Clear();
        economyEntries.Clear();
        militaryEntries.Clear();
        defenseEntries.Clear();
        logisticsEntries.Clear();

        // 1. Scan all active scene buildings
        Building[] sceneBuildings = UnityEngine.Object.FindObjectsOfType<Building>();
        for (int i = 0; i < sceneBuildings.Length; i++)
        {
            Building b = sceneBuildings[i];
            if (b == null) continue;

            Unit? unit = b.GetComponent<Unit>();
            if (unit != null && unit.disabled) continue;

            bool isFriendly = CommanderGameAccess.IsFriendlyUnit(unit, localHq) || b.NetworkHQ == localHq;
            if (!isFriendly) continue;

            BuildingCategory cat = ClassifyBuilding(b, unit, out string roleLabel);
            string name = !string.IsNullOrWhiteSpace(b.name) ? b.name : (unit != null ? unit.unitName : "Structure");
            name = CleanBuildingName(name);

            bool operational = unit == null || !unit.disabled;
            BuildingEntry entry = new(b, unit, name, cat, roleLabel, b.transform.position, operational);

            allEntries.Add(entry);
            switch (cat)
            {
                case BuildingCategory.Economy:
                    economyEntries.Add(entry);
                    break;
                case BuildingCategory.MilitarySpawning:
                    militaryEntries.Add(entry);
                    break;
                case BuildingCategory.Defense:
                    defenseEntries.Add(entry);
                    break;
                case BuildingCategory.Logistics:
                    logisticsEntries.Add(entry);
                    break;
            }
        }

        // 2. Scan active Airbases (Hangars / Runways)
        IEnumerable<Airbase> airbases = localHq.GetAirbases();
        if (airbases != null)
        {
            foreach (Airbase ab in airbases)
            {
                if (ab == null || ab.disabled) continue;
                string abName = ab.SavedAirbase != null && !string.IsNullOrWhiteSpace(ab.SavedAirbase.DisplayName)
                    ? ab.SavedAirbase.DisplayName
                    : ab.name;
                BuildingEntry abEntry = new(
                    null,
                    ab.GetComponentInParent<Unit>(),
                    CleanBuildingName(abName),
                    BuildingCategory.MilitarySpawning,
                    "AIRBASE & HANGARS",
                    ab.transform.position,
                    !ab.disabled);
                allEntries.Add(abEntry);
                militaryEntries.Add(abEntry);
            }
        }

        // 3. Scan Deployed FOBs
        if (CommanderForwardOutpostService.Instance?.DeployedFobs != null)
        {
            foreach (Unit fob in CommanderForwardOutpostService.Instance.DeployedFobs)
            {
                if (fob == null || fob.disabled) continue;
                BuildingEntry fobEntry = new(
                    null,
                    fob,
                    fob.unitName + " (FOB)",
                    BuildingCategory.Logistics,
                    "FORWARD LOGISTICS BASE",
                    fob.transform.position,
                    true);
                allEntries.Add(fobEntry);
                logisticsEntries.Add(fobEntry);
            }
        }

        // 4. Calculate Economy & Income Rate
        float baseIncome = localHq.regularIncome * 60f; // per minute
        float factoryBonus = economyEntries.Count * 12500f; // estimated factory output value per min
        EstimatedIncomePerMinute = baseIncome + factoryBonus;

        ControlledSectorsCount = economyEntries.Count + militaryEntries.Count;
        TotalSectorsCount = Mathf.Max(ControlledSectorsCount + 4, 12);
    }

    private static BuildingCategory ClassifyBuilding(Building b, Unit? unit, out string roleLabel)
    {
        string name = (b.name + " " + (unit != null ? unit.unitName : string.Empty)).ToLowerInvariant();

        // 1. Factory / Industrial / Economy
        if (b.GetComponent<Factory>() != null || name.Contains("factory") || name.Contains("plant") || name.Contains("refinery") || name.Contains("industrial") || name.Contains("power"))
        {
            roleLabel = "INDUSTRIAL FACTORY / REVENUE";
            return BuildingCategory.Economy;
        }

        // 2. Military Spawning (Depot, Hangar, Shipyard)
        if (b.GetComponent<VehicleDepot>() != null || name.Contains("depot") || name.Contains("hangar") || name.Contains("dock") || name.Contains("shipyard") || name.Contains("barracks"))
        {
            roleLabel = "VEHICLE DEPOT / SPAWNER";
            return BuildingCategory.MilitarySpawning;
        }

        // 3. Air Defense & Radar
        if (unit != null && (unit.GetComponentInChildren<Radar>(true) != null || unit.GetComponentInChildren<FireControl>(true) != null)
            || name.Contains("radar") || name.Contains("sam") || name.Contains("strato") || name.Contains("tower") || name.Contains("gun") || name.Contains("missile") || name.Contains("bunker"))
        {
            roleLabel = "AIR DEFENSE & RADAR";
            return BuildingCategory.Defense;
        }

        // 4. Logistics & Storage
        if (b.GetComponentInChildren<Rearmer>(true) != null || name.Contains("storage") || name.Contains("warehouse") || name.Contains("fuel") || name.Contains("ammo") || name.Contains("supply") || name.Contains("pad"))
        {
            roleLabel = "SUPPLY & LOGISTICS";
            return BuildingCategory.Logistics;
        }

        roleLabel = "GENERAL INFRASTRUCTURE";
        return BuildingCategory.Economy;
    }

    private static string CleanBuildingName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Structure";
        return raw.Replace("(Clone)", string.Empty).Replace("_", " ").Trim();
    }

    internal void ResetSession()
    {
        allEntries.Clear();
        economyEntries.Clear();
        militaryEntries.Clear();
        defenseEntries.Clear();
        logisticsEntries.Clear();
        filteredEntries.Clear();
        EstimatedIncomePerMinute = 0f;
    }
}
