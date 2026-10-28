using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using static BingBongSpawnTool;
using static Zorro.ControllerSupport.Rumble.RumbleClip;

namespace PvPEAK.Patches;

public class StatuePatch
{

    
    [HarmonyPatch(typeof(RespawnChest), nameof(RespawnChest.Interact_CastFinished))]
    [HarmonyPrefix]
    static bool Interact_CastFinishedPrefix(RespawnChest __instance, Character interactor)
    {
        if (!Plugin.ItsPvPEAK) { return true; }
        //Plugin.touchedStatueTeam = Plugin.teamTracker.GetPlayerTeam(interactor.player);
        //Plugin.StatueList.Add(__instance);
        return true;
    }

    public static bool CheckSkipRespawn()
    {
        if (!Plugin.ItsPvPEAK)
        {
            return Ascents.canReviveDead;
        }
        return false;
    }

    [HarmonyPatch(typeof(RespawnChest), nameof(RespawnChest.SpawnItems))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> SpawnItemsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        // Skip scout respawning by replacing the Ascents.canReviveDead check.
        // The game now returns the spawned items itself, so the old Pop -> Stloc_0 fix is no longer needed.
        CodeMatcher matcher = new CodeMatcher(instructions)
            .MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Ascents), nameof(Ascents.canReviveDead))));
        if (matcher.IsInvalid)
        {
            Plugin.Logger.LogError("StatuePatch: Ascents.canReviveDead call not found, statue respawn left untouched.");
            return instructions;
        }
        matcher.SetInstruction(CodeInstruction.Call(() => CheckSkipRespawn()).MoveLabelsFrom(matcher.Instruction));
        return matcher.Instructions();
    }

    //[HarmonyPatch(typeof(RespawnChest), nameof(RespawnChest.SpawnItems))]
    //[HarmonyPrefix]
    //static bool SpawnItemsPrefix(RespawnChest __instance)
    //{
    //    UnlimitedLogger.GetInstance().DebugMessage(LogLevel.Info, DebugLogType.CampfireLogic, $"Spawn item prefix");
    //    if (!PhotonNetwork.IsMasterClient)
    //    {
    //        return false;
    //    }
    //    return true;
    //}


    [HarmonyPatch(typeof(RespawnChest), nameof(RespawnChest.SpawnItems))]
    [HarmonyPostfix]
    static void SpawnItemsPostfix(ref List<PhotonView> __result, RespawnChest __instance)
    {
        if (!Plugin.ItsPvPEAK) { return; }
        //if (__result != null)
        //{
        //    UnlimitedLogger.GetInstance().DebugMessage(LogLevel.Info, DebugLogType.CampfireLogic, $"Items Spawned: {__result.Count}");
        //    foreach (PhotonView view in __result)
        //    {
        //        Plugin.itemTracker.SetItemToTeam(view.GetComponent<Item>(), Plugin.touchedStatueTeam);
        //    }
        //}
    }




    [HarmonyPatch(typeof(RespawnChest), nameof(RespawnChest.GetInteractionText))]
    [HarmonyPostfix]
    static void GetInteractionTextPostfix(ref string __result, RespawnChest __instance)
    {
        if (!Plugin.ItsPvPEAK) { return; }
        __result = LocalizedText.GetText("TOUCH");
    }
}

