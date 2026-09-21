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

* Heavy transport helicopters (`VL-49 Tarantula`, `UH-90 Ibis`) carry ammunition containers (`Container`) via cargo mounts.
* `MountedCargo.RemoveFromHardpoint()`: Releases cargo at Landing Zones (LZ) or initiates parachute airdrops.
