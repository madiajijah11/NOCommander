# 02. AI, Autopilot, & Pathfinding Subsystems

Technical documentation covering Artificial Intelligence, navigation agents, and logistics state machines in *Nuclear Option*.

---

## 1. Aircraft AI State Machine

```
PilotBaseState (Base State Machine)
   ├── AIPilotCombatModes (Combat & Mission State)
   │     ├── Target Engagement (AirGuard, CAS, Strike, ARAD)
   │     ├── Target Altitude & Area Constraints
   │     └── Winchester & Bingo Fuel Logic
   └── AIHeloTransportState (Helicopter Logistics)
         ├── Landing Zone (LZ) Calculation
         ├── Sling-load Cargo Pickup & Dropoff
         └── Airdrop Parachute Drop Conditions
```

### Critical Fields in `AIPilotCombatModes`:
* `FieldInfo destination`: Target patrol center or waypoint coordinates.
* `FieldInfo targetHeight`: Assigned cruise altitude above sea level (ASL).
* `FieldInfo timeWithoutTarget`: Timer elapsed before deciding to Return to Base (RTB).

---

## 2. Ground Navigation (`PathfindingAgent` & `RoadNetwork`)

### Pathfinding Mechanics:
1. Ground units utilize `PathfindingAgent` to calculate routes over 3D terrain.
2. By default, units prefer paved roads via `LevelInfo.i.roadNetwork` to maximize speed and avoid steep inclines.
3. NOCommander intercepts via Harmony prefix `CommanderDirectPathService.TryApplyShortcut` on `PathfindingAgent.Pathfind` to allow direct cross-country (off-road) routes on player demand.

---

## 3. Logistics & Resupply Subsystems (`RearmVehicleAI`)

* `RearmVehicleAI`: AI logic controlling ammunition supply trucks.
* `RearmMissionController`: Manages queue of field resupply missions for frontline combat units.
* When returning a logistics unit to base AI:
  * Invoke `RearmVehicleAI.DriveToRestock()` if ammo capacity $< 50%$.
  * Invoke `RearmVehicleAI.Wait()` if the vehicle is fully restocked and idle.
