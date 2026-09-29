# Gameplay QoL Next Stages

## Scope
Implement four independent gameplay QoL stages using existing NOCommander services/base-game AI:

1. Mission completion cleanup.
2. Damage-state behavior.
3. Smart logistics priority.
4. Convoy reroute.

Constraints: event-driven or throttled checks (minimum 1s), no secondary cameras/render textures, no global per-frame scans, reuse existing commands and mission controllers, no deployment without explicit confirmation.

## Current extension points
- Air mission RTB and lifecycle: `AirCommand/CommanderAirCommandService.cs`, `AirCommand/CommanderAirCommandPatches.cs`.
- Cargo mission lifecycle: `Supply/CommanderSupplyHeliService.cs`, `Supply/CommanderSupplyHeliMission.cs`.
- Existing throttled unit behavior: `Units/CommanderMoveService.cs`.
- Base-game repair/rearm dispatch: `RearmMissionController`, `Rearmer`, `Repairer`, `CommanderRepairPatches.cs`.
- Pathfinding/convoy APIs require assembly verification before adding hooks; do not guess method names.

## Implementation order

### 1. Mission cleanup
- Add bounded, throttled stale/complete checks to existing air and cargo mission services.
- Reuse existing RTB methods.
- Clear mission records, pinned units, visuals, terrain-autopilot bindings only after return/disable/completion signals.
- Avoid changing active mission behavior when target remains valid.

### 2. Damage-state behavior
- Add configurable damage-state thresholds.
- Evaluate only tracked aircraft/units on existing throttles.
- Damaged aircraft abort to existing landing state; damaged ground units hold/retreat only when a valid logistics/repair destination exists.
- Never force physics velocity in Update.

### 3. Smart logistics priority
- Add priority score from damage, ammo, fuel, and distance.
- Sort/select only when a rearm/repair mission is created or refreshed.
- Reuse `RearmMissionController`; do not run repeated global scans.
- Prune destroyed Unity references.

### 4. Convoy reroute
- First inspect Assembly-CSharp APIs for convoy/pathfinding route failure signals.
- Hook route-failure/target-invalid event if available; otherwise throttle checks at >=1s for already tracked convoy units only.
- Reuse existing destination/pathfinding command; no route calculation per frame.
- Add loop guard/cooldown to prevent reroute oscillation.

## Verification
- Write focused pure/helper tests first where test infrastructure exists; otherwise run static checks and build against game assemblies.
- Run `git diff --check`.
- Run available compiler/build command; report unavailable tools honestly.
- Review full diff and stale references.
- Do not copy DLLs to game directory.
