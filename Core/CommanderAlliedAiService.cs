using System;
using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderAlliedAiService
{
    private const float ThreatScanIntervalSeconds = 4f;
    private const float MilitaryProcurementIntervalSeconds = 10f;
    private const float AirScrambleCooldownSeconds = 45f;
    private const float EconomyInvestCooldownSeconds = 15f;
    private const float FactoryRetoolCooldownSeconds = 25f;
    private const float BattlegroupScanIntervalSeconds = 8f;
    private const float RepairDispatchIntervalSeconds = 6f;
    private const float FrontlineSupplyIntervalSeconds = 40f;
    private const float EmergencyRetreatIntervalSeconds = 5f;
    private const float AirAssaultCheckIntervalSeconds = 35f;

    private static readonly System.Reflection.FieldInfo? CaptureCapturableField =
        HarmonyLib.AccessTools.Field(typeof(Capture), "capturable");

    private readonly CommanderMoveService moveService;
    private readonly List<Unit> idleBattlegroupUnits = new();
    private readonly List<Unit> hostileUnitsScratch = new();
    private readonly List<Unit> hostileAirScratch = new();
    private readonly List<Unit> hostileArmorScratch = new();
    private readonly List<Unit> hostileRadarScratch = new();
    private readonly List<Unit> damagedFriendlyUnits = new();
    private readonly List<Unit> lowAmmoFriendlyUnits = new();
    private readonly List<Unit> availableRepairers = new();
    private readonly Dictionary<Unit, float> recentlySuppliedUnits = new();
    private readonly List<Airbase> cachedAirbases = new();
    private readonly List<VehicleDepot> cachedDepots = new();
    private float nextAirbaseCacheTime;
    private float nextDepotCacheTime;

    internal static GlobalPosition JtacTargetPosition { get; private set; }
    internal static float JtacTargetExpiryTime { get; private set; }
    internal static bool HasActiveJtacTarget => Time.unscaledTime < JtacTargetExpiryTime;

    private float nextThreatScanTime;
    private float nextProcurementTime;
    private float nextAirScrambleTime;
    private float nextEconomyInvestTime;
    private float nextFactoryRetoolTime;
    private float nextBattlegroupTime;
    private float nextRepairDispatchTime;
    private float nextSupplyCheckTime;
    private float nextRetreatCheckTime;
    private float nextAirAssaultCheckTime;

    private int trackedEnemyAir;
    private int trackedEnemyArmor;
    private int friendlySamCount;
    private int friendlyTankCount;
    private int friendlyIfvCount;
    private int friendlyShipCount;

    internal static CommanderAlliedAiService? Instance { get; private set; }

    internal bool IsEnabled
    {
        get => CommanderSettings.AlliedAutoCommanderEnabled;
        set => CommanderSettings.AlliedAutoCommanderEnabled = value;
    }

    internal string StatusText { get; private set; } = "ALLIED AUTO-COMMANDER: ACTIVE (MONITORING FRONT)";

    internal CommanderAlliedAiService(CommanderMoveService moveService)
    {
        this.moveService = moveService;
        Instance = this;
    }

    internal void Tick()
    {
        if (!IsEnabled)
        {
            StatusText = "ALLIED AUTO-COMMANDER: DISABLED";
            return;
        }

        float now = Time.unscaledTime;

        if (now >= nextThreatScanTime)
        {
            nextThreatScanTime = now + ThreatScanIntervalSeconds;
            EvaluateFrontlineThreats();
        }

        if (now >= nextRepairDispatchTime)
        {
            nextRepairDispatchTime = now + RepairDispatchIntervalSeconds;
            ExecuteAutonomousFieldRepair();
        }

        if (now >= nextSupplyCheckTime)
        {
            nextSupplyCheckTime = now + FrontlineSupplyIntervalSeconds;
            ExecuteAutonomousFrontlineResupply();
        }

        if (now >= nextAirAssaultCheckTime)
        {
            nextAirAssaultCheckTime = now + AirAssaultCheckIntervalSeconds;
            ExecuteAutonomousTroopAirAssault();
        }

        if (now >= nextRetreatCheckTime)
        {
            nextRetreatCheckTime = now + EmergencyRetreatIntervalSeconds;
            ExecuteTacticalEmergencyRetreat();
        }

        if (now >= nextProcurementTime)
        {
            nextProcurementTime = now + MilitaryProcurementIntervalSeconds;
            ExecuteAutonomousMilitaryProcurement();
        }

        if (now >= nextFactoryRetoolTime)
        {
            nextFactoryRetoolTime = now + FactoryRetoolCooldownSeconds;
            ExecuteSmartFactoryRetooling();
        }

        if (now >= nextAirScrambleTime)
        {
            nextAirScrambleTime = now + 8f;
            ExecuteAutonomousTacticalAirMissions();
        }

        if (now >= nextEconomyInvestTime)
        {
            nextEconomyInvestTime = now + EconomyInvestCooldownSeconds;
            ExecuteAutonomousEconomyReinvestment();
        }

        if (now >= nextBattlegroupTime)
        {
            nextBattlegroupTime = now + BattlegroupScanIntervalSeconds;
            ExecuteBattlegroupCoordination();
        }
    }

    private void EvaluateFrontlineThreats()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        trackedEnemyAir = 0;
        trackedEnemyArmor = 0;
        friendlySamCount = 0;
        friendlyTankCount = 0;
        friendlyIfvCount = 0;
        friendlyShipCount = 0;
        hostileUnitsScratch.Clear();
        damagedFriendlyUnits.Clear();
        lowAmmoFriendlyUnits.Clear();
        availableRepairers.Clear();

        // 1. Scan Tracked Hostiles in Intelligence Database
        hostileAirScratch.Clear();
        hostileArmorScratch.Clear();
        hostileRadarScratch.Clear();

        if (localHq.trackingDatabase != null)
        {
            foreach (KeyValuePair<PersistentID, TrackingInfo> entry in localHq.trackingDatabase)
            {
                if (!entry.Key.TryGetUnit(out Unit enemy) || enemy == null || enemy.disabled) continue;

                hostileUnitsScratch.Add(enemy);
                string eName = (!string.IsNullOrEmpty(enemy.unitName) ? enemy.unitName : enemy.name).ToLowerInvariant();

                if (enemy is Aircraft)
                {
                    trackedEnemyAir++;
                    hostileAirScratch.Add(enemy);
                }
                else if (enemy is GroundVehicle)
                {
                    trackedEnemyArmor++;
                    hostileArmorScratch.Add(enemy);

                    if (eName.Contains("radar") || eName.Contains("sam") || eName.Contains("strato") || enemy.GetComponentInChildren<Radar>(true) != null)
                    {
                        hostileRadarScratch.Add(enemy);
                    }
                }
                else if (enemy is Building && (eName.Contains("radar") || eName.Contains("sam") || enemy.GetComponentInChildren<Radar>(true) != null))
                {
                    hostileRadarScratch.Add(enemy);
                }
            }
        }

        // 2. Scan Friendly Forces (Track health, ammo, and repairers)
        if (localHq.factionUnits != null)
        {
            foreach (PersistentID id in localHq.factionUnits)
            {
                if (!id.TryGetUnit(out Unit friendly) || friendly == null || friendly.disabled) continue;

                string label = friendly.unitName.ToLowerInvariant();
                if (friendly is Ship)
                {
                    friendlyShipCount++;
                }
                else if (label.Contains("strato") || label.Contains("spaag") || label.Contains("sam") || label.Contains("23mm") || label.Contains("radar") || label.Contains("shard") || label.Contains("pistol") || label.Contains("challenger"))
                {
                    friendlySamCount++;
                }
                else if (friendly is GroundVehicle vehicle && !label.Contains("tanker") && !label.Contains("fuel") && !label.Contains("trailer") && !label.Contains("truck"))
                {
                    if (label.Contains("ifv") || label.Contains("apc") || label.Contains("bolide") || label.Contains("lynx") || label.Contains("jackal") || label.Contains("scout") || label.Contains("afv"))
                    {
                        friendlyIfvCount++;
                    }
                    else
                    {
                        friendlyTankCount++;
                    }
                }

                // Check for Repairers (Jacknife / Repair trucks)
                if (friendly.GetComponentInChildren<Repairer>(true) != null && !CommanderSamSiteService.IsReservedConstructionJacknife(friendly))
                {
                    availableRepairers.Add(friendly);
                }

                // Check for Damaged Units
                if (IsUnitDamaged(friendly))
                {
                    damagedFriendlyUnits.Add(friendly);
                }

                // Check for Low Ammo Units
                if (IsUnitLowAmmo(friendly))
                {
                    lowAmmoFriendlyUnits.Add(friendly);
                }
            }
        }

        // FEATURE 3: TACTICAL JTAC PINPOINT ACQUISITION
        if (hostileArmorScratch.Count > 0)
        {
            Unit enemy = hostileArmorScratch[0];
            if (enemy != null && !enemy.disabled)
            {
                JtacTargetPosition = enemy.transform.GlobalPosition();
                JtacTargetExpiryTime = Time.unscaledTime + 35f;
            }
        }
    }

    private static bool IsUnitDamaged(Unit unit)
    {
        if (unit == null || unit.disabled) return false;
        IRepairable[] repairables = unit.GetComponentsInChildren<IRepairable>(true);
        for (int i = 0; i < repairables.Length; i++)
        {
            if (repairables[i] != null && repairables[i].NeedsRepair())
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsUnitLowAmmo(Unit unit)
    {
        if (unit == null || unit.disabled || unit.weaponStations == null || unit.weaponStations.Count == 0)
        {
            return false;
        }

        float current = 0f;
        float max = 0f;
        for (int s = 0; s < unit.weaponStations.Count; s++)
        {
            WeaponStation station = unit.weaponStations[s];
            if (station?.Weapons == null) continue;
            for (int w = 0; w < station.Weapons.Count; w++)
            {
                Weapon wp = station.Weapons[w];
                if (wp != null)
                {
                    current += wp.ammo;
                    max += Mathf.Max(1, wp.GetFullAmmo());
                }
            }
        }
        return max > 0f && (current / max) <= 0.35f;
    }

    private void ExecuteAutonomousFieldRepair()
    {
        if (damagedFriendlyUnits.Count == 0) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderSpawnService? spawnSvc = CommanderSpawnService.Instance;
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        if (localHq == null) return;

        // 1. Dispatch Idle Repairers to Damaged Units
        for (int d = 0; d < damagedFriendlyUnits.Count; d++)
        {
            Unit damaged = damagedFriendlyUnits[d];
            if (damaged == null || damaged.disabled) continue;

            Vector3 damagedPos = damaged.transform.position;

            // SAFETY GATE: Do not send fragile repair trucks directly into hot kill-zones (enemy armor/guns within 900m)
            bool hotZone = false;
            for (int h = 0; h < hostileArmorScratch.Count; h++)
            {
                Unit enemyArmor = hostileArmorScratch[h];
                if (enemyArmor != null && !enemyArmor.disabled && Vector3.Distance(damagedPos, enemyArmor.transform.position) < 900f)
                {
                    hotZone = true;
                    break;
                }
            }
            if (hotZone)
            {
                continue; // Wait until enemy armor is cleared or friendly retreats to safe perimeter
            }

            Unit? closestRepairer = null;
            float minDistance = float.MaxValue;

            for (int r = 0; r < availableRepairers.Count; r++)
            {
                Unit repairer = availableRepairers[r];
                if (repairer == null || repairer.disabled) continue;

                float dist = Vector3.Distance(repairer.transform.position, damagedPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestRepairer = repairer;
                }
            }

            if (closestRepairer != null && minDistance > 35f)
            {
                CommanderGameAccess.SetUnitHoldPosition(closestRepairer, false);
                CommanderGameAccess.GetUnitCommand(closestRepairer)?.SetDestination(damaged.transform.GlobalPosition(), false);
                StatusText = $"ALLIED AI: DISPATCHED REPAIR TRUCK TO {damaged.unitName.ToUpperInvariant()}!";
                return;
            }
        }

        // 2. If no repairers available and units are damaged, auto-procure a Jacknife
        if (availableRepairers.Count == 0 && localHq.factionFunds >= 25000f && spawnSvc != null && factionSvc != null)
        {
            IReadOnlyList<VehicleDefinition> landDefs = factionSvc.LandDefinitions;
            for (int i = 0; i < landDefs.Count; i++)
            {
                VehicleDefinition def = landDefs[i];
                string name = def.unitName.ToLowerInvariant();
                if (name.Contains("jacknife") || name.Contains("repair"))
                {
                    if (spawnSvc.TryQueueVehicleAtAnyDepot(def))
                    {
                        StatusText = "ALLIED AI: PROCURED EMERGENCY JACKNIFE REPAIR VEHICLE!";
                        return;
                    }
                }
            }
        }
    }

    private void ExecuteAutonomousFrontlineResupply()
    {
        if (lowAmmoFriendlyUnits.Count == 0) return;

        // FEATURE 2: AUTO-REARM DRIVE FOR GROUND COMBAT VEHICLES
        ExecuteAutonomousGroundRearm();

        CommanderSupplyHeliService? supplySvc = CommanderSupplyHeliService.Instance;
        if (supplySvc == null || supplySvc.ActiveMissionCount >= 2) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        // Prune dead keys
        List<Unit>? stale = null;
        float now = Time.timeSinceLevelLoad;
        foreach (KeyValuePair<Unit, float> entry in recentlySuppliedUnits)
        {
            if (entry.Key == null || entry.Key.disabled || now - entry.Value > 180f)
            {
                stale ??= new List<Unit>();
                stale.Add(entry.Key);
            }
        }
        if (stale != null)
        {
            for (int s = 0; s < stale.Count; s++) recentlySuppliedUnits.Remove(stale[s]);
        }

        IEnumerable<Airbase> airbases = localHq.GetAirbases();

        for (int i = 0; i < lowAmmoFriendlyUnits.Count; i++)
        {
            Unit lowAmmo = lowAmmoFriendlyUnits[i];
            if (lowAmmo == null || lowAmmo.disabled) continue;

            Vector3 unitPos = lowAmmo.transform.position;

            // RULE 1: Never drop supplies inside or right next to an airbase/factory/depot (prevents building jamming)
            bool nearBase = false;
            if (airbases != null)
            {
                foreach (Airbase ab in airbases)
                {
                    if (ab != null && !ab.disabled && Vector3.Distance(unitPos, ab.transform.position) < 1500f)
                    {
                        nearBase = true;
                        break;
                    }
                }
            }
            if (nearBase) continue;

            if (recentlySuppliedUnits.TryGetValue(lowAmmo, out float lastTime) && (now - lastTime) < 90f)
            {
                continue;
            }

            if (supplySvc.RequestAutomaticCargoRun(unitPos.ToGlobalPosition()))
            {
                recentlySuppliedUnits[lowAmmo] = now;
                StatusText = $"ALLIED AI: DISPATCHED FRONTLINE MUNITIONS DROP TO {lowAmmo.unitName.ToUpperInvariant()}!";
                return;
            }
        }
    }

    private void ExecuteAutonomousGroundRearm()
    {
        if (lowAmmoFriendlyUnits.Count == 0) return;
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        float now = Time.unscaledTime;
        if (cachedDepots.Count == 0 || now >= nextDepotCacheTime)
        {
            nextDepotCacheTime = now + 40f;
            cachedDepots.Clear();
            VehicleDepot[] found = UnityEngine.Object.FindObjectsOfType<VehicleDepot>();
            if (found != null && found.Length > 0) cachedDepots.AddRange(found);
        }

        int maxProcess = Mathf.Min(lowAmmoFriendlyUnits.Count, 2);
        for (int i = 0; i < maxProcess; i++)
        {
            Unit unit = lowAmmoFriendlyUnits[i];
            if (unit == null || unit.disabled || unit is not GroundVehicle vehicle || moveService.HasActivePlayerDestination(vehicle))
            {
                continue;
            }

            Vector3 myPos = vehicle.transform.position;
            Vector3 bestRearmPos = Vector3.zero;
            float minDistance = float.MaxValue;

            // 1. Check Deployed FOBs
            if (CommanderForwardOutpostService.Instance?.DeployedFobs != null)
            {
                foreach (Unit fob in CommanderForwardOutpostService.Instance.DeployedFobs)
                {
                    if (fob != null && !fob.disabled)
                    {
                        float dist = Vector3.Distance(myPos, fob.transform.position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            bestRearmPos = fob.transform.position;
                        }
                    }
                }
            }

            // 2. Check friendly Vehicle Depots
            for (int d = 0; d < cachedDepots.Count; d++)
            {
                VehicleDepot depot = cachedDepots[d];
                if (depot != null && CommanderGameAccess.IsFriendlyDepot(depot, localHq))
                {
                    float dist = Vector3.Distance(myPos, depot.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestRearmPos = depot.transform.position;
                    }
                }
            }

            // 3. Check friendly Munitions Trucks
            if (minDistance > 2000f && localHq.factionUnits != null)
            {
                foreach (PersistentID pid in localHq.factionUnits)
                {
                    if (pid.TryGetUnit(out Unit fUnit) && fUnit != null && !fUnit.disabled && fUnit is GroundVehicle && fUnit.GetComponentInChildren<Rearmer>(true) != null)
                    {
                        float dist = Vector3.Distance(myPos, fUnit.transform.position);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            bestRearmPos = fUnit.transform.position;
                        }
                    }
                }
            }

            if (minDistance < 3500f && bestRearmPos != Vector3.zero)
            {
                if (minDistance > 40f)
                {
                    CommanderGameAccess.SetUnitHoldPosition(vehicle, false);
                    CommanderGameAccess.GetUnitCommand(vehicle)?.SetDestination(bestRearmPos.ToGlobalPosition(), false);
                    StatusText = $"ALLIED AI: ROUTED LOW-AMMO {vehicle.unitName.ToUpperInvariant()} TO REARM!";
                }
                else
                {
                    CommanderGameAccess.SetUnitHoldPosition(vehicle, true);
                }
            }
        }
    }

    private void ExecuteAutonomousTroopAirAssault()
    {
        CommanderSupplyHeliService? supplySvc = CommanderSupplyHeliService.Instance;
        if (supplySvc == null || supplySvc.ActiveMissionCount >= 2) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        // Scan all airbases in scene to find enemy or neutral airbases that are contestable
        float now = Time.unscaledTime;
        if (cachedAirbases.Count == 0 || now >= nextAirbaseCacheTime)
        {
            nextAirbaseCacheTime = now + 30f;
            cachedAirbases.Clear();
            Airbase[] found = UnityEngine.Object.FindObjectsOfType<Airbase>();
            if (found != null && found.Length > 0)
            {
                cachedAirbases.AddRange(found);
            }
        }
        if (cachedAirbases.Count == 0) return;

        Airbase? targetAirbase = null;
        float minDistance = float.MaxValue;
        Vector3 hqPos = localHq.transform.position;

        for (int i = 0; i < cachedAirbases.Count; i++)
        {
            Airbase ab = cachedAirbases[i];
            if (ab == null || ab.disabled || ab.CurrentHQ == localHq)
            {
                continue;
            }

            // Check if base has active capture component
            Capture? cap = ab.GetComponentInChildren<Capture>(true);
            if (cap != null && CaptureCapturableField?.GetValue(cap) is bool capturable && !capturable)
            {
                continue;
            }

            Vector3 abPos = ab.center != null ? ab.center.position : ab.transform.position;

            // STRICT COMPREHENSIVE COMBAT RECON GATE:
            // 1. Any active enemy presence within base capture zone (5,000m perimeter)
            bool hostileInPerimeter = false;
            for (int u = 0; u < hostileUnitsScratch.Count; u++)
            {
                Unit enemy = hostileUnitsScratch[u];
                if (enemy == null || enemy.disabled) continue;

                float distToTarget = Vector3.Distance(abPos, enemy.transform.position);

                // Any radar/SAM/SPAAG threat within 6,000m aborts air-assault immediately
                string eName = (!string.IsNullOrEmpty(enemy.unitName) ? enemy.unitName : enemy.name).ToLowerInvariant();
                bool isAirDefense = enemy.GetComponentInChildren<Radar>(true) != null
                    || eName.Contains("sam") || eName.Contains("radar") || eName.Contains("spaag") || eName.Contains("boltstrike") || eName.Contains("aa");

                if (isAirDefense && distToTarget < 6000f)
                {
                    hostileInPerimeter = true;
                    break;
                }

                // Any ground armor/infantry/vehicle within base boundary (< 2,500m) also blocks drop
                if (enemy is GroundVehicle && distToTarget < 2500f)
                {
                    hostileInPerimeter = true;
                    break;
                }
            }

            if (hostileInPerimeter)
            {
                continue; // Base is heavily defended or hot kill-zone; CAS/SEAD must soften first!
            }

            // 2. Air Superiority Check: Friendly air cover must be present or no hostile fighters in theater
            if (trackedEnemyAir > 1 && friendlySamCount == 0)
            {
                continue; // Contested sky; fragile transport helos will be intercepted
            }

            // Prioritize closest cleared enemy/neutral airbase to friendly territory
            float dist = Vector3.Distance(hqPos, abPos);
            if (dist < minDistance)
            {
                minDistance = dist;
                targetAirbase = ab;
            }
        }

        if (targetAirbase != null)
        {
            Transform abT = targetAirbase.center != null ? targetAirbase.center : targetAirbase.transform;
            GlobalPosition dropLz = abT.GlobalPosition();

            if (supplySvc.RequestTroopAssaultRun(dropLz))
            {
                StatusText = $"ALLIED AI: LAUNCHED TROOP AIR-ASSAULT TO CAPTURE {targetAirbase.name.ToUpperInvariant()}!";
            }
        }
    }

    private void ExecuteTacticalEmergencyRetreat()
    {
        if (damagedFriendlyUnits.Count == 0) return;
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        int slot = 0;
        for (int i = 0; i < damagedFriendlyUnits.Count; i++)
        {
            Unit damaged = damagedFriendlyUnits[i];
            if (damaged == null || damaged.disabled || damaged is not GroundVehicle vehicle || moveService.HasActivePlayerDestination(damaged))
            {
                continue;
            }

            int brokenComponents = 0;
            IRepairable[] repairables = damaged.GetComponentsInChildren<IRepairable>(true);
            for (int r = 0; r < repairables.Length; r++)
            {
                if (repairables[r] != null && repairables[r].NeedsRepair()) brokenComponents++;
            }

            if (brokenComponents >= 2)
            {
                Vector3 bestRetreatPos = Vector3.zero;
                float minDistance = float.MaxValue;
                Vector3 myPos = damaged.transform.position;

                if (CommanderForwardOutpostService.Instance?.DeployedFobs != null)
                {
                    foreach (Unit fob in CommanderForwardOutpostService.Instance.DeployedFobs)
                    {
                        if (fob != null && !fob.disabled)
                        {
                            float dist = Vector3.Distance(myPos, fob.transform.position);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestRetreatPos = fob.transform.position;
                            }
                        }
                    }
                }

                if (minDistance == float.MaxValue)
                {
                    IEnumerable<Airbase> airbases = localHq.GetAirbases();
                    if (airbases != null)
                    {
                        foreach (Airbase ab in airbases)
                        {
                            if (ab != null && !ab.disabled)
                            {
                                float dist = Vector3.Distance(myPos, ab.transform.position);
                                if (dist < minDistance)
                                {
                                    minDistance = dist;
                                    bestRetreatPos = ab.transform.position;
                                }
                            }
                        }
                    }
                }

                // Retreat to open staging perimeter (offset 100m outside building footprint)
                if (minDistance < float.MaxValue && minDistance > 80f && bestRetreatPos != Vector3.zero)
                {
                    Vector3 retreatDir = (myPos - bestRetreatPos).normalized;
                    if (retreatDir.sqrMagnitude < 0.01f) retreatDir = Vector3.forward;
                    Vector3 safePerimeter = bestRetreatPos + retreatDir * 120f;
                    GlobalPosition safeDest = CommanderDestinationFormation.ApplyOffset(safePerimeter.ToGlobalPosition(), slot++, 30f);

                    CommanderGameAccess.SetUnitHoldPosition(damaged, false);
                    CommanderGameAccess.GetUnitCommand(damaged)?.SetDestination(safeDest, false);

                    // FEATURE 4: TACTICAL REVERSE & FRONTAL THREAT ORIENTATION
                    // Lock turrets to track and suppress closest threat while reversing/retreating
                    if (hostileArmorScratch.Count > 0 && hostileArmorScratch[0] != null && !hostileArmorScratch[0].disabled)
                    {
                        Turret[] turrets = damaged.GetComponentsInChildren<Turret>(true);
                        for (int t = 0; t < turrets.Length; t++)
                        {
                            turrets[t]?.SetTargetFromController(hostileArmorScratch[0]);
                        }
                    }

                    StatusText = $"ALLIED AI: TACTICAL RETREAT OF DAMAGED {damaged.unitName.ToUpperInvariant()} TO REPAIR PERIMETER!";
                }
            }
        }
    }

    private void ExecuteAutonomousMilitaryProcurement()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        CommanderFactionVehicleService? factionSvc = CommanderFactionVehicleService.Instance;
        CommanderSpawnService? spawnSvc = CommanderSpawnService.Instance;
        if (localHq == null || factionSvc == null || spawnSvc == null) return;

        float funds = localHq.factionFunds;
        // Keep a $35k safety buffer for player manual purchases
        if (funds < 35000f) return;

        IReadOnlyList<VehicleDefinition> landDefs = factionSvc.LandDefinitions;
        if (landDefs.Count > 0)
        {
            List<VehicleDefinition> availableMbts = new();
            List<VehicleDefinition> availableSams = new();
            List<VehicleDefinition> availableIfvs = new();

            for (int i = 0; i < landDefs.Count; i++)
            {
                VehicleDefinition def = landDefs[i];
                if (IsAirDefense(def)) availableSams.Add(def);
                else if (IsMainBattleTank(def)) availableMbts.Add(def);
                else if (IsInfantryFightingVehicle(def)) availableIfvs.Add(def);
            }

            // Ground Combined Arms Procurement Logic:
            VehicleDefinition? chosenLand = null;

            // 1. Air Defense Priority if enemy air threatens or friendly SAMs are low (< 3)
            if ((trackedEnemyAir > friendlySamCount || friendlySamCount < 3) && availableSams.Count > 0)
            {
                chosenLand = availableSams[UnityEngine.Random.Range(0, availableSams.Count)];
            }
            // 2. IFV / APC support if MBT outnumbers IFV by 2:1
            else if (friendlyTankCount > 3 && friendlyIfvCount < friendlyTankCount / 2 && availableIfvs.Count > 0)
            {
                chosenLand = availableIfvs[UnityEngine.Random.Range(0, availableIfvs.Count)];
            }
            // 3. MBT / Armor Backbone
            else if (availableMbts.Count > 0)
            {
                chosenLand = availableMbts[UnityEngine.Random.Range(0, availableMbts.Count)];
            }
            // 4. Fallback to any available combat vehicle
            else if (availableIfvs.Count > 0)
            {
                chosenLand = availableIfvs[UnityEngine.Random.Range(0, availableIfvs.Count)];
            }

            if (chosenLand != null)
            {
                if (spawnSvc.TryQueueVehicleAtAnyDepot(chosenLand))
                {
                    CommanderPlugin.Log.LogInfo($"[Allied Auto-Commander] Procured {chosenLand.unitName} to frontline depot.");
                }
            }
        }

        // 2. Procure Combat Aircraft with Tactical Role Balance (Never buy only expensive bombers!)
        if (funds >= 75000f)
        {
            IReadOnlyList<AircraftDefinition> airDefs = factionSvc.AirDefinitions;
            if (airDefs.Count > 0)
            {
                List<AircraftDefinition> fighters = new();
                List<AircraftDefinition> casPlanes = new();
                List<AircraftDefinition> bombers = new();

                int totalReserveFighters = 0;
                int totalReserveCas = 0;
                int totalReserveBombers = 0;

                for (int a = 0; a < airDefs.Count; a++)
                {
                    AircraftDefinition airDef = airDefs[a];
                    int stock = factionSvc.GetReserveCount(airDef);

                    if (IsHeavyBomber(airDef))
                    {
                        bombers.Add(airDef);
                        totalReserveBombers += stock;
                    }
                    else if (IsCasOrAttack(airDef))
                    {
                        casPlanes.Add(airDef);
                        totalReserveCas += stock;
                    }
                    else
                    {
                        // Default to fighter / interceptor / multirole
                        fighters.Add(airDef);
                        totalReserveFighters += stock;
                    }
                }

                AircraftDefinition? chosenAir = null;

                // Priority 1: Maintain Air Superiority (at least 2-3 Fighters in reserve)
                if (totalReserveFighters < 3 && fighters.Count > 0)
                {
                    chosenAir = SelectBestAffordableAircraft(fighters, factionSvc, funds, maxReservePerType: 2);
                }
                // Priority 2: Close Air Support / Attack aircraft (at least 2 CAS in reserve)
                else if (totalReserveCas < 2 && casPlanes.Count > 0)
                {
                    chosenAir = SelectBestAffordableAircraft(casPlanes, factionSvc, funds, maxReservePerType: 2);
                }
                // Priority 3: Heavy Strike Bomber (ONLY if we have air cover, excess funds >= $120k, and max 1 bomber in reserve!)
                else if (funds >= 120000f && totalReserveFighters >= 2 && totalReserveBombers < 1 && bombers.Count > 0)
                {
                    chosenAir = SelectBestAffordableAircraft(bombers, factionSvc, funds, maxReservePerType: 1);
                }
                // Default fallback: Top off fighters/multirole
                else if (fighters.Count > 0)
                {
                    chosenAir = SelectBestAffordableAircraft(fighters, factionSvc, funds, maxReservePerType: 2);
                }

                if (chosenAir != null)
                {
                    if (factionSvc.TryBuyStockToReserve(chosenAir, 1, out _))
                    {
                        CommanderPlugin.Log.LogInfo($"[Allied Auto-Commander] Purchased combat aircraft {chosenAir.unitName} to faction reserve.");
                    }
                }
            }
        }

        // 3. Procure Naval Warships if sea lanes exist and funds >= $120k
        if (funds >= 120000f && friendlyShipCount < 2)
        {
            LevelInfo? levelInfo = NetworkSceneSingleton<LevelInfo>.i;
            if (levelInfo?.seaLanes != null && levelInfo.seaLanes.Exists())
            {
                IReadOnlyList<ShipDefinition> shipDefs = factionSvc.NavalDefinitions;
                if (shipDefs.Count > 0)
                {
                    ShipDefinition bestShip = shipDefs[0];
                    if (factionSvc.TryBuyStockToReserve(bestShip, 1, out _))
                    {
                        CommanderPlugin.Log.LogInfo($"[Allied Auto-Commander] Procured warship {bestShip.unitName} to naval reserve.");
                    }
                }
            }
        }
    }

    private static AircraftDefinition? SelectBestAffordableAircraft(
        List<AircraftDefinition> pool,
        CommanderFactionVehicleService factionSvc,
        float currentFunds,
        int maxReservePerType)
    {
        if (pool == null || pool.Count == 0) return null;

        // Shuffle candidate selection to prevent always picking the first alphabetized plane
        List<AircraftDefinition> candidates = new(pool);
        for (int i = 0; i < candidates.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, candidates.Count);
            (candidates[i], candidates[r]) = (candidates[r], candidates[i]);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            AircraftDefinition def = candidates[i];
            int currentStock = factionSvc.GetReserveCount(def);
            if (currentStock < maxReservePerType && currentFunds >= def.value * 1.3f)
            {
                return def;
            }
        }

        return null;
    }

    private void ExecuteSmartFactoryRetooling()
    {
        CommanderFactoryProductionService? factorySvc = CommanderFactoryProductionService.Instance;
        if (factorySvc == null || factorySvc.FriendlyFactories.Count == 0) return;

        IReadOnlyList<Factory> factories = factorySvc.FriendlyFactories;
        IReadOnlyList<VehicleDefinition> available = factorySvc.AvailableVehicleDefinitions;
        if (available.Count == 0) return;

        List<VehicleDefinition> samDefs = new();
        List<VehicleDefinition> tankDefs = new();
        List<VehicleDefinition> ifvDefs = new();

        for (int i = 0; i < available.Count; i++)
        {
            VehicleDefinition def = available[i];
            if (IsAirDefense(def)) samDefs.Add(def);
            else if (IsMainBattleTank(def)) tankDefs.Add(def);
            else if (IsInfantryFightingVehicle(def)) ifvDefs.Add(def);
        }

        for (int f = 0; f < factories.Count; f++)
        {
            Factory factory = factories[f];
            if (factory == null || factory.attachedUnit == null || factory.attachedUnit.disabled) continue;

            VehicleDefinition? targetDef = null;

            // Slot-based diversified production:
            if (f % 3 == 0 && samDefs.Count > 0)
            {
                targetDef = samDefs[0];
            }
            else if (f % 3 == 1 && ifvDefs.Count > 0)
            {
                targetDef = ifvDefs[0];
            }
            else if (tankDefs.Count > 0)
            {
                targetDef = tankDefs[0];
            }
            else if (samDefs.Count > 0)
            {
                targetDef = samDefs[0];
            }

            if (targetDef != null && !ReferenceEquals(factory.ProductionUnit, targetDef))
            {
                factorySvc.SetProductionUnit(factory, targetDef);
            }
        }
    }

    private static bool IsMainBattleTank(VehicleDefinition def)
    {
        if (def == null) return false;
        if (def.vehicleType == VehicleType.MBT) return true;

        string name = def.unitName.ToLowerInvariant();
        if (name.Contains("tanker") || name.Contains("fuel") || name.Contains("water") || name.Contains("trailer") || name.Contains("truck"))
        {
            return false;
        }

        return name.Contains("t-98") || name.Contains("vanguard") || name.Contains("mbt") || name.Contains("brawler") || name.Contains("heavy tank") || name.Contains("tank");
    }

    private static bool IsAirDefense(VehicleDefinition def)
    {
        if (def == null) return false;
        string name = def.unitName.ToLowerInvariant();
        string cat = CommanderGameAccess.GetVehicleCategoryLabel(def).ToLowerInvariant();

        return name.Contains("strato") || name.Contains("spaag") || name.Contains("sam") || name.Contains("23mm") || name.Contains("irm") || name.Contains("radar") || name.Contains("shard") || name.Contains("pistol") || name.Contains("challenger") || cat.Contains("air defense");
    }

    private static bool IsInfantryFightingVehicle(VehicleDefinition def)
    {
        if (def == null) return false;
        if (def.vehicleType == VehicleType.AFV) return true;

        string name = def.unitName.ToLowerInvariant();
        if (name.Contains("tanker") || name.Contains("fuel") || name.Contains("trailer") || name.Contains("truck"))
        {
            return false;
        }

        return name.Contains("ifv") || name.Contains("apc") || name.Contains("bolide") || name.Contains("lynx") || name.Contains("jackal") || name.Contains("scout") || name.Contains("armored");
    }

    private static bool IsFighter(AircraftDefinition def)
    {
        if (def == null) return false;
        string name = def.unitName.ToLowerInvariant();
        return name.Contains("revoker") || name.Contains("compass") || name.Contains("ifrit") || name.Contains("fs-12") || name.Contains("cricket") || name.Contains("fighter") || name.Contains("interceptor");
    }

    private static bool IsCasOrAttack(AircraftDefinition def)
    {
        if (def == null) return false;
        string name = def.unitName.ToLowerInvariant();
        return name.Contains("tarantula") || name.Contains("medusa") || name.Contains("vortex") || name.Contains("strike") || name.Contains("attack") || name.Contains("gunship");
    }

    private static bool IsHeavyBomber(AircraftDefinition def)
    {
        if (def == null) return false;
        string name = def.unitName.ToLowerInvariant();
        return name.Contains("darkreach") || name.Contains("hyperion") || name.Contains("bomber") || name.Contains("heavy");
    }

    private void ExecuteAutonomousTacticalAirMissions()
    {
        CommanderAirCommandService? airSvc = CommanderAirCommandService.Instance;
        if (airSvc == null) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        // 0. High-Priority Ground JTAC CAS Pinpoint Strike
        if (HasActiveJtacTarget)
        {
            if (airSvc.RequestAutonomousAirMission(CommanderAirCommandService.AirCommandMode.Cas, JtacTargetPosition, 20f))
            {
                nextAirScrambleTime = Time.unscaledTime + AirScrambleCooldownSeconds;
                StatusText = "ALLIED AI: DISPATCHED PRIORITY CAS STRIKE ON GROUND JTAC PINPOINT!";
                return;
            }
        }

        // 1. Air Threat -> Scramble CAP / Air Superiority Interceptors
        if (hostileAirScratch.Count > 0)
        {
            GlobalPosition airCenter = CalculateClusterCenter(hostileAirScratch);
            if (airSvc.RequestAutonomousAirMission(CommanderAirCommandService.AirCommandMode.AirGuard, airCenter, 30f))
            {
                nextAirScrambleTime = Time.unscaledTime + AirScrambleCooldownSeconds;
                StatusText = "ALLIED AI: SCRAMBLED COMBAT AIR PATROL (CAP) INTERCEPTORS!";
                return;
            }
        }

        // 2. SEAD / ARAD Threat -> Scramble Anti-Radiation Strike against Enemy Radar / SAMs
        if (hostileRadarScratch.Count > 0)
        {
            GlobalPosition radarCenter = CalculateClusterCenter(hostileRadarScratch);
            if (airSvc.RequestAutonomousAirMission(CommanderAirCommandService.AirCommandMode.Arad, radarCenter, 35f))
            {
                nextAirScrambleTime = Time.unscaledTime + AirScrambleCooldownSeconds;
                StatusText = "ALLIED AI: DISPATCHED SEAD / ARAD RADAR SUPPRESSION STRIKE!";
                return;
            }
        }

        // 3. Heavy Ground Threat -> Dispatch CAS Gunships / Anti-Tank Strike Jets
        if (hostileArmorScratch.Count >= 2)
        {
            GlobalPosition armorCenter = CalculateClusterCenter(hostileArmorScratch);
            if (airSvc.RequestAutonomousAirMission(CommanderAirCommandService.AirCommandMode.Cas, armorCenter, 20f))
            {
                nextAirScrambleTime = Time.unscaledTime + AirScrambleCooldownSeconds;
                StatusText = "ALLIED AI: DISPATCHED CAS TANK-BUSTER AIR STRIKE ON ENEMY ARMOR!";
                return;
            }
        }

        // 4. Recon & Electronic Warfare -> Scramble EW-25 / MC-260 AWACS & Jamming Radome Sortie
        if (airSvc.ActiveMissionCount < 3 && localHq.factionUnits != null && localHq.factionUnits.Count > 0)
        {
            GlobalPosition theaterFront = default;
            bool foundPos = false;
            foreach (PersistentID pid in localHq.factionUnits)
            {
                if (pid.TryGetUnit(out Unit u) && u != null && !u.disabled && MissionPosition.TryGetClosestPosition(u, out theaterFront))
                {
                    foundPos = true;
                    break;
                }
            }

            if (foundPos && airSvc.RequestAutonomousAirMission(CommanderAirCommandService.AirCommandMode.AwacsJammer, theaterFront, 40f))
            {
                nextAirScrambleTime = Time.unscaledTime + AirScrambleCooldownSeconds * 1.5f;
                StatusText = "ALLIED AI: DEPLOYED AIRBORNE RADOME AWACS & ELECTRONIC WARFARE (EW) PATROL!";
                return;
            }
        }
    }

    private static GlobalPosition CalculateClusterCenter(List<Unit> units)
    {
        if (units.Count == 0) return default;
        Vector3 sum = Vector3.zero;
        int valid = 0;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null && !units[i].disabled)
            {
                sum += units[i].transform.position;
                valid++;
            }
        }
        if (valid == 0) return default;
        return (sum / valid).ToGlobalPosition();
    }

    private void ExecuteAutonomousEconomyReinvestment()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || localHq.factionFunds < 90000f) return;

        CommanderBuildingEconomyService? bldSvc = CommanderBuildingEconomyService.Instance;
        if (bldSvc == null) return;

        // 1. Prioritize Strategic Capital Projects
        IReadOnlyList<CommanderBuildingEconomyService.EconomicProject> projects = bldSvc.Projects;
        for (int p = 0; p < projects.Count; p++)
        {
            CommanderBuildingEconomyService.EconomicProject proj = projects[p];
            if (proj.CurrentLevel < proj.MaxLevel && localHq.factionFunds >= proj.CurrentCost)
            {
                bldSvc.TryInvestInProject(proj.Id, out _);
                return;
            }
        }

        // 2. Upgrade Friendly Economy Facilities
        IReadOnlyList<CommanderBuildingEconomyService.BuildingEntry> econBuildings = bldSvc.EconomyEntries;
        for (int b = 0; b < econBuildings.Count; b++)
        {
            CommanderBuildingEconomyService.BuildingEntry entry = econBuildings[b];
            if (entry.Building != null && entry.UpgradeLevel < 3 && localHq.factionFunds >= (60000f * entry.UpgradeLevel))
            {
                bldSvc.TryUpgradeBuildingFacility(entry.Building, out _);
                return;
            }
        }
    }

    private void ExecuteBattlegroupCoordination()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null || localHq.factionUnits == null) return;

        idleBattlegroupUnits.Clear();

        // Find idle combat ground units waiting near friendly depots or factory gaps
        foreach (PersistentID id in localHq.factionUnits)
        {
            if (!id.TryGetUnit(out Unit unit) || unit == null || unit.disabled) continue;

            if (unit is GroundVehicle vehicle && !CommanderGameAccess.IsTrailerVehicleDefinition(vehicle.definition as VehicleDefinition))
            {
                // FEATURE 1: AUTO-STANDOFF DOCTRINE
                // Standoff artillery, ballistic missile trucks, and radar units MUST hold position in rear base
                if (CommanderGameAccess.IsStandoffUnit(vehicle))
                {
                    if (!moveService.HasActivePlayerDestination(vehicle))
                    {
                        CommanderGameAccess.SetUnitHoldPosition(vehicle, true);
                    }
                    continue;
                }

                string vName = (!string.IsNullOrEmpty(unit.unitName) ? unit.unitName : unit.name).ToLowerInvariant();
                if (vName.Contains("jacknife") || vName.Contains("repair")
                    || vName.Contains("tanker") || vName.Contains("fuel") || vName.Contains("truck")
                    || vehicle.TryGetComponent(out Repairer _))
                {
                    continue;
                }

                if (vehicle.UnitCommand != null && !moveService.HasActivePlayerDestination(vehicle))
                {
                    idleBattlegroupUnits.Add(vehicle);
                }
            }
        }

        if (idleBattlegroupUnits.Count == 0) return;

        // Disperse and push idle units to objective with formation offsets
        if (MissionPosition.TryGetClosestPosition(idleBattlegroupUnits[0], out GlobalPosition objective))
        {
            for (int i = 0; i < idleBattlegroupUnits.Count; i++)
            {
                Unit unit = idleBattlegroupUnits[i];
                GlobalPosition formationSlot = CommanderDestinationFormation.ApplyOffset(objective, i, 28f);
                CommanderGameAccess.SetUnitHoldPosition(unit, false);
                CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(formationSlot, false);
            }

            if (idleBattlegroupUnits.Count >= 2)
            {
                StatusText = $"ALLIED AI: DISPATCHED COMBAT BATTLEGROUP ({idleBattlegroupUnits.Count} UNITS) TO FRONTLINE!";
            }
        }
    }

    internal void ResetSession()
    {
        idleBattlegroupUnits.Clear();
        hostileUnitsScratch.Clear();
        hostileAirScratch.Clear();
        hostileArmorScratch.Clear();
        hostileRadarScratch.Clear();
        damagedFriendlyUnits.Clear();
        lowAmmoFriendlyUnits.Clear();
        availableRepairers.Clear();
        recentlySuppliedUnits.Clear();
        cachedAirbases.Clear();
        cachedDepots.Clear();
        nextAirbaseCacheTime = 0f;
        nextDepotCacheTime = 0f;
        JtacTargetPosition = default;
        JtacTargetExpiryTime = 0f;
        nextThreatScanTime = 0f;
        nextProcurementTime = 0f;
        nextAirScrambleTime = 0f;
        nextEconomyInvestTime = 0f;
        nextFactoryRetoolTime = 0f;
        nextBattlegroupTime = 0f;
        nextRepairDispatchTime = 0f;
        nextSupplyCheckTime = 0f;
        nextAirAssaultCheckTime = 0f;
        nextRetreatCheckTime = 0f;
        trackedEnemyAir = 0;
        trackedEnemyArmor = 0;
        friendlySamCount = 0;
        friendlyTankCount = 0;
        friendlyIfvCount = 0;
        friendlyShipCount = 0;
        StatusText = "ALLIED AUTO-COMMANDER: READY";
    }
}
