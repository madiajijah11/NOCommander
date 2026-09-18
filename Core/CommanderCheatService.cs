using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderCheatService
{
    private readonly CommanderSelectionService selectionService;
    private float statusUntil;
    private string statusText = string.Empty;

    internal static CommanderCheatService? Instance { get; private set; }

    internal bool GodModeEnabled { get; set; }
    internal bool FreeSpawningEnabled { get; set; }
    internal bool InfiniteAmmoEnabled { get; set; }

    internal string StatusText => Time.unscaledTime <= statusUntil ? statusText : string.Empty;

    internal CommanderCheatService(CommanderSelectionService selectionService)
    {
        this.selectionService = selectionService;
        Instance = this;
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
        SetStatus($"Added ${amount:N0} to faction funds.");
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

        SetStatus($"Healed {healedCount} unit{(healedCount == 1 ? string.Empty : "s")} to 100% HP.");
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

        SetStatus($"Restocked ammo and ordnance on {restockedCount} unit{(restockedCount == 1 ? string.Empty : "s")}.");
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

        SetStatus($"Eliminated {destroyedCount} target{(destroyedCount == 1 ? string.Empty : "s")}.");
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

        SetStatus($"Updated radar tracking for {revealedCount} enemy units.");
    }

    internal void ResetSession()
    {
        GodModeEnabled = false;
        FreeSpawningEnabled = false;
        InfiniteAmmoEnabled = false;
        statusText = string.Empty;
    }

    private void SetStatus(string text)
    {
        statusText = text;
        statusUntil = Time.unscaledTime + 5f;
    }
}