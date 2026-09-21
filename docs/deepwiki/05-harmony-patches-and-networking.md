# 05. Harmony Patching, Reflection, & Mirage Networking

Technical reference covering Harmony hook points, private reflection targets, and Mirage networking rules in NOCommander.

---

## 1. Active Harmony Patch Registry

| Target Class | Target Method | Patch Type | Purpose in NOCommander |
| :--- | :--- | :--- | :--- |
| `Unit` | `Damage` | Postfix | Detects friendly unit damage for incident alerts (`Space` jump) |
| `PathfindingAgent` | `Pathfind` | Prefix | Enables direct off-road navigation shortcuts |
| `VehicleDepot` | `TrySpawnVehicle` | Prefix | Intercepts deployment for Faction Reserve management |
| `FactionHQ` | `DeployVehicles` | Prefix/Postfix | Synchronizes automated deployment state |
| `Repairer` | `SearchForRepair` | Prefix | Directs Jackknife engineers to nearest damaged friendly units |

---

## 2. Private Reflection Patterns (`AccessTools`)

All private field/method accesses must include null-checks to prevent runtime exceptions across game updates:

```csharp
// Standard safe reflection pattern:
private static readonly FieldInfo? FireControlModeField = AccessTools.Field(typeof(FireControl), "targetAcquisitionMode");

// Safe usage:
if (FireControlModeField != null)
{
    FireControlModeField.SetValue(instance, "searchForRadar");
}
```

---

## 3. Mirage Networking Authority (Host vs Client)

In multiplayer sessions, spawning vehicles, ships, or modifying network-synchronized entities requires server authority:

```csharp
if (NetworkManagerNuclearOption.i == null || !NetworkManagerNuclearOption.i.Server.Active)
{
    SetStatus("This action is only available to the host in multiplayer.");
    return;
}
```
