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
   └── AIHeloTransportState (Helicopter Logistics & Cargo Transport)
         ├── TransportMode (Cargo, Sling, Drop, Transit)
         ├── Landing Zone (LZ) & SearchForLandingSpot Logic
         ├── Sling-load Cargo Pickup & Dropoff
         ├── Countermeasures & Missile Alert Reaction
         └── Airdrop Parachute Drop Conditions
```

### Critical Fields in `AIPilotCombatModes`:
* `FieldInfo destination`: Target patrol center or waypoint coordinates.
* `FieldInfo targetHeight`: Assigned cruise altitude above sea level (ASL).
* `FieldInfo timeWithoutTarget`: Timer elapsed before deciding to Return to Base (RTB).

### Critical Fields & Methods in `AIHeloTransportState`:
* `TransportMode transportMode`: Active helicopter logistics mode.
* `TransportDestination transportDestination`: Target landing pad, airfield, or field LZ.
* `void SearchForLandingSpot()`: Terrain clearance and slope calculation for touchdown.
* `void DeployCargo()`: Drops sling load or airdrop container.
* `void ChooseCountermeasures()`: Automated Flare/Chaff deployment on missile alert.
* `float timeWithoutMission`: Idle timeout before returning to nearest base.


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

---

## 4. Ground Unit Role & Standoff Doctrine

### Engine Limitation:
The base game assigns identical pathfinding and waypoint routing (`UnitCommand.SetDestination`) to all `GroundVehicle` entities. Without behavioral intervention:
1. Long-range standoff units (**StratoLance R9**, **Boltstrike**, **Radar Trucks**) will naively follow `MissionPosition` objectives along roads into enemy fire alongside frontline tanks.
2. AI aircraft spawn and fly directly into mission airspace without orbit staging or standoff holding.

### NOCommander Standoff Doctrine Guardrail:
- Standoff artillery, ballistic missile launchers, and radar vehicles must be excluded from automated frontline objective pushes (`CommanderAlliedAiService.ExecuteBattlegroupCoordination`).
- Units matching standoff signatures should be anchored at rear perimeters or dedicated defensive fire positions with `CommanderGameAccess.SetUnitHoldPosition(unit, true)`.

