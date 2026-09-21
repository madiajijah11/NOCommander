using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionCommander;

public enum SectorDirective
{
    AdvanceAndSecure = 0,
    HoldAndDefend = 1,
    ReconAndHarass = 2,
    TacticalFallback = 3
}

public enum SectorSecurityState
{
    Secure = 0,      // Friendly controlled, no/low threat
    Contested = 1,   // Active engagement / hostile presence
    Hostile = 2      // Enemy stronghold
}

public sealed class TheaterSector
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public GlobalPosition CenterPosition { get; set; }
    public float RadiusMeters { get; set; }
    public SectorSecurityState SecurityState { get; set; }
    public SectorDirective ActiveDirective { get; set; }
    public int AssignedTaskForceId { get; set; } = -1;
    public int EnemyStrengthEstimate { get; set; }
    public int FriendlyStrengthEstimate { get; set; }
    public float LastScannedTime { get; set; }
}

public sealed class TaskForce
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Unit> VanguardUnits { get; } = new();
    public List<Unit> AirDefenseUnits { get; } = new();
    public List<Unit> SupportUnits { get; } = new();
    public int TargetSectorId { get; set; } = -1;
    public Vector3 FormationCenter { get; set; }
    public float DesiredFormationSpeed { get; set; }
    public bool InCombat { get; set; }

    public int TotalUnitCount => VanguardUnits.Count + AirDefenseUnits.Count + SupportUnits.Count;

    public void PruneDeadReferences()
    {
        VanguardUnits.RemoveAll(static u => u == null || u.disabled);
        AirDefenseUnits.RemoveAll(static u => u == null || u.disabled);
        SupportUnits.RemoveAll(static u => u == null || u.disabled);
    }
}
