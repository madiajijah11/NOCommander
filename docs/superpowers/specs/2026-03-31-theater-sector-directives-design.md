# Design Specification: Theater Sector Directives & High-Command Battlegroup System

## 1. Overview & Vision
Transform NOCommander into an authentic Modern Warfare Grand Strategy / High-Command simulator where the player acts as Supreme Theater Commander. The player directs broad strategic objectives (Sector Directives & Air Tasking Orders) on the tactical theater map, while autonomous combined-arms battlegroups (AI Task Forces) execute movement, formations, self-defense screening, field repairs, and sensor-to-shooter engagements automatically.

---

## 2. Architecture & Data Structures

```
CommanderTheaterCommandService (Orchestrator)
  ├── TheaterSectorManager
  │     ├── List<TheaterSector> (Dynamic map zones with frontline status)
  │     └── Sector Directives: [ AdvanceAndSecure | HoldAndDefend | ReconAndHarass | TacticalFallback ]
  ├── BattlegroupManager
  │     ├── List<TaskForce> (Combined Arms Battlegroups)
  │     │     ├── Vanguard (MBTs / Heavy IFVs)
  │     │     ├── AirDefenseScreen (AFV6 AA / SPAAG / Short-Range SAMs)
  │     │     └── SupportEchelon (Jackknife Engineers / Supply Trucks)
  │     └── Autonomous Behaviors:
  │           ├── Speed matching & formation cohesion
  │           ├── Self-screening air defense positioning
  │           ├── Autonomous repair triage
  │           └── Datalink call-for-fire coordination
  └── TheaterCommandUi
        ├── Tactical Map Sector Overlays (Color-coded boundary circles)
        ├── Sector Directive Selection Panel
        └── Task Force Readiness & Air Tasking Bar
```

### 2.1 Core Types & Interfaces

```csharp
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
    Secure = 0,      // Friendly controlled, low/no threat
    Contested = 1,   // Active combat engagement
    Hostile = 2      // Enemy controlled stronghold
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
    public float AverageSpeed { get; set; }
    public bool InCombat { get; set; }
}
```

---

## 3. Autonomous Battlegroup Logic (Behaviors)

1. **Formation Assembly & Auto-Assignment:**
   - Newly spawned ground units from depots automatically assign into the nearest active `TaskForce`.
   - Units classify into roles by chassis & weapons (MBTs -> Vanguard, AA -> Screen, Jackknifes/Trucks -> Support).
2. **Speed Matching & Cohesion:**
   - Fast wheeled vehicles (AFV6/LCV) clamp max speed to match the slowest Vanguard unit (T-98 MBT ~45 km/h) to maintain combat screen integrity.
3. **Air Defense Self-Screening:**
   - Air defense units maintain an 80–120m offset relative to Vanguard center and prioritize low-flying CAS/helicopters.
4. **Autonomous Triage:**
   - Damaged units automatically halt and emit repair requests; Jackknife engineering vehicles navigate to repair them before resuming advance.
5. **Sensor-to-Shooter Datalink:**
   - Recon units (Hexhound/Medusa) spotting heavy fortifications broadcast target coordinates to friendly MLRS (Scythe) and Naval Railguns (Dynamo).

---

## 4. UI & Control Workflow

1. **Tactical Map Overlays:**
   - Sector zones rendered with themed circular outlines and directive icons on `DynamicMap`.
2. **Directive Card:**
   - Clicking a sector opens a top-level directive selector with 4 buttons:
     - `[ ⚔️ ADVANCE & SECURE ]`
     - `[ 🛡️ HOLD & DEFEND ]`
     - `[ 📡 RECON & BVR ]`
     - `[ ↩️ TACTICAL RETREAT ]`
3. **Air Tasking Quick Dispatch:**
   - One-click trigger to dispatch synchronized air packages directly to a contested sector.

---

## 5. Memory Management & Safety

- All `Unit` references in `TaskForce` collections implement `PruneDeadReferences()`.
- Full state reset on scene unload in `ResetSession()`.
- Zero-GC allocations in UI loops using cached theme styles and structs.
