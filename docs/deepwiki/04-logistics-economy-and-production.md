# 04. Logistik, Ekonomi, & Rantai Pasokan

Dokumentasi siklus logistik, pangkalan produksi, dan perputaran dana di *Nuclear Option*.

---

## 1. Pabrik & Pangkalan Produksi (`Factory`)

* Pabrik terhubung ke `FactionHQ` dan memproduksi unit secara periodik.
* Properti `NetworkproductionUnit`: Unit yang sedang dirakit di jalur produksi.
* Mod NOCommander mencegat deployment otomatis via `CommanderFactionVehicleService` agar pemain/AI komandan bisa menahan unit hasil produksi ke dalam cadangan strategis (*Reserve Pool*).

---

## 2. Depot Kendaraan (`VehicleDepot`)

* `bool TrySpawnVehicle(VehicleDefinition def)`: Method resmi game untuk menginstansiasi unit darat dari depot.
* Diatur oleh sistem antrean `CommanderSpawnService` yang mendukung titik kumpul otomatis (*Rally Points*) dan formasi penempatan.

---

## 3. Angkut Kargo Helikopter & Airdrop (`MountedCargo`)

* Helikopter angkut (`VL-49 Tarantula`, `UH-90 Ibis`) membawa kontainer suplai amunisi (`Container`) pada hardpoint kargo.
* `MountedCargo.RemoveFromHardpoint()`: Melepaskan sling-load atau kontainer di zona pendaratan (*Landing Zone*) atau penerjunan udara (*Airdrop Parachute*).
