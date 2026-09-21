# 01. Entitas & Hirarki Unit Base Game

Dokumentasi kelas dasar entitas tempur di *Nuclear Option* (`Assembly-CSharp.dll`).

---

## 1. Hirarki Pewarisan Kelas Unit

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

## 2. Kelas Inti `Unit`

### Properti & Variabel Utama:
* `string unitName`: Nama unit / panggilan taktis (misal: "Revoker", "T-98 Brawler").
* `UnitDefinition definition`: Definisi asset unit (prefab, ikon, harga, deskripsi).
* `FactionHQ NetworkHQ` / `CurrentHQ`: Faction pemilik unit (Mirage SyncVar).
* `PersistentID persistentID`: ID unik lintas jaringan & database tracking HQ.
* `bool disabled`: `true` jika unit telah hancur / lumpuh permanen.
* `Rigidbody rb`: Komponen fisika utama unit.
* `List<WeaponStation> weaponStations`: Daftar stasiun senjata aktif pada unit.
* `UnitCommand UnitCommand`: Handler perintah tujuan (`SetDestination(GlobalPosition, bool fromPlayer)`).

### Method Penting:
* `Damage(int partIndex, DamageInfo damageInfo)`: Menerapkan kerusakan fisika/komponen.
* `SetHoldPosition(bool hold)`: Memerintahkan AI unit untuk mengerem & menahan posisi.
* `GlobalPosition GlobalPosition()`: Mengembalikan koordinat presisi global dunia game.

---

## 3. Spesialisasi Kelas Tempur

### A. `Aircraft`
* `AutopilotPlane autopilot`: Sistem kendali terbang otomatis sayap tetap.
* `Radar radar`: Sensor radar hidung / kubah pencari target udara.
* `PilotBaseState pilotState`: Mesin status AI pilot (`AIPilotCombatModes`).
* `bool IsOperational()`: Status kelaikan terbang mesin & kontrol aerodinamika.

### B. `GroundVehicle`
* `PathfindingAgent pathfinder`: Agen pencari jalur di atas peta dan jalan raya.
* `void SetHoldPosition(bool hold)`: Mengaktifkan handbrake & menghentikan input gas.
* `ParachuteSystem parachuteSystem`: Sistem parasut saat diturunkan dari udara (*airdrop*).

### C. `Ship`
* `UnitCommand UnitCommand`: Mengendalikan navigasi di jalur laut (*sea lanes*).
* Memiliki turet otomatis berat (`Turret`) dan sistem pertahanan rudal (`CIWS`).

### D. `Building` & `Factory`
* `Factory`: Bangunan industri yang memproduksi unit darat/udara secara berkala (`NetworkproductionUnit`).
* `Airbase`: Mengelola hangar, landasan pacu, dan antrean peluncuran pesawat (`CanSpawnAircraft`).
* `VehicleDepot`: Tempat pengadaan & deployment kendaraan darat (`TrySpawnVehicle`).
