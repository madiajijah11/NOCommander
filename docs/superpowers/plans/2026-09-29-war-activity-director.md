# War Activity Director

## Goal
Convert idle faction money/reserves into sustained, visible warfare without frame-time regressions. Existing factory, spawn, air, naval, and logistics services remain authoritative; this feature only schedules bounded actions.

## Scope
1. Force Activity Monitor: throttled snapshot of active friendly air/sea/ground/logistics counts, factories, funds, reserve/queue state.
2. Auto-Reinforcement: host-only, cooldown-gated requests when active force falls below doctrine targets.
3. Budget + doctrine: balanced/air/ground/naval/defensive profiles; spend caps and minimum reserve.
4. Loss replacement: target deficits derived from active counts; no immediate respawn loops.
5. Frontline presence: choose existing friendly operational points; send only spawned/available units through existing commands.
6. Air tempo: maintain minimum CAP/strike/support through current air mission service, throttled.
7. Naval patrol: maintain minimum patrol/escort tasks through current naval purchase/service APIs; no guessed game methods.
8. Logistics backbone: maintain cargo/rearm/repair minimums before combat expansion.

## Files
- `Core/CommanderSettings.cs`: opt-in settings, doctrine, targets, cooldowns, budget cap.
- `Core/CommanderWarActivityService.cs`: monitor, scoring, bounded orchestration, dead-ref pruning/reset.
- `Core/CommanderModeController.cs`: construct/tick/reset/activate service.
- Existing `Core/CommanderFactoryProductionService.cs`, `Core/CommanderSpawnService.cs`, `AirCommand/*`, `Depot/*`, `Supply/*`: minimal integration calls only after API verification.
- `UI/CommanderOverlayUi.cs`: compact status/toggle only; cached styles/no allocations.

## Rules
- Host authority for all production/spawn/network actions.
- No `FindObjectsOfType` or `Resources.FindObjectsOfTypeAll` in Tick; refresh only via existing cached services and >=2s scheduler.
- No automatic action until explicit setting enabled; default conservative targets.
- Prioritize replacements/logistics, then doctrine combat units; enforce cooldown and funds reserve.
- Never fabricate API names. Inspect source/DeepWiki/game assembly references first.
- Preserve session reset and destroyed Unity-object pruning.

## Implementation order
1. Map APIs + monitor/status only.
2. Add settings and doctrine score.
3. Add production/reinforcement dispatch using existing factory setter and spawn queue.
4. Add air/naval/logistics schedulers only where concrete APIs exist.
5. Add frontline routing via existing `UnitCommand.SetDestination` with >=3s cooldown.
6. UI status/toggle.
7. Static checks; compiler if available; in-game validation still required; no deployment.
