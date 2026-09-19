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

    private void EnsureDefinitionsLoaded()
    {
        if (landDefinitions.Count == 0)
        {
            CommanderGameAccess.TryGetLocalVehicleDefinitions(landDefinitions);
        }

        if (airDefinitions.Count == 0)
        {
            AircraftDefinition[] aircraft = Resources.FindObjectsOfTypeAll<AircraftDefinition>();
            for (int i = 0; i < aircraft.Length; i++)
            {
                if (aircraft[i] != null && aircraft[i].unitPrefab != null && !airDefinitions.Contains(aircraft[i]))
                {
                    airDefinitions.Add(aircraft[i]);
                }
            }
        }

        if (navalDefinitions.Count == 0)
        {
            ShipDefinition[] ships = Resources.FindObjectsOfTypeAll<ShipDefinition>();
            for (int i = 0; i < ships.Length; i++)
            {
                if (ships[i] != null && ships[i].unitPrefab != null && !navalDefinitions.Contains(ships[i]))
                {
                    navalDefinitions.Add(ships[i]);
                }
            }
        }
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
            for (int i = 0; i < LandDefinitions.Count; i++)
            {
                string cat = CommanderGameAccess.GetVehicleCategoryLabel(LandDefinitions[i]);
                heldCategories.Add(cat);
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
        return hq != null ? hq.GetUnitSupply(definition) : 0;
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
        AutomaticDeploymentHq = null;
    }
}
