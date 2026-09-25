using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderBattlegroupService
{
    private const float FormationUpdateInterval = 0.5f;
    private const float TriageInterval = 8.0f;
    private const float AirDefenseFlankOffset = 90f;
    private const float SupportRearOffset = 110f;
    private const float MaxTaskForceRadius = 350f;
    private const float ReRouteDistanceThreshold = 120f;

    private readonly List<TaskForce> taskForces = new();
    private readonly HashSet<Unit> assignedUnits = new();
    private float nextFormationUpdateTime;
    private float nextTriageTime;
    private int roundRobinTfIndex;
    private int roundRobinSlotIndex;

    internal static CommanderBattlegroupService? Instance { get; private set; }

    internal IReadOnlyList<TaskForce> TaskForces => taskForces;

    internal CommanderBattlegroupService()
    {
        Instance = this;
        InitializeDefaultTaskForces();
    }

    private void InitializeDefaultTaskForces()
    {
        taskForces.Clear();
        taskForces.Add(new TaskForce { Id = 0, Name = "Task Force Alpha (Vanguard)" });
        taskForces.Add(new TaskForce { Id = 1, Name = "Task Force Bravo (Iron Screen)" });
        taskForces.Add(new TaskForce { Id = 2, Name = "Task Force Charlie (Reserve)" });
    }

    internal void Tick()
    {
        PruneDeadReferences();

        float now = Time.unscaledTime;
        if (now >= nextFormationUpdateTime)
        {
            nextFormationUpdateTime = now + FormationUpdateInterval;
            AutoAssignUnassignedUnits();
            UpdateTaskForceFormations();
        }

        if (now >= nextTriageTime)
        {
            nextTriageTime = now + TriageInterval;
            PerformAutonomousFieldTriage();
        }
    }

    internal TaskForce? GetTaskForce(int id)
    {
        for (int i = 0; i < taskForces.Count; i++)
        {
            if (taskForces[i].Id == id) return taskForces[i];
        }
        return null;
    }

    private void AutoAssignUnassignedUnits()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq?.factionUnits == null)
        {
            return;
        }

        foreach (PersistentID id in localHq.factionUnits)
        {
            if (!id.TryGetUnit(out Unit unit) || unit == null || unit.disabled || unit is not GroundVehicle)
            {
                continue;
            }

            if (assignedUnits.Contains(unit))
            {
                continue;
            }

            // Find closest task force with capacity (< 14 units)
            TaskForce? targetForce = null;
            float minDistance = float.MaxValue;
            Vector3 unitPos = unit.transform.position;

            for (int f = 0; f < taskForces.Count; f++)
            {
                TaskForce tf = taskForces[f];
                if (tf.TotalUnitCount >= 14) continue;

                if (tf.TotalUnitCount == 0)
                {
                    targetForce ??= tf;
                    continue;
                }

                float dist = Vector3.Distance(unitPos, tf.FormationCenter);
                if (dist < minDistance && dist <= MaxTaskForceRadius)
                {
                    minDistance = dist;
                    targetForce = tf;
                }
            }

            targetForce ??= taskForces[0];
            AssignUnitToTaskForce(unit, targetForce);
        }
    }

    private void AssignUnitToTaskForce(Unit unit, TaskForce tf)
    {
        assignedUnits.Add(unit);

        string name = (!string.IsNullOrEmpty(unit.unitName) ? unit.unitName : unit.name).ToLowerInvariant();
        if (CommanderGameAccess.IsStandoffUnit(unit))
        {
            tf.SupportUnits.Add(unit);
        }
        else if (unit.TryGetComponent(out Repairer _) || name.Contains("jack") || name.Contains("repair") || name.Contains("truck") || name.Contains("tractor"))
        {
            tf.SupportUnits.Add(unit);
        }
        else if (name.Contains("aa") || name.Contains("sam") || name.Contains("spaag") || name.Contains("shard"))
        {
            tf.AirDefenseUnits.Add(unit);
        }
        else
        {
            tf.VanguardUnits.Add(unit);
        }
    }

    private void UpdateTaskForceFormations()
    {
        if (taskForces.Count == 0) return;

        // 1. Advance round-robin task force index
        roundRobinTfIndex = (roundRobinTfIndex + 1) % taskForces.Count;
        TaskForce tf = taskForces[roundRobinTfIndex];
        tf.PruneDeadReferences();

        if (tf.TotalUnitCount == 0)
        {
            return;
        }

        // 2. Calculate Vanguard / Formation Center
        Vector3 center = Vector3.zero;
        int count = 0;
        if (tf.VanguardUnits.Count > 0)
        {
            for (int v = 0; v < tf.VanguardUnits.Count; v++)
            {
                center += tf.VanguardUnits[v].transform.position;
                count++;
            }
        }
        else
        {
            for (int a = 0; a < tf.AirDefenseUnits.Count; a++)
            {
                center += tf.AirDefenseUnits[a].transform.position;
                count++;
            }
        }

        if (count > 0)
        {
            tf.FormationCenter = center / count;
        }

        Vector3 forward = tf.VanguardUnits.Count > 0 ? tf.VanguardUnits[0].transform.forward : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // 3. Staggered Round-Robin Dispatch (Only re-route at most 1 unit per tick to eliminate A* CPU storms!)
        bool dispatched = false;

        // Check Air Defense Slot
        if (tf.AirDefenseUnits.Count > 0)
        {
            roundRobinSlotIndex = (roundRobinSlotIndex + 1) % tf.AirDefenseUnits.Count;
            Unit aaUnit = tf.AirDefenseUnits[roundRobinSlotIndex];
            if (aaUnit != null && !aaUnit.disabled)
            {
                float side = (roundRobinSlotIndex % 2 == 0) ? 1f : -1f;
                float flankDist = AirDefenseFlankOffset * ((roundRobinSlotIndex / 2) + 1);
                Vector3 targetFlank = tf.FormationCenter + (right * side * flankDist) - (forward * 20f);
                float distToFlank = Vector3.Distance(aaUnit.transform.position, targetFlank);

                if (distToFlank > ReRouteDistanceThreshold)
                {
                    UnitCommand? cmd = CommanderGameAccess.GetUnitCommand(aaUnit);
                    cmd?.SetDestination(targetFlank.ToGlobalPosition(), false);
                    dispatched = true;
                }
                else if (distToFlank < 25f && aaUnit is GroundVehicle gv)
                {
                    CommanderGameAccess.SetUnitHoldPosition(gv, true);
                }
            }
        }

        // If no AA was re-routed, check Support Rear Slot
        if (!dispatched && tf.SupportUnits.Count > 0)
        {
            int supIdx = UnityEngine.Random.Range(0, tf.SupportUnits.Count);
            Unit supUnit = tf.SupportUnits[supIdx];
            if (supUnit != null && !supUnit.disabled)
            {
                float rearOffset = CommanderGameAccess.IsStandoffUnit(supUnit) ? (SupportRearOffset + 180f) : (SupportRearOffset + (supIdx * 25f));
                Vector3 targetRear = tf.FormationCenter - (forward * rearOffset);
                float distToRear = Vector3.Distance(supUnit.transform.position, targetRear);

                if (distToRear > ReRouteDistanceThreshold)
                {
                    UnitCommand? cmd = CommanderGameAccess.GetUnitCommand(supUnit);
                    cmd?.SetDestination(targetRear.ToGlobalPosition(), false);
                }
                else if (distToRear < 25f && supUnit is GroundVehicle gvSup)
                {
                    CommanderGameAccess.SetUnitHoldPosition(gvSup, true);
                }
            }
        }
    }

    private void PerformAutonomousFieldTriage()
    {
        for (int i = 0; i < taskForces.Count; i++)
        {
            TaskForce tf = taskForces[i];
            if (tf.SupportUnits.Count == 0 || tf.VanguardUnits.Count == 0) continue;

            // Check only 1 random vanguard unit per triage interval
            int vIdx = UnityEngine.Random.Range(0, tf.VanguardUnits.Count);
            Unit vanguard = tf.VanguardUnits[vIdx];
            if (vanguard == null || vanguard.disabled) continue;

            bool needsRepair = false;
            IRepairable[] repairables = vanguard.GetComponentsInChildren<IRepairable>(true);
            for (int r = 0; r < repairables.Length; r++)
            {
                if (repairables[r] != null && repairables[r].NeedsRepair())
                {
                    needsRepair = true;
                    break;
                }
            }

            if (needsRepair)
            {
                for (int s = 0; s < tf.SupportUnits.Count; s++)
                {
                    Unit jack = tf.SupportUnits[s];
                    if (jack != null && !jack.disabled && jack.TryGetComponent(out Repairer _))
                    {
                        UnitCommand? cmd = CommanderGameAccess.GetUnitCommand(jack);
                        cmd?.SetDestination(vanguard.transform.position.ToGlobalPosition(), true);
                        break;
                    }
                }
            }
        }
    }

    internal void PruneDeadReferences()
    {
        assignedUnits.RemoveWhere(static u => u == null || u.disabled);
        for (int i = 0; i < taskForces.Count; i++)
        {
            taskForces[i].PruneDeadReferences();
        }
    }

    internal void ResetSession()
    {
        assignedUnits.Clear();
        InitializeDefaultTaskForces();
    }
}
