using System.Linq;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace PvPEAK.Versus;

public class VersusPatches
{
    // ---------- setup ----------

    [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
    [HarmonyPostfix]
    static void PlayerAwakePostfix(Player __instance)
    {
        if (__instance.GetComponent<VersusNet>() == null)
        {
            __instance.gameObject.AddComponent<VersusNet>();
            __instance.view.RefreshRpcMonoBehaviourCache();
        }
    }

    [HarmonyPatch(typeof(AirportCheckInKiosk), nameof(AirportCheckInKiosk.Awake))]
    [HarmonyPostfix]
    static void AirportAwakePostfix()
    {
        VersusGame.Reset();
        VersusLocal.ResetLocal();
    }

    // Our IMGUI windows behave like the game's own menus: free cursor, no character input.
    [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.UpdateWindowStatus))]
    [HarmonyPostfix]
    static void UpdateWindowStatusPostfix(GUIManager __instance)
    {
        if (!VersusHud.IsAnyWindowOpen) { return; }
        __instance.windowShowingCursor = true;
        __instance.windowBlockingInput = true;
    }

    // ---------- Versus items ----------

    [HarmonyPatch(typeof(Item), nameof(Item.GetItemName))]
    [HarmonyPostfix]
    static void GetItemNamePostfix(Item __instance, ItemInstanceData data, ref string __result)
    {
        ItemInstanceData source = data ?? __instance.data;
        VersusItemKind kind = VersusItems.GetKind(source);
        if (kind == VersusItemKind.None) { return; }
        __result = VersusItems.DisplayName(kind);
        if (kind == VersusItemKind.Scissors && source.TryGetDataEntry<IntItemData>(VersusItems.UsesKey, out var uses))
        {
            __result += $" ({uses.Value})";
        }
    }

    // Scissors, incense and traps have no vanilla use: Versus handles their input itself.
    [HarmonyPatch(typeof(Item), nameof(Item.CanUsePrimary))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    static bool CanUsePrimaryPrefix(Item __instance, ref bool __result)
    {
        VersusItemKind kind = VersusItems.GetKind(__instance);
        if (kind == VersusItemKind.Scissors || kind == VersusItemKind.Incense || kind == VersusItemKind.CampfireTrap)
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Item), nameof(Item.CanUseSecondary))]
    [HarmonyPrefix]
    static bool CanUseSecondaryPrefix(Item __instance, ref bool __result)
    {
        VersusItemKind kind = VersusItems.GetKind(__instance);
        if (kind == VersusItemKind.Scissors || kind == VersusItemKind.Incense || kind == VersusItemKind.CampfireTrap)
        {
            __result = false;
            return false;
        }
        return true;
    }

    // ---------- defective piton ----------

    static bool pendingDefective;
    static string pendingTeam = "";

    [HarmonyPatch(typeof(CharacterItems), "HammerClimbingSpike")]
    [HarmonyPrefix]
    static void HammerPrefix(CharacterItems __instance)
    {
        ItemSlot slot = __instance.currentClimbingSpikeItemSlot;
        pendingDefective = slot != null && VersusItems.GetKind(slot.data) == VersusItemKind.DefectivePiton;
        Plugin.Logger.LogInfo($"Hammering piton, defective: {pendingDefective}");
        pendingTeam = pendingDefective ? VersusGame.TeamKey(__instance.character) : "";
    }

    [HarmonyPatch(typeof(CharacterItems), "HammerClimbingSpike")]
    [HarmonyPostfix]
    static void HammerPostfix()
    {
        pendingDefective = false;
    }

    // The hammered piton is spawned inside HammerClimbingSpike: catch the exact object it creates.
    [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.InstantiateItemRoom))]
    [HarmonyPostfix]
    static void InstantiateItemRoomPostfix(GameObject __result)
    {
        if (!pendingDefective || __result == null) { return; }
        pendingDefective = false;
        PhotonView view = __result.GetComponent<PhotonView>() ?? __result.GetComponentInChildren<PhotonView>();
        if (view == null)
        {
            Plugin.Logger.LogWarning($"Defective piton {__result.name} has no PhotonView");
            return;
        }
        Plugin.Logger.LogInfo($"Defective piton placed: {__result.name} view {view.ViewID} team {pendingTeam}");
        VersusNet.ToMaster(nameof(VersusNet.RPC_MarkPiton), view.ViewID, pendingTeam);
        VersusHud.Toast("Piton défectueux posé : il cassera sous un adversaire", 3f);
    }

    // ---------- falls from Versus traps ----------

    [HarmonyPatch(typeof(CharacterClimbing), "TryToStartWallClimb")]
    [HarmonyPrefix]
    static bool TryToStartWallClimbPrefix(CharacterClimbing __instance, bool forceAttempt)
    {
        if (!__instance.character.IsLocal) { return true; }
        if (Time.time < VersusLocal.noGrabUntil) { return false; }
        if (VersusLocal.voluntaryGrabOnly && (forceAttempt || __instance.character.data.sincePressClimb >= 0.1f)) { return false; }
        return true;
    }

    // ---------- help detection (vanilla helping hand) ----------

    [HarmonyPatch(typeof(CharacterGrabbing), "Reach")]
    [HarmonyPostfix]
    static void ReachPostfix(CharacterGrabbing __instance)
    {
        if (!VersusGame.Active || VersusLocal.Instance == null) { return; }
        Character helper = __instance.character;
        Character me = Character.localCharacter;
        if (helper == null || me == null || helper == me) { return; }
        float distance = Vector3.Distance(helper.Center, me.Center);
        if (distance > 4f || Vector3.Angle(helper.data.lookDirection, me.Center - helper.Center) > 60f) { return; }
        if (!CharacterGrabbing.TargetCanBeHelped(me, helper)) { return; }
        VersusLocal.Instance.NoteHelpedBy(helper);
    }

    // ---------- backpacks: pickpocketing enemies is allowed, but it makes noise ----------

    [HarmonyPatch(typeof(BackpackOnBackVisuals), nameof(BackpackOnBackVisuals.Interact_CastFinished))]
    [HarmonyPostfix]
    static void BackpackOpenedPostfix(BackpackOnBackVisuals __instance, Character interactor)
    {
        if (!VersusGame.Active || interactor == null || !interactor.IsLocal) { return; }
        Character owner = __instance.character;
        if (owner == null || owner == interactor) { return; }
        if (!VersusGame.IsEnemy(VersusGame.TeamKey(interactor), VersusGame.TeamKey(owner))) { return; }
        string ownerUser = VersusGame.UserOf(owner);
        VersusNet.ForUser(ownerUser)?.view.RPC(nameof(VersusNet.RPC_Zip), owner.photonView.Owner);
        VersusNet.ToMaster(nameof(VersusNet.RPC_ReportPickpocket), VersusGame.LocalUser, ownerUser);
        VersusLocal.CompleteMission(MissionType.Pickpocket);
    }

    // ---------- death ----------

    [HarmonyPatch(typeof(Character), nameof(Character.RPCA_Die))]
    [HarmonyPostfix]
    static void DiePostfix(Character __instance)
    {
        VersusLocal.deathPosition[__instance] = __instance.Center;
        if (VersusGame.Active && __instance.IsLocal)
        {
            VersusNet.ToMaster(nameof(VersusNet.RPC_ReportDeath), VersusGame.LocalUser);
        }
    }

    // ---------- voice ----------

    [HarmonyPatch(typeof(CharacterVoiceHandler), "Update")]
    [HarmonyPostfix]
    static void VoiceUpdatePostfix(CharacterVoiceHandler __instance)
    {
        if (!VersusGame.Active) { return; }
        Character speaker = __instance.m_character;
        Character me = Character.localCharacter;
        AudioSource source = __instance.m_source;
        if (speaker == null || me == null || speaker == me || source == null) { return; }
        string speakerTeam = VersusGame.TeamKey(speaker);
        bool teammate = speakerTeam != "" && speakerTeam == VersusGame.TeamKey(me);
        if (teammate)
        {
            if (Plugin.ConfigurationHandler.TeamVoiceInGame) { source.spatialBlend = 0f; }
            else { source.volume = 0f; }
            return;
        }
        switch (VersusGame.State.modifier)
        {
            case BiomeModifier.Silence:
                source.volume = 0f;
                break;
            case BiomeModifier.Storm:
                source.spatialBlend = 0.5f;
                break;
        }
    }

    // ---------- fog modifier hides enemy names ----------

    [HarmonyPatch(typeof(UIPlayerNames), nameof(UIPlayerNames.UpdateName))]
    [HarmonyPrefix]
    static void UpdateNamePrefix(UIPlayerNames __instance, int index, ref bool visible)
    {
        if (!VersusGame.Active || VersusGame.State.modifier != BiomeModifier.Fog || !visible) { return; }
        if (index >= __instance.playerNameText.Length) { return; }
        Character? other = __instance.playerNameText[index]?.characterInteractable?.character;
        Character me = Character.localCharacter;
        if (other != null && me != null && VersusGame.TeamKey(other) != VersusGame.TeamKey(me))
        {
            visible = false;
        }
    }

    // ---------- end of game ----------

    [HarmonyPatch(typeof(Character), nameof(Character.EndGame))]
    [HarmonyPrefix]
    static void EndGamePrefix()
    {
        if (VersusGame.Active && VersusGame.IsMaster && !VersusGame.State.finished)
        {
            VersusGame.MasterEndGame();
        }
    }
}
