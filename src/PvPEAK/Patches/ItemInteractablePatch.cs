using BepInEx.Logging;
using HarmonyLib;
using PvPEAK.Model;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Text;

namespace PvPEAK.Patches;
public class ItemInteractablePatch
{
    [HarmonyPatch(typeof(Item), nameof(Item.IsInteractible))]
    [HarmonyPrefix]
    static bool ItemIsInteractiblePrefix(ref bool __result, Item __instance, Character interactor)
    {
        //UnlimitedLogger.GetInstance().DebugMessage(LogLevel.Info, DebugLogType.CampfireLogic, $"PvPEAK Enabled: {Plugin.ConfigurationHandler.EnablePvPEAK}");
        if (!Plugin.ItsPvPEAK) { return true; }
        if (!Plugin.IsAfterAwake)
        {
            //UnlimitedLogger.GetInstance().DebugMessage(LogLevel.Info, DebugLogType.CampfireLogic, $"Not after awake");
            return true;
        }
        if (!Plugin.itemTracker.ItemIsOwned(__instance) || !Utility.ItemIsInSafeZone(__instance))
        {
            //UnlimitedLogger.GetInstance().DebugMessage(LogLevel.Info, DebugLogType.CampfireLogic, $"Item is not owned or is not in safe zone. Owned: {Plugin.itemTracker.ItemIsOwned(__instance)}, Safe Zone: {Utility.ItemIsInSafeZone(__instance)}");
            if (__instance.itemState != ItemState.Ground)
            {
                __result = false;
                return false;
            }
            return true;
        }
        Team team = Plugin.teamTracker.GetPlayerTeam(interactor.player);
        if (!Plugin.itemTracker.ItemCanBeGrabbedByTeam(__instance, team))
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(BackpackOnBackVisuals), nameof(BackpackOnBackVisuals.IsInteractible))]
    [HarmonyPrefix]
    static bool BackpackOnBackVisualsIsIntreactablePrefix(ref bool __result, BackpackOnBackVisuals __instance, Character interactor)
    {
        if (!Plugin.ItsPvPEAK) { return true; }
        if (!Plugin.IsAfterAwake)
        {
            //UnlimitedLogger.GetInstance().DebugMessage(LogLevel.Info, DebugLogType.CampfireLogic, $"Not after awake");
            return true;
        }
        // PEAK Versus: enemies may open your backpack from behind (vanilla rule); VersusPatches makes it noisy.
        return true;
    }

    //[HarmonyPatch(typeof(BackpackOnBackVisuals), nameof(BackpackOnBackVisuals.HoverEnter))]
    //[HarmonyPrefix]
    //static bool BackpackOnBackVisualsHoverEnterPrefix(BackpackOnBackVisuals __instance)
    //{
    //    if (!Plugin.ConfigurationHandler.EnablePvPEAK) { return true; }
    //    if (!Plugin.IsAfterAwake)
    //    {
    //        UnlimitedLogger.GetInstance().DebugMessage(LogLevel.Info, DebugLogType.CampfireLogic, $"Not after awake");
    //        return true;
    //    }
    //    if (Plugin.teamTracker.GetPlayerTeam(__instance.character.player) != Plugin.teamTracker.GetPlayerTeam(interactor.player))
    //    {
    //        UnlimitedLogger.GetInstance().DebugMessage(LogLevel.Info, DebugLogType.CampfireLogic, $"Backpack is not on your team");
    //        __result = false;
    //        return false;
    //    }
    //    return true;
    //}

}
