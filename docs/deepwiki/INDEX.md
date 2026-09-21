# NOCommander DeepWiki - AI & Developer Reference

Dokumentasi internal & basis pengetahuan teknis (*Knowledge Base*) komprehensif mengenai struktur engine *Nuclear Option* (`Assembly-CSharp.dll`), subsistem game, API referensi, dan arsitektur mod **NOCommander**.

---

## 📚 Daftar Isi DeepWiki

1. **[01. Entitas & Hirarki Unit Base Game](01-basegame-entities-and-units.md)**
   - Hirarki kelas `Unit` (`Aircraft`, `GroundVehicle`, `Ship`, `Building`, `Missile`, `Munition`).
   - Sistem `FactionHQ`, `UnitDefinition`, `Loadout`, `HardpointSet`, dan `DamageInfo`.
   - Parameter fisik, batas G, dan `Radar Cross Section (RCS)`.

2. **[02. AI, Autopilot, & Pathfinding Subsystems](02-ai-and-pathfinding-subsystems.md)**
   - Autopilot pesawat & helikopter (`AutopilotPlane`, `AutopilotHelo`, `AIPilotCombatModes`).
   - AI logistik darat & rearm (`RearmVehicleAI`, `RearmMissionController`).
   - Agen navigasi darat & jaringan jalan (`PathfindingAgent`, `RoadNetwork`, `Shortcut`).

3. **[03. Senjata, Sensor Radar, & Peperangan Elektronik](03-weapons-radar-and-iads.md)**
   - Sistem radar & kuncian turet (`Radar`, `Turret`, `FireControl`, `Jammer`).
   - Mode akuisisi target (`searchForRadar`, `targetAcquisitionMode`, `RpcToggleRadar`).
   - Penandaan laser & integrasi sistem IADS.

4. **[04. Logistik, Ekonomi, & Rantai Pasokan](04-logistics-economy-and-production.md)**
   - Pabrik & jalur produksi (`Factory`, `NetworkproductionUnit`).
   - Pangkalan udara & depot kendaraan (`Airbase`, `VehicleDepot`, `TrySpawnVehicle`).
   - Kargo gantung (*sling-load*) & parasut (`MountedCargo`, `Container`, `ParachuteSystem`).

5. **[05. Harmony Patching, Refleksi, & Jaringan Mirage](05-harmony-patches-and-networking.md)**
   - Titik injeksi Harmony aktif di seluruh codebase.
   - Peta field private yang diakses via refleksi (`AccessTools.Field / Method`).
   - Otoritas server vs client di Mirage Networking (`NetworkManagerNuclearOption`, `Spawner`).

---

## 🎯 Panduan Modding Cepat untuk AI Agent
- **Memory Safety:** Selalu jalankan `PruneDeadReferences()` pada koleksi `Unit` / `GameObject`.
- **Physics Rule:** Jangan zeroing `rb.velocity` di `Update()` (gunakan `SetUnitHoldPosition(true)`).
- **Zero-GC UI:** Gunakan style dan texture yang sudah di-cache di `CommanderUiTheme.cs`.
- **Server Guard:** Periksa `NetworkManagerNuclearOption.i.Server.Active` untuk aksi spawn/network entity.
