# 05. Harmony Patching, Refleksi, & Jaringan Mirage

Panduan teknis mengenai seluruh titik injeksi (*Hook Points*) dan integritas jaringan multiplayer di NOCommander.

---

## 1. Daftar Patch Harmony Utama

| Target Kelas | Target Method | Jenis Patch | Fungsi di NOCommander |
| :--- | :--- | :--- | :--- |
| `Unit` | `Damage` | Postfix | Mendeteksi unit friendly yang terkena serangan (*Alert / Incident Jump*) |
| `PathfindingAgent` | `Pathfind` | Prefix | Menerapkan jalan pintas langsung (*Direct Off-road Routing*) |
| `VehicleDepot` | `TrySpawnVehicle` | Prefix | Filter deployment cadangan faksi (*Faction Reserve System*) |
| `FactionHQ` | `DeployVehicles` | Prefix/Postfix | Sinkronisasi status deployment kendaraan otomatis |
| `Repairer` | `SearchForRepair` | Prefix | Mengarahkan Jackknife ke unit terdekat (*Nearest Damaged Triage*) |

---

## 2. Akses Refleksi Private (`AccessTools`)

Untuk menjaga stabilitas saat game update, semua refleksi dibungkus dengan pengecekan `null`:

```csharp
// Contoh standar pemanggilan refleksi aman:
private static readonly FieldInfo? FireControlModeField = AccessTools.Field(typeof(FireControl), "targetAcquisitionMode");

// Penggunaan aman:
if (FireControlModeField != null)
{
    FireControlModeField.SetValue(instance, "searchForRadar");
}
```

---

## 3. Otoritas Mirage Networking (Host vs Client)

Pada sesi Multiplayer, pemanggilan `Spawner` atau modifikasi entitas jaringan hanya diizinkan di sisi server:

```csharp
if (NetworkManagerNuclearOption.i == null || !NetworkManagerNuclearOption.i.Server.Active)
{
    SetStatus("Perintah ini hanya dapat dijalankan oleh Host di mode Multiplayer.");
    return;
}
```
