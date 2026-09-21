# 03. Weapons, Radar Sensors, & Electronic Warfare

Technical documentation covering weapon mounts, radar emission sensors, fire control, and electronic countermeasures in *Nuclear Option*.

---

## 1. Radar Sensor Systems (`Radar`)

### Key Properties & Methods:
* `bool activated`: Active radio frequency emission state.
* `float range`: Sensor detection range in meters.
* `List<Unit> detectedTargets`: List of targets currently tracked within the radar scan volume.
* `bool IsOperational()`: Verifies the radar antenna and transceiver are undamaged.
* `void ResetRotators()`: Resets rotating radar dishes to zero orientation when powered down.

---

## 2. Turret & Fire Control Systems (`Turret` & `FireControl`)

### Target Acquisition Modes (`targetAcquisitionMode`):
* `"searchForRadar"`: Passive radar seeker mode (used by SAM batteries and ARAD seekers).
* `"automatic"`: Automatically engages any hostile target entering weapon range.
* `"manual"`: Turret awaits direct firing solutions from player/external controller.

### Rules of Engagement & Stance in NOCommander:
* `CommanderStanceService` disables weapon firing by toggling `turret.enabled = false` and `fireControl.enabled = false` when set to **HOLD FIRE**, preventing premature position exposure to hostile sensors.

---

## 3. Damage Modeling (`DamageInfo`)

* `float hitpoints`: Direct damage dealt to component modules and subsystems.
* `float structuralHitpoints`: Structural chassis integrity damage.
* `float armorDamage`: Kinetic/explosive penetration applied against armored plating.
