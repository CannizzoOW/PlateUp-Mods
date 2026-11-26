using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenLib;
using KitchenLib.Event;
using KitchenLib.Utils;
using KitchenLib.References;
using KitchenMods;
using UnityEngine;

namespace HotPotHouseEnhanced;

public sealed class Main : BaseMod
{
    public const string ModId = "cannizzo.hotpothouse.enhanced";

    public Main()
        : base(
            ModId,
            "Hot Pot House Enhanced",
            "Cannizzo",
            "2.4.13",
            ">=1.5.0",
            Assembly.GetExecutingAssembly())
    {
    }

    protected override void OnPostActivate(Mod mod)
    {
        Events.BuildGameDataEvent += (_, args) => UseVanillaProviderPrefabs.Apply(args.gamedata);
        Debug.Log("[Hot Pot House Enhanced] v2.4.13 loaded; shared-pot guest bowls are created once per completed order.");
    }
}

internal static class UseVanillaProviderPrefabs
{
    internal static GameObject CompletedSoupBowlPrefab { get; private set; }
    internal static int DirtySideDishId { get; private set; }
    private static readonly HashSet<string> RefrigeratorProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "duck_blood_provider",
        "fish_paste_provider",
        "frozen_beef_provider",
        "frozen_lamb_provider",
        "jelly_provider",
        "sago_provider",
        "shrimp_provider",
        "soft_serve_machine",
        "soy_milk_provider",
        "tangyuan_provider",
        "tripe_provider"
    };

    internal static void Apply(GameData data)
    {
        if (data == null)
            return;

        ConfigureSideDishStorage(data);

        Appliance vanillaRefrigerator = data.Get<Appliance>(ApplianceReferences.SourceMeat);
        Appliance vanillaCounter = data.Get<Appliance>(ApplianceReferences.Countertop);
        Appliance vanillaPlateStack = data.Get<Appliance>(ApplianceReferences.PlateStack);
        Appliance vanillaPotStack = data.Get<Appliance>(ApplianceReferences.PotStack);
        Appliance hotPotWasher = data.Get<Appliance>().FirstOrDefault(appliance =>
            appliance != null && appliance.name.EndsWith(" - pot_washer", StringComparison.OrdinalIgnoreCase));
        Transform hotPotRackModel = hotPotWasher?.Prefab == null
            ? null
            : FindDescendant(hotPotWasher.Prefab.transform, "HPH Model");
        if (vanillaRefrigerator?.Prefab == null || vanillaCounter?.Prefab == null ||
            vanillaPlateStack?.Prefab == null || vanillaPotStack?.Prefab == null)
        {
            Debug.LogWarning("[Hot Pot House Enhanced] A required vanilla counter, refrigerator, plate-stack, or pot-stack prefab was unavailable during GameData build.");
            return;
        }
        if (hotPotRackModel == null)
            Debug.LogWarning("[Hot Pot House Enhanced] The Hot Pot House pot-rack model was unavailable; Small Hot Pots will keep the counter fallback.");

        int replaced = 0;
        foreach (Appliance gdo in data.Get<Appliance>())
        {
            try
            {
                if (gdo?.Prefab == null || !gdo.name.StartsWith("hotpothouse - ", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Regular HProvider prefabs always contain the display model
                // created by HProvider.Prefab. Bespoke machines do not.
                if (gdo.Prefab.transform.Find("Display") == null)
                    continue;

                string uniqueName = gdo.name.Substring("hotpothouse - ".Length);
                bool useRefrigerator = RefrigeratorProviders.Contains(uniqueName);
                bool usePlateStack = string.Equals(uniqueName, "side_dish_stack", StringComparison.OrdinalIgnoreCase);
                bool usePotRack = hotPotRackModel != null &&
                    string.Equals(uniqueName, "hot_pot_stack", StringComparison.OrdinalIgnoreCase);
                bool useCounter = !useRefrigerator && !usePlateStack && !usePotRack;
                GameObject source = usePlateStack
                    ? vanillaPlateStack.Prefab
                    : usePotRack
                        ? vanillaPotStack.Prefab
                    : useCounter
                        ? vanillaCounter.Prefab
                        : vanillaRefrigerator.Prefab;
                Transform hotPotDisplay = gdo.Prefab.transform.Find("Display");
                GameObject ingredientModel = usePlateStack
                    ? null
                    : usePotRack
                        ? UnityEngine.Object.Instantiate(hotPotRackModel.gameObject)
                        : CreateSoupsPlusDisplay(data, uniqueName);
                bool usesSoupsPlusDisplay = ingredientModel != null;
                if (!usePlateStack && !usePotRack && ingredientModel == null && hotPotDisplay != null)
                    ingredientModel = UnityEngine.Object.Instantiate(hotPotDisplay.gameObject);
                Transform prefabHolder = gdo.Prefab.transform.parent;
                GameObject replacement = UnityEngine.Object.Instantiate(source);
                replacement.name = gdo.Prefab.name;

                // Preserve vanilla size, serialized ItemSourceView references,
                // indicator renderer and refrigerator-door Animator exactly.
                replacement.transform.SetParent(prefabHolder, false);
                replacement.transform.localPosition = source.transform.localPosition;
                replacement.transform.localRotation = source.transform.localRotation;
                replacement.transform.localScale = source.transform.localScale;

                if (usePotRack)
                    HideVisualRenderers(replacement);
                else if (usePlateStack)
                    ConfigureNumericSideDishRack(replacement);
                else if (!useCounter && !usePlateStack)
                    RemoveVanillaIngredientModels(replacement);
                if (ingredientModel != null)
                {
                    ingredientModel.name = "Hot Pot House Ingredient Display";
                    ingredientModel.transform.SetParent(replacement.transform, false);
                    ingredientModel.transform.localPosition = useCounter
                        ? Vector3.zero
                        : useRefrigerator
                            ? new Vector3(0f, 0.61f, -0.03f)
                            : new Vector3(0f, 0.52f, 0f);
                    if (!usesSoupsPlusDisplay)
                        ingredientModel.transform.localRotation = Quaternion.identity;
                    if (usePotRack)
                    {
                        FitModelToApplianceHeight(ingredientModel, source, 1f);
                    }
                    else if (useCounter)
                    {
                        if (string.Equals(uniqueName, "family_pot_stack", StringComparison.OrdinalIgnoreCase))
                            ingredientModel.transform.localScale *= 1.3f;
                        float surfaceY = GetSurfaceHeight(replacement, 0.5f);
                        if (usesSoupsPlusDisplay)
                            FitDisplayOnCounter(ingredientModel, 0.62f, 0.38f, surfaceY);
                        else
                            AlignDisplayOnSurface(ingredientModel, surfaceY);
                    }
                }

                gdo.Prefab = replacement;
                replaced++;

                string replacementType = usePlateStack ? "plate stack" : usePotRack ? "pot rack" : useCounter ? "counter" : "refrigerator";
                Debug.Log($"[Hot Pot House Order Cleanup] Replaced {gdo.name} with vanilla {replacementType} prefab.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Hot Pot House Order Cleanup] Could not replace provider prefab for {gdo?.name}: {ex}");
            }
        }

        Debug.Log($"[Hot Pot House Order Cleanup] Replaced {replaced} Hot Pot House provider prefabs during GameData build.");
        ReplaceMalaPickupModel(data);
        CacheCompletedSoupBowl(data);
    }

    private static void ConfigureSideDishStorage(GameData data)
    {
        Item cleanSideDish = data.Get<Item>().FirstOrDefault(item =>
            item != null && item.name.EndsWith(" - side_dish", StringComparison.OrdinalIgnoreCase));
        Item dirtySideDish = data.Get<Item>().FirstOrDefault(item =>
            item != null && item.name.EndsWith(" - dirty_side_dish", StringComparison.OrdinalIgnoreCase));

        if (cleanSideDish == null || dirtySideDish == null)
        {
            Debug.LogWarning("[Hot Pot House Enhanced] Could not apply vanilla dish-storage flags because one or both Hot Pot House side-dish items were unavailable.");
            return;
        }

        // Vanilla sinks, washbasins and dishwashers use these flags to place each
        // dish in their serialized storage slots and update the visible count.
        cleanSideDish.ItemStorageFlags = ItemStorage.Dish;
        dirtySideDish.ItemStorageFlags = ItemStorage.DirtyDish;
        DirtySideDishId = dirtySideDish.ID;
        Debug.Log("[Hot Pot House Enhanced] Side dishes now use vanilla clean/dirty dish storage and appliance count visuals.");
    }

    private static void CacheCompletedSoupBowl(GameData data)
    {
        Item soup = data.Get<Item>().FirstOrDefault(item =>
            item != null && item.Prefab != null &&
            item.name.EndsWith(" - Chicken Noodle Soup", StringComparison.OrdinalIgnoreCase));
        CompletedSoupBowlPrefab = soup?.Prefab;
        if (CompletedSoupBowlPrefab == null)
            Debug.LogWarning("[Hot Pot House Order Cleanup] Soups Plus soup bowl was unavailable; completed hot pots will keep their normal model.");
        else
            Debug.Log("[Hot Pot House Order Cleanup] Soups Plus soup bowl cached for completed shared-pot servings.");
    }

    private static void ReplaceMalaPickupModel(GameData data)
    {
        Item mala = data.Get<Item>().FirstOrDefault(item =>
            item != null && item.Prefab != null &&
            item.name.EndsWith(" - mala_block", StringComparison.OrdinalIgnoreCase));
        Item miso = data.Get<Item>().FirstOrDefault(item =>
            item != null && item.Prefab != null &&
            item.name.EndsWith(" - Miso", StringComparison.OrdinalIgnoreCase));
        if (mala == null || miso == null)
        {
            Debug.LogWarning("[Hot Pot House Order Cleanup] Could not replace the held Mala Base model because Mala Base or Soups Plus Miso was unavailable.");
            return;
        }

        Transform holder = mala.Prefab.transform.parent;
        GameObject replacement = UnityEngine.Object.Instantiate(miso.Prefab);
        replacement.name = mala.Prefab.name;
        replacement.transform.SetParent(holder, false);
        replacement.transform.localPosition = miso.Prefab.transform.localPosition;
        replacement.transform.localRotation = miso.Prefab.transform.localRotation;
        replacement.transform.localScale = miso.Prefab.transform.localScale;
        ApplyRedTint(replacement);
        mala.Prefab = replacement;
        Debug.Log("[Hot Pot House Order Cleanup] Replaced the held Mala Base model with red-tinted Soups Plus Miso.");
    }

    private static GameObject CreateSoupsPlusDisplay(GameData data, string providerName)
    {
        string itemName;
        string childName;
        bool tintRed = false;

        switch (providerName.ToLowerInvariant())
        {
            case "mala_block_provider":
                itemName = "Miso";
                childName = "Miso.001";
                tintRed = true;
                break;
            default:
                return null;
        }

        Item item = data.Get<Item>().FirstOrDefault(candidate =>
            candidate != null && candidate.Prefab != null &&
            candidate.name.EndsWith(itemName, StringComparison.OrdinalIgnoreCase));
        if (item == null)
        {
            Debug.LogWarning($"[Hot Pot House Order Cleanup] Soups Plus item '{itemName}' was unavailable; keeping the Hot Pot House display.");
            return null;
        }

        Transform source = FindDescendant(item.Prefab.transform, childName);
        if (source == null)
        {
            Debug.LogWarning($"[Hot Pot House Order Cleanup] Soups Plus model '{childName}' was unavailable in {item.name}; keeping the Hot Pot House display.");
            return null;
        }

        GameObject result = UnityEngine.Object.Instantiate(source.gameObject);
        result.name = $"Soups Plus {childName}";
        if (tintRed)
            ApplyRedTint(result);
        return result;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            return root;
        foreach (Transform child in root)
        {
            Transform match = FindDescendant(child, name);
            if (match != null)
                return match;
        }
        return null;
    }

    private static void ApplyRedTint(GameObject model)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.materials;
            foreach (Material material in materials)
            {
                Color original = material.color;
                material.color = new Color(
                    Mathf.Max(original.r, 0.72f),
                    original.g * 0.28f,
                    original.b * 0.22f,
                    original.a);
            }
            renderer.materials = materials;
        }
    }

    private static void FitDisplayOnCounter(GameObject model, float maxWidth, float maxHeight, float surfaceY)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        float factor = Mathf.Min(
            horizontal > 0.0001f ? maxWidth / horizontal : 1f,
            bounds.size.y > 0.0001f ? maxHeight / bounds.size.y : 1f);
        model.transform.localScale *= factor;

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        model.transform.localPosition += new Vector3(-bounds.center.x, surfaceY - bounds.min.y, -bounds.center.z);
    }

    private static void AlignDisplayOnSurface(GameObject model, float surfaceY)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        model.transform.localPosition += new Vector3(-bounds.center.x, surfaceY - bounds.min.y, -bounds.center.z);
    }

    private static void HideVisualRenderers(GameObject appliance)
    {
        foreach (Renderer renderer in appliance.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
    }

    private static void ConfigureNumericSideDishRack(GameObject appliance)
    {
        LimitedItemSourceView sourceView = appliance.GetComponentInChildren<LimitedItemSourceView>(true);
        if (sourceView == null)
        {
            Debug.LogWarning("[Hot Pot House Enhanced] Side-dish rack has no LimitedItemSourceView; visible dishes could not be replaced with a number.");
            return;
        }

        List<GameObject> displayedDishes = AccessTools.Field(typeof(LimitedItemSourceView), "Items")
            ?.GetValue(sourceView) as List<GameObject>;
        if (displayedDishes != null)
            foreach (GameObject displayedDish in displayedDishes)
                if (displayedDish != null)
                    foreach (Renderer renderer in displayedDish.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = false;

        GameObject numberRoot = new GameObject("Side Dish Stock Number");
        numberRoot.transform.SetParent(appliance.transform, false);
        numberRoot.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        SideDishCountDisplay display = numberRoot.AddComponent<SideDishCountDisplay>();
        display.CreateLabels();
        appliance.AddComponent<SideDishRackMarker>().Display = display;
    }

    private static void FitModelToApplianceHeight(GameObject model, GameObject reference, float heightMultiplier)
    {
        Renderer[] modelRenderers = model.GetComponentsInChildren<Renderer>(true);
        Renderer[] referenceRenderers = reference.GetComponentsInChildren<Renderer>(true);
        if (modelRenderers.Length == 0 || referenceRenderers.Length == 0)
            return;

        Bounds modelBounds = modelRenderers[0].bounds;
        for (int index = 1; index < modelRenderers.Length; index++)
            modelBounds.Encapsulate(modelRenderers[index].bounds);
        Bounds referenceBounds = referenceRenderers[0].bounds;
        for (int index = 1; index < referenceRenderers.Length; index++)
            referenceBounds.Encapsulate(referenceRenderers[index].bounds);

        if (modelBounds.size.y > 0.0001f)
            model.transform.localScale *= referenceBounds.size.y * heightMultiplier / modelBounds.size.y;

        modelBounds = modelRenderers[0].bounds;
        for (int index = 1; index < modelRenderers.Length; index++)
            modelBounds.Encapsulate(modelRenderers[index].bounds);
        Vector3 localBottomCentre = reference.transform.InverseTransformPoint(
            new Vector3(modelBounds.center.x, modelBounds.min.y, modelBounds.center.z));
        model.transform.localPosition -= new Vector3(localBottomCentre.x, localBottomCentre.y, localBottomCentre.z);
    }

    private static float GetSurfaceHeight(GameObject appliance, float fallback)
    {
        foreach (Transform transform in appliance.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(transform.name, "HoldPoint", StringComparison.Ordinal))
                return appliance.transform.InverseTransformPoint(transform.position).y;
        }
        return fallback;
    }

    private static void RemoveVanillaIngredientModels(GameObject prefab)
    {
        ItemSourceView itemSourceView = prefab.GetComponentInChildren<ItemSourceView>(true);
        MeshRenderer indicator = itemSourceView == null
            ? null
            : AccessTools.Field(typeof(ItemSourceView), "Renderer")?.GetValue(itemSourceView) as MeshRenderer;

        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer == indicator)
                continue;

            string description = renderer.name;
            foreach (Material material in renderer.sharedMaterials)
                description += " " + (material == null ? string.Empty : material.name);

            string value = description.ToLowerInvariant();
            if (value.Contains("meat") || value.Contains("onion") ||
                value.Contains("ingredient") || value.Contains("content") ||
                value.Contains("food") || value.Contains("item display"))
            {
                Debug.Log($"[Hot Pot House Order Cleanup] Hid vanilla ingredient renderer '{description}' in {prefab.name}.");
                renderer.gameObject.SetActive(false);
            }
        }
    }
}

[HarmonyPatch(typeof(LimitedItemSourceLightsView), "UpdateData")]
internal static class SideDishDishwasherLightsPatch
{
    private static void Prefix(ref LimitedItemSourceLightsView.ViewData __0)
    {
        if (UseVanillaProviderPrefabs.DirtySideDishId != 0 &&
            __0.DisplayedType == UseVanillaProviderPrefabs.DirtySideDishId)
        {
            // Vanilla dishwasher LEDs have a colour lookup for PlateDirty only.
            // Reuse that colour while preserving the real Hot Pot House amount.
            __0.DisplayedType = ItemReferences.PlateDirty;
        }
    }
}

internal sealed class SideDishCountDisplay : MonoBehaviour
{
    private TextMesh label;
    private TextMesh shadow;

    internal void CreateLabels()
    {
        shadow = CreateText("Shadow", Color.black, new Vector3(0.018f, -0.018f, 0.01f));
        label = CreateText("Number", Color.white, Vector3.zero);
        SetAmount(8);
    }

    internal void SetAmount(int amount)
    {
        string value = Mathf.Max(0, amount).ToString();
        if (label != null)
            label.text = value;
        if (shadow != null)
            shadow.text = value;
    }

    private TextMesh CreateText(string objectName, Color color, Vector3 localPosition)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = localPosition;
        TextMesh text = child.AddComponent<TextMesh>();
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 80;
        text.characterSize = 0.0125f;
        text.color = color;
        return text;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera != null)
            transform.rotation = camera.transform.rotation;
    }
}

internal sealed class SideDishRackMarker : MonoBehaviour
{
    internal SideDishCountDisplay Display;
}

[HarmonyPatch(typeof(LimitedItemSourceView), "UpdateData")]
internal static class NumericSideDishStockPatch
{
    private static void Postfix(LimitedItemSourceView __instance, LimitedItemSourceView.ViewData __0)
    {
        SideDishRackMarker marker = __instance.GetComponentInParent<SideDishRackMarker>();
        if (marker?.Display != null)
        {
            List<GameObject> displayedDishes = AccessTools.Field(typeof(LimitedItemSourceView), "Items")
                ?.GetValue(__instance) as List<GameObject>;
            if (displayedDishes != null)
                foreach (GameObject displayedDish in displayedDishes)
                    if (displayedDish != null)
                        displayedDish.SetActive(false);
            marker.Display.SetAmount(__0.Amount);
        }
    }
}

[HarmonyPatch(typeof(ItemCollectionView), "UpdateData")]
internal static class PrepareCompletedSharedPotViewsPatch
{
    private static void Prefix(ref ItemCollectionView.ViewData view_data)
    {
        List<ItemCollectionView.ItemData> items = view_data.Items;
        if (items == null || items.Count == 0)
            return;
        for (int index = 0; index < items.Count; index++)
        {
            ItemCollectionView.ItemData item = items[index];
            if (!item.IsComplete || !SharedPotIds.IsSharedFamilyPot(item.ItemID))
                continue;
            item.IsSatisfiedBySharer = false;
            items[index] = item;
        }
    }
}

internal static class SharedPotIds
{
    private static readonly HashSet<string> SharedFamilyPotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hotpothouse - family_hot_pot",
        "hotpothouse - family_mala_hot_pot",
        "hotpothouse - family_mushroom_hot_pot",
        "hotpothouse - family_bone_hot_pot"
    };
    private static HashSet<int> SharedFamilyPotIds;
    internal static bool IsSharedFamilyPot(int itemId)
    {
        if (SharedFamilyPotIds == null || SharedFamilyPotIds.Count == 0)
        {
            SharedFamilyPotIds = new HashSet<int>();
            foreach (ItemGroup item in GameData.Main.Get<ItemGroup>())
                if (item != null && SharedFamilyPotNames.Contains(item.name))
                    SharedFamilyPotIds.Add(item.ID);
            Debug.Log($"[Hot Pot House Enhanced] Registered {SharedFamilyPotIds.Count} shared family-pot IDs.");
        }

        return SharedFamilyPotIds.Contains(itemId);
    }
}

[HarmonyPatch(typeof(ItemCollectionView), "UpdateDrawnItem")]
internal static class ShowCompletedSharedPotsAsSoupBowlsPatch
{
    private static readonly FieldInfo DrawnItemsField = AccessTools.Field(typeof(ItemCollectionView), "DrawnItems");

    private static void Postfix(
        ItemCollectionView __instance,
        int index,
        ItemCollectionView.ItemData item_info)
    {
        ReplaceCompletedItem(__instance, index, item_info);
    }

    internal static void ReplaceCompletedItem(
        ItemCollectionView instance,
        int index,
        ItemCollectionView.ItemData itemInfo)
    {
        GameObject template = UseVanillaProviderPrefabs.CompletedSoupBowlPrefab;
        if (!itemInfo.IsComplete ||
            !SharedPotIds.IsSharedFamilyPot(itemInfo.ItemID) ||
            template == null || DrawnItemsField == null)
            return;

        ItemCollectionView.DrawnItem[] drawnItems =
            DrawnItemsField.GetValue(instance) as ItemCollectionView.DrawnItem[];
        if (drawnItems == null || index < 0 || index >= drawnItems.Length)
            return;

        ItemCollectionView.DrawnItem drawn = drawnItems[index];
        if (drawn.Item == null)
            return;

        if (string.Equals(drawn.Item.name, "Hot Pot Guest Soup Bowl", StringComparison.Ordinal))
            return;

        Transform parent = drawn.Item.transform.parent;
        UnityEngine.Object.Destroy(drawn.Item);
        GameObject bowl = UnityEngine.Object.Instantiate(template, parent, false);
        bowl.name = "Hot Pot Guest Soup Bowl";
        bowl.transform.localPosition = Vector3.zero;
        bowl.transform.localRotation = Quaternion.identity;
        bowl.SetActive(true);
        FillBowlFromServedPot(bowl, itemInfo);

        foreach (Renderer renderer in bowl.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.materials)
                material.SetInt("_IsAlwaysInside", 1);
        }

        drawn.Item = bowl;
        drawnItems[index] = drawn;
        CreateBowlsForOtherGuests(instance, itemInfo, template);
    }

    private static void CreateBowlsForOtherGuests(
        ItemCollectionView view,
        ItemCollectionView.ItemData itemInfo,
        GameObject template)
    {
        string holderName = $"Hot Pot Guest Bowls {indexFor(itemInfo)}";
        Transform previous = view.transform.Find(holderName);
        if (previous != null)
        {
            // Destroy is deferred until the end of the frame. Disable the old
            // holder immediately so a replaced order can never show two layers.
            previous.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(previous.gameObject);
        }

        GameObject holder = new GameObject(holderName);
        holder.transform.SetParent(view.transform, false);
        Vector3 table = itemInfo.TablePosition;
        int created = 0;
        var occupied = new List<Vector3> { itemInfo.SeatPosition };

        foreach (CustomerView customer in UnityEngine.Object.FindObjectsOfType<CustomerView>())
        {
            if (customer == null || !customer.gameObject.activeInHierarchy)
                continue;

            Vector3 seat = customer.transform.position;
            seat.y = 0f;
            Vector3 horizontal = seat - new Vector3(table.x, 0f, table.z);
            float distance = horizontal.magnitude;
            if (distance < 0.45f || distance > 1.8f ||
                occupied.Any(position => (position - seat).sqrMagnitude < 0.04f))
                continue;

            occupied.Add(seat);
            GameObject extra = UnityEngine.Object.Instantiate(template, holder.transform, false);
            extra.name = "Hot Pot Guest Soup Bowl Extra";
            extra.transform.position = seat * 0.4f + table * 0.6f;
            extra.transform.position = new Vector3(extra.transform.position.x, 0.5f, extra.transform.position.z);
            extra.transform.rotation = Quaternion.LookRotation(horizontal, Vector3.up);
            extra.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            extra.SetActive(true);
            FillBowlFromServedPot(extra, itemInfo);
            foreach (Renderer renderer in extra.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.materials)
                    material.SetInt("_IsAlwaysInside", 1);
            created++;
        }

        Debug.Log($"[Hot Pot House Order Cleanup] Added {created} extra soup bowls around shared-pot table at {table}.");
    }

    private static string indexFor(ItemCollectionView.ItemData item)
    {
        return $"{Mathf.RoundToInt(item.TablePosition.x * 10f)}_{Mathf.RoundToInt(item.TablePosition.z * 10f)}";
    }

    private static void FillBowlFromServedPot(GameObject bowl, ItemCollectionView.ItemData itemInfo)
    {
        Transform bowlMesh = FindChild(bowl.transform, "Bowl");
        if (bowlMesh == null)
            return;

        // The Soups Plus prefab is a complete chicken-noodle soup. Keep only
        // its bowl geometry; the served Hot Pot supplies the broth and add-ins.
        foreach (Renderer renderer in bowl.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.transform.IsChildOf(bowlMesh) && renderer.transform != bowlMesh)
                renderer.gameObject.SetActive(false);
        }

        GameObject source = GameData.Main.GetPrefab(itemInfo.ItemID);
        if (source == null)
            return;
        GameObject contents = UnityEngine.Object.Instantiate(source, bowl.transform, false);
        contents.name = "Served Hot Pot Contents";
        contents.transform.localPosition = Vector3.zero;
        contents.transform.localRotation = Quaternion.identity;
        contents.GetComponent<IItemSpecificView>()?.PerformUpdate(itemInfo.ItemID, itemInfo.Components, false);

        Transform potBase = contents.transform.Find("base");
        if (potBase != null)
            potBase.gameObject.SetActive(false);
        foreach (Component component in contents.GetComponentsInChildren<Component>(true))
            if (component != null && string.Equals(component.GetType().FullName, "HotPotHouse.NameTag", StringComparison.Ordinal))
                component.gameObject.SetActive(false);
        Renderer[] bowlRenderers = bowlMesh.GetComponentsInChildren<Renderer>(true);
        Renderer[] contentRenderers = contents.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.gameObject.activeInHierarchy)
            .ToArray();
        if (bowlRenderers.Length == 0 || contentRenderers.Length == 0)
            return;

        Bounds bowlBounds = CombinedBounds(bowlRenderers);
        Bounds contentBounds = CombinedBounds(contentRenderers);
        float bowlWidth = Mathf.Max(bowlBounds.size.x, bowlBounds.size.z);
        float contentWidth = Mathf.Max(contentBounds.size.x, contentBounds.size.z);
        if (contentWidth > 0.0001f)
            contents.transform.localScale *= bowlWidth * 0.72f / contentWidth;

        contentBounds = CombinedBounds(contentRenderers);
        Vector3 target = new Vector3(
            bowlBounds.center.x,
            bowlBounds.center.y + bowlBounds.extents.y * 0.35f,
            bowlBounds.center.z);
        contents.transform.position += target - new Vector3(
            contentBounds.center.x,
            contentBounds.min.y,
            contentBounds.center.z);
    }

    private static Bounds CombinedBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            return root;
        foreach (Transform child in root)
        {
            Transform match = FindChild(child, name);
            if (match != null)
                return match;
        }
        return null;
    }
}

[HarmonyPatch(typeof(ItemCollectionView), "UpdateData")]
internal static class ReplaceSharedPotWhenOrderBecomesCompletePatch
{
    private static void Postfix(
        ItemCollectionView __instance,
        ItemCollectionView.ViewData view_data)
    {
        if (view_data.Items == null)
            return;
        for (int index = 0; index < view_data.Items.Count; index++)
            ShowCompletedSharedPotsAsSoupBowlsPatch.ReplaceCompletedItem(
                __instance,
                index,
                view_data.Items[index]);
    }
}

