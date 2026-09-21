# 02. AI, Autopilot, & Pathfinding Subsystems

Dokumentasi perilaku kecerdasan buatan (*Artificial Intelligence*), navigasi, dan logistik lapangan di *Nuclear Option*.

---

## 1. Arsitektur AI Pilot Pesawat

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

### Parameter Penting `AIPilotCombatModes`:
* `FieldInfo destination`: Koordinat target jelajah atau area patroli.
* `FieldInfo targetHeight`: Ketinggian target penerbangan jelajah (ASL).
* `FieldInfo timeWithoutTarget`: Timer sebelum pesawat memutuskan *Return to Base (RTB)*.

---

## 2. Navigasi Darat (`PathfindingAgent` & `RoadNetwork`)

### Cara Kerja Pathfinding:
1. Unit darat menggunakan `PathfindingAgent` untuk menavigasi topografi 3D.
2. Secara default, unit darat mengutamakan `LevelInfo.i.roadNetwork` untuk menghemat konsumsi energi dan menghindari medan terjal.
3. Mod NOCommander menginjeksi `CommanderDirectPathService.TryApplyShortcut` pada `PathfindingAgent.Pathfind` untuk memotong jalur langsung (*off-road shortcut*) saat diperintahkan pemain.

---

## 3. Sistem Logistik & Reparasi Darat (`RearmVehicleAI`)

* `RearmVehicleAI`: AI khusus kendaraan logistik amunisi (*Munitions Truck*).
* `RearmMissionController`: Mengelola antrean misi pengisian ulang amunisi unit garis depan.
* Saat unit logistik dikembalikan ke kontrol AI:
  * Panggil `RearmVehicleAI.DriveToRestock()` jika amunisi truk $< 50%$.
  * Panggil `RearmVehicleAI.Wait()` jika truk sudah siap beroperasi.
