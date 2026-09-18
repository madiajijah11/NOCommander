using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderStanceService
{
    private readonly CommanderSelectionService selectionService;
    private readonly HashSet<Unit> holdFireUnits = new();

    internal static CommanderStanceService? Instance { get; private set; }

    internal CommanderStanceService(CommanderSelectionService selectionService)
    {
        this.selectionService = selectionService;
        Instance = this;
    }

    internal bool IsHoldFire(Unit? unit)
    {
        PruneDeadReferences();
        return unit != null && !unit.disabled && holdFireUnits.Contains(unit);
    }

    internal void ToggleHoldFireForSelection()
    {
        IReadOnlyList<Unit> selected = selectionService.SelectedUnits;
        if (selected.Count == 0)
        {
            return;
        }

        PruneDeadReferences();
        bool anyHoldFire = false;
        for (int i = 0; i < selected.Count; i++)
        {
            if (holdFireUnits.Contains(selected[i]))
            {
                anyHoldFire = true;
                break;
            }
        }

        bool setHold = !anyHoldFire;
        for (int i = 0; i < selected.Count; i++)
        {
            Unit unit = selected[i];
            if (unit == null || unit.disabled || !CommanderGameAccess.IsFriendlyUnit(unit, CommanderGameAccess.GetLocalHq()))
            {
                continue;
            }

            if (setHold)
            {
                holdFireUnits.Add(unit);
                ApplyHoldFire(unit, true);
            }
            else
            {
                holdFireUnits.Remove(unit);
                ApplyHoldFire(unit, false);
            }
        }
    }

    internal void ApplyHoldFire(Unit unit, bool hold)
    {
        if (unit == null || unit.disabled)
        {
            return;
        }

        Turret[] turrets = unit.GetComponentsInChildren<Turret>(true);
        for (int i = 0; i < turrets.Length; i++)
        {
            if (turrets[i] != null)
            {
                turrets[i].enabled = !hold;
            }
        }

        FireControl[] fireControls = unit.GetComponentsInChildren<FireControl>(true);
        for (int i = 0; i < fireControls.Length; i++)
        {
            if (fireControls[i] != null)
            {
                fireControls[i].enabled = !hold;
            }
        }
    }

    internal void PruneDeadReferences()
    {
        holdFireUnits.RemoveWhere(static u => u == null || u.disabled);
    }

    internal void ResetSession()
    {
        holdFireUnits.Clear();
    }
}
