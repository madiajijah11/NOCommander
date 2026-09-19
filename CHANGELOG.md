# Changelog - NOCommander

All notable changes to the NOCommander mod are documented in this file.

---

## [0.2.0.0] - 2026-09-19

### 🌟 New Features (Fitur Baru)
- **Control Groups & Quick Recall:**
  - `Ctrl + 0..9` to assign selected units to control groups.
  - `0..9` to recall control groups (hold `Shift` for additive multi-group selection).
  - Double-tap group number to center/focus the RTS camera on the group.
  - Dedicated **`GROUPS`** tab in the Pinned/Unit List window displaying active groups, unit count, and vehicle composition.
  - `F2` shortcut to select all friendly combat army across the map in 1 click.
- **Tactical Micro & Selection:**
  - **Box Selection (Drag Marquee Box):** Click and drag in screen space to select multiple friendly units simultaneously (additive with `Shift`).
  - **Attack Orders & Focus Fire:** Right-click directly on enemy units (`RMB`) to issue focus fire orders with visual red **`[ATTACK]`** 3D world markers.
  - **Shift-Queued Waypoints:** Hold `Shift + RMB` to chain sequential movement waypoints with 3D **`[WAYPOINT 1..N]`** path markers.
  - **Tactical Minimap Orders:** Right-click (`RMB` or `Shift + RMB`) directly on the tactical minimap to issue move and waypoint orders without moving the 3D camera.
  - **Scatter / Evade Order (`X` / UI Button):** Instantly scatters selected units radially (55 m) to evade nuclear blasts, artillery salvos, and cluster munitions.
  - **Continuous Patrol Mode (`P` / UI Button):** Sets up continuous looped patrol routes between start and target points with 3D **`[PATROL 1..N]`** markers.
  - **Rules of Engagement / Fire Stance (`F` / UI Button):** Toggle between `HOLD FIRE` (disables turret/weapon target acquisition) and `FREE FIRE`.
- **Combat Alerts & Emergency Reaction:**
  - **Under-Attack Incident Jump (`Space`):** Automatically detects incoming damage on friendly units; pressing `Space` when no unit is selected jumps the camera to the latest incident and selects the damaged unit.
- **3D Overhead Health & Status Bars:**
  - Dynamic floating HP bars (Green/Yellow/Red) and Weapon Ammo bars (Cyan) rendered directly above selected units in 3D world space.
- **Multi-Domain Faction Reserve Upgrades:**
  - Added dedicated tabs for **`LAND`**, **`AIR`**, and **`NAVAL`** branches, plus **`CATEGORIES`**.
  - **Batch Multipliers (`x1`, `x5`, `x10`, `MAX`):** Buy, sell, or deploy units in bulk without repetitive clicking.
  - **`+BUY`:** Purchase units from faction funds directly into the reserve stockpile.
  - **`SELL`:** Scrap surplus reserve units for a 75% cash refund to faction funds.
  - **`FREE / DEPLOY`:** Instantly deploy reserve units to active depots at zero cost ($0).
  - **`HOLD ALL / RELEASE ALL`:** Bulk toggle factory output retention per category.
- **Smart AI System & Macro Counters:**
  - **Autonomous Air & Naval Auto-Deploy:** AI bot allies automatically sortie reserve aircraft from nearby airbases and deploy reserve warships to sea lanes (when units are not set to `HOLD`).
  - **Adaptive Counter-Production:** Enemy AI monitors player force composition and dynamically alters factory output to counter air or armor spam (e.g. producing AAA/SAM when player deploys heavy air).
  - **Reactive Evasive Scatter:** AI ground vehicles automatically scatter (40–75 m) away from incoming artillery and bomb detonations.
  - **Threat-Scoring Heuristics:** Dynamic threat prioritization prioritizing active combat vehicles and aircraft over unarmed support trucks.
- **Dedicated Sandbox & Cheat Menu Window:**
  - Replaced settings tab with an independent, dedicated floating window accessible via the **`CHEAT / SANDBOX`** panel button.
  - **Interactive 3D Entity Spawner:** Spawn any entity directly into the 3D world: **BUILDINGS** (Radar towers, SAM sites, Factories, Hangars, Outposts), **LAND**, **AIR**, and **NAVAL** units with friendly/enemy faction assignment and 3D cursor placement.
  - Category filters and dynamic search bar.
  - Economy cheats (`+$100K`, `+$1M`, `MAX FUNDS`), `God Mode`, `Heal 100% HP`, `Restock Ammo`, `Kill Target`, and `Reveal All Enemies`.

---

### 🔧 Bug Fixes & Engine Optimizations (Perbaikan & Stabilitas)
- **Dead Reference Memory Leak Fix:**
  - Implemented automatic pruning (`RemoveWhere(u => u == null || u.disabled)`) across all static and instance unit collections (`CommanderDirectPathService`, `CommanderSamSiteCoreRegistry`, `CommanderRadarService`, `CommanderRepairService`, `CommanderSelectionService`, `CommanderSmartAiService`).
- **Physics Anti-Jitter on Slopes:**
  - Removed per-frame `rb.velocity = Vector3.zero` from `Update()` loop in `CommanderMoveService`. Replaced with gentle horizontal drift damping (`sqrMagnitude < 0.25f`) while preserving gravity and wheel suspension.
- **Zero-GC UI Caching:**
  - Statically cached accent colors and styles in `CommanderUiTheme` to eliminate heap allocation spikes and frame drops during IMGUI drawing.
- **Keybind Conflict Resolution:**
  - Separated Stance Toggle (`F`), Toggle UI (`H`), Camera Center/Alert Jump (`Space`), Scatter (`X`), and Patrol (`P`) without overlapping base game camera controls.
- **Multiplayer Authority Checks:**
  - Added robust guards and status notifications for host-only actions on non-server multiplayer clients.

---

### 📚 Documentation & Project Specs
- Added comprehensive documentation across `README.md`, `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, and `.cursorrules`.
