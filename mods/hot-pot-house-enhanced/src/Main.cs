using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenLib;
using KitchenLib.Customs;
using KitchenLib.Event;
using KitchenLib.Utils;
using KitchenLib.References;
using KitchenMods;
using Unity.Entities;
using UnityEngine;

namespace HotPotHouseEnhanced;

internal sealed class AutomaticPotWasher : CustomAppliance
{
    private static GameObject prefabHolder;
    private GameObject prefab;
    private Appliance SoakingSink => (Appliance)GDOUtils.GetExistingGDO(ApplianceReferences.SinkSoak);

    public override string UniqueNameID => "automatic_pot_washer";
    public override bool IsPurchasable => false;
    public override PriceTier PriceTier => SoakingSink.PriceTier;
    public override RarityTier RarityTier => SoakingSink.RarityTier;
    public override ShoppingTags ShoppingTags => SoakingSink.ShoppingTags;
    public override OccupancyLayer Layer => SoakingSink.Layer;
    public override DecorationType ThemeRequired => SoakingSink.ThemeRequired;
    public override bool IsNonInteractive => SoakingSink.IsNonInteractive;
    public override List<IApplianceProperty> Properties => SoakingSink.Properties.ToList();
    public override List<Appliance.ApplianceProcesses> Processes => SoakingSink.Processes.ToList();
    public override List<Appliance> Upgrades => new List<Appliance>();
    public override List<(Locale, ApplianceInfo)> InfoList => new List<(Locale, ApplianceInfo)>
    {
        ((Locale)1, LocalisationUtils.CreateApplianceInfo(
            "Automatic Pot Washer",
            "Automatically soaks dirty hot pots so grabbers can load and unload them.",
            new List<Appliance.Section>(),
            new List<string>()))
    };

    public override GameObject Prefab
    {
        get
        {
            if (prefab != null)
                return prefab;
            if (prefabHolder == null)
            {
                prefabHolder = new GameObject("Hot Pot House Enhanced Prefab Holder");
                UnityEngine.Object.DontDestroyOnLoad(prefabHolder);
                prefabHolder.SetActive(false);
            }
            prefab = UnityEngine.Object.Instantiate(SoakingSink.Prefab);
            prefab.name = UniqueNameID;
            prefab.transform.SetParent(prefabHolder.transform, false);
            prefab.transform.localPosition = Vector3.zero;
            prefab.transform.localRotation = Quaternion.identity;
            return prefab;
        }
    }
}

public sealed class Main : BaseMod
{
    public const string ModId = "cannizzo.hotpothouse.enhanced";

    public Main()
        : base(
            ModId,
            "Hot Pot House Enhanced",
            "Cannizzo",
            "2.5.0",
            ">=1.5.0",
            Assembly.GetExecutingAssembly())
    {
    }

    protected override void OnPostActivate(Mod mod)
    {
        AddGameDataObject<AutomaticPotWasher>();
        Events.BuildGameDataEvent += (_, args) => UseVanillaProviderPrefabs.Apply(args.gamedata);
        Debug.Log("[Hot Pot House Enhanced] v2.5.0 loaded; Automatic Pot Washer upgrade registered.");
    }
}

internal static class UseVanillaProviderPrefabs
{
    internal static GameObject CompletedSoupBowlPrefab { get; private set; }
    internal static int CleanSideDishId { get; private set; }
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
        Appliance automaticPotWasher = data.Get<Appliance>().FirstOrDefault(appliance =>
            appliance != null && appliance.name.EndsWith(" - automatic_pot_washer", StringComparison.OrdinalIgnoreCase));
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

        ConfigureAutomaticPotWasher(hotPotWasher, automaticPotWasher, hotPotRackModel);

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
                // CountUp compatibility owns the side-dish stock number. Do
                // not add Enhanced's old TextMesh counter to the plate stack.
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

    private static void ConfigureAutomaticPotWasher(
        Appliance manualWasher,
        Appliance automaticWasher,
        Transform rackModel)
    {
        if (manualWasher == null || automaticWasher?.Prefab == null)
        {
            Debug.LogWarning("[Hot Pot House Enhanced] Automatic Pot Washer upgrade could not be linked to the HPH Pot Washer.");
            return;
        }

        if (!manualWasher.Upgrades.Contains(automaticWasher))
            manualWasher.Upgrades.Add(automaticWasher);

        ApplyAutomaticWasherSpeed(automaticWasher);

        if (rackModel == null || automaticWasher.Prefab.transform.Find("HPH Automatic Pot Washer Visual") != null)
            return;

        // Keep the automatic soaking-sink base and its correctly controlled
        // visual states. Only add the HPH rack and motor; cloning the entire
        // manual prefab would leave all of its animation states visible.
        GameObject visual = new GameObject("HPH Automatic Pot Washer Visual");
        visual.transform.SetParent(automaticWasher.Prefab.transform, false);

        GameObject rack = UnityEngine.Object.Instantiate(rackModel.gameObject);
        rack.name = "HPH Automatic Pot Rack";
        rack.transform.SetParent(visual.transform, false);
        rack.transform.localPosition = rackModel.localPosition;
        rack.transform.localRotation = rackModel.localRotation;
        rack.transform.localScale = rackModel.localScale;

        AddAutomaticWasherMotor(visual.transform, rack, manualWasher.Prefab);
        Debug.Log("[Hot Pot House Enhanced] Automatic Pot Washer now matches the manual washer height and differs only by its motor while idle.");
    }

    private static void ApplyAutomaticWasherSpeed(Appliance automaticWasher)
    {
        Appliance originalSoakingSink = GDOUtils.GetExistingGDO(ApplianceReferences.SinkSoak) as Appliance;
        Appliance.ApplianceProcesses originalClean = originalSoakingSink == null
            ? default
            : originalSoakingSink.Processes.FirstOrDefault(process =>
                process.Process != null && process.Process.ID == ProcessReferences.Clean);
        for (int index = 0; index < automaticWasher.Processes.Count; index++)
        {
            Appliance.ApplianceProcesses process = automaticWasher.Processes[index];
            if (process.Process == null || process.Process.ID != ProcessReferences.Clean)
                continue;
            float baseSpeed = originalClean.Process == null ? process.Speed : originalClean.Speed;
            process.Speed = baseSpeed * 1.15f;
            automaticWasher.Processes[index] = process;
            Debug.Log($"[Hot Pot House Enhanced] Automatic Pot Washer clean speed set to {process.Speed:0.###} (15% above its original soaking-sink speed).");
        }
    }

    private static void AddAutomaticWasherMotor(Transform parent, GameObject washerModel, GameObject manualWasher)
    {
        Renderer templateRenderer = washerModel.GetComponentsInChildren<Renderer>(true).FirstOrDefault();
        Material template = templateRenderer?.sharedMaterial;
        Material housing = CreateTintedMaterial(template, new Color(0.86f, 0.82f, 0.72f, 1f));
        Material panel = CreateTintedMaterial(template, new Color(0.16f, 0.27f, 0.42f, 1f));

        Renderer[] manualRenderers = manualWasher.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = manualRenderers.Length == 0
            ? new Bounds(new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f))
            : GetCombinedBounds(manualRenderers);
        Vector3 localMin = manualWasher.transform.InverseTransformPoint(bounds.min);
        Vector3 localMax = manualWasher.transform.InverseTransformPoint(bounds.max);
        float width = Mathf.Min(0.22f, (localMax.x - localMin.x) * 0.28f);
        float height = Mathf.Min(0.28f, (localMax.y - localMin.y) * 0.45f);
        float depth = Mathf.Min(0.18f, (localMax.z - localMin.z) * 0.25f);
        Vector3 motorPosition = new Vector3(
            localMax.x - width * 0.58f,
            localMax.y - height * 0.55f,
            localMax.z - depth * 0.58f);

        CreateConceptCube(parent, "Motor Housing", motorPosition, new Vector3(width, height, depth), housing);
        CreateConceptCube(parent, "Motor Vent", motorPosition + new Vector3(0f, 0f, depth * 0.53f), new Vector3(width * 0.68f, height * 0.58f, 0.018f), panel);
    }

    private static GameObject CreateConceptCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = scale;
        Component collider = part.GetComponent("BoxCollider");
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        return part;
    }

    private static Material CreateTintedMaterial(Material template, Color colour)
    {
        if (template == null)
            return null;
        Material material = new Material(template);
        material.name = "HPH Automatic Pot Washer Material";
        if (material.HasProperty("_Color"))
            material.color = colour;
        return material;
    }

    private static void FitRackToOneTile(GameObject rack, GameObject heightReference, GameObject tileReference)
    {
        Renderer[] rackRenderers = rack.GetComponentsInChildren<Renderer>(true);
        Renderer[] heightRenderers = heightReference == null
            ? Array.Empty<Renderer>()
            : heightReference.GetComponentsInChildren<Renderer>(true);
        Renderer[] tileRenderers = tileReference.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => !renderer.transform.IsChildOf(rack.transform))
            .ToArray();
        if (rackRenderers.Length == 0 || tileRenderers.Length == 0)
            return;

        Bounds rackBounds = GetCombinedBounds(rackRenderers);
        Bounds tileBounds = GetCombinedBounds(tileRenderers);
        float allowedWidth = Mathf.Min(0.9f, tileBounds.size.x * 0.92f);
        float allowedDepth = Mathf.Min(0.9f, tileBounds.size.z * 0.92f);
        float allowedHeight = heightRenderers.Length == 0
            ? tileBounds.size.y
            : GetCombinedBounds(heightRenderers).size.y;
        float scale = Mathf.Min(
            rackBounds.size.x <= 0.0001f ? 1f : allowedWidth / rackBounds.size.x,
            rackBounds.size.z <= 0.0001f ? 1f : allowedDepth / rackBounds.size.z,
            rackBounds.size.y <= 0.0001f ? 1f : allowedHeight / rackBounds.size.y);
        rack.transform.localScale *= Mathf.Min(1f, scale);
    }

    private static Bounds GetCombinedBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return bounds;
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
        CleanSideDishId = cleanSideDish.ID;
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
        if (UseVanillaProviderPrefabs.CleanSideDishId != 0 &&
            __0.DisplayedType == UseVanillaProviderPrefabs.CleanSideDishId)
        {
            // Reuse vanilla clean-plate yellow LEDs.
            __0.DisplayedType = ItemReferences.Plate;
        }
        else if (UseVanillaProviderPrefabs.DirtySideDishId != 0 &&
                 __0.DisplayedType == UseVanillaProviderPrefabs.DirtySideDishId)
        {
            // Vanilla dishwasher LEDs have a colour lookup for PlateDirty only.
            // Reuse that colour while preserving the real Hot Pot House amount.
            __0.DisplayedType = ItemReferences.PlateDirty;
        }
    }
}

[HarmonyPatch]
internal static class ConsolidateSideDishLabelsPatch
{
    private const string ConsolidatedName = "HPH Enhanced Side Dish Label";

    private static MethodBase TargetMethod()
    {
        Type markerType = AccessTools.TypeByName("HotPotHouse.ItemNameTag");
        return markerType == null ? null : AccessTools.Method(markerType, "Start");
    }

    private static bool Prefix(Component __instance)
    {
        if (__instance == null)
            return true;
        if (IsColorblindPlusLoaded())
            return false;
        string content = AccessTools.Field(__instance.GetType(), "Content")?.GetValue(__instance) as string ?? string.Empty;
        if (!string.Equals(content, "Side Dish", StringComparison.OrdinalIgnoreCase) &&
            !content.StartsWith("Dirty", StringComparison.OrdinalIgnoreCase))
            return true;
        return true;
    }

    private static void Postfix(Component __instance)
    {
        if (__instance == null || IsColorblindPlusLoaded())
            return;
        string content = AccessTools.Field(__instance.GetType(), "Content")?.GetValue(__instance) as string ?? string.Empty;
        if (!string.Equals(content, "Side Dish", StringComparison.OrdinalIgnoreCase) &&
            !content.StartsWith("Dirty", StringComparison.OrdinalIgnoreCase))
            return;

        ApplianceView appliance = __instance.GetComponentInParent<ApplianceView>();
        Type nameTagType = AccessTools.TypeByName("HotPotHouse.NameTag");
        if (appliance == null || nameTagType == null)
            return;

        // ItemNameTag.Start creates exactly one NameTag below this item. Work
        // only on that label and a direct appliance child, avoiding the old
        // O(n²) hierarchy scan whenever dishes are picked up or placed.
        Component newest = __instance.GetComponentsInChildren(nameTagType, true)
            .OfType<Component>()
            .FirstOrDefault(label => label != null && IsSideDishText(label));
        if (newest == null)
            return;

        Transform existing = appliance.transform.Find(ConsolidatedName);
        Component consolidated = existing == null ? null : existing.GetComponent(nameTagType);

        if (consolidated == null)
        {
            consolidated = newest;
            consolidated.gameObject.name = ConsolidatedName;
            consolidated.transform.SetParent(appliance.transform, true);
        }
        else if (newest != null)
        {
            CopyText(newest, consolidated);
            consolidated.transform.position = newest.transform.position;
        }

        consolidated.gameObject.SetActive(true);
        if (!ReferenceEquals(newest, consolidated))
            newest.gameObject.SetActive(false);
    }

    private static bool IsSideDishText(Component label)
    {
        string text = AccessTools.Property(label.GetType(), "text")?.GetValue(label, null) as string;
        if (string.IsNullOrEmpty(text))
        {
            Component tmp = label.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType().Namespace == "TMPro");
            text = tmp == null ? string.Empty :
                AccessTools.Property(tmp.GetType(), "text")?.GetValue(tmp, null) as string ?? string.Empty;
        }
        return text.IndexOf("Side Dish", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.StartsWith("Dirty", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyText(Component source, Component destination)
    {
        Component sourceText = source.GetComponents<Component>()
            .FirstOrDefault(component => component != null && component.GetType().Namespace == "TMPro");
        Component destinationText = destination.GetComponents<Component>()
            .FirstOrDefault(component => component != null && component.GetType().Namespace == "TMPro");
        if (sourceText == null || destinationText == null)
            return;
        string text = AccessTools.Property(sourceText.GetType(), "text")?.GetValue(sourceText, null) as string;
        AccessTools.Property(destinationText.GetType(), "text")?.SetValue(destinationText, text, null);
    }

    private static readonly bool ColorblindPlusLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
        (assembly.GetName().Name ?? string.Empty).StartsWith("Colorblind", StringComparison.OrdinalIgnoreCase));

    private static bool IsColorblindPlusLoaded()
    {
        return ColorblindPlusLoaded;
    }

    internal static bool ColorblindPlusInstalled => ColorblindPlusLoaded;
}

[HarmonyPatch]
internal static class SuppressHotPotHouseOrderLabelsWithColorblindPlusPatch
{
    private static MethodBase TargetMethod()
    {
        Type nameTagType = AccessTools.TypeByName("HotPotHouse.NameTag");
        return nameTagType == null ? null : AccessTools.Method(nameTagType, "ForOrder");
    }

    private static bool Prefix()
    {
        return !ConsolidateSideDishLabelsPatch.ColorblindPlusInstalled;
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

[HarmonyPatch(typeof(ItemCollectionView), "UpdateData")]
internal static class CollapseDuplicateSharedPotOrdersPatch
{
    private sealed class PotClaim
    {
        public ItemCollectionView Owner;
        public ItemCollectionView.ItemData Order;
    }

    private static readonly List<PotClaim> Claims = new List<PotClaim>();

    private static void Prefix(ItemCollectionView __instance, ref ItemCollectionView.ViewData view_data)
    {
        Claims.RemoveAll(claim => claim.Owner == null || ReferenceEquals(claim.Owner, __instance));
        List<ItemCollectionView.ItemData> items = view_data.Items;
        if (items == null || items.Count == 0)
            return;

        var filtered = new List<ItemCollectionView.ItemData>(items.Count);
        foreach (ItemCollectionView.ItemData candidate in items)
        {
            if (!SharedPotIds.IsSharedFamilyPot(candidate.ItemID) || candidate.IsComplete)
            {
                filtered.Add(candidate);
                continue;
            }

            bool alreadyDrawnForTable = Claims.Exists(claim =>
                claim.Order.ItemID == candidate.ItemID &&
                (claim.Order.TablePosition - candidate.TablePosition).sqrMagnitude < 0.01f &&
                claim.Order.Components.IsEquivalent(candidate.Components, false));
            if (alreadyDrawnForTable)
                continue;

            Claims.Add(new PotClaim { Owner = __instance, Order = candidate });
            filtered.Add(candidate);
        }

        if (filtered.Count != items.Count)
            view_data.Items = filtered;
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
    private sealed class BowlClaim
    {
        public ItemCollectionView Owner;
        public int Index;
    }
    private static readonly Dictionary<string, BowlClaim> BowlClaims = new Dictionary<string, BowlClaim>();

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

        string tableKey = indexFor(itemInfo);
        if (BowlClaims.TryGetValue(tableKey, out BowlClaim claim) &&
            (claim.Owner != instance || claim.Index != index) && ClaimHasActiveBowl(claim))
        {
            // Only one completed shared-order view may create visuals for a
            // table. Other customer/order views would otherwise add another
            // complete set of bowls at the same seats.
            drawn.Item.SetActive(false);
            return;
        }
        BowlClaims[tableKey] = new BowlClaim { Owner = instance, Index = index };

        if (string.Equals(drawn.Item.name, "Hot Pot Guest Soup Bowl", StringComparison.Ordinal))
            return;

        Transform parent = drawn.Item.transform.parent;
        UnityEngine.Object.Destroy(drawn.Item);
        GameObject bowl = UnityEngine.Object.Instantiate(template, parent, false);
        bowl.name = "Hot Pot Guest Soup Bowl";
        bowl.transform.localPosition = Vector3.zero;
        bowl.transform.localRotation = Quaternion.identity;
        bowl.SetActive(true);
        DisableItemLabels(bowl);
        FillBowlFromServedPot(bowl, itemInfo);

        foreach (Renderer renderer in bowl.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.materials)
                material.SetInt("_IsAlwaysInside", 1);
        }

        drawn.Item = bowl;
        drawnItems[index] = drawn;
        CreateBowlsForOtherGuests(instance, itemInfo, bowl);
    }

    private static bool ClaimHasActiveBowl(BowlClaim claim)
    {
        if (claim?.Owner == null || !claim.Owner.gameObject.activeInHierarchy)
            return false;
        ItemCollectionView.DrawnItem[] items = DrawnItemsField.GetValue(claim.Owner) as ItemCollectionView.DrawnItem[];
        if (items == null || claim.Index < 0 || claim.Index >= items.Length)
            return false;
        GameObject item = items[claim.Index].Item;
        return item != null && item.activeInHierarchy &&
               item.name.StartsWith("Hot Pot Guest Soup Bowl", StringComparison.Ordinal);
    }

    private static void CreateBowlsForOtherGuests(
        ItemCollectionView view,
        ItemCollectionView.ItemData itemInfo,
        GameObject preparedBowl)
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
        Vector3 primarySeat = itemInfo.SeatPosition;
        primarySeat.y = 0f;
        var occupied = new List<Vector3> { primarySeat };

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
            // Clone the already-filled primary bowl. Rebuilding the complete
            // pot prefab, item view, bounds and materials for every guest was
            // the main delivery hitch for hot pots and dips.
            GameObject extra = UnityEngine.Object.Instantiate(preparedBowl, holder.transform, false);
            extra.name = "Hot Pot Guest Soup Bowl Extra";
            extra.transform.position = seat * 0.4f + table * 0.6f;
            extra.transform.position = new Vector3(extra.transform.position.x, 0.5f, extra.transform.position.z);
            extra.transform.rotation = Quaternion.LookRotation(horizontal, Vector3.up);
            extra.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            extra.SetActive(true);
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
        DisableItemLabels(contents);
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

    private static void DisableItemLabels(GameObject root)
    {
        if (root == null)
            return;
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;
            string typeName = component.GetType().FullName ?? string.Empty;
            if (string.Equals(typeName, "HotPotHouse.NameTag", StringComparison.Ordinal) ||
                string.Equals(typeName, "HotPotHouse.ItemNameTag", StringComparison.Ordinal) ||
                string.Equals(component.GetType().Namespace, "TMPro", StringComparison.Ordinal))
            {
                if (component is Behaviour behaviour)
                    behaviour.enabled = false;
                component.gameObject.SetActive(false);
            }
            if (component is ItemGroupView itemView)
                itemView.ForceNoColourblind = true;
        }
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

