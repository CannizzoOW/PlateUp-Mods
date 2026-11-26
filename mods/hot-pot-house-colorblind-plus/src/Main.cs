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
    internal const string HideCountUpIngredientProviderLabels = "CountUp! ingredient provider counters";
    internal const string HideCountUpOtherProviderLabels = "CountUp! other provider counters";
    internal const string HideCountUpItemLabels = "CountUp! item portion and process labels";
    private static readonly bool[] ToggleValues = { false, true };
    private static readonly string[] ToggleNames = { "Show text", "Hide text" };
    private static PreferenceSystemManager preferences;

    public Main() : base(
        ModId,
        "Hot Pot House - Colorblind+ Compatibility",
        "Cannizzo",
        "1.6.1",
        ">=1.5.0",
        Assembly.GetExecutingAssembly())
    {
        // KitchenLib can construct the initializer again while injecting mod
        // systems. A second PreferenceSystemManager creates another dynamic
        // top-level menu type even though its preference registry is rejected.
        if (preferences != null)
            return;

        preferences = new PreferenceSystemManager(ModId, "Text Display Controls")
            .AddSubmenu("Hot Pot House", "HotPotHouse")
            .AddInfo("Controls text created by Hot Pot House. Changes apply immediately.")
            .AddSubmenu("Items", "HotPotHouseItems")
            .AddLabel("Regular item names")
            .AddOption<bool>(HideRegularItemLabels, true, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .AddLabel("Pot and broth names")
            .AddOption<bool>(HidePotItemLabels, true, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .SubmenuDone()
            .AddSubmenu("Providers", "HotPotHouseProviders")
            .AddLabel("Ingredient provider names")
            .AddOption<bool>(HideIngredientProviderLabels, true, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .AddLabel("Hot-pot rack names")
            .AddOption<bool>(HidePotRackLabels, true, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .AddLabel("Side-dish rack name")
            .AddOption<bool>(HideSideDishRackLabel, true, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .SubmenuDone()
            .AddSubmenu("Orders", "HotPotHouseOrders")
            .AddLabel("Shared hot-pot order text")
            .AddOption<bool>(HideSharedOrderLabels, true, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .SubmenuDone()
            .SubmenuDone()
            .AddSubmenu("Colorblind+", "ColorblindPlus")
            .AddInfo("Controls text created by Colorblind+. Changes apply immediately.")
            .AddLabel("All Colorblind+ item text")
            .AddOption<bool>(HideColorblindItemLabels, false, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .AddLabel("All dirty HPH side-dish text")
            .AddOption<bool>(HideColorblindDirtySideDishLabel, true, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .AddInfo("Controls both Colorblind+ and the original Hot Pot House Dirty text.")
            .AddInfo("Other Colorblind+ dish and font settings remain in the original Colorblind+ menu.")
            .SubmenuDone()
            .AddSubmenu("CountUp!", "CountUp")
            .AddInfo("Controls text created by CountUp!. Changes apply immediately.")
            .AddLabel("Ingredient provider counters")
            .AddOption<bool>(HideCountUpIngredientProviderLabels, false, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .AddLabel("Other provider counters")
            .AddOption<bool>(HideCountUpOtherProviderLabels, false, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .AddLabel("Item portion and process text")
            .AddOption<bool>(HideCountUpItemLabels, false, ToggleValues, ToggleNames, OnTextPreferenceChanged)
            .SubmenuDone()
            .AddInfo("Choose the mod that creates the text you want to control. Changes apply immediately.");
        preferences.RegisterMenu((PreferenceSystemManager.MenuType)0);
        preferences.RegisterMenu((PreferenceSystemManager.MenuType)1);
    }

    protected override void OnPostActivate(Mod mod)
    {
        Debug.Log("[Hot Pot House - Colorblind+ Compatibility] v1.6.1 loaded with live text controls and split CountUp provider controls.");
    }

    private static void OnTextPreferenceChanged(bool value)
    {
        RefreshLiveText();
    }

    internal static void RefreshLiveText()
    {
        ConfigureHotPotHouseItemAndProviderLabelsPatch.RefreshExisting();
        PersistentlySuppressHotPotHouseLabelsPatch.RefreshExisting();
        ConfigureColorblindPlusItemLabelsPatch.RefreshExisting();
        ConfigureCountUpProviderLabelsPatch.RefreshExisting();
        ConfigureCountUpItemLabelsPatch.RefreshExisting();
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
        // A dirty side dish inside a dishwasher or washbasin inherits an
        // ApplianceView from the appliance. It is still an item label, not a
        // provider label, so recognise it before checking isProvider.
        if (string.Equals(content, "Dirty", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(content, "Dirty Side Dish", StringComparison.OrdinalIgnoreCase))
            return Hide(HideColorblindDirtySideDishLabel);

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
            string itemName = item?.name ?? string.Empty;
            if (itemName.IndexOf("hotpothouse", StringComparison.OrdinalIgnoreCase) >= 0 &&
                itemName.IndexOf("dirty_side_dish", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            GameObject prefab = GameData.Main.GetPrefab(itemId);
            string prefabName = prefab?.name ?? string.Empty;
            return prefabName.IndexOf("dirty_side_dish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefabName.IndexOf("side_dish_dirty", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsServedHotPotBowlVisual(Component component)
    {
        for (Transform current = component?.transform; current != null; current = current.parent)
        {
            string objectName = current.name ?? string.Empty;
            if (objectName.StartsWith("Hot Pot Guest Soup Bowl", StringComparison.Ordinal) ||
                objectName.StartsWith("Hot Pot Guest Bowls ", StringComparison.Ordinal) ||
                string.Equals(objectName, "Served Hot Pot Contents", StringComparison.Ordinal))
                return true;
        }
        return false;
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
        bool hide = Main.IsServedHotPotBowlVisual(__instance) || Main.ShouldHideHotPotHouseLabel(content, isProvider);

        Transform scope = isProvider
            ? __instance.GetComponentInParent<ApplianceView>().transform
            : __instance.transform;
        List<Component> existingTags = scope.GetComponentsInChildren<Component>(true)
            .Where(component => component != null && component.GetType().FullName == "HotPotHouse.NameTag")
            .ToList();

        if (hide)
        {
            foreach (Component tag in existingTags)
                tag.gameObject.SetActive(false);
            if (__instance is Behaviour hiddenMarker)
                hiddenMarker.enabled = false;
            return false;
        }

        if (existingTags.Count > 0)
        {
            existingTags[0].gameObject.SetActive(true);
            foreach (Component duplicate in existingTags.Skip(1))
                duplicate.gameObject.SetActive(false);
            if (__instance is Behaviour markerBehaviour)
                markerBehaviour.enabled = false;
            return false;
        }

        return true;
    }

    internal static void RefreshExisting()
    {
        Type markerType = AccessTools.TypeByName("HotPotHouse.ItemNameTag");
        MethodInfo start = markerType == null ? null : AccessTools.Method(markerType, "Start");
        if (markerType == null || start == null)
            return;

        foreach (UnityEngine.Object found in Resources.FindObjectsOfTypeAll(markerType))
        {
            Component marker = found as Component;
            if (marker == null || !marker.gameObject.scene.IsValid())
                continue;

            marker.gameObject.SetActive(true);
            string content = AccessTools.Field(markerType, "Content")?.GetValue(marker) as string ?? string.Empty;
            ApplianceView appliance = marker.GetComponentInParent<ApplianceView>();
            bool hide = Main.IsServedHotPotBowlVisual(marker) ||
                        Main.ShouldHideHotPotHouseLabel(content, appliance != null);
            Transform scope = appliance != null ? appliance.transform : marker.transform;
            List<Component> tags = scope.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && component.GetType().FullName == "HotPotHouse.NameTag")
                .ToList();

            if (hide)
            {
                foreach (Component tag in tags)
                    tag.gameObject.SetActive(false);
                if (marker is Behaviour hiddenMarker)
                    hiddenMarker.enabled = false;
                continue;
            }

            if (tags.Count > 0)
            {
                tags[0].gameObject.SetActive(true);
                foreach (Component duplicate in tags.Skip(1))
                    duplicate.gameObject.SetActive(false);
                if (marker is Behaviour existingMarker)
                    existingMarker.enabled = false;
            }
            else
            {
                if (marker is Behaviour missingMarker)
                    missingMarker.enabled = true;
                start.Invoke(marker, null);
            }
        }
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
        bool hide = Main.IsServedHotPotBowlVisual(__instance) ||
                    (isOrder && Main.Hide(Main.HideSharedOrderLabels));
        if (!hide && !isOrder)
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

        // Disable the label root, not only the renderer. HPH LateUpdate and
        // pooled request holders can otherwise re-enable or move the text.
        __instance.gameObject.SetActive(false);
        return false;
    }

    internal static void RefreshExisting()
    {
        Type nameTagType = AccessTools.TypeByName("HotPotHouse.NameTag");
        if (nameTagType == null)
            return;
        foreach (UnityEngine.Object found in Resources.FindObjectsOfTypeAll(nameTagType))
        {
            Component tag = found as Component;
            if (tag == null || !tag.gameObject.scene.IsValid())
                continue;
            if (Main.IsServedHotPotBowlVisual(tag))
            {
                tag.gameObject.SetActive(false);
                continue;
            }
            bool isOrder = AccessTools.Field(nameTagType, "isOrderLabel")?.GetValue(tag) as bool? ?? false;
            if (isOrder)
                tag.gameObject.SetActive(!Main.Hide(Main.HideSharedOrderLabels));
        }
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

    private static void Postfix()
    {
        // Always let HPH create the order label, then hide it when requested.
        // Keeping the inactive object makes Show text reversible immediately.
        PersistentlySuppressHotPotHouseLabelsPatch.RefreshExisting();
    }
}

[HarmonyPatch(typeof(ItemGroupView), "PerformUpdate")]
[HarmonyAfter("blargle.ColorblindPlus")]
[HarmonyPriority(HarmonyLib.Priority.Last)]
internal static class ConfigureColorblindPlusItemLabelsPatch
{
    private static readonly Dictionary<ItemGroupView, int> KnownViews = new Dictionary<ItemGroupView, int>();
    private static readonly Dictionary<ItemGroupView, HashSet<GameObject>> HiddenTextObjects = new Dictionary<ItemGroupView, HashSet<GameObject>>();
    private static readonly Dictionary<ItemGroupView, bool> OriginalForceNoColourblind = new Dictionary<ItemGroupView, bool>();

    private static void Postfix(ItemGroupView __instance, int __0)
    {
        if (__instance == null)
            return;
        KnownViews[__instance] = __0;
        Apply(__instance, __0);
    }

    private static void Apply(ItemGroupView instance, int itemId)
    {
        if (!Main.IsColorblindPlusLoaded() || instance == null)
            return;
        bool dirtySideDish = Main.IsHotPotHouseDirtySideDish(itemId);
        bool hide = Main.IsServedHotPotBowlVisual(instance) ||
                    Main.Hide(Main.HideColorblindItemLabels, false) ||
                    (Main.Hide(Main.HideColorblindDirtySideDishLabel) && dirtySideDish);
        Component label = AccessTools.Field(typeof(ItemGroupView), "ColourblindLabel")
            ?.GetValue(instance) as Component;

        if (hide)
        {
            if (!OriginalForceNoColourblind.ContainsKey(instance))
                OriginalForceNoColourblind[instance] = instance.ForceNoColourblind;
            instance.ForceNoColourblind = true;
            if (!HiddenTextObjects.TryGetValue(instance, out HashSet<GameObject> hidden))
                HiddenTextObjects[instance] = hidden = new HashSet<GameObject>();

            if (label != null && label.gameObject.activeSelf)
            {
                hidden.Add(label.gameObject);
                label.gameObject.SetActive(false);
            }

            // Colorblind+ can populate additional TMP children rather than
            // the single vanilla ColourblindLabel field.
            if (dirtySideDish || Main.Hide(Main.HideColorblindItemLabels, false))
            {
                foreach (Component component in instance.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && component.GetType().Namespace == "TMPro" && component.gameObject.activeSelf)
                    {
                        hidden.Add(component.gameObject);
                        component.gameObject.SetActive(false);
                    }
                }
            }
            return;
        }

        // Restore only objects hidden by this compatibility mod. Activating
        // every TMP child exposed Colorblind+'s internal/debug labels.
        if (HiddenTextObjects.TryGetValue(instance, out HashSet<GameObject> restore))
        {
            foreach (GameObject textObject in restore)
                if (textObject != null)
                    textObject.SetActive(true);
            HiddenTextObjects.Remove(instance);
        }
        if (OriginalForceNoColourblind.TryGetValue(instance, out bool originalForceNoColourblind))
        {
            instance.ForceNoColourblind = originalForceNoColourblind;
            OriginalForceNoColourblind.Remove(instance);
        }
    }

    internal static void RefreshExisting()
    {
        foreach (KeyValuePair<ItemGroupView, int> entry in KnownViews.ToArray())
        {
            if (entry.Key == null)
                KnownViews.Remove(entry.Key);
            else
                Apply(entry.Key, entry.Value);
        }
    }
}

[HarmonyPatch]
internal static class ConfigureCountUpProviderLabelsPatch
{
    private static readonly HashSet<GameObject> HiddenCounters = new HashSet<GameObject>();

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
        Apply(__instance);
    }

    private static void Apply(object instance)
    {
        Component view = instance as Component;
        if (view == null)
            return;
        Component countText = AccessTools.Field(instance.GetType(), "CountText")
            ?.GetValue(instance) as Component;
        if (countText == null)
            return;

        bool ingredientProvider = IsIngredientProvider(view);
        string preference = ingredientProvider
            ? Main.HideCountUpIngredientProviderLabels
            : Main.HideCountUpOtherProviderLabels;
        bool hide = Main.Hide(preference, false);

        if (hide)
        {
            if (countText.gameObject.activeSelf)
                HiddenCounters.Add(countText.gameObject);
            countText.gameObject.SetActive(false);
        }
        else if (HiddenCounters.Remove(countText.gameObject))
        {
            countText.gameObject.SetActive(true);
        }
    }

    private static bool IsIngredientProvider(Component countView)
    {
        ApplianceView applianceView = countView.GetComponent<ApplianceView>() ??
                                      countView.GetComponentInParent<ApplianceView>();
        if (applianceView == null)
            return false;

        Component marker = applianceView.GetComponentsInChildren<Component>(true)
            .FirstOrDefault(component => component != null && component.GetType().FullName == "HotPotHouse.ItemNameTag");
        if (marker != null)
        {
            string content = AccessTools.Field(marker.GetType(), "Content")?.GetValue(marker) as string ?? string.Empty;
            return !string.Equals(content, "Side Dishes", StringComparison.OrdinalIgnoreCase) &&
                   !ConfigureHotPotHouseItemAndProviderLabelsPatch.PotRackLabels.Contains(content);
        }

        try
        {
            GameObject prefab = AccessTools.Field(typeof(ApplianceView), "Prefab")?.GetValue(applianceView) as GameObject;
            Appliance appliance = GameData.Main.Get<Appliance>().FirstOrDefault(candidate =>
                candidate != null && prefab != null &&
                (candidate.Prefab == prefab || string.Equals(candidate.Prefab?.name, prefab.name, StringComparison.Ordinal)));
            CItemProvider? provider = appliance?.Properties.OfType<CItemProvider>().Cast<CItemProvider?>().FirstOrDefault();
            Item providedItem = provider.HasValue ? GameData.Main.Get<Item>(provider.Value.ProvidedItem) : null;

            // Plates, dishes, pots and similar reusable vessels are providers,
            // but not ingredients. Ingredient items are disposable stock.
            return providedItem != null && !providedItem.IsIndisposable;
        }
        catch
        {
            return false;
        }
    }

    internal static void RefreshExisting()
    {
        Type viewType = AccessTools.TypeByName("KitchenCountUp.Views.ApplianceCountView");
        if (viewType == null)
            return;
        foreach (UnityEngine.Object found in Resources.FindObjectsOfTypeAll(viewType))
            if (found is Component component && component.gameObject.scene.IsValid())
                Apply(component);
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
        Apply(__instance);
    }

    private static void Apply(object instance)
    {
        if (instance == null)
            return;
        Component processIcons = AccessTools.Field(instance.GetType(), "ProcessIcons")
            ?.GetValue(instance) as Component;
        if (processIcons != null)
            processIcons.gameObject.SetActive(!Main.Hide(Main.HideCountUpItemLabels, false));
    }

    internal static void RefreshExisting()
    {
        Type viewType = AccessTools.TypeByName("KitchenCountUp.Views.ItemIconView");
        if (viewType == null)
            return;
        foreach (UnityEngine.Object found in Resources.FindObjectsOfTypeAll(viewType))
            if (found is Component component && component.gameObject.scene.IsValid())
                Apply(component);
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
