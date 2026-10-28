using HarmonyLib;
using PvPEAK.GameObjects;
using PvPEAK.Model;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Zorro.Core;

namespace PvPEAK.Patches;
internal class EndGamePatch
{

    static bool TeamAtPeak(Character character, out List<Character> missingMembers)
    {
        missingMembers = new List<Character>();
        Team team = Plugin.teamTracker.GetPlayerTeam(character.player);
        if (team != null)
        {
            foreach (string userId in team.players)
            {
                Photon.Realtime.Player photon_player = null;
                foreach (KeyValuePair<int, Photon.Realtime.Player> player in PhotonNetwork.CurrentRoom.Players)
                {
                    if (player.Value.UserId == userId)
                    {
                        photon_player = player.Value;
                        break;
                    }
                }
                if (photon_player == null) { continue; }
                Character other_character = PlayerHandler.GetPlayerCharacter(photon_player);
                if (!other_character.photonView.Owner.IsInactive && !other_character.data.dead && !Singleton<MountainProgressHandler>.Instance.IsAtPeak(other_character.Center))
                {
                    missingMembers.Add(other_character);
                }
                if (other_character.data.dead && Plugin.deathMapping.ContainsKey(other_character) && (Time.time - Plugin.deathMapping[other_character]) < 60)
                {
                    missingMembers.Add(other_character);
                }
            }
        }
        return missingMembers.Count == 0;
    }

    [HarmonyPatch(typeof(Flare), nameof(Flare.TriggerHelicopter))]
    [HarmonyPostfix]
    static void FlareTriggerHelicopterPostfix(Flare __instance)
    {
        if (!Plugin.ItsPvPEAK) { return; }
        Character character = __instance.item.holderCharacter;
        if (!(bool)character) { return; }
        Team team = Plugin.teamTracker.GetPlayerTeam(character.player);
        if (team != null)
        {
            Plugin.winningTeam = team;
        }
    }

    [HarmonyPatch(typeof(Item), nameof(Item.CanUsePrimary))]
    [HarmonyPrefix]
    static bool ItemCanUsePrimaryPrefix(ref bool __result, Item __instance)
    {
        if (!Plugin.ItsPvPEAK) { return true; }
        Character character = __instance.holderCharacter;
        if (!(bool)character) { return true; }
        Flare flare = __instance.gameObject.GetComponent<Flare>();
        if (flare == null) { return true; }
        if (Singleton<MountainProgressHandler>.Instance.IsAtPeak(character.Center) && !Singleton<PeakHandler>.Instance.summonedHelicopter && !TeamAtPeak(character, out List<Character> missingMembers))
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Flare), nameof(Flare.Update))]
    [HarmonyPrefix]
    static bool FlareUpdatePrefix(Flare __instance)
    {
        if (!Plugin.ItsPvPEAK) { return true; }
        Character character = __instance.item.holderCharacter;
        if (!(bool)character) { return true; }
        if (character != Character.localCharacter) { return true; }
        if (__instance.item.itemState == ItemState.Held)
        {
            if (Singleton<MountainProgressHandler>.Instance.IsAtPeak(character.Center) && !Singleton<PeakHandler>.Instance.summonedHelicopter && !TeamAtPeak(character, out List<Character> missingMembers))
            {
                string message = LocalizedText.GetText("PVPEAK_GATHERYOURPARTY", false);
                if (message == "")
                {
                    message = "Gather your party before adventuring forth";
                }
                string diedtoorecently = LocalizedText.GetText("PVPEAK_DIEDTOORECENTLY", false);
                if (diedtoorecently == "")
                {
                    diedtoorecently = "died too recently, wait";
                }
                string seconds_string = LocalizedText.GetText("PVPEAK_SECONDS", false);
                if (seconds_string == "")
                {
                    seconds_string = "seconds";
                }
                foreach (Character otherCharacter in missingMembers)
                {
                    if (Plugin.deathMapping.ContainsKey(otherCharacter) && (Time.time - Plugin.deathMapping[otherCharacter]) < 60)
                    {
                        message += $"\n{otherCharacter.photonView.Owner.NickName} {diedtoorecently} {60-Mathf.RoundToInt(Time.time - Plugin.deathMapping[otherCharacter])} {seconds_string}";
                    } else
                    {
                        float num = Vector3.Distance(character.Center, otherCharacter.Center);
                        message += $"\n{otherCharacter.photonView.Owner.NickName} {Mathf.RoundToInt(num * CharacterStats.unitsToMeters)}m";
                    }
                }
                NotificationsHandler.SetNotification(message, .5f);
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(Character), nameof(Character.RPCA_Die))]
    [HarmonyPostfix]
    static void CharacterRPCA_DiePostfix(Character __instance)
    {
        if (!Plugin.ItsPvPEAK) { return; }
        Plugin.deathMapping[__instance] = Time.time;
    }

    [HarmonyPatch(typeof(Character), nameof(Character.CheckWinCondition))]
    [HarmonyPrefix]
    static bool CheckWinConditionPrefix(ref bool __result, Character c)
    {
        // PEAK Versus: everyone at the peak when the countdown ends escapes, so the pact (all 4) or a brawl decides.
        return true;
    }


    [HarmonyPatch(typeof(PeakSequence), nameof(PeakSequence.Start))]
    [HarmonyPrefix]
    static bool PeakSequenceStartPrefix(PeakSequence __instance)
    {
        if (!Plugin.ItsPvPEAK) { return true; }
        // PEAK Versus: a longer countdown leaves time for the other team to arrive (pact) or for a brawl.
        __instance.totalSeconds = Plugin.ConfigurationHandler.PeakCountdown;
        return true;
    }
    
}
