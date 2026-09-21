# 03. Senjata, Sensor Radar, & Peperangan Elektronik

Dokumentasi sistem sensor, turet tempur, dan rudal di *Nuclear Option*.

---

## 1. Sistem Sensor Radar (`Radar`)

### Properti & Method Utama:
* `bool activated`: Status emisi radar aktif/nonaktif.
* `float range`: Jangkauan pancaran gelombang radar dalam meter.
* `List<Unit> detectedTargets`: Daftar kontak target yang sedang terkunci dalam kerucut radar.
* `bool IsOperational()`: Memeriksa apakah antena/sistem radar tidak rusak.
* `void ResetRotators()`: Mengembalikan antena putar radar ke posisi netral saat dimatikan.

---

## 2. Sistem Kontrol Turet (`Turret` & `FireControl`)

### Mode Akuisisi Target (`targetAcquisitionMode`):
* `"searchForRadar"`: Mode pelacak radar pasif (khusus baterai SAM dan rudal ARAD).
* `"automatic"`: Menyerang semua target musuh yang masuk jangkauan tembak.
* `"manual"`: Turet menunggu instruksi tembak langsung.

### Penanganan Stance / Hold Fire di NOCommander:
* `CommanderStanceService` mematikan `turret.enabled = false` dan `fireControl.enabled = false` pada unit yang diatur dalam status **HOLD FIRE** agar tidak membocorkan posisi ke musuh sebelum waktunya.

---

## 3. Model Kerusakan (`DamageInfo`)

* `float hitpoints`: Kerusakan langsung pada komponen / modul fungsional.
* `float structuralHitpoints`: Kerusakan pada integritas rangka fisik utama.
* `float armorDamage`: Tingkat penetrasi peluru terhadap lempeng baja/ERA.
