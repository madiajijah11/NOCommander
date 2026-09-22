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

## 4. Electronic Countermeasures (ECM) & Defenses

### A. Radar Jamming (`RadarJammer` & `JammingPod`)
* `void Fire()`: Engages active radio-frequency jamming.
* `float GetMaxJammingIntensity()`: Returns peak effective jamming output in dB/watts.
* `ThreatTypes GetThreatTypes()`: Returns detectable radar bands jammed by this pod.
* Units targeted by jamming receive the `JammedMarker` component and degraded tracking fidelity.

### B. Expendable Countermeasures (`FlareEjector` & `ChaffEjector`)
* `FlareEjector`: Dispenses pyrotechnic heat decoys (`IRFlare`) to divert infrared (IR) guided missiles.
* `ChaffEjector`: Dispenses reflective dipole clouds (`ChaffDoor`) to break radar-guided missile locks.
* Key Methods:
  * `void Fire()`: Ejects a single or burst salvo of decoys.
  * `int GetAmmo()` / `int GetMaxAmmo()`: Current magazine and maximum capacity.
  * `void Rearm()`: Replenishes expendable stores at an airbase or ammo depot.


**Constructor:** `new DamageInfo(float pierce, float blast, float structural, float fire)`

* `DamageValue pierceDamage`: Kinetic penetration damage (armor piercing, railgun slugs).
* `DamageValue blastDamage`: Explosive blast damage (bombs, HE warheads).
* `DamageValue structuralDamage`: Structural integrity damage (hull / chassis).
* `DamageValue fireDamage`: Incendiary / burn damage.
* `DamageValue impactDamage`: Physical impact force damage.
* Each `DamageValue` exposes a `.Value` float property.

**Zero-damage guard pattern:**
```csharp
if (info.pierceDamage.Value == 0 && info.blastDamage.Value == 0
    && info.fireDamage.Value == 0 && info.impactDamage.Value == 0)
{
    return; // cosmetic / zero-damage hit, skip
}
```
