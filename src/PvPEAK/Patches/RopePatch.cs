using BepInEx.Logging;
using HarmonyLib;
using PvPEAK.GameObjects;
using Photon.Pun;
using pworld.Scripts;
using pworld.Scripts.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using Zorro.Core;
using static Zorro.ControllerSupport.Rumble.RumbleClip;

namespace PvPEAK.Patches;
internal class RopePatch
{
    [HarmonyPatch(typeof(RopeSegment), nameof(RopeSegment.IsInteractible))]
    [HarmonyPrefix]
    static bool RopeSegmentIsInteractiblePrefix(ref bool __result, RopeSegment __instance, Character interactor)
    {
        if (!Plugin.ItsPvPEAK) { return true; }
        if (__instance.GetComponent<RopeEnd>() != null)
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Rope), nameof(Rope.AttachToAnchor_Rpc))]
    [HarmonyPostfix]
    static void AttachToAnchor_RpcPostfix(Rope __instance, PhotonView anchorView, float ropeLength)
    {
        //Some ropes get anchored before they get segments, so we need to handle those elsewhere
        //OnRopeSpooled(__instance);
    }

    [HarmonyPatch(typeof(Rope), nameof(Rope.Update))]
    [HarmonyPostfix]
    static void RopeUpdatePostfix(Rope __instance)
    {
        if (!Plugin.ItsPvPEAK) { return; }
        //Pain in the ass that we have to do this, but ropes act very weirdly remotely, so we're just gonna hard update this.
        if (__instance.attachmenState == Rope.ATTACHMENT.anchored)
        {
            List<Transform> segments = __instance.GetRopeSegments();
            if (segments.Count > 0) {
                // First segment is an invisible one at the anchor point; don't know what its deal is.
                // Last segment is the first one cennected to that segment.
                Transform startSegment = segments[0];
                Transform lastSegment = segments[^1];
                if (lastSegment.GetComponent<RopeEnd>() == null)
                {
                    foreach (Transform segment in segments.Skip(1))
                    {
                        if (segment != lastSegment)
                        {
                            RopeEnd temp = segment.GetComponent<RopeEnd>();
                            if (temp != null)
                            {
                                UnityEngine.Object.DestroyImmediate(temp);
                            }
                        }
                    }
                    lastSegment.gameObject.AddComponent<RopeEnd>();
                }
                // Remote ropes act very weirdly. You can't interact with the first rope secment locally, but remotely you can, so we de this here.
                if (startSegment.GetComponent<RopeEnd>() == null)
                {
                    startSegment.gameObject.AddComponent<RopeEnd>();
                }
            }
        }
    }

    [HarmonyPatch(typeof(Rope), nameof(Rope.Detach_Rpc))]
    [HarmonyPostfix]
    static void Detach_RpcPostfix(Rope __instance, float segmentLength)
    {
        if (!Plugin.ItsPvPEAK) { return; }
        // Ropes may not be real remotely, so we need to destroy the fake ones and make a new one to fall.
        if (!__instance.photonView.IsMine && __instance.spool == null)
        {
            int segmentCount = __instance.remoteColliderSegments.Count;
            if (segmentCount > 0)
            {
                __instance.Clear(true);
                for (int i = 0; i < segmentCount; i++)
                {
                    __instance.AddSegment();
                }
            }
        }
    }

    //static void OnRopeSpooled(Rope rope)
    //{
        
    //}

    //Since this function uses a local enumerator, we need to recreate the whele thing in the prefix and never run the original.
    // This might end up making more sense to just subclass the RopeSegment and hijack Rope.Awake to us our subclass in the future, we'll se how this goes.
    //[HarmonyPatch(typeof(RopeAnchorWithRope), nameof(RopeAnchorWithRope.SpawnRope))]
    //[HarmonyPrefix]
    //static bool RopeAnchorWithRopeSpawnRopePrefix(ref Rope __result, RopeAnchorWithRope __instance)
    //{
    //    if (!Plugin.ItsPvPEAK) { return true; }
    //    if (!__instance.photonView.IsMine)
    //    {
    //        __result = null;
    //        return false;
    //    }

    //    __instance.ropeInstance = PhotonNetwork.Instantiate(__instance.ropePrefab.name, __instance.anchor.anchorPoint.position, __instance.anchor.anchorPoint.rotation, 0);
    //    __instance.rope = __instance.ropeInstance.GetComponent<Rope>();
    //    __instance.rope.Segments = __instance.ropeSegmentLength;
    //    __instance.rope.photonView.RPC("AttachToAnchor_Rpc", RpcTarget.AllBuffered, __instance.anchor.photonView, __instance.ropeSegmentLength);
    //    __instance.StartCoroutine(SpoolOut());
    //    __result = __instance.rope;
    //    return false;
    //    IEnumerator SpoolOut()
    //    {
    //        float elapsed = 0f;
    //        while (elapsed < __instance.spoolOutTime)
    //        {
    //            elapsed += Time.deltaTime;
    //            __instance.rope.Segments = Mathf.Lerp(0f, __instance.ropeSegmentLength, (elapsed / __instance.spoolOutTime).Clamp01());
    //            yield return null;
    //        }
    //        //OnRopeSpooled(__instance.rope);
    //    }
    //}




    public static Item GetInteractableItem(Transform transform)
    {
        Item[] componentsInParent = transform.GetComponentsInParent<Item>();
        foreach (Item item in componentsInParent)
        {
            if (item.IsInteractible(Character.localCharacter))
            {
                return item;
            }
        }
        return null;
    }

    public static IInteractible GetInteractableIInteractable(Transform transform)
    {
        IInteractible[] componentsInParent = transform.GetComponentsInParent<IInteractible>();
        foreach (IInteractible obj in componentsInParent)
        {
            if (obj.IsInteractible(Character.localCharacter))
            {
                return obj;
            }
        }
        return null;
    }

    // This will be appliod regardless of whether ItsPvPEAK. I don't think it will hurt anything.
    [HarmonyPatch(typeof(Interaction), nameof(Interaction.DoInteractableRaycasts))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> DoInteractableRaycastsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher matcher = new CodeMatcher(instructions);
        // The default implementation of this function will only check one component on a gameObject.
        // This means I can't add additional interactors on an object that asready has interactors, which makes implementing cutting ropes hard.
        // Replace all these calls with a test against multiple components.
        matcher.Start();
        matcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt));
        while (matcher.IsValid) {
            if (matcher.Operand.ToString() is "IInteractible GetComponentInParent[IInteractible]()")
            {
                matcher.RemoveInstruction();
                matcher.InsertAndAdvance(CodeInstruction.Call((Transform transform) => GetInteractableIInteractable(transform)));
            } else if (matcher.Operand.ToString() is "Item GetComponentInParent[Item]()")
            {
                matcher.RemoveInstruction();
                matcher.InsertAndAdvance(CodeInstruction.Call((Transform transform) => GetInteractableItem(transform)));
            } else
            {
                matcher.Advance(1);
            }
            matcher.MatchStartForward(new CodeMatch(OpCodes.Callvirt));
        }

        return matcher.Instructions();
    }

    //[HarmonyPatch(typeof(RopeSegment), nameof(RopeSegment.Awake))]
    //[HarmonyPostfix]
    //static void RopeSegmentAwake(RopeAnchorWithRope __instance)
    //{
    //    __instance.rope.GetRopeSegments()[^1].gameObject.AddComponent<RopeEnd>();
    //}
}
