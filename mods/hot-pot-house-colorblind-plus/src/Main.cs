using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenLib;
using KitchenLib.Utils;
using KitchenMods;
using PreferenceSystem;
using UnityEngine;

namespace HotPotHouseColorblindPlus;

public sealed class Main : BaseMod
{
    public const string ModId = "cannizzo.hotpothouse.colorblindplus";
    internal const string HideRegularItemLabels = "Regular item labels";
    internal const string HidePotItemLabels = "Pot and broth item labels";
    internal const string HideIngredientProviderLabels = "Ingredient provider labels";
    internal const string HidePotRackLabels = "Pot rack labels";
    internal const string HideSideDishRackLabel = "Side-dish rack label";
    internal const string HideSharedOrderLabels = "Shared order labels";
    internal const string HideColorblindItemLabels = "Colorblind+ item labels";
    internal const string HideColorblindDirtySideDishLabel = "Dirty Hot Pot House side-dish label";
    internal const string HideCountUpProviderLabels = "CountUp! provider counters";
    internal const string HideCountUpItemLabels = "CountUp! item portion and process labels";
    private static readonly bool[] ToggleValues = { false, true };
    private static readonly string[] ToggleNames = { "Show", "Hide" };
    private static PreferenceSystemManager preferences;

    public Main() : base(
        ModId,
        "Hot Pot House - Colorblind+ Compatibility",
        "Cannizzo",
        "1.3.0",
        ">=1.5.0",
        Assembly.GetExecutingAssembly())
    {
        preferences = new PreferenceSystemManager(ModId, "Text Display Controls")
            .AddSubmenu("Hot Pot House", "HotPotHouse")
            .AddLabel("Items")
            .AddOption<bool>(HideRegularItemLabels, true, ToggleValues, ToggleNames)
            .AddOption<bool>(HidePotItemLabels, true, ToggleValues, ToggleNames)
            .AddLabel("Providers")
            .AddOption<bool>(HideIngredientProviderLabels, true, ToggleValues, ToggleNames)
            .AddOption<bool>(HidePotRackLabels, true, ToggleValues, ToggleNames)
            .AddOption<bool>(HideSideDishRackLabel, true, ToggleValues, ToggleNames)
            .AddLabel("Orders")
            .AddOption<bool>(HideSharedOrderLabels, true, ToggleValues, ToggleNames)
            .SubmenuDone()
            .AddSubmenu("Colorblind+", "ColorblindPlus")
            .AddLabel("Item text")
            .AddOption<bool>(HideColorblindItemLabels, false, ToggleValues, ToggleNames)
            .AddOption<bool>(HideColorblindDirtySideDishLabel, true, ToggleValues, ToggleNames)
            .AddInfo("Controls labels owned by Colorblind+. Its own detailed dish and font settings remain available in the Colorblind+ menu.")
            .SubmenuDone()
            .AddSubmenu("CountUp!", "CountUp")
            .AddLabel("Provider text")
            .AddOption<bool>(HideCountUpProviderLabels, false, ToggleValues, ToggleNames)
            .AddLabel("Item text")
            .AddOption<bool>(HideCountUpItemLabels, false, ToggleValues, ToggleNames)
            .SubmenuDone()
            .AddInfo("Text controls are grouped by the mod that creates them. Changes take effect after restarting PlateUp.");
        preferences.RegisterMenu((PreferenceSystemManager.MenuType)0);
        preferences.RegisterMenu((PreferenceSystemManager.MenuType)1);
    }

    protected override void OnPostActivate(Mod mod)
    {
        Debug.Log("[Hot Pot House - Colorblind+ Compatibility] v1.3.0 loaded with persistent Hot Pot House text suppression and per-mod controls.");
    }

    internal static bool IsColorblindPlusLoaded()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            (assembly.GetName().Name ?? string.Empty).StartsWith("Colorblind", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool Hide(string key, bool fallback = true)
    {
        return preferences != null && preferences.TryGet<bool>(key, out bool value) ? value : fallback;
    }

    internal static bool ShouldHideHotPotHouseLabel(string content, bool isProvider)
    {
        if (isProvider)
        {
            if (string.Equals(content, "Side Dishes", StringComparison.OrdinalIgnoreCase))
                return Hide(HideSideDishRackLabel);
            if (ConfigureHotPotHouseItemAndProviderLabelsPatch.PotRackLabels.Contains(content))
                return Hide(HidePotRackLabels);
            return Hide(HideIngredientProviderLabels);
        }

        bool isPotOrBroth = content.IndexOf("pot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            content.IndexOf("broth", StringComparison.OrdinalIgnoreCase) >= 0;
        return Hide(isPotOrBroth ? HidePotItemLabels : HideRegularItemLabels);
    }

    internal static bool IsHotPotHouseDirtySideDish(int itemId)
    {
        try
        {
            Item item = GameData.Main.Get<Item>(itemId);
            return item != null && item.name.EndsWith(" - dirty_side_dish", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

[HarmonyPatch]
internal static class ConfigureHotPotHouseItemAndProviderLabelsPatch
{
    internal static readonly HashSet<string> PotRackLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Small Hot Pots",
        "Large Hot Pots",
        "Yin-Yang Pots"
    };

    private static MethodBase TargetMethod()
    {
        Type itemNameTag = AccessTools.TypeByName("HotPotHouse.ItemNameTag");
        return itemNameTag == null ? null : AccessTools.Method(itemNameTag, "Start");
    }

    private static bool Prefix(Component __instance)
    {
        if (__instance == null)
            return true;

        string content = AccessTools.Field(__instance.GetType(), "Content")?.GetValue(__instance) as string ?? string.Empty;
        bool isProvider = __instance.GetComponentInParent<ApplianceView>() != null;
        return !Main.ShouldHideHotPotHouseLabel(content, isProvider);
    }
}

[HarmonyPatch]
internal static class PersistentlySuppressHotPotHouseLabelsPatch
{
    private static MethodBase TargetMethod()
    {
        Type nameTag = AccessTools.TypeByName("HotPotHouse.NameTag");
        return nameTag == null ? null : AccessTools.Method(nameTag, "LateUpdate");
    }

    private static bool Prefix(Component __instance)
    {
        if (__instance == null)
            return true;

        bool isOrder = AccessTools.Field(__instance.GetType(), "isOrderLabel")?.GetValue(__instance) as bool? ?? false;
        bool hide = isOrder && Main.Hide(Main.HideSharedOrderLabels);
        if (!isOrder)
        {
            Component marker = __instance.GetComponentsInParent<Component>(true)
                .FirstOrDefault(component => component != null && component.GetType().FullName == "HotPotHouse.ItemNameTag");
            if (marker == null)
                return true;
            string content = AccessTools.Field(marker.GetType(), "Content")?.GetValue(marker) as string ?? string.Empty;
            hide = Main.ShouldHideHotPotHouseLabel(content, marker.GetComponentInParent<ApplianceView>() != null);
        }

        if (!hide)
            return true;

        Behaviour text = AccessTools.Field(__instance.GetType(), "text")?.GetValue(__instance) as Behaviour;
        if (text == null)
            text = __instance.GetComponents<Component>().OfType<Behaviour>()
                .FirstOrDefault(component => component.GetType().Namespace == "TMPro");
        if (text != null)
            text.enabled = false;
        return false;
    }
}

[HarmonyPatch]
internal static class HideHotPotHouseOrderTextPatch
{
    private static MethodBase TargetMethod()
    {
        Type nameTag = AccessTools.TypeByName("HotPotHouse.NameTag");
        return nameTag == null ? null : AccessTools.Method(nameTag, "ForOrder");
    }

    private static bool Prefix() => !Main.Hide(Main.HideSharedOrderLabels);
}

[HarmonyPatch(typeof(ItemGroupView), "PerformUpdate")]
[HarmonyAfter("blargle.ColorblindPlus")]
[HarmonyPriority(HarmonyLib.Priority.Last)]
internal static class ConfigureColorblindPlusItemLabelsPatch
{
    private static void Postfix(ItemGroupView __instance, int __0)
    {
        if (!Main.IsColorblindPlusLoaded() || __instance == null)
            return;

        bool hide = Main.Hide(Main.HideColorblindItemLabels, false) ||
                    (Main.Hide(Main.HideColorblindDirtySideDishLabel) && Main.IsHotPotHouseDirtySideDish(__0));
        if (!hide)
            return;

        Component label = AccessTools.Field(typeof(ItemGroupView), "ColourblindLabel")
            ?.GetValue(__instance) as Component;
        if (label != null)
            label.gameObject.SetActive(false);
    }
}

[HarmonyPatch]
internal static class ConfigureCountUpProviderLabelsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type view = AccessTools.TypeByName("KitchenCountUp.Views.ApplianceCountView");
        if (view == null)
            yield break;
        foreach (MethodInfo method in view.GetMethods(AccessTools.all))
            if (method.Name == "UpdateData")
                yield return method;
    }

    private static void Postfix(object __instance)
    {
        if (!Main.Hide(Main.HideCountUpProviderLabels) || __instance == null)
            return;
        Component countText = AccessTools.Field(__instance.GetType(), "CountText")
            ?.GetValue(__instance) as Component;
        if (countText != null)
            countText.gameObject.SetActive(false);
    }
}

[HarmonyPatch]
internal static class ConfigureCountUpItemLabelsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type view = AccessTools.TypeByName("KitchenCountUp.Views.ItemIconView");
        if (view == null)
            yield break;
        foreach (MethodInfo method in view.GetMethods(AccessTools.all))
            if (method.Name == "UpdateData")
                yield return method;
    }

    private static void Postfix(object __instance)
    {
        if (!Main.Hide(Main.HideCountUpItemLabels) || __instance == null)
            return;
        Component processIcons = AccessTools.Field(__instance.GetType(), "ProcessIcons")
            ?.GetValue(__instance) as Component;
        if (processIcons != null)
            processIcons.gameObject.SetActive(false);
    }
}

[HarmonyPatch(typeof(ItemCollectionView), "UpdateData")]
internal static class CollapseDuplicateSharedPotOrdersPatch
{
    private static readonly HashSet<string> SharedFamilyPotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hotpothouse - family_hot_pot",
        "hotpothouse - family_mala_hot_pot",
        "hotpothouse - family_mushroom_hot_pot",
        "hotpothouse - family_bone_hot_pot"
    };
    private sealed class PotClaim
    {
        public ItemCollectionView Owner;
        public ItemCollectionView.ItemData Order;
    }

    private static readonly List<PotClaim> Claims = new List<PotClaim>();
    private static HashSet<int> SharedFamilyPotIds;

    private static void Prefix(ItemCollectionView __instance, ref ItemCollectionView.ViewData view_data)
    {
        if (!Main.IsColorblindPlusLoaded())
            return;

        Claims.RemoveAll(claim => claim.Owner == null || ReferenceEquals(claim.Owner, __instance));
        List<ItemCollectionView.ItemData> items = view_data.Items;
        if (items == null || items.Count == 0)
            return;

        var filtered = new List<ItemCollectionView.ItemData>(items.Count);
        foreach (ItemCollectionView.ItemData candidate in items)
        {
            if (!IsSharedFamilyPot(candidate.ItemID) || candidate.IsComplete)
            {
                filtered.Add(candidate);
                continue;
            }

            if (Claims.Exists(claim => IsSameSharedOrder(claim.Order, candidate)))
                continue;

            Claims.Add(new PotClaim { Owner = __instance, Order = candidate });
            filtered.Add(candidate);
        }

        if (filtered.Count != items.Count)
            view_data.Items = filtered;
    }

    private static bool IsSameSharedOrder(ItemCollectionView.ItemData existing, ItemCollectionView.ItemData candidate)
    {
        return existing.ItemID == candidate.ItemID &&
               (existing.TablePosition - candidate.TablePosition).sqrMagnitude < 0.01f &&
               existing.Components.IsEquivalent(candidate.Components, false);
    }

    private static bool IsSharedFamilyPot(int itemId)
    {
        if (SharedFamilyPotIds == null || SharedFamilyPotIds.Count == 0)
        {
            SharedFamilyPotIds = new HashSet<int>();
            foreach (ItemGroup item in GameData.Main.Get<ItemGroup>())
                if (item != null && SharedFamilyPotNames.Contains(item.name))
                    SharedFamilyPotIds.Add(item.ID);
        }
        return SharedFamilyPotIds.Contains(itemId);
    }
}
