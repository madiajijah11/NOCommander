# Theater Sector Directives & High-Command Battlegroups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a Supreme Theater Commander Grand Strategy system where players issue macro Sector Directives on the map while autonomous Combined-Arms Battlegroups (AI Task Forces) handle movement, formations, self-defense air screening, triage repairs, and call-for-fire coordination.

**Architecture:** 
- `Core/CommanderTheaterTypes.cs`: Enums and data structures for `TheaterSector`, `TaskForce`, `SectorDirective`, `SectorSecurityState`.
- `Units/CommanderBattlegroupService.cs`: Autonomous battlegroup coordination (Vanguard, AA Screen, Support Echelon, Speed matching, and self-triage).
- `Core/CommanderTheaterSectorService.cs`: Sector lifecycle, frontline state calculation, threat assessment, and directive dispatching.
- `UI/CommanderTheaterCommandUi.cs`: Map sector overlays, directive card controls, and air tasking quick dispatch.
- Integration in `CommanderModeController`, `CommanderSettings`, and `CommanderOverlayUi`.

**Tech Stack:** C# .NET Framework 4.7.2, Unity Engine (Mono), BepInEx 5.x, HarmonyLib, Mirage Networking.

---

### Task 1: Core Models & Enums (`Core/CommanderTheaterTypes.cs`)

**Files:**
- Create: `Core/CommanderTheaterTypes.cs`

- [ ] **Step 1: Write Core/CommanderTheaterTypes.cs**

```csharp
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
    Secure = 0,
    Contested = 1,
    Hostile = 2
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
    public float DesiredFormationSpeed { get; set; }
    public bool InCombat { get; set; }

    public int TotalUnitCount => VanguardUnits.Count + AirDefenseUnits.Count + SupportUnits.Count;

    public void PruneDeadReferences()
    {
        VanguardUnits.RemoveWhere(static u => u == null || u.disabled);
        AirDefenseUnits.RemoveWhere(static u => u == null || u.disabled);
        SupportUnits.RemoveWhere(static u => u == null || u.disabled);
    }
}
```

---

### Task 2: Battlegroup & Task Force Service (`Units/CommanderBattlegroupService.cs`)

**Files:**
- Create: `Units/CommanderBattlegroupService.cs`

- [ ] **Step 1: Write Units/CommanderBattlegroupService.cs**
  - Implement task force grouping, formation offset positioning, speed matching, self-screening, and repair triage.

---

### Task 3: Theater Sector & Directives Orchestrator (`Core/CommanderTheaterSectorService.cs`)

**Files:**
- Create: `Core/CommanderTheaterSectorService.cs`

- [ ] **Step 1: Write Core/CommanderTheaterSectorService.cs**
  - Scan map capture points/airbases/depots to dynamically generate `TheaterSector` nodes.
  - Periodic threat scanning and security state evaluation (Secure / Contested / Hostile).
  - Issue high-level macro destinations to assigned `TaskForce` instances based on active `SectorDirective`.

---

### Task 4: Tactical Map Overlays & IMGUI Directive UI (`UI/CommanderTheaterCommandUi.cs`)

**Files:**
- Create: `UI/CommanderTheaterCommandUi.cs`

- [ ] **Step 1: Write UI/CommanderTheaterCommandUi.cs**
  - Render color-coded sector circles on tactical minimap and 3D world overlays.
  - Draw Sector Directive Card with the 4 command buttons.
  - Draw Task Force Readiness Bar and quick Air Tasking dispatch button.

---

### Task 5: Integration into Mode Controller, Settings, and Overlay UI

**Files:**
- Modify: `Core/CommanderSettings.cs`
- Modify: `Core/CommanderModeController.cs`
- Modify: `UI/CommanderOverlayUi.cs`

- [ ] **Step 1: Update CommanderSettings.cs** (Add settings toggles for Theater Command).
- [ ] **Step 2: Update CommanderModeController.cs** (Instantiate services and wire lifecycle / reset).
- [ ] **Step 3: Update CommanderOverlayUi.cs** (Render Theater Command UI when active).

---

### Task 6: Compilation & Verification

- [ ] **Step 1: Verify compilation with Roslyn csc**
- [ ] **Step 2: Present binary artifact to user**
