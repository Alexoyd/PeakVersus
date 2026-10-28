using BepInEx.Logging;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using PvPEAK.Versus;
using static Zorro.ControllerSupport.Rumble.RumbleClip;

namespace PvPEAK.GameObjects;
public class RopeEnd : MonoBehaviour, IInteractibleConstant, IInteractible
{
    public Rope rope;
    public RopeSegment ropeSegment;

    public bool holdOnFinish => false;
    public float cutTime = 2f;

    public void Awake()
    {
        rope = GetComponentInParent<Rope>();
        ropeSegment = GetComponent<RopeSegment>();
    }

    public bool IsInteractible(Character interactor)
    {
        if (this.rope.attachmenState != Rope.ATTACHMENT.anchored || this.rope.isHelicopterRope) {  return false; }
        // PEAK Versus: cutting or sabotaging a rope needs scissors in hand.
        if (VersusItems.HeldKind(interactor) != VersusItemKind.Scissors) { return false; }
        if (interactor.data.isRopeClimbing)
        {
            return interactor.data.heldRope != rope;
        }
        return true;
    }

    public string GetInteractionText()
    {
        if (Character.localCharacter != null && Character.localCharacter.data.isCrouching)
        {
            return "saboter";
        }
        string cut_text = LocalizedText.GetText("CUT", false);
        // This string is not in the localization and I'm not good enough to add it. If it gets added later by the devs, this allows it to get picked up.
        if (cut_text == "")
        {
            cut_text = "cut";
        }
        return cut_text;
    }

    public Vector3 Center()
    {
        return base.transform.position;
    }

    public Transform GetTransform()
    {
        return base.transform;
    }

    public void HoverEnter()
    {
    }

    public void HoverExit()
    {
    }

    public void Interact(Character interactor)
    {
    }

    public string GetName()
    {
        return this.gameObject.GetComponent<RopeSegment>().GetName();
    }

    public bool IsConstantlyInteractable(Character interactor)
    {
        return this.IsInteractible(interactor);
    }

    public float GetInteractTime(Character interactor)
    {
        return interactor.data.isCrouching ? 3f : this.cutTime;
    }

    public void Interact_CastFinished(Character interactor)
    {
        if (VersusLocal.InTruce(this.transform.position))
        {
            VersusHud.Toast("Trêve du feu de camp : pas de sabotage ici", 2f);
            return;
        }
        if (interactor.data.isCrouching)
        {
            VersusNet.ToMaster(nameof(VersusNet.RPC_MarkSabotage), this.rope.photonView.ViewID, VersusGame.TeamKey(interactor));
            VersusHud.Toast("Corde sabotée : elle lâchera sous un adversaire", 3f);
        }
        else
        {
            this.rope.photonView.RPC("Detach_Rpc", RpcTarget.AllBuffered, 0f);
        }
        VersusItems.ConsumeHeld(interactor, useCharges: true);
    }

    public void CancelCast(Character interactor)
    {
    }

    public void ReleaseInteract(Character interactor)
    {
    }
}
