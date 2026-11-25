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
using UnityEngine;

namespace HotPotHouseColorblindPlus;

public sealed class Main : BaseMod
{
    public const string ModId = "cannizzo.hotpothouse.colorblindplus";

    public Main() : base(
        ModId,
        "Hot Pot House - Colorblind+ Compatibility",
        "Cannizzo",
        "1.0.2",
        ">=1.5.0",
        Assembly.GetExecutingAssembly())
    {
    }

    protected override void OnPostActivate(Mod mod)
    {
        Debug.Log("[Hot Pot House - Colorblind+ Compatibility] v1.0.2 loaded.");
    }

    internal static bool IsColorblindPlusLoaded()
    {
        return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            string.Equals(assembly.GetName().Name, "Colorblind", StringComparison.OrdinalIgnoreCase));
    }
}

[HarmonyPatch]
internal static class HideHotPotHouseFloatingLabelsPatch
{
    private static MethodBase TargetMethod()
    {
        Type nameTag = AccessTools.TypeByName("HotPotHouse.NameTag");
        return nameTag == null ? null : AccessTools.Method(nameTag, "Create");
    }

    private static void Postfix(Component __result)
    {
        if (Main.IsColorblindPlusLoaded() && __result != null)
            __result.gameObject.SetActive(false);
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

    private static bool Prefix() => !Main.IsColorblindPlusLoaded();
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
