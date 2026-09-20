using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderTargetDeconflictionService
{
    private readonly Dictionary<Unit, int> targetAssignmentCount = new();

    internal static CommanderTargetDeconflictionService? Instance { get; private set; }

    internal CommanderTargetDeconflictionService()
    {
        Instance = this;
    }

    internal void DeconflictGroupAttack(IReadOnlyList<Unit> attackers, IReadOnlyList<Unit> enemies, CommanderMoveService moveService)
    {
        if (attackers == null || attackers.Count == 0 || enemies == null || enemies.Count == 0 || moveService == null)
        {
            return;
        }

        targetAssignmentCount.Clear();
        List<Unit> enemyAir = new();
        List<Unit> enemyArmor = new();
        List<Unit> enemySoft = new();

        for (int e = 0; e < enemies.Count; e++)
        {
            Unit enemy = enemies[e];
            if (enemy == null || enemy.disabled) continue;

            if (enemy is Aircraft)
            {
                enemyAir.Add(enemy);
            }
            else if (IsArmorTarget(enemy))
            {
                enemyArmor.Add(enemy);
            }
            else
            {
                enemySoft.Add(enemy);
            }
        }

        for (int a = 0; a < attackers.Count; a++)
        {
            Unit attacker = attackers[a];
            if (attacker == null || attacker.disabled || !CommanderGameAccess.ShouldAllowCommanderMove(attacker))
            {
                continue;
            }

            Unit? assignedTarget = null;
            string role = attacker.unitName.ToLowerInvariant();

            // 1. Air Defense / SAM / SPAAG -> Prioritize Enemy Aircraft
            if (role.Contains("sam") || role.Contains("spaag") || role.Contains("strato") || role.Contains("shard") || role.Contains("23mm") || role.Contains("radar"))
            {
                assignedTarget = GetLeastAssignedTarget(enemyAir) ?? GetLeastAssignedTarget(enemySoft) ?? GetLeastAssignedTarget(enemyArmor);
            }
            // 2. MBT / Heavy Armor -> Prioritize Enemy Heavy Armor
            else if (role.Contains("t-98") || role.Contains("vanguard") || role.Contains("mbt") || role.Contains("brawler") || role.Contains("tank"))
            {
                assignedTarget = GetLeastAssignedTarget(enemyArmor) ?? GetLeastAssignedTarget(enemySoft) ?? GetLeastAssignedTarget(enemyAir);
            }
            // 3. IFV / Light Combat / Recon -> Prioritize Soft Targets, Radars, Logistics
            else
            {
                assignedTarget = GetLeastAssignedTarget(enemySoft) ?? GetLeastAssignedTarget(enemyArmor) ?? GetLeastAssignedTarget(enemyAir);
            }

            if (assignedTarget != null)
            {
                if (!targetAssignmentCount.ContainsKey(assignedTarget)) targetAssignmentCount[assignedTarget] = 0;
                targetAssignmentCount[assignedTarget]++;

                // Automatically switch to the optimal weapon station for this target
                OptimizeWeaponSelectionForTarget(attacker, assignedTarget);
                moveService.AssignFocusAttackTarget(attacker, assignedTarget);
            }
        }
    }

    private Unit? GetLeastAssignedTarget(List<Unit> candidates)
    {
        if (candidates.Count == 0) return null;

        Unit? best = null;
        int minAssigned = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            Unit c = candidates[i];
            if (c == null || c.disabled) continue;

            targetAssignmentCount.TryGetValue(c, out int count);
            if (count < minAssigned)
            {
                minAssigned = count;
                best = c;
            }
        }

        return best;
    }

    private static void OptimizeWeaponSelectionForTarget(Unit attacker, Unit target)
    {
        WeaponManager? weaponManager = attacker.GetComponentInChildren<WeaponManager>(true);
        if (attacker.weaponStations == null || attacker.weaponStations.Count <= 1 || weaponManager == null)
        {
            return;
        }

        bool isAir = target is Aircraft;
        bool isArmor = IsArmorTarget(target);

        int bestStation = -1;
        float bestScore = -1f;

        for (int i = 0; i < attacker.weaponStations.Count; i++)
        {
            WeaponStation station = attacker.weaponStations[i];
            if (station == null || station.Cargo) continue;

            WeaponInfo? info = station.WeaponInfo;
            if (info == null) continue;

            float score = 0f;
            if (isAir)
            {
                score = info.effectiveness.antiAir * 10f;
                if (info.missile) score += 5f;
            }
            else if (isArmor)
            {
                score = info.effectiveness.antiSurface * 10f;
                if (info.missile || info.laserGuided || info.glideBomb) score += 6f;
            }
            else
            {
                // Soft / light vehicle: prioritize rapid-fire guns and HE over expensive heavy missiles
                score = info.effectiveness.antiSurface * 8f;
                if (info.gun) score += 6f;
                if (info.missile) score -= 3f; // Preserve heavy missiles
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestStation = i;
            }
        }

        if (bestStation >= 0)
        {
            weaponManager.currentWeaponStation = attacker.weaponStations[bestStation];
        }
    }

    private static bool IsArmorTarget(Unit unit)
    {
        if (unit == null) return false;
        string name = unit.unitName.ToLowerInvariant();
        return name.Contains("t-98") || name.Contains("mbt") || name.Contains("brawler") || name.Contains("vanguard") || name.Contains("tank") || name.Contains("afv") || name.Contains("ifv") || name.Contains("bolide") || unit is Ship;
    }

    internal void ResetSession()
    {
        targetAssignmentCount.Clear();
    }
}
