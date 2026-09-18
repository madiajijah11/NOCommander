using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderControlGroupsService
{
    private const float DoubleTapThreshold = 0.35f;

    public readonly struct ControlGroupInfo
    {
        public int Index { get; }
        public int Count { get; }
        public string Summary { get; }

        public ControlGroupInfo(int index, int count, string summary)
        {
            Index = index;
            Count = count;
            Summary = summary;
        }
    }

    private readonly CommanderSelectionService selectionService;
    private readonly Dictionary<int, List<Unit>> groups = new();
    private int lastSelectedGroup = -1;
    private float lastGroupSelectTime;

    internal static CommanderControlGroupsService? Instance { get; private set; }

    internal CommanderControlGroupsService(CommanderSelectionService selectionService)
    {
        this.selectionService = selectionService;
        Instance = this;
    }

    internal void AssignGroup(int index, IReadOnlyList<Unit> units)
    {
        if (index < 0 || index > 9)
        {
            return;
        }

        List<Unit> groupUnits = new();
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit != null && !unit.disabled && CommanderGameAccess.IsFriendlyUnit(unit, CommanderGameAccess.GetLocalHq()))
            {
                groupUnits.Add(unit);
            }
        }

        if (groupUnits.Count > 0)
        {
            groups[index] = groupUnits;
        }
        else
        {
            groups.Remove(index);
        }
    }

    internal bool SelectGroup(int index, bool additive)
    {
        if (index < 0 || index > 9 || !groups.TryGetValue(index, out List<Unit> groupUnits))
        {
            return false;
        }

        // Prune disabled or dead units
        for (int i = groupUnits.Count - 1; i >= 0; i--)
        {
            Unit unit = groupUnits[i];
            if (unit == null || unit.disabled)
            {
                groupUnits.RemoveAt(i);
            }
        }

        if (groupUnits.Count == 0)
        {
            groups.Remove(index);
            return false;
        }

        if (!additive)
        {
            selectionService.DeselectAll();
        }

        for (int i = 0; i < groupUnits.Count; i++)
        {
            selectionService.SelectUnit(groupUnits[i], additive: true);
        }

        float currentTime = Time.unscaledTime;
        if (lastSelectedGroup == index && (currentTime - lastGroupSelectTime) <= DoubleTapThreshold)
        {
            CommanderCameraFollowService.Instance?.CenterOnSelectionIfFollowing();
        }

        lastSelectedGroup = index;
        lastGroupSelectTime = currentTime;
        return true;
    }

    internal IReadOnlyList<Unit>? GetGroupUnits(int index)
    {
        if (!groups.TryGetValue(index, out List<Unit> groupUnits))
        {
            return null;
        }

        for (int i = groupUnits.Count - 1; i >= 0; i--)
        {
            if (groupUnits[i] == null || groupUnits[i].disabled)
            {
                groupUnits.RemoveAt(i);
            }
        }

        return groupUnits.Count > 0 ? groupUnits : null;
    }

    internal int GetGroupUnitCount(int index)
    {
        IReadOnlyList<Unit>? list = GetGroupUnits(index);
        return list?.Count ?? 0;
    }

    internal bool HasGroup(int index)
    {
        return GetGroupUnitCount(index) > 0;
    }

    internal void GetAllActiveGroups(List<ControlGroupInfo> buffer)
    {
        buffer.Clear();
        for (int i = 1; i <= 9; i++)
        {
            CheckAndAddGroup(i, buffer);
        }
        CheckAndAddGroup(0, buffer);
    }

    private void CheckAndAddGroup(int index, List<ControlGroupInfo> buffer)
    {
        IReadOnlyList<Unit>? units = GetGroupUnits(index);
        if (units == null || units.Count == 0)
        {
            return;
        }

        string primaryName = CommanderGameAccess.GetUnitLabel(units[0]);
        string summary = units.Count == 1
            ? primaryName
            : $"{primaryName} +{units.Count - 1}";

        buffer.Add(new ControlGroupInfo(index, units.Count, summary));
    }

    internal void SelectAllArmy(bool combatOnly = true)
    {
        FactionHQ? hq = CommanderGameAccess.GetLocalHq();
        if (hq == null)
        {
            return;
        }

        List<Unit> allUnits = new();
        CommanderGameAccess.CollectFriendlySurfaceUnits(allUnits);

        selectionService.DeselectAll();
        for (int i = 0; i < allUnits.Count; i++)
        {
            Unit unit = allUnits[i];
            if (unit == null || unit.disabled)
            {
                continue;
            }

            if (combatOnly && !IsCombatUnit(unit))
            {
                continue;
            }

            selectionService.SelectUnit(unit, additive: true);
        }
    }

    private static bool IsCombatUnit(Unit unit)
    {
        return unit.weaponStations != null && unit.weaponStations.Count > 0;
    }

    internal void ResetSession()
    {
        groups.Clear();
        lastSelectedGroup = -1;
    }
}
