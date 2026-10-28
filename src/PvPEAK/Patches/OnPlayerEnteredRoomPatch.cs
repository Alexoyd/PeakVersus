using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using PvPEAK.GameObjects;
using PvPEAK.Model;
using System;
using System.Collections;
using Unity.Networking.Transport.Logging;
using UnityEngine;
using Zorro.Core;
using static Zorro.ControllerSupport.Rumble.RumbleClip;

namespace PvPEAK.Patches;

public class OnPlayerEnteredRoomPatch
{
    [HarmonyPatch(typeof(PlayerConnectionLog), "OnPlayerEnteredRoom")]
    [HarmonyPostfix]
    static void Postfix(PlayerConnectionLog __instance, Photon.Realtime.Player newPlayer)
    {
        Plugin.Logger.LogInfo("Player Joined");
        if (PhotonNetwork.IsMasterClient && Plugin.ItsPvPEAK)
        {
            __instance.StartCoroutine(WarnIfNotPvPEAK(newPlayer, __instance));
        }
    }

    static IEnumerator WarnIfNotPvPEAK(Photon.Realtime.Player newPlayer, PlayerConnectionLog log)
    {
        Plugin.Logger.LogInfo("Starting kick player");
        // Wait for the specified number of seconds
        yield return new WaitForSeconds(3);
        
        string version = Plugin.player_mapping[newPlayer.UserId].GetComponent<Networking>().PvPEAKVersion;
        if (version != Plugin.Version)
        {
            Plugin.Logger.LogInfo("Starting warn player");
            if (version == null)
            {
                log.AddMessage($"{log.GetColorTag(log.leftColor)} Player {log.GetColorTag(log.userColor)} {newPlayer.NickName} {log.GetColorTag(log.leftColor)} n'a pas PEAK Versus installé ! Behavior will be undefined, things will break.");
            } else
            {
                log.AddMessage($"{log.GetColorTag(log.leftColor)} Player {log.GetColorTag(log.userColor)} {newPlayer.NickName} {log.GetColorTag(log.leftColor)} utilise une autre version de PEAK Versus. Your version: {Plugin.Version}; Their version: {version}. Behavior will be undefined, things may break.");
            }   
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
    [HarmonyPostfix]
    static void PlayerAwakePostfix(Player __instance)
    {
        Plugin.player_mapping[__instance.view.Owner.UserId] = __instance;
        Networking networking = __instance.gameObject.AddComponent<Networking>();
        if (PhotonNetwork.IsMasterClient && __instance.view.IsMine)
        {
            networking.view.RPC("SetItsPvPEAK_RPC", RpcTarget.AllBuffered, Plugin.ItsPvPEAK);
        }
        else if (!PhotonNetwork.IsMasterClient && __instance.view.IsMine)
        {
            networking.view.RPC("SetPvPEAKVersion_RPC", RpcTarget.MasterClient, __instance.view, Plugin.Version);
        }
        if (__instance.view.IsMine)
        {
            NotificationsHandler.Initialize();
        }
    }

    [HarmonyPatch(typeof(ReconnectHandler), nameof(ReconnectHandler.RegisterPlayer))]
    [HarmonyPostfix]
    static void RegisterPlayerPostfix(ReconnectHandler __instance, Player player)
    {
        Plugin.Logger.LogInfo($"Registering player: {player.view.Owner.UserId}");
        Plugin.player_mapping[player.view.Owner.UserId] = player;
    }

    // This happens after all the networking stuff happens, so we can be sure we have all the interesting information, also that our color is correct
    [HarmonyPatch(typeof(CharacterCustomization), nameof(CharacterCustomization.Start))]
    [HarmonyPostfix]
    static void CharacterCustomizationStartPostfix(CharacterCustomization __instance)
    {
        if (!Plugin.IsAfterAwake) { return; }
        if (__instance.view.IsMine)
        {
            Plugin.teamTracker.CreateTeams();
        }
        if (!Plugin.ItsPvPEAK) { return; }


        // This handles joining/rejoining mid session
        int teamCount = Plugin.teamTracker.Count;
        if (!__instance.view.IsMine)
        {
            Team team = Plugin.teamTracker.GetOrCreateTeamByColor(__instance.PlayerColor);
            team.AddPlayer(__instance.view.Owner);
            if (PhotonNetwork.IsMasterClient)
            {
                Plugin.itemTracker.TrySyncItemData(__instance.view.Owner);
            }
        }
    }
}