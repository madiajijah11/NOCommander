# NOCommander DeepWiki - AI & Developer Reference

Comprehensive internal technical reference and knowledge base covering the *Nuclear Option* game engine (`Assembly-CSharp.dll`), subsystems, base game API, and **NOCommander** mod architecture.

---

## 📚 DeepWiki Table of Contents

1. **[01. Base Game Entities & Unit Class Hierarchy](01-basegame-entities-and-units.md)**
   - `Unit` base class hierarchy (`Aircraft`, `GroundVehicle`, `Ship`, `Building`, `Missile`, `Munition`).
   - `FactionHQ`, `UnitDefinition`, `Loadout`, `HardpointSet`, and `DamageInfo` systems.
   - Physical constraints, G-limits, and `Radar Cross Section (RCS)` tracking.

2. **[02. AI, Autopilot, & Pathfinding Subsystems](02-ai-and-pathfinding-subsystems.md)**
   - Aircraft & helicopter flight state machines (`AutopilotPlane`, `AutopilotHelo`, `AIPilotCombatModes`).
   - Ground logistics & rearm dispatch (`RearmVehicleAI`, `RearmMissionController`).
   - Navigation agents & road networks (`PathfindingAgent`, `RoadNetwork`, `Shortcut`).

3. **[03. Weapons, Radar Sensors, & Electronic Warfare](03-weapons-radar-and-iads.md)**
   - Radar emission & turret acquisition systems (`Radar`, `Turret`, `FireControl`, `Jammer`).
   - Target acquisition modes (`searchForRadar`, `targetAcquisitionMode`, `RpcToggleRadar`).
   - Laser designation & Integrated Air Defense System (IADS) EMCON logic.

4. **[04. Logistics, Economy, & Supply Chains](04-logistics-economy-and-production.md)**
   - Industrial factories & production lines (`Factory`, `NetworkproductionUnit`).
   - Airbases, runways, and vehicle depots (`Airbase`, `VehicleDepot`, `TrySpawnVehicle`).
   - Sling-load cargo & airdrops (`MountedCargo`, `Container`, `ParachuteSystem`).

5. **[05. Harmony Patching, Reflection, & Mirage Networking](05-harmony-patches-and-networking.md)**
   - Active Harmony injection points across the codebase.
   - Private reflection field index (`AccessTools.Field / Method`).
   - Server vs client network authority with Mirage Networking (`NetworkManagerNuclearOption`, `Spawner`).

---

## 🎯 Core Engineering Guidelines for AI Agents
- **Memory Safety:** Always invoke `PruneDeadReferences()` on any collections holding `Unit` / `GameObject`.
- **Physics Safety:** Never zero `rb.velocity` in `Update()` (use `SetUnitHoldPosition(true)`).
- **Zero-GC UI:** Use statically cached styles, textures, and structs from `CommanderUiTheme.cs`.
- **Server Guard:** Guard network spawning actions with `NetworkManagerNuclearOption.i.Server.Active`.
