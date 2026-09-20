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
        internal int UpgradeLevel { get; set; }

        internal BuildingEntry(Building? building, Unit? unit, string name, BuildingCategory category, string roleLabel, Vector3 pos, bool operational, int upgradeLevel = 1)
        {
            Building = building;
            AttachedUnit = unit;
            Name = name;
            Category = category;
            RoleLabel = roleLabel;
            WorldPosition = pos;
            IsOperational = operational;
            UpgradeLevel = upgradeLevel;
        }
    }

    internal sealed class EconomicProject
    {
        internal int Id { get; }
        internal string Title { get; }
        internal string Description { get; }
        internal float BaseCost { get; }
        internal float IncomeBoostPerMin { get; }
        internal int CurrentLevel { get; set; }
        internal int MaxLevel { get; }

        internal float CurrentCost => BaseCost * (CurrentLevel + 1);

        internal EconomicProject(int id, string title, string description, float baseCost, float incomeBoostPerMin, int maxLevel = 5)
        {
            Id = id;
            Title = title;
            Description = description;
            BaseCost = baseCost;
            IncomeBoostPerMin = incomeBoostPerMin;
            MaxLevel = maxLevel;
            CurrentLevel = 0;
        }
    }

    private readonly List<BuildingEntry> allEntries = new();
    private readonly List<BuildingEntry> economyEntries = new();
    private readonly List<BuildingEntry> militaryEntries = new();
    private readonly List<BuildingEntry> defenseEntries = new();
    private readonly List<BuildingEntry> logisticsEntries = new();
    private readonly List<BuildingEntry> filteredEntries = new();
    private readonly Dictionary<Building, int> buildingUpgradeLevels = new();
    private readonly List<EconomicProject> projects = new();

    private float nextRefreshTime;
    private float nextIncomeTickTime;
    private float accumulatedBonusFunds;

    internal static CommanderBuildingEconomyService? Instance { get; private set; }

    internal IReadOnlyList<BuildingEntry> AllEntries => allEntries;
    internal IReadOnlyList<BuildingEntry> EconomyEntries => economyEntries;
    internal IReadOnlyList<BuildingEntry> MilitaryEntries => militaryEntries;
    internal IReadOnlyList<BuildingEntry> DefenseEntries => defenseEntries;
    internal IReadOnlyList<BuildingEntry> LogisticsEntries => logisticsEntries;
    internal IReadOnlyList<EconomicProject> Projects => projects;

    internal float EstimatedIncomePerMinute { get; private set; }
    internal float ProjectsIncomePerMinute { get; private set; }
    internal float FacilityUpgradesIncomePerMinute { get; private set; }
    internal int ControlledSectorsCount { get; private set; }
    internal int TotalSectorsCount { get; private set; }

    internal CommanderBuildingEconomyService()
    {
        Instance = this;
        InitializeProjects();
    }

    private void InitializeProjects()
    {
        projects.Clear();
        projects.Add(new EconomicProject(
            1,
            "MINING & ENERGY CONVOYS",
            "Establish strategic raw mineral and power trade routes to generate continuous capital.",
            75000f,
            35000f,
            5));
        projects.Add(new EconomicProject(
            2,
            "INDUSTRIAL AUTOMATION & REFINERIES",
            "Overclock factory assembly lines, automated manufacturing, and fuel processing nodes.",
            150000f,
            75000f,
            5));
        projects.Add(new EconomicProject(
            3,
            "OFFSHORE & STRATEGIC TRADE GRID",
            "Secure maritime shipping lanes, deep-sea exploration, and high-yield strategic exports.",
            300000f,
            160000f,
            5));
    }

    internal void Tick()
    {
        float now = Time.unscaledTime;
        if (now >= nextRefreshTime)
        {
            nextRefreshTime = now + 3f;
            RefreshBuildingDatabase();
        }

        // Generate recurring economic bonus into faction funds
        if (now >= nextIncomeTickTime)
        {
            nextIncomeTickTime = now + 1f; // tick every second
            DistributeEconomicIncome(1f);
        }
    }

    private void DistributeEconomicIncome(float deltaSeconds)
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            return;
        }

        float totalBonusPerMinute = ProjectsIncomePerMinute + FacilityUpgradesIncomePerMinute;
        if (totalBonusPerMinute <= 0f)
        {
            return;
        }

        float secondIncrement = (totalBonusPerMinute / 60f) * deltaSeconds;
        accumulatedBonusFunds += secondIncrement;

        if (accumulatedBonusFunds >= 10f)
        {
            float toAdd = Mathf.Floor(accumulatedBonusFunds);
            accumulatedBonusFunds -= toAdd;
            localHq.AddFunds(toAdd);
        }
    }

    internal bool TryInvestInProject(int projectId, out string status)
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            status = "No active faction HQ.";
            return false;
        }

        EconomicProject? project = projects.Find(p => p.Id == projectId);
        if (project == null)
        {
            status = "Unknown economic project.";
            return false;
        }

        if (project.CurrentLevel >= project.MaxLevel)
        {
            status = $"{project.Title} is already fully upgraded (MAX LEVEL).";
            return false;
        }

        float cost = project.CurrentCost;
        if (localHq.factionFunds < cost)
        {
            string costStr = UnitConverter.ValueReading(cost) ?? ("$" + cost.ToString("N0"));
            status = $"Insufficient funds ({costStr} required).";
            return false;
        }

        localHq.AddFunds(-cost);
        project.CurrentLevel++;
        CalculateTotalEconomy();

        string newIncStr = UnitConverter.ValueReading(project.IncomeBoostPerMin) ?? ("$" + project.IncomeBoostPerMin.ToString("N0"));
        status = $"Invested in {project.Title} Lv.{project.CurrentLevel} (+{newIncStr}/min)!";
        return true;
    }

    internal bool TryUpgradeBuildingFacility(Building building, out string status)
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || building == null)
        {
            status = "Structure unavailable.";
            return false;
        }

        int currentLevel = 1;
        if (buildingUpgradeLevels.TryGetValue(building, out int lvl))
        {
            currentLevel = lvl;
        }

        if (currentLevel >= 3)
        {
            status = "Facility is already at maximum level (LEVEL 3 - OVERCLOCKED).";
            return false;
        }

        float cost = 60000f * currentLevel;
        if (localHq.factionFunds < cost)
        {
            string costStr = UnitConverter.ValueReading(cost) ?? ("$" + cost.ToString("N0"));
            status = $"Insufficient funds ({costStr} required to upgrade).";
            return false;
        }

        localHq.AddFunds(-cost);
        int newLevel = currentLevel + 1;
        buildingUpgradeLevels[building] = newLevel;

        CalculateTotalEconomy();
        string name = CleanBuildingName(building.name);
        status = $"{name} upgraded to Level {newLevel} (+ $20,000/min output)!";
        return true;
    }

    internal int GetBuildingUpgradeLevel(Building? building)
    {
        if (building != null && buildingUpgradeLevels.TryGetValue(building, out int level))
        {
            return level;
        }
        return 1;
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

            int upLevel = GetBuildingUpgradeLevel(b);
            bool operational = unit == null || !unit.disabled;
            BuildingEntry entry = new(b, unit, name, cat, roleLabel, b.transform.position, operational, upLevel);

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
                    !ab.disabled,
                    1);
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
                    true,
                    1);
                allEntries.Add(fobEntry);
                logisticsEntries.Add(fobEntry);
            }
        }

        CalculateTotalEconomy();
    }

    private void CalculateTotalEconomy()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        float baseIncome = localHq.regularIncome * 60f; // per minute
        float factoryBaseOutput = economyEntries.Count * 12500f;

        // Calculate Project Bonuses
        float projectBonus = 0f;
        for (int p = 0; p < projects.Count; p++)
        {
            projectBonus += projects[p].CurrentLevel * projects[p].IncomeBoostPerMin;
        }
        ProjectsIncomePerMinute = projectBonus;

        // Calculate Facility Upgrade Bonuses
        float facilityBonus = 0f;
        foreach (KeyValuePair<Building, int> entry in buildingUpgradeLevels)
        {
            if (entry.Key != null)
            {
                facilityBonus += (entry.Value - 1) * 20000f; // +20k/min per level
            }
        }
        FacilityUpgradesIncomePerMinute = facilityBonus;

        EstimatedIncomePerMinute = baseIncome + factoryBaseOutput + ProjectsIncomePerMinute + FacilityUpgradesIncomePerMinute;
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

        // 4. Logistics (Storage, Depot, Warehouse, Fuel)
        roleLabel = "STRATEGIC INFRASTRUCTURE";
        return BuildingCategory.Logistics;
    }

    private static string CleanBuildingName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Military Structure";

        string cleaned = raw.Replace("(Clone)", string.Empty).Trim();
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[d_]+$", string.Empty).Trim();

        return cleaned.ToLowerInvariant() switch
        {
            "factory large" => "Heavy Industrial Factory Complex",
            "factory tall" => "Advanced Manufacturing Plant",
            "vehicledepot" => "Vehicle Depot Facility",
            "radarstation" => "Early Warning Radar Station",
            "radartower" => "Surveillance Radar Tower",
            "hangar" => "Aircraft Maintenance Hangar",
            "refinery" => "Petroleum & Fuel Refinery",
            "powerplant" => "Thermal Energy Power Grid",
            "warehouse" => "Munitions Storage Warehouse",
            "headquarters" or "hq" => "Supreme Command Headquarters",
            _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned)
        };
    }

    internal void ResetSession()
    {
        allEntries.Clear();
        economyEntries.Clear();
        militaryEntries.Clear();
        defenseEntries.Clear();
        logisticsEntries.Clear();
        buildingUpgradeLevels.Clear();
        accumulatedBonusFunds = 0f;
        ProjectsIncomePerMinute = 0f;
        FacilityUpgradesIncomePerMinute = 0f;
        EstimatedIncomePerMinute = 0f;
        InitializeProjects();
    }
}
