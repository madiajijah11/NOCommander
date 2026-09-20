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

    private readonly CommanderMoveService moveService;
    private readonly List<Unit> idleBattlegroupUnits = new();
    private readonly List<Unit> hostileUnitsScratch = new();
    private readonly List<Unit> damagedFriendlyUnits = new();
    private readonly List<Unit> lowAmmoFriendlyUnits = new();
    private readonly List<Unit> availableRepairers = new();
    private readonly Dictionary<Unit, float> recentlySuppliedUnits = new();

    private float nextThreatScanTime;
    private float nextProcurementTime;
    private float nextAirScrambleTime;
    private float nextEconomyInvestTime;
    private float nextFactoryRetoolTime;
    private float nextBattlegroupTime;
    private float nextRepairDispatchTime;
    private float nextSupplyCheckTime;
    private float nextRetreatCheckTime;

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
            ExecuteAirDefenseScramble();
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
        if (localHq.trackingDatabase != null)
        {
            foreach (KeyValuePair<PersistentID, TrackingInfo> entry in localHq.trackingDatabase)
            {
                if (!entry.Key.TryGetUnit(out Unit enemy) || enemy == null || enemy.disabled) continue;

                if (enemy is Aircraft)
                {
                    trackedEnemyAir++;
                    hostileUnitsScratch.Add(enemy);
                }
                else if (enemy is GroundVehicle)
                {
                    trackedEnemyArmor++;
                    hostileUnitsScratch.Add(enemy);
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

            Unit? closestRepairer = null;
            float minDistance = float.MaxValue;
            Vector3 damagedPos = damaged.transform.position;

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

        CommanderSupplyHeliService? supplySvc = CommanderSupplyHeliService.Instance;
        if (supplySvc == null || supplySvc.ActiveMissionCount >= 2) return;

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

        for (int i = 0; i < lowAmmoFriendlyUnits.Count; i++)
        {
            Unit lowAmmo = lowAmmoFriendlyUnits[i];
            if (lowAmmo == null || lowAmmo.disabled) continue;

            if (recentlySuppliedUnits.TryGetValue(lowAmmo, out float lastTime) && (now - lastTime) < 90f)
            {
                continue;
            }

            if (supplySvc.RequestAutomaticCargoRun(lowAmmo.transform.position.ToGlobalPosition()))
            {
                recentlySuppliedUnits[lowAmmo] = now;
                StatusText = $"ALLIED AI: DISPATCHED MUNITIONS SUPPLY RUN TO {lowAmmo.unitName.ToUpperInvariant()}!";
                return;
            }
        }
    }

    private void ExecuteTacticalEmergencyRetreat()
    {
        if (damagedFriendlyUnits.Count == 0) return;
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        for (int i = 0; i < damagedFriendlyUnits.Count; i++)
        {
            Unit damaged = damagedFriendlyUnits[i];
            if (damaged == null || damaged.disabled || damaged is not GroundVehicle vehicle || moveService.HasActivePlayerDestination(damaged))
            {
                continue;
            }

            // Check if severely damaged (at least 2 components damaged or structural loss)
            int brokenComponents = 0;
            IRepairable[] repairables = damaged.GetComponentsInChildren<IRepairable>(true);
            for (int r = 0; r < repairables.Length; r++)
            {
                if (repairables[r] != null && repairables[r].NeedsRepair()) brokenComponents++;
            }

            if (brokenComponents >= 2)
            {
                // Find nearest friendly FOB, Airbase, or Depot to retreat to for repairs
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

                if (minDistance < float.MaxValue && minDistance > 60f)
                {
                    CommanderGameAccess.SetUnitHoldPosition(damaged, false);
                    CommanderGameAccess.GetUnitCommand(damaged)?.SetDestination(bestRetreatPos.ToGlobalPosition(), false);
                    StatusText = $"ALLIED AI: TACTICAL RETREAT OF DAMAGED {damaged.unitName.ToUpperInvariant()} TO REPAIR ZONE!";
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

    private void ExecuteAirDefenseScramble()
    {
        if (trackedEnemyAir <= 0) return;

        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null) return;

        bool hostileNearBase = false;
        IEnumerable<Airbase> airbases = localHq.GetAirbases();
        if (airbases != null)
        {
            foreach (Airbase ab in airbases)
            {
                if (ab == null || ab.disabled) continue;
                Vector3 basePos = ab.transform.position;

                for (int i = 0; i < hostileUnitsScratch.Count; i++)
                {
                    Unit enemy = hostileUnitsScratch[i];
                    if (enemy != null && !enemy.disabled && Vector3.Distance(enemy.transform.position, basePos) <= 40000f)
                    {
                        hostileNearBase = true;
                        break;
                    }
                }
                if (hostileNearBase) break;
            }
        }

        if (hostileNearBase)
        {
            CommanderAirCommandService? airSvc = CommanderAirCommandService.Instance;
            if (airSvc != null)
            {
                if (airSvc.QuickCallInMission(CommanderAirCommandService.AirCommandMode.AirGuard))
                {
                    nextAirScrambleTime = Time.unscaledTime + AirScrambleCooldownSeconds;
                    StatusText = "ALLIED AI: SCRAMBLED COMBAT AIR PATROL (CAP) INTERCEPTORS!";
                }
            }
        }
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

        // Find idle combat ground units waiting near friendly depots
        foreach (PersistentID id in localHq.factionUnits)
        {
            if (!id.TryGetUnit(out Unit unit) || unit == null || unit.disabled) continue;

            if (unit is GroundVehicle vehicle && !CommanderGameAccess.IsTrailerVehicleDefinition(vehicle.definition as VehicleDefinition))
            {
                if (vehicle.UnitCommand != null && !moveService.HasActivePlayerDestination(vehicle))
                {
                    idleBattlegroupUnits.Add(vehicle);
                }
            }
        }

        // When a platoon of 3-5 units has gathered, push towards nearest enemy or frontline
        if (idleBattlegroupUnits.Count >= 3)
        {
            if (MissionPosition.TryGetClosestPosition(idleBattlegroupUnits[0], out GlobalPosition objective))
            {
                for (int i = 0; i < idleBattlegroupUnits.Count; i++)
                {
                    Unit unit = idleBattlegroupUnits[i];
                    CommanderGameAccess.SetUnitHoldPosition(unit, false);
                    CommanderGameAccess.GetUnitCommand(unit)?.SetDestination(objective, false);
                }
                StatusText = $"ALLIED AI: DISPATCHED BATTLEGROUP OF {idleBattlegroupUnits.Count} UNITS TO FRONTLINE!";
            }
        }
    }

    internal void ResetSession()
    {
        idleBattlegroupUnits.Clear();
        hostileUnitsScratch.Clear();
        damagedFriendlyUnits.Clear();
        lowAmmoFriendlyUnits.Clear();
        availableRepairers.Clear();
        recentlySuppliedUnits.Clear();
        nextThreatScanTime = 0f;
        nextProcurementTime = 0f;
        nextAirScrambleTime = 0f;
        nextEconomyInvestTime = 0f;
        nextFactoryRetoolTime = 0f;
        nextBattlegroupTime = 0f;
        nextRepairDispatchTime = 0f;
        nextSupplyCheckTime = 0f;
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
