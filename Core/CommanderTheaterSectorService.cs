using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderTheaterSectorService
{
    private const float SectorScanInterval = 3f;
    private const float DefaultSectorRadius = 3500f;

    private readonly List<TheaterSector> sectors = new();
    private float nextScanTime;
    private bool sectorsInitialized;
    private int selectedSectorId = -1;

    internal static CommanderTheaterSectorService? Instance { get; private set; }

    internal IReadOnlyList<TheaterSector> Sectors => sectors;
    internal int SelectedSectorId => selectedSectorId;
    internal TheaterSector? SelectedSector => GetSector(selectedSectorId);

    internal CommanderTheaterSectorService()
    {
        Instance = this;
    }

    internal void SelectSector(int id)
    {
        selectedSectorId = id;
    }

    internal TheaterSector? GetSector(int id)
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            if (sectors[i].Id == id) return sectors[i];
        }
        return null;
    }

    internal void SetSectorDirective(int sectorId, SectorDirective directive)
    {
        TheaterSector? sector = GetSector(sectorId);
        if (sector == null)
        {
            return;
        }

        sector.ActiveDirective = directive;
        CommanderPlugin.Log.LogInfo($"[Theater High-Command] Sector '{sector.Name}' assigned directive: {directive}");

        // Dispatch assigned or closest TaskForce
        CommanderBattlegroupService? bgService = CommanderBattlegroupService.Instance;
        if (bgService != null)
        {
            TaskForce? targetTf = null;
            if (sector.AssignedTaskForceId >= 0)
            {
                targetTf = bgService.GetTaskForce(sector.AssignedTaskForceId);
            }
            if (targetTf == null && bgService.TaskForces.Count > 0)
            {
                targetTf = bgService.TaskForces[0];
                sector.AssignedTaskForceId = targetTf.Id;
            }

            if (targetTf != null)
            {
                ApplyDirectiveToTaskForce(targetTf, sector);
            }
        }
    }

    private static void ApplyDirectiveToTaskForce(TaskForce tf, TheaterSector sector)
    {
        GlobalPosition targetPos = sector.CenterPosition;

        switch (sector.ActiveDirective)
        {
            case SectorDirective.AdvanceAndSecure:
                for (int v = 0; v < tf.VanguardUnits.Count; v++)
                {
                    Unit u = tf.VanguardUnits[v];
                    if (u != null && !u.disabled)
                    {
                        CommanderGameAccess.SetUnitHoldPosition(u, false);
                        CommanderGameAccess.GetUnitCommand(u)?.SetDestination(targetPos, true);
                    }
                }
                break;

            case SectorDirective.HoldAndDefend:
                for (int v = 0; v < tf.VanguardUnits.Count; v++)
                {
                    Unit u = tf.VanguardUnits[v];
                    if (u != null && !u.disabled)
                    {
                        CommanderGameAccess.SetUnitHoldPosition(u, true);
                    }
                }
                break;

            case SectorDirective.TacticalFallback:
                FactionHQ? hq = CommanderGameAccess.GetLocalHq();
                if (hq != null)
                {
                    GlobalPosition hqPos = hq.transform.position.ToGlobalPosition();
                    for (int v = 0; v < tf.VanguardUnits.Count; v++)
                    {
                        Unit u = tf.VanguardUnits[v];
                        if (u != null && !u.disabled)
                        {
                            CommanderGameAccess.SetUnitHoldPosition(u, false);
                            CommanderGameAccess.GetUnitCommand(u)?.SetDestination(hqPos, true);
                        }
                    }
                }
                break;

            case SectorDirective.ReconAndHarass:
                // Fast vehicles scout forward
                for (int a = 0; a < tf.AirDefenseUnits.Count; a++)
                {
                    Unit u = tf.AirDefenseUnits[a];
                    if (u != null && !u.disabled)
                    {
                        CommanderGameAccess.SetUnitHoldPosition(u, false);
                        CommanderGameAccess.GetUnitCommand(u)?.SetDestination(targetPos, true);
                    }
                }
                break;
        }
    }

    internal void Tick()
    {
        if (!sectorsInitialized)
        {
            InitializeMapSectors();
        }

        if (Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + SectorScanInterval;
            EvaluateSectorStates();
        }
    }

    private void InitializeMapSectors()
    {
        sectors.Clear();
        sectorsInitialized = true;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            return;
        }

        int idCounter = 0;

        // 1. Airbases as major sectors
        Airbase[] allAirbases = UnityEngine.Object.FindObjectsOfType<Airbase>();
        for (int i = 0; i < allAirbases.Length; i++)
        {
            Airbase ab = allAirbases[i];
            if (ab == null || ab.disabled) continue;

            string abName = !string.IsNullOrEmpty(ab.name) ? ab.name : $"Airbase {idCounter + 1}";
            sectors.Add(new TheaterSector
            {
                Id = idCounter++,
                Name = abName,
                CenterPosition = ab.transform.position.ToGlobalPosition(),
                RadiusMeters = DefaultSectorRadius,
                SecurityState = (ab.CurrentHQ == localHq) ? SectorSecurityState.Secure : SectorSecurityState.Hostile,
                ActiveDirective = SectorDirective.HoldAndDefend
            });
        }

        // 2. Depots / Major Capture Objectives
        VehicleDepot[] allDepots = UnityEngine.Object.FindObjectsOfType<VehicleDepot>();
        for (int i = 0; i < allDepots.Length; i++)
        {
            VehicleDepot vd = allDepots[i];
            if (vd == null || vd.disabled) continue;

            // Check if far enough from existing sectors
            Vector3 pos = vd.transform.position;
            bool tooClose = false;
            for (int s = 0; s < sectors.Count; s++)
            {
                if (Vector3.Distance(pos, sectors[s].CenterPosition.ToLocalPosition()) < 2000f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                string depName = !string.IsNullOrEmpty(vd.name) ? vd.name : $"Depot Sector {idCounter + 1}";
                sectors.Add(new TheaterSector
                {
                    Id = idCounter++,
                    Name = depName,
                    CenterPosition = pos.ToGlobalPosition(),
                    RadiusMeters = DefaultSectorRadius * 0.8f,
                    SecurityState = (vd.NetworkHQ == localHq) ? SectorSecurityState.Secure : SectorSecurityState.Hostile,
                    ActiveDirective = SectorDirective.AdvanceAndSecure
                });
            }
        }

        if (sectors.Count > 0 && selectedSectorId < 0)
        {
            selectedSectorId = sectors[0].Id;
        }
    }

    private void EvaluateSectorStates()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            return;
        }

        for (int s = 0; s < sectors.Count; s++)
        {
            TheaterSector sec = sectors[s];
            Vector3 center = sec.CenterPosition.ToLocalPosition();

            int friendlyCount = 0;
            int enemyCount = 0;

            if (localHq.factionUnits != null)
            {
                foreach (PersistentID pid in localHq.factionUnits)
                {
                    if (pid.TryGetUnit(out Unit u) && u != null && !u.disabled)
                    {
                        if (Vector3.Distance(u.transform.position, center) <= sec.RadiusMeters)
                        {
                            friendlyCount++;
                        }
                    }
                }
            }

            if (localHq.trackingDatabase != null)
            {
                foreach (KeyValuePair<PersistentID, TrackingInfo> entry in localHq.trackingDatabase)
                {
                    if (entry.Key.TryGetUnit(out Unit u) && u != null && !u.disabled && !CommanderGameAccess.IsFriendlyUnit(u, localHq))
                    {
                        if (Vector3.Distance(u.transform.position, center) <= sec.RadiusMeters)
                        {
                            enemyCount++;
                        }
                    }
                }
            }

            sec.FriendlyStrengthEstimate = friendlyCount;
            sec.EnemyStrengthEstimate = enemyCount;

            if (enemyCount > 0 && friendlyCount > 0)
            {
                sec.SecurityState = SectorSecurityState.Contested;
            }
            else if (enemyCount > 0 && friendlyCount == 0)
            {
                sec.SecurityState = SectorSecurityState.Hostile;
            }
            else
            {
                sec.SecurityState = SectorSecurityState.Secure;
            }
        }
    }

    internal void ResetSession()
    {
        sectors.Clear();
        sectorsInitialized = false;
        selectedSectorId = -1;
    }
}
