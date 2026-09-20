# NOCommander

**NOCommander** is a comprehensive real-time strategy (RTS) mod for *Nuclear Option* (powered by BepInEx 5). It transforms the game into a 3D tactical command suite, enabling complete battlefield management, multi-domain logistics, air mission tasking, smart AI behaviors, and 3D sandbox controls.

Mainly intended for **Escalation** and **Terminal Control** game modes.

---

## 🌟 Key Features

### 1. Control Groups & Army Management
- **Control Groups (0–9):**
  - `Ctrl + 0..9`: Assign selected units to control group.
  - `0..9`: Recall control group (hold `Shift` for additive multi-group selection).
  - *Double-tap* group number: Instantly center and focus the RTS camera on the group.
- **Dedicated Groups UI Tab:**
  - The **`GROUPS`** tab in the **Unit List / Pins** window displays all active control groups, unit counts, and vehicle compositions. Click any entry to select directly.
- **Select All Combat Army (`~` / `BackQuote`):**
  - One-key shortcut to select all friendly combat ground vehicles and surface warships across the map (mapped to tilde `~` to avoid conflict with base game camera keys `F1`–`F4`).
- **Military Formations (`V` / UI Button):**
  - Cycle convoy formations: **`LINE`** (frontal firing line), **`COLUMN`** (road march), **`WEDGE`** (assault spearhead), **`BOX`** (grid), **`ECHELON`** (flank defense), and **`RING`** (perimeter).
- **Guard & Escort Order (`G` / `Alt + RMB` on Friendly Unit):**
  - Command combat units to guard and escort logistics trucks, commanders, or flagship vessels automatically.
- **Auto-RTB (Return to Base for Repair & Rearm):**
  - Automatic toggle: Units critically damaged or low on ammunition ($le 20%$) autonomously seek the nearest repair/rearm truck or depot.
- **Artillery & MRLS Barrage Call-In (`B` / UI Button):**
  - Designate a circular target area to command all in-range artillery and MRLS units to fire simultaneous salvos.
- **Global EMCON / Radar Silence (`Ctrl + R`):**
  - Instantly toggle all friendly radar emitters on/off across the map to protect against Anti-Radiation Missiles (ARAD).
- **Order of Battle (OOB) Dashboard (`ARMY OOB` Button):**
  - Real-time comprehensive overview of active armor, air defense, aircraft squadrons, naval fleet, factories, and SAM installations.

---

### 2. Tactical Micro, Selection & Combat Orders
- **Live Unit Telemetry & Combat Inspector Box:**
  - When a unit is selected, a dedicated tactical HUD box appears above the selection bar displaying real-time telemetry:
    - **Header:** Sub-Role Badge (e.g. `[LAND | MAIN BATTLE TANK]`, `[AIR | AIR SUPERIORITY]`) and Unit Name.
    - **Combat Readiness Badge:** `[COMBAT READY]`, `[DAMAGED]`, or `[CRITICAL DAMAGE]`.
    - **Live Telemetry:** Speed (km/h), Compass Heading (000° with cardinal direction), Radar Altitude (m), Fuel %, and Stance (`HOLD FIRE` / `FREE FIRE`).
    - **Armament Breakdown:** Complete breakdown of equipped weapons and live ammunition counts.
- **Tactical Minimap Orders (`RMB` on Minimap):**
  - Right-click directly on the tactical minimap to issue move orders or queue waypoints (`Shift + RMB`) without panning the 3D camera.
- **Box Selection (Drag Marquee Selection):**
  - Left-click and drag across the screen to marquee-select multiple units simultaneously (additive with `Shift`).
- **Attack Orders & Focus Fire (`RMB` on Enemy):**
  - Right-click directly on enemy units to issue focus fire orders with a red 3D **`[ATTACK]`** target marker.
- **3D Vector Path Lines (Active for Selected Units):**
  - Real-time glowing 3D vector lines connecting selected units to destinations (Cyan), sequential waypoints (Amber), guard targets (Green), patrol routes (Sky Blue), and attack targets (Crimson Red).
- **Attack-Move Order (`A-MOVE` / `T`):**
  - Advance toward target coordinates while autonomously engaging any hostiles encountered en route.
- **Shift-Queued Waypoints (`Shift + RMB`):**
  - Chain sequential waypoints with 3D **`[WAYPOINT 1..N]`** path visualizers.
- **Scatter / Evade Order (`X` / UI Button):**
  - Immediately spread units radially by 55 m to minimize damage from incoming artillery, cluster bombs, or nuclear detonations.
- **Continuous Patrol Mode (`P` / UI Button):**
  - Set continuous looping patrol routes between start and target points with 3D **`[PATROL 1..N]`** markers.
- **Rules of Engagement / Fire Stance (`F` / UI Button):**
  - Toggle between **`HOLD FIRE`** (disables turret target acquisition) and **`FREE FIRE`**.

---

### 3. Combat Alerts & Emergency Reaction
- **Under-Attack Incident Jump (`Space`):**
  - Automatically tracks friendly units taking damage; pressing `Space` (when no unit is selected) pans the camera to the latest incident location and selects the damaged unit.

---

### 4. Dedicated Sandbox & Cheat Menu
Accessible via the **`CHEAT / SANDBOX`** button in the Commander panel:
- **Interactive 3D Hologram Spawner:**
  - Spawn any game entity directly into the 3D world: **`BUILDINGS`** (Radar towers, Hangars, Factories, SAM sites, Outposts), **`LAND`** (Tanks, Trucks, Tractors), **`AIR`** (Fighters, Bombers, Helicopters), and **`NAVAL`** (Warships, Barges).
  - Real-time 3D semi-transparent hologram preview at the cursor position.
  - Interactive Rotation: Rotate heading ($0^circ - 360^circ$) via **Mouse Scroll Wheel** or **`[` / `]`** keys.
  - Category filters and dynamic search bar.
  - Faction Toggle: **`SPAWN AS: FRIENDLY`** or **`SPAWN AS: ENEMY`**.
- **Economy Cheats:** `+$100,000`, `+$1,000,000`, and `MAX FUNDS ($10M)`.
- **True Multi-Part God Mode:** 100% invulnerability for friendly units/subsystems across all damage types (UnitPart, AeroPart, ShipPart, Unit), while enemies take full normal damage.
- **Heal Selection (100% HP):** Instantly repair all subsystems and restore full HP.
- **Restock Ammo Selection:** Refill all missile, bomb, cannon, and logistics capacities.
- **Destroy Target (Kill Selection):** Instantly eliminate targeted enemy units.
- **Reveal All Enemy Units:** Update radar tracking for all hostile units on the map.

---

### 5. Factory Production Line Management (Core RTS Economy)
- **Dedicated Factories Window (`FACTORIES / PRODUCTION`):**
  - View all friendly vehicle manufacturing facilities in real-time.
  - Reassign production lines on the fly (e.g. switch a factory from producing utility trucks to Main Battle Tanks, SPAAGs, IFVs, or Radar SAMs).
  - Live cycle progress bar tracking next unit roll-out.

### 6. Forward Operating Base (FOB) Logistics (Field Deployment)
- **Deployable Field FOBs (`DEPLOY FOB` on Logistics / Rearmer Units):**
  - Deploy logistics trucks (MSV, Flatbed, Jackknife, Ammo Trucks) into operational Forward Operating Bases on the front lines.
  - Active FOBs project a 75 m tactical logistics aura that continuously repairs damaged friendly units and restocks depleted munitions.
  - Pack up and redeploy anywhere on the battlefield with full mobility.
  - 3D visual **`[FOB LOGISTICS]`** world marker overlay.

### 7. Multi-Domain Faction Reserve
- **Domain Tabs (`LAND` / `AIR` / `NAVAL` / `CATEGORIES`):**
  - **Land:** Main battle tanks, IFVs, SPAAGs, munitions/repair trucks, radars, trailers.
  - **Air:** Strike fighters, interceptors, bombers, and cargo/gunship helicopters.
  - **Naval:** Corvettes, frigates, destroyers, aircraft carriers, and supply barges.
- **Integrated Stockpile Management:**
  - **`HOLD`:** Intercepts automatic AI deployment to reserve units for manual command.
  - **`BATCH MULTIPLIERS (x1 / x5 / x10 / MAX)`:** Perform bulk transactions with one click.
  - **`+BUY`:** Procure units from faction funds directly into reserve storage.
  - **`SELL`:** Scrap surplus reserve units for a 75% cash refund to faction funds.
  - **`FREE / DEPLOY`:** Instantly deploy reserve units to active depots/airbases at zero cost ($0).
  - **`HOLD ALL / RELEASE ALL`:** Bulk control factory output retention per category.

---

### 6. Smart AI & Autonomous Theater Commander (Supreme Commander Automation)
- **Autonomous Air Wing Controller (Winchester & Bingo Fuel Auto-RTB):**
  - Combat aircraft (CAS, Strike, ARAD, Air Guard) automatically trigger RTB when offensive missiles/bombs are expended (*Winchester*) or fuel drops below 15% (*Bingo Fuel*), returning safely to base inventory.
- **Autonomous Emergency Air Intercept Scramble:**
  - AI sub-commander automatically scrambles Air Guard interceptors from reserve stockpiles when hostile aircraft approach within 40 km of friendly bases or fleet.
- **Autonomous Frontline Logistics Ferry Loop:**
  - AI logistics controller monitors all friendly armor, SAM installations, and FOB outposts; automatically dispatches cargo helicopters with ammo containers whenever frontline units reach $< 30\%$ ammunition.
- **Autonomous Ground Reinforcement:**
  - Automatically maintains frontline armor presence by deploying reserve combat vehicles from friendly depots when active forces drop below minimum theater thresholds.
- **Adaptive Counter-Production:**
  - Enemy factory AI dynamically adapts production lines to counter force compositions (e.g. producing SAM/AAA against heavy air, or anti-tank against armor).
- **Reactive Evasive Scatter:**
  - AI ground vehicles automatically scatter (40–75 m) away from incoming artillery and bomb impact zones.
- **Settings Switches:**
  - Full toggle control over *Smart AI*, *Auto-Scramble*, *Auto-Supply*, *Auto-Reinforce*, and *Adaptive Production* under **`SETTINGS -> GAMEPLAY`**.

---

### 7. Aircraft Carrier & Flight Safety
- **Carrier Bow-Drop Prevention:** Automatically applies positive vertical climb assist ($v_y ge +4.5	ext{ m/s}$) during carrier catapult/deck launches, preventing aircraft from dipping into the sea.
- **Helicopter Island Clearance:** Helicopters launching from warships climb vertically past superstructure height ($> 22	ext{ m}$) before forward flight.
- **TailHook Auto-Deployment:** Automatically lowers the `TailHook` during carrier landing approaches for wire capture.
- **Deck Arresting Deceleration:** Dampens wheel roll speed upon carrier deck touchdown to prevent aircraft from overshooting into the ocean.
- **Anti-Crash Touchdown Cushion:** Dampens excessive descent rates near ground contact and ensures landing gear is deployed.

---

## ⌨️ Keybind Reference

| Key / Shortcut | Action | Service |
| :--- | :--- | :--- |
| `Mouse0` (LMB) | Select unit / Place 3D marker / Box drag | `CommanderSelectionService` |
| `Mouse1` (RMB) | Issue immediate 3D move order | `CommanderMoveService` |
| `Shift + RMB` | Queue sequential movement waypoint | `CommanderMoveService` |
| `Shift + LMB` | Additive unit selection | `CommanderSelectionService` |
| `Ctrl + 0..9` | Assign selection to Control Group 0-9 | `CommanderControlGroupsService` |
| `0..9` | Select Control Group 0-9 (*Double-tap*: center camera) | `CommanderControlGroupsService` |
| `~` / `BackQuote` | Select all combat army (*Select All Army*) | `CommanderControlGroupsService` |
| `V` | Cycle military formation (*Line, Column, Wedge, Box, Ring*) | `CommanderMoveService` |
| `G` / `Alt + RMB` | Guard / Escort target friendly unit | `CommanderMoveService` |
| `B` | Call in artillery/MRLS barrage on target area | `CommanderMoveService` |
| `Ctrl + R` | Toggle Global EMCON / Radar Silence | `CommanderRadarService` |
| `F` | Toggle Hold Fire / Free Fire (Stance) | `CommanderStanceService` |
| `X` | Scatter / Evade area damage | `CommanderMoveService` |
| `P` | Toggle continuous patrol mode | `CommanderMoveService` |
| `Space` | Center camera on unit / Jump to under-attack alert | `CommanderAlertService` |
| `H` | Cycle UI visibility (*Full / Minimal / Hidden*) | `CommanderOverlayUi` |
| `W, A, S, D, Q, E` | RTS Camera Pan & Elevation | `CommanderCameraController` |
| `Left / Right Arrow` | Rotate camera view left / right (Yaw) | `CommanderCameraController` |
| `Up / Down Arrow` | Tilt camera view up / down (Pitch) | `CommanderCameraController` |
| `Mouse2` (Hold) | Free-look camera rotation (Mouse) | `CommanderCameraController` |
| `Shift` (Hold) | Camera movement speed boost | `CommanderCameraController` |
| `Alt` (Hold) | Expose unit delete action (DEL) on selection | `CommanderOverlayUi` |
| `Mouse Scroll / [ / ]` | Rotate 3D hologram spawn orientation | `CommanderCheatService` |

---

## 📦 Requirements & Installation

- **Nuclear Option** (Steam)
- **BepInEx 5.x**

### Installation:
1. Build or download `NuclearOptionCommander.dll`.
2. Copy the DLL to:
```
<Nuclear Option Install Directory>\BepInEx\plugins\NuclearOptionCommander.dll
```
3. Launch *Nuclear Option* and enter a mission. Press **`M`** to open the map and click **`Commander`** to activate.
