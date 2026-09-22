# 04. Logistics, Economy, & Supply Chains

Technical documentation covering resource flows, industrial factories, depot spawning, and air logistics in *Nuclear Option*.

---

## 1. Production Factories (`Factory`)

* Connected directly to `FactionHQ`, assembling units incrementally over time.
* Property `NetworkproductionUnit`: The unit blueprint currently in the factory build queue.
* NOCommander intercepts automatic deployment via `CommanderFactionVehicleService` to hold units in the tactical reserve pool.

---

## 2. Vehicle Depots (`VehicleDepot`)

* `bool TrySpawnVehicle(VehicleDefinition def)`: Official base game method to instantiate ground vehicles from depots.
* Governed by `CommanderSpawnService` with spawn queue management, rally points, and formation placement.

---

## 3. Helicopter Logistics & Airdrops (`MountedCargo`)

---

## 4. Airbases & Base Capture Mechanics (`Airbase` & `Capture`)

### A. Airbase Operations (`Airbase`)
* `IReadOnlyList<Aircraft> ControlledAircraft`: Active aircraft assigned to this airbase.
* `IReadOnlyList<Hangar> hangars`: Storage facilities supporting rearm and repair cycles.
* `FactionHQ CurrentHQ`: Faction controlling the runway and operations.
* `event Action onLostControl`: Dispatched when enemy ground forces successfully capture the base.

### B. Territorial Sector Capture (`Capture`)
* `float controlBalance`: Network-synchronized capture progress ($0.0 \dots 1.0$).
* `FactionHQ capturingHQ`: The faction with superior ground unit presence currently capturing the facility.
* `bool capturable`: Whether the base can currently be contested.
* `void ForceCapture(FactionHQ newHq)`: Administrative/instantaneous capture override.
* **Capture Mechanic:** Units with ground combat presence within the base capture radius accumulate capture credit each `checkInterval` (2s). Once `controlBalance` reaches threshold, the facility transitions to the invading faction.

