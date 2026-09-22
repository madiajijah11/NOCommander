# 01. Base Game Entities & Unit Class Hierarchy

Technical documentation for combat entities and data structures in *Nuclear Option* (`Assembly-CSharp.dll`).

---

## 1. Unit Class Inheritance Hierarchy

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

## 2. Core `Unit` Class

### Key Properties & Fields:
* `string unitName`: Tactical unit display name (e.g. "Revoker", "T-98 Brawler").
* `UnitDefinition definition`: Base asset definition (prefab, icon, cost, descriptions).
* `FactionHQ NetworkHQ` / `CurrentHQ`: Controlling faction headquarters (Mirage SyncVar).
* `PersistentID persistentID`: Unique identifier across network state and HQ tracking databases.
* `bool disabled`: `true` when the unit is destroyed, derelict, or non-functional.
* `Rigidbody rb`: Primary physics component.
* `List<WeaponStation> weaponStations`: List of active weapon stations on the chassis.
* `UnitCommand UnitCommand`: Command interface for waypoints (`SetDestination(GlobalPosition, bool fromPlayer)`).

### Key Methods:
* `Damage(int partIndex, DamageInfo damageInfo)`: Applies structural/component damage.
* `SetHoldPosition(bool hold)`: Signals unit AI to halt, apply brakes, and hold current position.
* `GlobalPosition GlobalPosition()`: Returns double-precision world position coordinates.

---

## 3. Combat Class Specializations

### A. `Aircraft`
* `AutopilotPlane autopilot`: Fixed-wing flight control and stability system.
* `Radar radar`: Nose/rotodome radar sensor for aerial target acquisition.
* `PilotBaseState pilotState`: Pilot state machine (`AIPilotCombatModes`).
* `bool IsOperational()`: Returns true if engine, control surfaces, and cockpit remain functional.

### B. `GroundVehicle`
* `PathfindingAgent pathfinder`: Real-time pathfinding over terrain and road networks.
* `void SetHoldPosition(bool hold)`: Engages vehicle handbrake and suppresses throttle input.
* `ParachuteSystem parachuteSystem`: Parachute deployment system for aerial cargo drops.

### C. `Ship`
* `UnitCommand UnitCommand`: Navigates along sea lanes (`RoadNetwork seaLanes`).
* Equipped with heavy autocannon turrets (`Turret`) and Point Defense (`CIWS`).

### D. `Building` & `Factory`
* `Factory`: Industrial production facility producing ground/air units periodically (`NetworkproductionUnit`).
* `Airbase`: Manages runway slots, hangars, and takeoff queues (`CanSpawnAircraft`).
* `VehicleDepot`: Ground unit deployment and purchase depot (`TrySpawnVehicle`).

---

## 4. Official Game Unit Registry & Rosters (Extracted from Game Data)

*Note: Data extracted directly from `resources.assets` and `Assembly-CSharp.dll`.*

### A. Air Vehicles (`Aircraft`)
| Unit Designation | Tactical Role & Classification |
| :--- | :--- |
| **CI-22 Cricket** | Light COIN / Close Air Support (Turboprop) |
| **T/A-30 Compass** | Trainer / Light Strike & Ground Attack Jet |
| **SAH-46 Chicane** | Dedicated Attack Helicopter / Gunship |
| **FS-12 Revoker** | Air Superiority Fighter / High-G Interceptor |
| **FS-20 Vortex** | Multi-Role Stealth VTOL Fighter |
| **VL-49 Tarantula** | Heavy Cargo / Logistics Tilt-Jet VTOL |
| **KR-67 Ifrit** | Heavy Stealth Air Dominance Multi-Role Fighter |
| **EW-25 Medusa** | Electronic Warfare (EW) / SEAD & Radar Jamming Jet |
| **SFB-81 Darkreach** | Strategic Long-Range Heavy Stealth Bomber |
| **FGA-57 Anvil** | Heavy Strike / Armored Ground Attack Aircraft |

### B. Ground Vehicles (`GroundVehicle`)

#### 1. Standoff Artillery, Long-Range Missile & Radar Systems
*(Doctrinal Behavior: Must hold position at rear base perimeters / elevated standoff terrain, NOT push frontlines)*
| Unit Name | Classification & Role |
| :--- | :--- |
| **StratoLance R9 Launcher** / **MSV R9 Stratolance** | Heavy Cruise / Guided Ballistic Missile Transporter Erector Launcher |
| **MSV Nuclear Ballistic Missile Launcher** | Nuclear Strategic Ballistic Missile Transporter Erector Launcher |
| **T9K41 Boltstrike** / **RAM45 Launcher** | Long-Range Surface-to-Air Missile (SAM) Radar-Guided Battery |
| **Linebreaker SAM** | Heavy Armored Surface-to-Air Missile Launcher |
| **Hexhound SAM** | High-Mobility Medium Air Defense SAM |
| **LCV25 SAM / LCV25 AA** | Light Mobile Air Defense Truck |
| **AFV6 AA / AFV8 Mobile Air Defense** | Armored Anti-Air Missile / Gun Carrier |
| **AeroSentry SPAAG** | Self-Propelled Anti-Aircraft Dual-Autocannon Gun |
| **HLT Radar Truck** | Mobile Early Warning, Surveillance, & Target Acquisition Radar |

#### 2. Frontline Combat Armor (Direct Offensive / Assault)
*(Doctrinal Behavior: Assigned to Vanguard push to capture frontline objectives)*
| Unit Name | Classification & Role |
| :--- | :--- |
| **Type-12 MBT** | Main Battle Tank |
| **Spearhead MBT** | Heavy Frontline Main Battle Tank |
| **AFV6 IFV / AFV8 IFV** | Infantry Fighting Vehicle (Autocannon + Troop Transport) |
| **Linebreaker IFV** | Heavy Armored Assault IFV |
| **AFV6 AT / LCV25 AT** | Dedicated Anti-Tank Guided Missile (ATGM) Carrier |
| **AFV6 APC / AFV8 APC / Linebreaker APC** | Armored Personnel Carrier |
| **LCV45 Recon Truck** | High-Speed Scout & Forward Reconnaissance Truck |

#### 3. Logistics & Field Support
| Unit Name | Classification & Role |
| :--- | :--- |
| **HLT Munitions Truck** | Field Ammunition Resupply & Rearm Truck |
| **HLT Fuel Tanker / MSV Fuel Tanker** | Heavy Fuel Carrier & Forward Refuel Truck |
| **Airport Fuel Truck** | Airbase Taxiway Refueling Truck |
| **OTB-31** | Amphibious Assault / Landing Craft |

### C. Naval Warships (`Ship`)
| Warship Class | Classification & Role |
| :--- | :--- |
| **Shard Class Corvette** | Fast Guided-Missile Patrol Corvette |
| **Dynamo Class Destroyer** | Multi-Role Guided Missile Destroyer (VLS Air Defense + Heavy Naval Guns) |
| **Annex Class Carrier** | Amphibious Assault Helicopter / VTOL Carrier |
| **Hyperion Class Carrier** | Heavy Fleet Aircraft Carrier (Catapult & Arrestor Gear) |

