using System;
using System.Reflection;
using HarmonyLib;
using Kitchen;
using Kitchen.Modules;
using KitchenLib;
using KitchenMods;
using UnityEngine;

namespace KitchenCannizzoRestartDay;

public sealed class Main : BaseMod
{
    public const string ModId = "cannizzo.restart.day";

    public Main() : base(
        ModId,
        "Restart Day - Soft Opening",
        "Cannizzo",
        "1.0.0",
        ">=1.5.0",
        Assembly.GetExecutingAssembly())
    {
    }

    protected override void OnPostActivate(Mod mod)
    {
        Debug.Log("[Restart Day - Soft Opening] Loaded. Hosts can restart an active day from the pause menu.");
    }
}

public sealed class RestartDayController : GameSystemBase, IModSystem
{
    private static RestartDayController instance;

    protected override void Initialise()
    {
        base.Initialise();
        instance = this;
    }

    protected override void OnUpdate()
    {
    }

    internal static bool TryRestart()
    {
        if (instance == null || !instance.TryGetSingleton(out SKitchenStatus status))
        {
            Debug.LogWarning("[Restart Day - Soft Opening] Restart was requested before kitchen status was available.");
            return false;
        }

        instance.Set(new SKitchenStatus
        {
            RemainingLives = status.TotalLives,
            TotalLives = status.TotalLives
        });
        instance.World.Add(new COfferRestartDay { Reason = LossReason.Patience });
        Debug.Log("[Restart Day - Soft Opening] Restart requested and lives restored for the current day.");
        return true;
    }
}

[HarmonyPatch(typeof(MainMenu), "Setup")]
internal static class AddRestartButtonPatch
{
    private static void Prefix(MainMenu __instance)
    {
        if (GameInfo.CurrentScene != SceneType.Kitchen || GameInfo.IsPreparationTime ||
            (int)Session.NetworkedPlayState == 3)
            return;

        MethodInfo addSubmenu = __instance.GetType().GetMethod(
            "AddSubmenuButton",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (addSubmenu == null)
        {
            Debug.LogError("[Restart Day - Soft Opening] Could not add the restart button to the pause menu.");
            return;
        }

        addSubmenu.Invoke(__instance, new object[] { "Restart Day", typeof(RestartDayConfirmationMenu), false });
    }
}

[HarmonyPatch(typeof(PlayerPauseView), "SetupMenus")]
internal static class RegisterRestartMenuPatch
{
    private static void Prefix(PlayerPauseView __instance)
    {
        Type type = __instance.GetType();
        FieldInfo moduleListField = type.GetField("ModuleList", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo addMenu = type.GetMethod("AddMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        ModuleList moduleList = moduleListField?.GetValue(__instance) as ModuleList;
        if (moduleList == null || addMenu == null)
        {
            Debug.LogError("[Restart Day - Soft Opening] Could not register the confirmation menu.");
            return;
        }

        addMenu.Invoke(__instance, new object[]
        {
            typeof(RestartDayConfirmationMenu),
            new RestartDayConfirmationMenu(__instance.ButtonContainer, moduleList)
        });
    }
}

public sealed class RestartDayConfirmationMenu : Menu<MenuAction>
{
    public RestartDayConfirmationMenu(Transform container, ModuleList moduleList) : base(container, moduleList)
    {
    }

    public override void Setup(int _)
    {
        AddLabel("Restart the current day?");
        AddInfo("The restaurant returns to preparation and all progress made during the current day is discarded.");
        AddButton("Restart Day", _ =>
        {
            if (RestartDayController.TryRestart())
                RequestAction(new MenuAction(PauseMenuAction.CloseMenu));
        }, 0, 1f, 0.2f);
        AddButton("Cancel", _ => RequestPreviousMenu(), 0, 1f, 0.2f);
    }
}
