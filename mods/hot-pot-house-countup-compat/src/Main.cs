using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenLib;
using KitchenLib.Event;
using KitchenMods;
using UnityEngine;

namespace HotPotHouseCountUpCompat;

public sealed class Main : BaseMod
{
    public const string ModId = "cannizzo.hotpothouse.countup";

    public Main() : base(
        ModId,
        "Hot Pot House - CountUp! Compatibility",
        "Cannizzo",
        "1.0.0",
        ">=1.5.0",
        Assembly.GetExecutingAssembly())
    {
    }

    protected override void OnPostActivate(Mod mod)
    {
        Events.BuildGameDataEvent += (_, args) => EnableCountUpForSideDishes(args.gamedata);
        Debug.Log("[Hot Pot House - CountUp! Compatibility] v1.0.0 loaded.");
    }

    private static void EnableCountUpForSideDishes(GameData data)
    {
        Item sideDish = data?.Get<Item>().FirstOrDefault(item =>
            item != null && item.Prefab != null &&
            item.name.EndsWith(" - side_dish", StringComparison.OrdinalIgnoreCase));
        if (sideDish == null)
        {
            Debug.LogWarning("[Hot Pot House - CountUp! Compatibility] Hot Pot House side dish was unavailable.");
            return;
        }

        // CountUp suppresses provider counts whenever the provided item has a
        // direct child named exactly "Colour Blind". Hot Pot House uses that
        // child for the redundant floating "Dish" label.
        Transform redundantLabel = sideDish.Prefab.transform.Find("Colour Blind");
        if (redundantLabel != null)
        {
            redundantLabel.name = "Colour Blind (Hidden by CountUp Compat)";
            redundantLabel.gameObject.SetActive(false);
        }

        Debug.Log("[Hot Pot House - CountUp! Compatibility] Enabled CountUp counting for the side-dish provider.");
    }
}

[HarmonyPatch(typeof(LimitedItemSourceView), "UpdateData")]
internal static class PreferCountUpSideDishNumberPatch
{
    private static void Postfix(LimitedItemSourceView __instance)
    {
        // Enhanced 2.4.10+ has a fallback number for players without CountUp.
        // Hide only that fallback when this compatibility mod is installed.
        Transform current = __instance.transform;
        while (current != null)
        {
            Transform fallback = current.Find("Side Dish Stock Number");
            if (fallback != null)
            {
                fallback.gameObject.SetActive(false);
                return;
            }
            current = current.parent;
        }
    }
}
