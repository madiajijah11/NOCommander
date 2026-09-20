using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderForwardOutpostService
{
    private const float FobAuraRadius = 75f;
    private const float ServiceTickInterval = 1.5f;

    private readonly CommanderSelectionService selectionService;
    private readonly HashSet<Unit> deployedFobs = new();
    private readonly List<Unit> nearbyUnitsScratch = new();
    private float nextServiceTickTime;

    internal static CommanderForwardOutpostService? Instance { get; private set; }

    internal IReadOnlyCollection<Unit> DeployedFobs => deployedFobs;

    internal CommanderForwardOutpostService(CommanderSelectionService selectionService)
    {
        this.selectionService = selectionService;
        Instance = this;
    }

    internal bool CanDeployFob(Unit? unit)
    {
        if (unit == null || unit.disabled || unit is Aircraft)
        {
            return false;
        }

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || !CommanderGameAccess.IsFriendlyUnit(unit, localHq))
        {
            return false;
        }

        // Must be a logistics vehicle, tractor, flatbed, or rearmer
        if (unit.GetComponentInChildren<Rearmer>(true) != null
            || unit.GetComponentInChildren<Repairer>(true) != null)
        {
            return true;
        }

        if (unit is GroundVehicle gv)
        {
            string name = gv.unitName.ToLowerInvariant();
            return name.Contains("truck") || name.Contains("tractor") || name.Contains("msv") || name.Contains("hlt") || name.Contains("logistics");
        }

        return false;
    }

    internal bool IsFobDeployed(Unit? unit)
    {
        PruneDeadReferences();
        return unit != null && !unit.disabled && deployedFobs.Contains(unit);
    }

    internal void ToggleFobForSelection()
    {
        Unit? focused = selectionService.FocusedSelection;
        if (!CanDeployFob(focused) || focused == null)
        {
            return;
        }

        PruneDeadReferences();
        if (deployedFobs.Contains(focused))
        {
            deployedFobs.Remove(focused);
            CommanderGameAccess.SetUnitHoldPosition(focused, false);
            CommanderPlugin.Log.LogInfo($"[FOB] Packed up Forward Operating Base at {focused.unitName}.");
        }
        else
        {
            deployedFobs.Add(focused);
            CommanderGameAccess.SetUnitHoldPosition(focused, true);
            CommanderPlugin.Log.LogInfo($"[FOB] Deployed Forward Operating Base at {focused.unitName} (Radius: {FobAuraRadius}m).");
        }
    }

    internal void Tick()
    {
        float now = Time.unscaledTime;
        if (now < nextServiceTickTime || deployedFobs.Count == 0)
        {
            return;
        }

        nextServiceTickTime = now + ServiceTickInterval;
        PruneDeadReferences();

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            return;
        }

        Unit[] allUnits = UnityEngine.Object.FindObjectsOfType<Unit>();

        foreach (Unit fob in deployedFobs)
        {
            if (fob == null || fob.disabled)
            {
                continue;
            }

            Vector3 fobPos = fob.transform.position;

            for (int i = 0; i < allUnits.Length; i++)
            {
                Unit candidate = allUnits[i];
                if (candidate == null || candidate.disabled || ReferenceEquals(candidate, fob))
                {
                    continue;
                }

                if (!CommanderGameAccess.IsFriendlyUnit(candidate, localHq))
                {
                    continue;
                }

                if (Vector3.Distance(fobPos, candidate.transform.position) <= FobAuraRadius)
                {
                    ServiceNearbyUnit(candidate);
                }
            }
        }
    }

    private static void ServiceNearbyUnit(Unit unit)
    {
        if (unit == null || unit.disabled)
        {
            return;
        }

        // 1. Field Repair
        IRepairable[] repairables = unit.GetComponentsInChildren<IRepairable>(true);
        for (int r = 0; r < repairables.Length; r++)
        {
            if (repairables[r] != null && repairables[r].NeedsRepair())
            {
                try
                {
                    repairables[r].Repair(unit, 150f);
                }
                catch
                {
                }
            }
        }

        // 2. Field Ammo Restock
        if (unit.weaponStations != null)
        {
            for (int s = 0; s < unit.weaponStations.Count; s++)
            {
                WeaponStation station = unit.weaponStations[s];
                if (station?.Weapons == null) continue;

                for (int w = 0; w < station.Weapons.Count; w++)
                {
                    Weapon weapon = station.Weapons[w];
                    if (weapon != null && weapon.ammo < weapon.GetFullAmmo())
                    {
                        weapon.ammo = Mathf.Min(weapon.GetFullAmmo(), weapon.ammo + 2);
                        station.AccountAmmo();
                        station.Updated();
                    }
                }
            }
        }
    }

    internal void PruneDeadReferences()
    {
        deployedFobs.RemoveWhere(static u => u == null || u.disabled);
    }

    internal void ResetSession()
    {
        deployedFobs.Clear();
        nextServiceTickTime = 0f;
    }
}
