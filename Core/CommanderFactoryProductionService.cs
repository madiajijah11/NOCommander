using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionCommander;

internal sealed class CommanderFactoryProductionService
{
    private static readonly MethodInfo? FactoryProductionSetter =
        AccessTools.PropertySetter(typeof(Factory), "NetworkproductionUnit")
        ?? AccessTools.PropertySetter(typeof(Factory), "ProductionUnit");

    private static readonly FieldInfo? VehiclesField = AccessTools.Field(typeof(Factory), "vehicles");

    private readonly List<Factory> friendlyFactories = new();
    private readonly List<VehicleDefinition> availableVehicleDefinitions = new();
    private readonly Dictionary<Factory, GlobalPosition> factoryRallyPoints = new();

    private float nextRefreshTime;
    private FactionHQ? boundHq;
    private int selectedFactoryIndex;

    internal static CommanderFactoryProductionService? Instance { get; private set; }

    internal IReadOnlyList<Factory> FriendlyFactories => friendlyFactories;
    internal IReadOnlyList<VehicleDefinition> AvailableVehicleDefinitions => availableVehicleDefinitions;
    internal int SelectedFactoryIndex => selectedFactoryIndex;

    internal Factory? SelectedFactory =>
        friendlyFactories.Count > 0 && selectedFactoryIndex >= 0 && selectedFactoryIndex < friendlyFactories.Count
            ? friendlyFactories[selectedFactoryIndex]
            : null;

    internal CommanderFactoryProductionService()
    {
        Instance = this;
    }

    internal void SelectFactory(int index)
    {
        if (index >= 0 && index < friendlyFactories.Count)
        {
            selectedFactoryIndex = index;
        }
    }

    internal void Tick()
    {
        float now = Time.unscaledTime;
        if (now >= nextRefreshTime)
        {
            nextRefreshTime = now + 2f;
            RefreshFriendlyFactories();
        }
    }

    internal void RefreshFriendlyFactories()
    {
        FactionHQ? localHq = CommanderGameAccess.GetLocalHq();
        if (localHq == null)
        {
            friendlyFactories.Clear();
            return;
        }

        if (!ReferenceEquals(localHq, boundHq))
        {
            boundHq = localHq;
            friendlyFactories.Clear();
            availableVehicleDefinitions.Clear();
            CommanderGameAccess.TryGetLocalVehicleDefinitions(availableVehicleDefinitions);
        }

        Factory[] allFactories = UnityEngine.Object.FindObjectsOfType<Factory>();
        friendlyFactories.Clear();

        for (int i = 0; i < allFactories.Length; i++)
        {
            Factory factory = allFactories[i];
            if (factory == null || factory.attachedUnit == null || factory.attachedUnit.disabled)
            {
                continue;
            }

            if (!CommanderGameAccess.IsFriendlyUnit(factory.attachedUnit, localHq))
            {
                continue;
            }

            bool isVehicleFactory = (VehiclesField?.GetValue(factory) as bool?) == true
                || factory.ProductionUnit is VehicleDefinition;

            if (isVehicleFactory)
            {
                friendlyFactories.Add(factory);
            }
        }

        if (selectedFactoryIndex >= friendlyFactories.Count)
        {
            selectedFactoryIndex = Mathf.Max(0, friendlyFactories.Count - 1);
        }
    }

    internal bool SetProductionUnit(Factory factory, VehicleDefinition newDefinition)
    {
        if (factory == null || factory.attachedUnit == null || factory.attachedUnit.disabled || newDefinition == null)
        {
            return false;
        }

        try
        {
            if (FactoryProductionSetter != null)
            {
                FactoryProductionSetter.Invoke(factory, new object[] { newDefinition });
            }
            else
            {
                factory.productionUnit = newDefinition;
            }

            CommanderPlugin.Log.LogInfo($"[Production Line] Set factory {factory.attachedUnit.unitName} to produce {newDefinition.unitName}.");
            return true;
        }
        catch (Exception ex)
        {
            CommanderPlugin.Log.LogWarning($"Failed to set factory production: {ex.Message}");
            return false;
        }
    }

    internal float GetProductionProgress(Factory factory)
    {
        if (factory == null || factory.productionInterval <= 0.01f)
        {
            return 0f;
        }

        float elapsed = Time.timeSinceLevelLoad - factory.lastProductionTime;
        return Mathf.Clamp01(elapsed / factory.productionInterval);
    }

    internal void ResetSession()
    {
        friendlyFactories.Clear();
        availableVehicleDefinitions.Clear();
        factoryRallyPoints.Clear();
        boundHq = null;
        selectedFactoryIndex = 0;
    }
}
