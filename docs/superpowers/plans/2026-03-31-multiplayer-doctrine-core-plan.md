# Multiplayer Doctrine Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a host-authoritative, session-safe doctrine core that classifies friendly units, validates gameplay order intent, and prevents standoff units from being pushed into frontline automation.

**Architecture:** Keep the existing services intact and add two focused services. `CommanderDoctrineService` owns role classification and throttled friendly-unit snapshots. `CommanderOrderAuthority` owns server checks, command IDs, stale/duplicate rejection, and accepted-order bookkeeping. The mode controller ticks and resets both services; existing movement remains the execution boundary until later rollout phases add threat-aware routing.

**Tech Stack:** C#/.NET Framework 4.7.2, Unity Mono, BepInEx, Harmony, Mirage Networking, existing NOCommander services.

---

## Files and responsibilities

- Create: `Core/CommanderOrderAuthority.cs` — host-only order envelope validation and idempotency.
- Create: `Units/CommanderDoctrineService.cs` — role classification, anchor policy, throttled unit snapshot, dead-reference pruning.
- Modify: `Core/CommanderModeController.cs` — construct, tick, activate/reset/deactivate the new services.
- Modify: `Units/CommanderMoveService.cs` — consult doctrine only for an explicit standoff guard on direct move orders; do not alter native movement physics.
- Modify: `Core/CommanderSettings.cs` — add opt-in doctrine and interval settings.
- Modify: `Core/CommanderInputController.cs` — reject client-side gameplay order submission when the local instance is not server-authoritative.
- Test: `tests/DoctrineCoreTests.cs` — pure classification and order validation tests if the repository test harness is available; otherwise compile-time/manual test matrix is recorded in the task.

### Task 1: Add pure doctrine types and classification tests

**Files:**
- Create: `Units/CommanderDoctrineService.cs`
- Create: `tests/DoctrineCoreTests.cs` (only if the repository has a test project; inspect before creating)

- [ ] **Step 1: Inspect the existing test setup**

Run:

```powershell
Get-ChildItem -Recurse -Include '*Tests*.csproj','*.Tests.csproj'
```

Expected: either an existing test project path or no output. Do not add a new test framework to the game project.

- [ ] **Step 2: Define doctrine roles and policy values**

Add these internal types in `NuclearOptionCommander`:

```csharp
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
```

- [ ] **Step 3: Implement deterministic role classification**

Implement `CommanderDoctrineService.Classify(Unit unit)` with null/disabled safety and these ordered rules:

```csharp
if (unit is Aircraft) return CommanderDoctrineRole.Recon;
if (unit is Ship) return CommanderDoctrineRole.Frontline;
if (unit.GetComponent<RearmVehicleAI>() != null) return CommanderDoctrineRole.Logistics;
if (unit.GetComponent<Radar>() != null) return CommanderDoctrineRole.Standoff;
if (unit.GetComponent<Turret>() != null && unit.name.IndexOf("SAM", StringComparison.OrdinalIgnoreCase) >= 0)
    return CommanderDoctrineRole.AirDefense;
return CommanderDoctrineRole.Frontline;
```

Keep this method pure apart from reading the unit. Do not scan the scene from it. Use conservative defaults when signatures are unavailable.

- [ ] **Step 4: Add policy lookup**

Implement `GetPolicy(CommanderDoctrineRole role)` returning fixed values:

```csharp
Frontline  => (Frontline, 0f, true)
Standoff   => (Standoff, 500f, false)
AirDefense => (AirDefense, 250f, false)
Recon      => (Recon, 1000f, false)
Logistics  => (Logistics, 750f, false)
```

- [ ] **Step 5: Add classification tests or a documented manual matrix**

For an existing test project, test the enum/policy behavior with fakeable inputs only if the project already supports Unity/game mocks. Otherwise do not introduce mocks; record a manual matrix in the source comments and validate through the compile/manual checks in Task 5.

### Task 2: Implement throttled doctrine snapshot and lifecycle safety

**Files:**
- Modify: `Units/CommanderDoctrineService.cs`

- [ ] **Step 1: Add bounded cache fields**

Use a `Dictionary<Unit, CommanderDoctrineRole>` cache, `List<Unit>` snapshot, `float nextRefreshAt`, and a refresh interval default of `2f`. Do not use `FindObjectsOfType` in `Tick`.

- [ ] **Step 2: Refresh from existing tracked units only**

Add a constructor accepting `CommanderSelectionService`. Refresh the selected units plus any units already exposed by existing marker/selection services only if an existing public collection is available. If no broad tracked collection exists, keep the first implementation selection-scoped rather than scanning the scene.

- [ ] **Step 3: Add dead-reference pruning**

Implement:

```csharp
internal void PruneDeadReferences()
{
    doctrineByUnit.Keys
        .Where(static unit => unit == null || unit.disabled)
        .ToList()
        .ForEach(unit => doctrineByUnit.Remove(unit));
}
```

If LINQ is not used by neighboring hot services, replace this with an indexed temporary list allocated only during the two-second refresh, never every frame. Clear all collections in `ResetSession()`.

- [ ] **Step 4: Add safe query methods**

Implement `TryGetRole(Unit unit, out CommanderDoctrineRole role)`, `TryGetPolicy(Unit unit, out CommanderDoctrinePolicy policy)`, and `CanAdvanceFrontline(Unit unit)`. Unknown units return the conservative frontline policy only when the unit is valid and friendly; invalid units return false.

- [ ] **Step 5: Add `Tick()` with timer gate**

`Tick()` must return before collection work when `Time.unscaledTime < nextRefreshAt`. Set the next refresh before processing. Call `PruneDeadReferences()` during refresh only.

### Task 3: Implement host-authoritative order envelopes

**Files:**
- Create: `Core/CommanderOrderAuthority.cs`

- [ ] **Step 1: Define order types and result enum**

Add:

```csharp
internal enum CommanderOrderKind
{
    Move,
    Attack,
    Hold,
    Retreat,
}

internal enum CommanderOrderResult
{
    Accepted,
    NotServer,
    InvalidSender,
    InvalidUnit,
    Duplicate,
    Stale,
}

internal readonly struct CommanderOrderEnvelope
{
    internal readonly uint CommandId;
    internal readonly int SessionToken;
    internal readonly CommanderOrderKind Kind;
    internal readonly Unit Unit;
    internal readonly GlobalPosition Destination;
    internal readonly float CreatedAt;

    internal CommanderOrderEnvelope(
        uint commandId,
        int sessionToken,
        CommanderOrderKind kind,
        Unit unit,
        GlobalPosition destination,
        float createdAt)
    {
        CommandId = commandId;
        SessionToken = sessionToken;
        Kind = kind;
        Unit = unit;
        Destination = destination;
        CreatedAt = createdAt;
    }
}
```

- [ ] **Step 2: Implement server and ownership checks**

Add `TryAccept(CommanderOrderEnvelope envelope, out CommanderOrderResult result)`:

1. Require `NetworkManagerNuclearOption.i != null && ...Server.Active`.
2. Require `envelope.Unit != null`, not disabled, and friendly to `CommanderGameAccess.GetLocalHq()`.
3. Require the current session token.
4. Reject command IDs already seen.
5. Reject `CreatedAt` older than 10 seconds.
6. Store accepted IDs in a bounded `HashSet<uint>` and FIFO queue capped at 256 entries.

This service validates intent only; it must not call `SetDestination`, spawn, repair, or fire.

- [ ] **Step 3: Add lifecycle methods**

Implement `BeginSession()`, `ResetSession()`, `NextCommandId()`, and `CurrentSessionToken`. Generate a new nonzero session token using `Time.frameCount` plus a monotonically incremented static counter; do not use cryptographic or per-frame allocation-heavy code.

### Task 4: Wire authority and doctrine into the mode controller

**Files:**
- Modify: `Core/CommanderModeController.cs`

- [ ] **Step 1: Add fields and construct services**

Add nullable fields for `CommanderDoctrineService` and `CommanderOrderAuthority`. Construct them after selection/move services are available:

```csharp
selectionService = new CommanderSelectionService();
...
moveService = new CommanderMoveService(selectionService);
doctrineService = new CommanderDoctrineService(selectionService);
orderAuthority = new CommanderOrderAuthority();
```

- [ ] **Step 2: Tick doctrine outside per-frame scans**

Call `doctrineService?.Tick()` only when Commander mode is active. Do not tick authority with a collection loop; authority is called at order submission time.

- [ ] **Step 3: Add activation/reset/deactivation lifecycle calls**

Call `BeginSession()` on activation, `ResetSession()` in `OnActiveSceneChanged`, and `ResetSession()` on deactivation if the mode was active. Ensure scene reset clears both services even when Commander mode is already inactive.

### Task 5: Gate direct move orders without breaking single-player

**Files:**
- Modify: `Units/CommanderMoveService.cs`
- Modify: `Core/CommanderInputController.cs`
- Modify: `Core/CommanderSettings.cs`

- [ ] **Step 1: Add settings**

Add:

```csharp
internal static bool DoctrineEnabled { get => Get("Gameplay", "DoctrineEnabled", true); set => Set("Gameplay", "DoctrineEnabled", value); }
internal static float DoctrineRefreshIntervalSeconds { get => Get("Gameplay", "DoctrineRefreshIntervalSeconds", 2f); set => Set("Gameplay", "DoctrineRefreshIntervalSeconds", value); }
```

Initialize both in `Initialize`.

- [ ] **Step 2: Add move service dependencies**

Extend `CommanderMoveService` constructor with optional `CommanderDoctrineService? doctrineService = null` and store it. Preserve the old call signature only if existing callers require it; otherwise update the single constructor call.

- [ ] **Step 3: Apply the minimal standoff guard**

Before `SetDestination` in `IssueDirectMoveOrder`, query doctrine. If doctrine is enabled, the unit is classified as standoff, and the order is an ordinary frontline move, preserve the destination as an explicit player order but do not silently reject it. Instead, set a status/warning through the existing alert/status path if one is available. The first phase must not change player-issued behavior; automation guardrails apply only to future autonomous orders.

- [ ] **Step 4: Gate client submission**

In the input path, do not block camera/UI interaction. For gameplay order submission in multiplayer, call the authority validator and show the existing status mechanism when the result is `NotServer`. In single-player, retain current behavior by treating the local instance as the executor. Do not add a second network RPC layer in this phase.

### Task 6: Compile and review

**Files:**
- All files above

- [ ] **Step 1: Build without deployment**

Run:

```powershell
 dotnet build NuclearOptionCommander.csproj --configuration Release -p:GameDir="<actual Nuclear Option install directory>"
```

Expected: zero compiler errors. Do not copy the DLL to the game directory.

- [ ] **Step 2: Review references and lifecycle**

Run:

```powershell
Select-String -Path Core\*.cs,Units\*.cs -Pattern 'CommanderDoctrineService|CommanderOrderAuthority'
```

Expected: construction, tick, reset, and deactivation references exist; no stale constructor calls remain.

- [ ] **Step 3: Verify performance constraints**

Confirm manually from the diff:

- no `FindObjectsOfType` in doctrine `Tick`;
- no transform mutation or velocity zeroing;
- no IMGUI allocations;
- refresh interval is at least one second;
- destroyed Unity references are pruned;
- no server-only operation is executed from the client path.

- [ ] **Step 4: Report build status and request deployment confirmation**

If build passes, report the exact build result and ask whether the user wants the DLL deployed. Never deploy automatically.
