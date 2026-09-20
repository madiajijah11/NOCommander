using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderFactionVehicleService
{
    private readonly HashSet<string> heldCategories = new(StringComparer.Ordinal);
    private readonly HashSet<UnitDefinition> heldDefinitions = new();

    private readonly List<VehicleDefinition> landDefinitions = new();
    private readonly List<AircraftDefinition> airDefinitions = new();
    private readonly List<ShipDefinition> navalDefinitions = new();
    private readonly List<string> allLandCategories = new();
    private readonly List<VehicleDefinition> filteredLandBuffer = new();

    internal static CommanderFactionVehicleService? Instance { get; private set; }
    internal static FactionHQ? AutomaticDeploymentHq { get; set; }

    internal CommanderFactionVehicleService()
    {
        Instance = this;
    }

    internal float FactionFunds => CommanderGameAccess.GetLocalHq()?.factionFunds ?? 0f;

    internal IReadOnlyList<VehicleDefinition> LandDefinitions
    {
        get
        {
            EnsureDefinitionsLoaded();
            return landDefinitions;
        }
    }

    internal IReadOnlyList<AircraftDefinition> AirDefinitions
    {
        get
        {
            EnsureDefinitionsLoaded();
            return airDefinitions;
        }
    }

    internal IReadOnlyList<ShipDefinition> NavalDefinitions
    {
        get
        {
            EnsureDefinitionsLoaded();
            return navalDefinitions;
        }
    }

    internal IReadOnlyList<string> AllLandCategories
    {
        get
        {
            EnsureDefinitionsLoaded();
            return allLandCategories;
        }
    }

    private void EnsureDefinitionsLoaded()
    {
        if (landDefinitions.Count == 0)
        {
            CommanderGameAccess.TryGetLocalVehicleDefinitions(landDefinitions);
            allLandCategories.Clear();
            for (int i = 0; i < landDefinitions.Count; i++)
            {
                string cat = CommanderGameAccess.GetVehicleCategoryLabel(landDefinitions[i]);
                if (!string.IsNullOrWhiteSpace(cat) && !allLandCategories.Contains(cat))
                {
                    allLandCategories.Add(cat);
                }
            }
            allLandCategories.Sort(StringComparer.OrdinalIgnoreCase);
        }

        if (airDefinitions.Count == 0)
        {
            AircraftDefinition[] aircraft = Resources.FindObjectsOfTypeAll<AircraftDefinition>();
            for (int i = 0; i < aircraft.Length; i++)
            {
                AircraftDefinition def = aircraft[i];
                if (def != null && def.unitPrefab != null && !airDefinitions.Contains(def))
                {
                    if (def.unitPrefab.GetComponentInChildren<Aircraft>(true) != null || def is AircraftDefinition)
                    {
                        airDefinitions.Add(def);
                    }
                }
            }
            airDefinitions.Sort(static (a, b) => string.Compare(a.unitName, b.unitName, StringComparison.OrdinalIgnoreCase));
        }

        if (navalDefinitions.Count == 0)
        {
            ShipDefinition[] ships = Resources.FindObjectsOfTypeAll<ShipDefinition>();
            for (int i = 0; i < ships.Length; i++)
            {
                ShipDefinition def = ships[i];
                if (def != null && def.unitPrefab != null && !navalDefinitions.Contains(def))
                {
                    if (def.unitPrefab.GetComponentInChildren<Ship>(true) != null || def is ShipDefinition)
                    {
                        navalDefinitions.Add(def);
                    }
                }
            }
            navalDefinitions.Sort(static (a, b) => string.Compare(a.unitName, b.unitName, StringComparison.OrdinalIgnoreCase));
        }
    }

    internal IReadOnlyList<VehicleDefinition> GetFilteredLandDefinitions(string? categoryFilter)
    {
        EnsureDefinitionsLoaded();
        if (string.IsNullOrEmpty(categoryFilter) || string.Equals(categoryFilter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return landDefinitions;
        }

        filteredLandBuffer.Clear();
        for (int i = 0; i < landDefinitions.Count; i++)
        {
            VehicleDefinition def = landDefinitions[i];
            string cat = CommanderGameAccess.GetVehicleCategoryLabel(def);
            if (string.Equals(cat, categoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                filteredLandBuffer.Add(def);
            }
        }
        return filteredLandBuffer;
    }

    internal int GetCategoryReserveTotal(string category)
    {
        EnsureDefinitionsLoaded();
        int total = 0;
        for (int i = 0; i < landDefinitions.Count; i++)
        {
            VehicleDefinition def = landDefinitions[i];
            if (string.Equals(CommanderGameAccess.GetVehicleCategoryLabel(def), category, StringComparison.OrdinalIgnoreCase))
            {
                total += GetReserveCount(def);
            }
        }
        return total;
    }

    internal bool IsCategoryHeld(string category)
    {
        return heldCategories.Contains(category);
    }

    internal void ToggleCategory(string category)
    {
        if (string.Equals(category, "All", StringComparison.Ordinal))
        {
            return;
        }

        if (!heldCategories.Add(category))
        {
            heldCategories.Remove(category);
        }
    }

    internal void SetAllCategoriesHeld(bool hold)
    {
        heldCategories.Clear();
        if (hold)
        {
            EnsureDefinitionsLoaded();
            for (int i = 0; i < allLandCategories.Count; i++)
            {
                heldCategories.Add(allLandCategories[i]);
            }
        }
    }

    internal bool IsDefinitionHeld(UnitDefinition definition)
    {
        return heldDefinitions.Contains(definition);
    }

    internal void ToggleDefinition(UnitDefinition definition)
    {
        if (!heldDefinitions.Add(definition))
        {
            heldDefinitions.Remove(definition);
        }
    }

    internal int GetReserveCount(UnitDefinition definition)
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null) return 0;
        int supply = hq.GetUnitSupply(definition);
        return Mathf.Max(0, supply);
    }

    internal float GetPurchaseCost(UnitDefinition definition)
    {
        return Math.Max(0f, definition.value);
    }

    internal bool CanAcquire(UnitDefinition definition, out string reason)
    {
        if (GetReserveCount(definition) > 0)
        {
            reason = string.Empty;
            return true;
        }

        float cost = GetPurchaseCost(definition);
        if (FactionFunds >= cost)
        {
            reason = string.Empty;
            return true;
        }

        reason = "Insufficient faction funds for " + definition.unitName + ".";
        return false;
    }

    internal void CommitAcquisition(UnitDefinition definition)
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null)
        {
            return;
        }

        if (hq.GetUnitSupply(definition) > 0)
        {
            hq.ModifyUnitSupply(definition, -1);
            return;
        }

        hq.AddFunds(-GetPurchaseCost(definition));
    }

    internal bool TryBuyStockToReserve(UnitDefinition definition, int count, out string status)
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null)
        {
            status = "No active faction HQ.";
            return false;
        }

        float unitCost = GetPurchaseCost(definition);
        if (unitCost <= 0.01f) unitCost = 1000f;

        int affordableCount = count == int.MaxValue
            ? Mathf.FloorToInt(hq.factionFunds / unitCost)
            : count;

        if (affordableCount <= 0)
        {
            status = "Insufficient funds for " + definition.unitName + ".";
            return false;
        }

        float totalCost = unitCost * affordableCount;
        if (hq.factionFunds < totalCost)
        {
            affordableCount = Mathf.FloorToInt(hq.factionFunds / unitCost);
            if (affordableCount <= 0)
            {
                status = "Insufficient funds ($" + unitCost.ToString("N0") + " required).";
                return false;
            }
            totalCost = unitCost * affordableCount;
        }

        hq.AddFunds(-totalCost);
        hq.ModifyUnitSupply(definition, affordableCount);
        status = "Purchased " + affordableCount + "x " + definition.unitName + " to Reserve ($" + totalCost.ToString("N0") + ").";
        return true;
    }

    internal bool TryScrapStockFromReserve(UnitDefinition definition, int count, out string status)
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null)
        {
            status = "No active faction HQ.";
            return false;
        }

        int currentSupply = hq.GetUnitSupply(definition);
        if (currentSupply <= 0)
        {
            status = "No " + definition.unitName + " in reserve to scrap.";
            return false;
        }

        int scrapCount = count == int.MaxValue ? currentSupply : Mathf.Min(count, currentSupply);
        float refundPerUnit = GetPurchaseCost(definition) * 0.75f;
        float totalRefund = refundPerUnit * scrapCount;

        hq.ModifyUnitSupply(definition, -scrapCount);
        hq.AddFunds(totalRefund);
        status = "Scrapped " + scrapCount + "x " + definition.unitName + " from Reserve (+$" + totalRefund.ToString("N0") + " refunded).";
        return true;
    }

    internal bool ShouldBlockAutomaticDeployment(VehicleDepot depot, VehicleDefinition definition)
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        FactionHQ? deploymentHq = AutomaticDeploymentHq;
        if (deploymentHq == null || localHq == null || deploymentHq != localHq || depot.NetworkHQ != localHq)
        {
            return false;
        }

        return heldDefinitions.Contains(definition)
            || heldCategories.Contains(CommanderGameAccess.GetVehicleCategoryLabel(definition));
    }

    internal void ResetSession()
    {
        heldCategories.Clear();
        heldDefinitions.Clear();
        landDefinitions.Clear();
        airDefinitions.Clear();
        navalDefinitions.Clear();
        allLandCategories.Clear();
        filteredLandBuffer.Clear();
        AutomaticDeploymentHq = null;
    }
}
