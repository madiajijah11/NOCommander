# 01. Base Game Entities & Unit Class Hierarchy

Technical documentation for combat entities and data structures in *Nuclear Option* (`Assembly-CSharp.dll`).

---

## 1. Unit Class Inheritance Hierarchy

```
UnityEngine.MonoBehaviour
   └── NetworkBehaviour (Mirage)
         └── Unit
               ├── Aircraft
               ├── GroundVehicle
               ├── Ship
               ├── Building
               └── Missile / Munition
```

---

## 2. Core `Unit` Class

### Key Properties & Fields:
* `string unitName`: Tactical unit display name (e.g. "Revoker", "T-98 Brawler").
* `UnitDefinition definition`: Base asset definition (prefab, icon, cost, descriptions).
* `FactionHQ NetworkHQ` / `CurrentHQ`: Controlling faction headquarters (Mirage SyncVar).
* `PersistentID persistentID`: Unique identifier across network state and HQ tracking databases.
* `bool disabled`: `true` when the unit is destroyed, derelict, or non-functional.
* `Rigidbody rb`: Primary physics component.
* `List<WeaponStation> weaponStations`: List of active weapon stations on the chassis.
* `UnitCommand UnitCommand`: Command interface for waypoints (`SetDestination(GlobalPosition, bool fromPlayer)`).

### Key Methods:
* `Damage(int partIndex, DamageInfo damageInfo)`: Applies structural/component damage.
* `SetHoldPosition(bool hold)`: Signals unit AI to halt, apply brakes, and hold current position.
* `GlobalPosition GlobalPosition()`: Returns double-precision world position coordinates.

---

## 3. Combat Class Specializations

### A. `Aircraft`
* `AutopilotPlane autopilot`: Fixed-wing flight control and stability system.
* `Radar radar`: Nose/rotodome radar sensor for aerial target acquisition.
* `PilotBaseState pilotState`: Pilot state machine (`AIPilotCombatModes`).
* `bool IsOperational()`: Returns true if engine, control surfaces, and cockpit remain functional.

### B. `GroundVehicle`
* `PathfindingAgent pathfinder`: Real-time pathfinding over terrain and road networks.
* `void SetHoldPosition(bool hold)`: Engages vehicle handbrake and suppresses throttle input.
* `ParachuteSystem parachuteSystem`: Parachute deployment system for aerial cargo drops.

### C. `Ship`
* `UnitCommand UnitCommand`: Navigates along sea lanes (`RoadNetwork seaLanes`).
* Equipped with heavy autocannon turrets (`Turret`) and Point Defense (`CIWS`).

### D. `Building` & `Factory`
* `Factory`: Industrial production facility producing ground/air units periodically (`NetworkproductionUnit`).
* `Airbase`: Manages runway slots, hangars, and takeoff queues (`CanSpawnAircraft`).
* `VehicleDepot`: Ground unit deployment and purchase depot (`TrySpawnVehicle`).
