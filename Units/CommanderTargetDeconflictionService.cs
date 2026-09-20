using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderTargetDeconflictionService
{
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

        // Assign deconflicted targets
        int airIdx = 0;
        int armorIdx = 0;
        int softIdx = 0;

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
                if (enemyAir.Count > 0)
                {
                    assignedTarget = enemyAir[airIdx % enemyAir.Count];
                    airIdx++;
                }
                else if (enemySoft.Count > 0)
                {
                    assignedTarget = enemySoft[softIdx % enemySoft.Count];
                    softIdx++;
                }
                else if (enemyArmor.Count > 0)
                {
                    assignedTarget = enemyArmor[armorIdx % enemyArmor.Count];
                    armorIdx++;
                }
            }
            // 2. MBT / Heavy Armor -> Prioritize Enemy Heavy Armor
            else if (role.Contains("t-98") || role.Contains("vanguard") || role.Contains("mbt") || role.Contains("brawler") || role.Contains("tank"))
            {
                if (enemyArmor.Count > 0)
                {
                    assignedTarget = enemyArmor[armorIdx % enemyArmor.Count];
                    armorIdx++;
                }
                else if (enemySoft.Count > 0)
                {
                    assignedTarget = enemySoft[softIdx % enemySoft.Count];
                    softIdx++;
                }
                else if (enemyAir.Count > 0)
                {
                    assignedTarget = enemyAir[airIdx % enemyAir.Count];
                    airIdx++;
                }
            }
            // 3. IFV / Light Combat / Recon -> Prioritize Soft Targets, Radars, Supply
            else
            {
                if (enemySoft.Count > 0)
                {
                    assignedTarget = enemySoft[softIdx % enemySoft.Count];
                    softIdx++;
                }
                else if (enemyArmor.Count > 0)
                {
                    assignedTarget = enemyArmor[armorIdx % enemyArmor.Count];
                    armorIdx++;
                }
                else if (enemyAir.Count > 0)
                {
                    assignedTarget = enemyAir[airIdx % enemyAir.Count];
                    airIdx++;
                }
            }

            if (assignedTarget != null)
            {
                moveService.AssignFocusAttackTarget(attacker, assignedTarget);
            }
        }
    }

    private static bool IsArmorTarget(Unit unit)
    {
        if (unit == null) return false;
        string name = unit.unitName.ToLowerInvariant();
        return name.Contains("t-98") || name.Contains("mbt") || name.Contains("brawler") || name.Contains("vanguard") || name.Contains("tank") || name.Contains("afv") || name.Contains("ifv") || name.Contains("bolide");
    }

    internal void ResetSession()
    {
    }
}
