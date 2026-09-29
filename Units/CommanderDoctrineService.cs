using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal enum CommanderDoctrineRole
{
    Frontline,
    Standoff,
    AirDefense,
    Recon,
    Logistics,
}

internal readonly struct CommanderDoctrinePolicy
{
    internal readonly CommanderDoctrineRole Role;
    internal readonly float PreferredStandoffMeters;
    internal readonly bool AllowFrontlineAdvance;

    internal CommanderDoctrinePolicy(
        CommanderDoctrineRole role,
        float preferredStandoffMeters,
        bool allowFrontlineAdvance)
    {
        Role = role;
        PreferredStandoffMeters = preferredStandoffMeters;
        AllowFrontlineAdvance = allowFrontlineAdvance;
    }
}

internal sealed class CommanderDoctrineService
{
    private readonly CommanderSelectionService selectionService;
    private readonly Dictionary<Unit, CommanderDoctrineRole> doctrineByUnit = new();
    private readonly List<Unit> snapshot = new();
    private readonly float refreshInterval;
    private float nextRefreshAt;

    internal CommanderDoctrineService(CommanderSelectionService selectionService, float refreshInterval = 2f)
    {
        this.selectionService = selectionService;
        this.refreshInterval = refreshInterval > 0f ? refreshInterval : 2f;
    }

    internal static CommanderDoctrineRole Classify(Unit? unit)
    {
        if (unit == null || unit.disabled)
        {
            return CommanderDoctrineRole.Frontline;
        }

        if (unit is Aircraft) return CommanderDoctrineRole.Recon;
        if (unit is Ship) return CommanderDoctrineRole.Frontline;
        if (unit.GetComponent<RearmVehicleAI>() != null) return CommanderDoctrineRole.Logistics;
        if (unit.GetComponent<Radar>() != null) return CommanderDoctrineRole.Standoff;
        if (unit.GetComponent<Turret>() != null
            && unit.name.IndexOf("SAM", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return CommanderDoctrineRole.AirDefense;
        }

        return CommanderDoctrineRole.Frontline;
    }

    internal static CommanderDoctrinePolicy GetPolicy(CommanderDoctrineRole role)
    {
        return role switch
        {
            CommanderDoctrineRole.Standoff => new CommanderDoctrinePolicy(role, 500f, false),
            CommanderDoctrineRole.AirDefense => new CommanderDoctrinePolicy(role, 250f, false),
            CommanderDoctrineRole.Recon => new CommanderDoctrinePolicy(role, 1000f, false),
            CommanderDoctrineRole.Logistics => new CommanderDoctrinePolicy(role, 750f, false),
            _ => new CommanderDoctrinePolicy(CommanderDoctrineRole.Frontline, 0f, true),
        };
    }

    internal void Tick()
    {
        if (Time.unscaledTime < nextRefreshAt)
        {
            return;
        }

        nextRefreshAt = Time.unscaledTime + refreshInterval;
        PruneDeadReferences();
        snapshot.Clear();

        IReadOnlyList<Unit> selectedUnits = selectionService.SelectedUnits;
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        for (int i = 0; i < selectedUnits.Count; i++)
        {
            Unit unit = selectedUnits[i];
            if (unit == null || unit.disabled || !CommanderGameAccess.IsFriendlyUnit(unit, localHq))
            {
                continue;
            }

            snapshot.Add(unit);
            doctrineByUnit[unit] = Classify(unit);
        }
    }

    internal void PruneDeadReferences()
    {
        List<Unit>? deadUnits = null;
        foreach (Unit unit in doctrineByUnit.Keys)
        {
            if (unit == null || unit.disabled)
            {
                (deadUnits ??= new List<Unit>()).Add(unit!);
            }
        }

        if (deadUnits != null)
        {
            for (int i = 0; i < deadUnits.Count; i++)
            {
                doctrineByUnit.Remove(deadUnits[i]);
            }
        }

        for (int i = snapshot.Count - 1; i >= 0; i--)
        {
            Unit unit = snapshot[i];
            if (unit == null || unit.disabled)
            {
                snapshot.RemoveAt(i);
            }
        }
    }

    internal bool TryGetRole(Unit? unit, out CommanderDoctrineRole role)
    {
        role = CommanderDoctrineRole.Frontline;
        if (unit == null || unit.disabled || !CommanderGameAccess.IsFriendlyUnit(unit, CommanderGameAccess.GetLocalHq()))
        {
            return false;
        }

        return doctrineByUnit.TryGetValue(unit, out role);
    }

    internal bool TryGetPolicy(Unit? unit, out CommanderDoctrinePolicy policy)
    {
        policy = default;
        if (unit == null || unit.disabled || !CommanderGameAccess.IsFriendlyUnit(unit, CommanderGameAccess.GetLocalHq()))
        {
            return false;
        }

        if (!doctrineByUnit.TryGetValue(unit, out CommanderDoctrineRole role))
        {
            policy = GetPolicy(CommanderDoctrineRole.Frontline);
            return true;
        }

        policy = GetPolicy(role);
        return true;
    }

    internal bool CanAdvanceFrontline(Unit? unit)
    {
        return TryGetPolicy(unit, out CommanderDoctrinePolicy policy) && policy.AllowFrontlineAdvance;
    }

    internal void ResetSession()
    {
        doctrineByUnit.Clear();
        snapshot.Clear();
        nextRefreshAt = 0f;
    }
}
