# Design Specification: Multiplayer Gameplay QoL Automation

## 1. Scope

Add gameplay automation for NOCommander without replacing player intent or making clients authoritative. The system targets Escalation and Terminal Control first, then expands across ground, logistics, air operations, reconnaissance, and counter-battery play.

Non-goals:
- No per-frame transform or physics control.
- No client-authoritative spawn, repair, firing, or enemy-state mutation.
- No omniscient enemy detection.
- No secondary cameras or unthrottled scene-wide scans.

## 2. Authority Model

Clients create and display orders. The host validates and executes them.

- Client: selection, order intent, local previews, UI state.
- Host: faction/ownership checks, doctrine evaluation, threat scans, path requests, mission queues, spawn, repair, and fire orders.
- Network payloads: compact order intents, state transitions, target IDs, waypoint/anchor data, timestamps, and command IDs.
- Commands are idempotent. The host rejects stale, duplicate, unauthorized, or invalid commands.
- Native Mirage objects remain the source of truth for unit state.

Any feature that mutates gameplay must be gated by server authority. Clients may predict visuals only.

## 3. Shared Gameplay Model

### Doctrine roles

Units receive derived roles rather than permanent class changes:

- Frontline: tanks, IFVs, assault units.
- Standoff: artillery, MLRS, ballistic launchers, radar vehicles.
- Air defense: SPAAG, short-range SAM, long-range SAM.
- Recon: scouts, recon aircraft, AWACS.
- Logistics: supply, repair, transport, depot assets.

Each role has an anchor distance, movement preference, fallback behavior, ROE, and service thresholds. Existing `CommanderGameAccess`, `CommanderMoveService`, `CommanderStanceService`, and native `PathfindingAgent`/`UnitCommand` behavior are reused.

### Threat knowledge

The host maintains a bounded, throttled threat cache from known radar contacts, confirmed damage/fire events, markers, and recon reports. Contacts have a freshness timestamp and confidence. Threat zones affect route selection and doctrine decisions; they do not reveal unknown enemy units.

Scans run on staggered timers (minimum one second for collection queries). Destroyed Unity references are pruned. Session reset clears caches and delegates.

## 4. Feature Rollout

### Phase 1 — Doctrine and order authority

Implement host-validated order envelopes and doctrine roles. Add anchor/hold/fallback behavior while preserving native AI movement. Standoff units are excluded from frontline pushes and can receive defensive fire positions.

### Phase 2 — Threat-aware ground movement

Add route policy that prefers safe known corridors, avoids active threat zones when alternatives exist, and falls back to native road pathfinding when no safe route is available. The host owns route decisions; clients render previews.

### Phase 3 — Logistics and objective defense

Low-ammo, damaged, or disabled units enter a host-side service queue. The system selects compatible rearm/repair assets, stores the interrupted mission, and resumes it after service. Defense zones assign patrol anchors, ROE, fallback points, and alert responses.

### Phase 4 — Battle groups and reinforcement

Battle groups expose Advance, Defend, Assault, Retreat, Escort, and Hold orders. Formation spacing derives from role and native movement capabilities. Factories/depots feed a host-side reserve pool with rally points and mission templates. Spawn remains server-only.

### Phase 5 — Air mission queue and reconnaissance

Queue CAS, Strike, CAP, AWACS, ARAD, and Recon missions. Native aircraft AI receives bounded destinations, altitude, target-area, and RTB intent. Bingo fuel, Winchester, damage, loss of base, and no-target timeout trigger RTB. Recon contacts expire and become stale/lost.

### Phase 6 — Counter-battery and emergency response

Use confirmed firing/contact events to estimate artillery origins. Host selects eligible artillery/MRLS units according to range, ammunition, ROE, and confidence. Add base-under-attack intercept and threatened-radar responses without granting unknown enemy information.

## 5. Lifecycle and Performance

Services subscribe/unsubscribe during `CommanderModeController` session transitions. Every unit/depot collection has dead-reference pruning. Expensive threat, distance, and path evaluations are timer-gated and staggered. No allocations in IMGUI hot loops. No cumulative transform mutations. No secondary cameras.

## 6. Multiplayer Safety

- Validate sender, server authority, faction, ownership, scene/session token, command sequence, and target validity.
- Replicate accepted state changes only; never trust client-reported unit state.
- Use command IDs to prevent duplicate execution after retries.
- Reject commands when the unit is destroyed, disabled, captured, or no longer owned.
- Keep host and client UI tolerant of delayed or rejected commands.
- Test host, joining client, late join, disconnect, scene reload, and authority loss.

## 7. Validation

Each phase requires:

1. Clean compile against game assemblies.
2. Single-player regression check.
3. Host/client command and rejection tests.
4. Late-join and scene-reset tests.
5. Performance check for scan cadence, allocations, and frame time.
6. Full diff review before any deployment.

Deployment remains explicit: build success does not authorize copying DLLs to the game directory.
