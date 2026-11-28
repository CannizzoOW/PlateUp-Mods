using System;
using System.Collections.Generic;
using System.Reflection;
using KitchenData;
using KitchenLib;
using KitchenLib.Utils;
using PreferenceSystem;

namespace MoreUpgrades;

public sealed class Main : BaseMod
{
    private const string ModId = "MoreUpgrades";
    private const string CycleHobs = "CycleHobs";
    private const string CycleCounters = "CycleCounters";
    private const string CycleMops = "CycleMops";
    private const string CycleCombiner = "CycleCombiner";
    private const string CycleTables = "CycleTables";
    private const string CycleShoes = "CycleShoes";
    private const string CycleAccessories = "CycleAccessories";

    private static readonly bool[] BoolValues = { false, true };
    private static readonly string[] BoolNames = { "Disabled", "Enabled" };
    private static readonly int[] UpgradeModes = { 0, 1, 2 };
    private static readonly string[] UpgradeModeNames = { "Vanilla paths only", "Random between all", "Cycle in listed order" };

    private readonly PreferenceSystemManager _preferences;

    public Main()
        : base(ModId, "More Upgrades", "Belkorin / compatibility rebuild", "2.1.0", ">=1.5.0", Assembly.GetExecutingAssembly())
    {
        _preferences = new PreferenceSystemManager(ModId, "More Upgrades")
            .AddLabel("Vanilla-style cycles")
            .AddLabel("Safety Hob <-> Danger Hob")
            .AddOption(CycleHobs, true, BoolValues, BoolNames)
            .AddInfo("Allows either upgraded hob to research into the other.")
            .AddLabel("Freezer <-> Workstation")
            .AddOption(CycleCounters, false, BoolValues, BoolNames)
            .AddInfo("Allows the two Counter upgrades to research into each other.")
            .AddLabel("Dining Table upgrade mode")
            .AddOption(CycleTables, 0, UpgradeModes, UpgradeModeNames)
            .AddInfo("Cycle order: Bar Table -> Metal Table -> Simple Cloth Table -> Fancy Cloth Table -> Bar Table.")
            .AddLabel("Additional cycles")
            .AddLabel("Robot Buffer -> Fast Mop -> Lasting Mop")
            .AddOption(CycleMops, false, BoolValues, BoolNames)
            .AddInfo("Adds the missing reverse research links between mop upgrades.")
            .AddLabel("Combiner <-> Portioner")
            .AddOption(CycleCombiner, false, BoolValues, BoolNames)
            .AddInfo("Allows a Combiner and Portioner to research into each other.")
            .AddLabel("Footwear upgrade mode")
            .AddOption(CycleShoes, 0, UpgradeModes, UpgradeModeNames)
            .AddInfo("Cycle order: Work Boots -> Trainers -> Wellies -> Work Boots.")
            .AddLabel("Player tool upgrade mode")
            .AddOption(CycleAccessories, 0, UpgradeModes, UpgradeModeNames)
            .AddInfo("Cycle order: Rolling Pin -> Sharp Knife -> Scrubbing Brush -> Rolling Pin.")
            .AddInfo("Changes take effect after restarting PlateUp.");

        _preferences.RegisterMenu(PreferenceSystemManager.MenuType.MainMenu);
        _preferences.RegisterMenu(PreferenceSystemManager.MenuType.PauseMenu);
    }

    protected override void OnInitialise()
    {
        ConfigurePair(1266458729, -1448690107, Get(CycleHobs, true));
        ConfigurePair(-1857890774, -1573577293, Get(CycleCounters, false));
        ConfigureMops(Get(CycleMops, false));
        ConfigurePair(-1906799936, -1462602185, Get(CycleCombiner, false));
        ConfigureCycle(new[] { 723626409, 1796077718, 230848637 }, Get(CycleShoes, 0));
        ConfigureCycle(new[] { -203679687, -3721951, -34659638, -2019409936 }, Get(CycleTables, 0));
        ConfigureCycle(new[] { 1738351766, 2023704259, 624465484 }, Get(CycleAccessories, 0));
    }

    private T Get<T>(string key, T fallback)
    {
        return _preferences.TryGet(key, out T value) ? value : fallback;
    }

    private static Appliance Find(int id)
    {
        return GDOUtils.GetExistingGDO(id) as Appliance;
    }

    private static void ConfigurePair(int firstId, int secondId, bool enabled)
    {
        Appliance first = Find(firstId);
        Appliance second = Find(secondId);
        if (first == null || second == null)
            return;

        Remove(first, second);
        Remove(second, first);
        if (enabled)
        {
            Add(first, second);
            Add(second, first);
        }
    }

    private static void ConfigureMops(bool enabled)
    {
        Appliance lasting = Find(-1495393751);
        Appliance fast = Find(1776760557);
        Appliance robot = Find(-2147057861);
        if (lasting == null || fast == null || robot == null)
            return;

        Remove(fast, lasting);
        Remove(robot, fast);
        if (enabled)
        {
            Add(fast, lasting);
            Add(robot, fast);
        }
    }

    private static void ConfigureCycle(int[] ids, int mode)
    {
        var appliances = new List<Appliance>(ids.Length);
        foreach (int id in ids)
        {
            Appliance appliance = Find(id);
            if (appliance == null)
                return;
            appliances.Add(appliance);
        }

        foreach (Appliance source in appliances)
            foreach (Appliance target in appliances)
                if (source != target)
                    Remove(source, target);

        if (mode == 0)
            return;

        for (int i = 0; i < appliances.Count; i++)
            Add(appliances[i], appliances[(i + 1) % appliances.Count]);

        if (mode == 1)
            foreach (Appliance source in appliances)
                foreach (Appliance target in appliances)
                    if (source != target)
                        Add(source, target);
    }

    private static void Add(Appliance source, Appliance target)
    {
        if (!source.Upgrades.Contains(target))
            source.Upgrades.Add(target);
    }

    private static void Remove(Appliance source, Appliance target)
    {
        while (source.Upgrades.Remove(target)) { }
    }
}
