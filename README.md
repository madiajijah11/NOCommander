# NOCommander
This is a BepInEx mod for Nuclear Option that implements full RTS-style gameplay features in 3D. Intended for **Escalation** and **Terminal Control** game modes.

---

## 🌟 Fitur Baru (Newly Added Features)

### 1. Control Groups & Army Management
- **Hotkeys (0–9):**
  - `Ctrl + 0..9`: Simpan unit terpilih ke grup kontrol.
  - `0..9`: Panggil grup kontrol (tahan `Shift` untuk seleksi aditif).
  - *Double-tap* nomor grup: Memusatkan kamera langsung ke posisi grup.
- **Dedicated Groups UI Tab:**
  - Tab **`GROUPS`** di jendela **UNIT LIST / PINS** menampilkan seluruh grup aktif, jumlah unit, dan ringkasan tipe kendaraan. Klik untuk memilih langsung.
- **Select All Combat Army (`F2`):**
  - Satu tombol untuk memilih seluruh unit tempur darat dan kapal permukaan milik friendly di seluruh peta.

### 2. Tactical Micro, Selection & Combat Orders
- **Tactical Minimap Orders (RMB pada Minimap):**
  - Klik kanan langsung pada area tactical minimap untuk memberi perintah gerak atau antrean waypoint (`Shift + RMB`) tanpa perlu mengarahkan kamera 3D ke lokasi tersebut.
- **Overhead 3D Health & Ammo Bars:**
  - Bar status visual HP (Hijau/Kuning/Merah) dan amunisi senjata (Cyan) mengambang di atas setiap unit yang sedang dipilih secara real-time.
- **Box Selection (Drag Marquee Selection):**
  - Klik kiri & tahan drag mouse di layar untuk membuat kotak seleksi area guna memilih banyak unit sekaligus (tahan `Shift` untuk aditif).
- **Attack Order & Focus Fire (RMB pada Musuh):**
  - Klik kanan langsung pada unit musuh untuk mengunci target (*Focus Fire*) dan menggerakkan unit tempur ke jarak tembak efektif dengan indikator target **`[ATTACK]`**.
- **Shift-Queued Waypoints (`Shift + RMB`):**
  - Menambahkan titik jalan (*waypoint*) berurutan. Unit otomatis menyusuri rute titik demi titik.
  - **3D World Waypoint Markers:** Menampilkan penanda `WAYPOINT 1`, `WAYPOINT 2`, dst. di dunia 3D.
- **Scatter / Evade Order (`X` / UI Button):**
  - Memerintahkan unit terpilih langsung berpencar (*scatter radially*) sejauh 55 m untuk menghindari serangan area/artileri/bom nuklir.
- **Continuous Patrol Mode (`P` / UI Button):**
  - Mengatur rute patroli bolak-balik terus-menerus antara titik awal dan titik patroli lengkap dengan penanda **`[PATROL 1..N]`** di dunia 3D.
- **Rules of Engagement / Fire Stance (`F` / UI Button):**
  - Beralih antara **`HOLD FIRE`** (turet/sistem senjata tidak menembak) dan **`FREE FIRE`** (serang musuh bebas).

### 3. Combat Alerts & Emergency Reaction
- **Under-Attack Incident Jump (`Space`):**
  - Saat ada unit friendly yang terkena serangan, menekan tombol `Space` (saat tidak ada unit terfokus) akan langsung melompatkan kamera ke lokasi insiden dan menyeleksi unit yang terkena damage.

### 4. Dedicated Cheat & Sandbox Menu (Jendela Khusus)
Dibuka via tombol **`CHEAT / SANDBOX`** di panel utama Commander:
- **3D Unit & Building Spawner:**
  - Men-spawn semua jenis entitas game: **BUILDINGS** (Menara Radar, Hanggar, Pabrik, Pos SAM, Bunker), **LAND** (Tank, Truk, Traktor), **AIR** (Pesawat tempur, Helikopter), dan **NAVAL** (Kapal perang).
  - Filter kategori & bar pencarian (*Search Box*).
  - Sakelar Faksi: **SPAWN AS: FRIENDLY** atau **SPAWN AS: ENEMY**.
  - Penempatan 3D Interaktif: Klik tombol **`PLACE IN 3D`** $ightarrow$ klik di tanah/air untuk instan spawn di titik kursor.
- **Economy Cheats:** `+ $100,000`, `+ $1,000,000`, dan `MAX FUNDS ($10M)`.
- **God Mode (Toggle):** Semua unit & bangunan friendly kebal dari proyektil, rudal, dan bom.
- **Heal Selection (100% HP):** Pulihkan HP dan perbaiki seluruh subsistem unit terpilih seketika.
- **Restock Ammo Selection:** Isi penuh semua amunisi rudal, bom, kanon, dan kapasitas logistik.
- **Destroy Target (Kill Selection):** Hancurkan unit musuh/target seketika.
- **Reveal All Enemy Units:** Buka dan perbarui radar tracking semua unit musuh di peta.

### 5. Smart AI Behaviors & Macro Counters
- **Autonomous Air & Naval Auto-Deploy:** AI bot faksi otomatis mengerahkan pesawat cadangan dari pangkalan udara terdekat dan kapal perang cadangan ke jalur laut (*sea lanes*) secara dinamis dari stok Faction Reserve (selama unit tersebut tidak di-`HOLD`).
- **Adaptive Counter-Production:** AI pabrik faksi lawan memantau komposisi pasukan friendly secara berkala. Jika player banyak menggunakan pesawat, pabrik AI otomatis memproduksi sistem pertahanan udara (AAA/SAM). Jika player banyak menggunakan tank, pabrik AI beralih memproduksi *Anti-Tank* / tank tempur berat.
- **Reactive Evasive Scatter on Incoming Ordnance:** Unit darat AI otomatis melakukan manuver penghindaran (*scatter*) sejauh 40–75 m saat mendeteksi ledakan proyektil atau bom nuklir di dekat posisinya.
- **Threat-Scoring Heuristics:** Sistem penilaian ancaman dinamis memprioritaskan unit tempur aktif dan pesawat berbahaya dibanding truk logistik kosong.
- **Toggle di Settings:** Opsi *Smart AI*, *Reactive Evasion*, *Adaptive Production*, *Auto-Deploy Air*, dan *Auto-Deploy Naval* dapat diaktifkan/dinonaktifkan di menu **SETTINGS -> GAMEPLAY**.

### 6. Stabilitas & Optimasi Engine (Bugfixes)
- **Dead Reference Pruning:** Pembersihan otomatis referensi `Unit` yang hancur di memori untuk mencegah *Memory Leak* dan *MissingReferenceException*.
- **Physics Anti-Jitter:** Perbaikan getaran fisik kendaraan yang di-stop di medan miring (*slopes*).
- **Zero-GC UI Caching:** Caching warna dan tema UI statis untuk menghilangkan *frame drop* / GC spikes.

---

## 🎮 Fitur Bawaan (Core & Base Features)

### Core RTS & Camera
- **RTS Camera Controls:** Kamera bebas 3D (`W, A, S, D, Q, E`, `Mouse2` free look, `Shift` boost).
- **Unit Follow & POV:** Mengikuti unit bergerak dan POV crew snapping ke posisi helm kru.
- **Tactical Minimap:** Minimap taktis movable, klik untuk geser kamera, seleksi unit via peta.
- **Selection Bar:** Perintah `STOP`, pengembalian kendali ke `AI`, sakelar jalur aspal (`ROAD ON/OFF`), dan `PIN`/`DEL`.

### Depot Spawning & Multi-Domain Faction Reserve
- **Multi-Domain Faction Reserve (LAND / AIR / NAVAL / CATEGORIES):**
  - **Tab Land:** Semua kendaraan darat (Tank, IFV, SPAAG, Truk Logistik, Radar).
  - **Tab Air:** Pesawat tempur, pesawat serang, bomber, dan helikopter kargo.
  - **Tab Naval:** Kapal korvet, frigate, destroyer, dan kapal tongkang.
  - **Aksi Cadangan Terpadu:**
    - **`HOLD`:** Menahan unit agar tidak di-deploy otomatis oleh AI.
    - **`BATCH MULTIPLIER (x1 / x5 / x10 / MAX)`:** Membeli, menjual, atau mengerahkan unit secara borongan (*batch operations*) tanpa harus klik satu per satu.
    - **`+BUY (x1 / x5 / x10 / MAX)`:** Membeli stok unit langsung ke gudang cadangan (*reserve pool*).
    - **`SELL (x1 / x5 / x10 / ALL)`:** Menjual unit cadangan untuk *cash refund* 75% kembali ke kas faksi.
    - **`FREE / DEPLOY`:** Mengerahkan unit langsung dari cadangan ke depot secara instan tanpa biaya ($0).
    - **`HOLD ALL / RELEASE ALL`:** Kontrol produksi massal per kategori pabrik.

### Air Command & Helicopters
- **Air Command Missions:** Dispatch pesawat untuk misi *Air Superiority*, *AWACS/Jammer*, *CAS*, *ARAD*, dan *Strike*.
- Konfigurasi loadout persenjataan & area patroli (radius lingkaran biru/merah).
- **Cargo Supply Helicopters:** Misi pengiriman kargo, pendaratan di LZ, atau *parachute airdrop*.

### Unit Systems & Defenses
- Sakelar radar on/off dan radar coverage footprint display.
- Relokasi kontainer & *Mobile Emplacements* via truk trailer HLT/MSV.
- *Nearest Target Priority* untuk truk reparasi Jackknife.
- Pembelian unit kapal perang permukaan (*Naval Purchase*).
- Alat eksperimental analisis dan pembangunan pangkalan SAM otomatis.

---

## ⌨️ Daftar Shortcut Lengkap (Keybind Reference)

| Shortcut | Fungsi |
| :--- | :--- |
| `LMB` (Klik Kiri) | Seleksi unit / Tempatkan target |
| `RMB` (Klik Kanan) | Perintah gerak (*Move Order*) |
| `Shift + RMB` | Antrean titik jalan (*Queue Waypoints*) |
| `Shift + LMB` | Tambah unit ke seleksi (*Multi-select*) |
| `Ctrl + 0..9` | Simpan ke Control Group 0-9 |
| `0..9` | Panggil Control Group 0-9 (*Double-tap*: fokus kamera) |
| `F2` | Pilih Seluruh Pasukan Tempur (*Select All Army*) |
| `F` | Sakelar *Hold Fire* / *Free Fire* |
| `Space` | Pusatkan kamera ke unit / Lompat ke unit diserang |
| `H` | Siklus tampilan UI (*Full / Minimal / Hidden*) |
| `W, A, S, D, Q, E` | Gerakan Kamera RTS |
| `Mouse2` (Tahan) | *Free Look* Rotasi Kamera |
| `Shift` (Tahan) | *Speed Boost* Kamera |
| `Alt` (Tahan) | Beralih mode PIN menjadi DEL pada Selection Bar |

---

## 📦 Requirements & Installation
- **Nuclear Option** (PC)
- **BepInEx 5.x**

### Instalasi:
1. Jalankan build atau download `NuclearOptionCommander.dll`.
2. Salin file DLL ke:
```
<Folder Game>Nuclear OptionBepInExplugins```
