# Multiplayer QoL — Remaining Phases (3–6)

Status: **Phase 1 + Phase 2 implemented and compiling. Phase 3–6 NOT started.**
Branch state as of this note: build clean (`0 Warning(s) 0 Error(s)`), DLL **not deployed**.

Design authority: `docs/superpowers/specs/2026-03-31-multiplayer-gameplay-qol-design.md`.
Phase 1 plan: `docs/superpowers/plans/2026-03-31-multiplayer-doctrine-core-plan.md`.

## Done

| Phase | Scope | Files |
| --- | --- | --- |
| 1 | Doctrine roles + host order authority | `Units/CommanderDoctrineService.cs`, `Core/CommanderOrderAuthority.cs`, `Core/CommanderSettings.cs`, `Core/CommanderModeController.cs`, `Units/CommanderMoveService.cs` |
| 2 | Threat-aware ground movement (host gate) | `Units/CommanderRadarService.cs`, `Units/CommanderMoveService.cs`, `Core/CommanderSettings.cs` |

Phase 2 behavior recap: host checks `CommanderRadarService.IsPositionDangerous(dest, ThreatDangerRadiusMeters)`
(radar contacts + counter-battery pings, 1 s cache) before `SetDestination`. Non-Frontline doctrine roles are
rejected; explicit Frontline player orders execute with a throttled `[THREAT]` warning. Rejected queued
waypoints stay queued (peek before dequeue).
Settings: `ThreatAwareMovementEnabled` (default true), `ThreatDangerRadiusMeters` (default 200).

## Audit results — host gates already present (no work needed)

- `Core/CommanderWarActivityService.cs` — `IsHost()` (line ~319) short-circuits every autonomous action
  (supply run, ground routing, air reinforcement, naval purchase/patrol, vehicle purchase + spawn).
- `Supply/CommanderSupplyHeliService.cs:1382` `CanHostSpawn(...)` rejects non-host.
- `Units/CommanderMobileEmplacementService.cs:151` `BeginRelocation()` host-only.
- `Naval/CommanderNavalPurchaseService.cs:127,234` host-gated spawn.
- `AirCommand/CommanderAirCommandService.cs:1039` host-gated.

## Phase 3 — Auto-logistics + objective defense

1. `AirCommand/CommanderAirLoiterService.cs:103` — `SetDestination(orbitTarget.ToGlobalPosition(), false)`
   runs **every frame for every orbit**, with no host check. Add a host gate + ≥1 s re-issue throttle
   (orbit waypoints only need ~1 Hz, not 60 Hz).
2. `Units/CommanderRepairPatches.cs:63` — Harmony `Repairer` prefix issues a move to the repair target.
   Already 30 s throttled and `FindNearestRepairTarget` filters by `repairerUnit.NetworkHQ`, but not
   host-gated. Add the same host guard.
3. `Core/CommanderGameAccess.cs:449` `TrySetDestination(Unit, GlobalPosition)` is the shared chokepoint used
   by `Depot/CommanderSpawnService.cs:1010` (rally dispatch), `Naval/CommanderNavalPurchaseService.cs:269`,
   and `Core/CommanderWarActivityService.cs:200,254`. Gating it **once** covers all three callers.
4. Objective defense: reuse `CommanderDoctrineService` roles + `CommanderRadarService.IsPositionDangerous`
   to hold objective-adjacent units in Standoff when threats approach; must run host-side only.

## Phase 4 — Battle groups + reinforcement

- Reuse `Units/CommanderControlGroupsService.cs` grouping data as battle-group membership.
- Reinforcement requests must flow through `CommanderOrderAuthority` (host-validated, idempotent command IDs).
- Host-only spending guard: all purchase/spawn paths must call `IsHost()` first.

## Phase 5 — Air mission queue + recon

- Mission queue entries are orders → must pass `CommanderOrderAuthority.TryAccept` (new `CommanderOrderKind`
  value if needed, e.g. `AirMission`).
- Recon freshness: `TrackingInfo.lastKnownPosition` / `lastSpottedTime` are last-known, not live. Never treat
  stale contacts as exact; carry a staleness bound (markers use ≤8 s today).
- No secondary camera / off-screen render textures for recon rendering (AGENTS.md ban).

## Phase 6 — Counter-battery + emergency response

- `Units/CommanderCounterBatteryRadarService.cs` already exposes `ActivePings` (25 s TTL, max 8) and is
  host-fed by the `Weapon.Fire` Postfix → `NotifyWeaponFired`.
- Emergency response = automated order issuance → must be host-gated through the same authority path,
  never client-side.

## Hard constraints for all remaining phases

- Host-authoritative: validate faction/ownership/doctrine/threat on the host **before** any world mutation
  (`SetDestination`, spawn, repair, fire). Clients get intent/UI/preview only.
- AGENTS.md: no `FindObjectsOfType` in hot loops, no cumulative transform mutation, no secondary cameras,
  never zero `rb.velocity` in `Update`, always `PruneDeadReferences()`, ≥1 s throttles, zero-GC cached UI styles.
- Never deploy the DLL to the game directory without explicit user confirmation.
- Compile gate: 0 errors, 0 warnings.

## Reusable API facts (verified against Assembly-CSharp)

- `Radar.detectedTargets` is `List<Unit>`; skip `!radar.activated || !radar.IsOperational()`.
- `CommanderCounterBatteryRadarService.CounterBatteryPing` is a **nested** type — fully qualify it.
- `FastMath.InRange(Vector3, Vector3, float)` / `FastMath.Distance` are GAME-assembly statics.
- `CommanderGameAccess.HorizontalDistance(Vector3, Vector3)`; `GlobalPosition.ToLocalPosition()`.
- `hq.IsTargetPositionAccurate(target, meters)` gates stale-intel accuracy.
- `Core/CommanderScheduler.cs`: `IsDue(ref float nextRun, float interval)`, `Stagger(taskName, interval, maxDelay)`.
