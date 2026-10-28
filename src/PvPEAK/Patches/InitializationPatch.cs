using BepInEx.Logging;
using HarmonyLib;
using PvPEAK.GameObjects;
using PvPEAK.Model;
using Photon.Pun;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zorro.Core;

namespace PvPEAK.Patches;

public class InitializationPatch
{
    static GameObject PvPEAKButton = null;

    [HarmonyPatch(typeof(MainMenuMainPage), nameof(MainMenuMainPage.PlayClicked))]
    [HarmonyPostfix]
    static void Postfix(MainMenuMainPage __instance)
    {
        //This is a gross way of testing if a user created a lobby, since PhotonNetwork.IsMasterClient doesn't seem to work in PlayerConnectionLog
        Plugin.Logger.LogInfo("Play clicked, this player is the lobby host!");
        //Plugin.isHost = true;
    }

    [HarmonyPatch(typeof(AirportCheckInKiosk), nameof(AirportCheckInKiosk.Awake))]
    [HarmonyPostfix]
    static void AirportLoadPostfix()
    {
        Plugin.Reset();
    }

    [HarmonyPatch(typeof(MainMenuMainPage), nameof(MainMenuMainPage.Start))]
    [HarmonyPostfix]
    static void StartPostfix(MainMenuMainPage __instance)
    {
        Plugin.ItsPvPEAK = false;
        Plugin.leavingLobby = false;
        CanvasGroup canvasGroup = __instance.GetComponent<CanvasGroup>();
        GameObject buttonsHolder = __instance.transform.Find("Menu/Buttons").gameObject;
        GameObject buttonPrefab = buttonsHolder.transform.Find("Button_PlayWithFriends").gameObject;
        PvPEAKButton = Object.Instantiate(buttonPrefab, buttonsHolder.transform);
        PvPEAKButton.name = "Button_PeakVersus";
        RectTransform transform = PvPEAKButton.transform as RectTransform;
        PvPEAKButton.transform.position += new Vector3(0, transform.sizeDelta[1] * buttonsHolder.transform.lossyScale[1], 0);
        GameObject hinge = PvPEAKButton.transform.Find("Hinge").gameObject;
        GameObject text = hinge.transform.Find("Text").gameObject;
        GameObject icons = hinge.transform.Find("Icons").gameObject;
        text.GetComponent<LocalizedText>().SetText("Host PEAK Versus");
        foreach (Image image in icons.transform.GetComponentsInChildren<Image>().Skip(2))
        {
            image.transform.localEulerAngles += new Vector3(0, 180, 0);
        }
        RectTransform rt = PvPEAKButton.GetComponent<RectTransform>();
        rt.eulerAngles = new Vector3(0, 0, 0);
        rt = hinge.GetComponent<RectTransform>();
        rt.offsetMax = new Vector2(-80, 0);
        rt.offsetMin = new Vector2(30, 0);

        PvPEAKButton.GetComponent<Button>().onClick.AddListener(()=>PvPEAKClicked(__instance));


        //PvPEAKButton.AddComponent<Button>();
    }

    static void PvPEAKClicked(MainMenuMainPage main_menu)
    {
        Plugin.ItsPvPEAK = true;
        main_menu.PlayClicked();
    }

    [HarmonyPatch(typeof(MainMenuMainPage), nameof(MainMenuMainPage.Update))]
    [HarmonyPostfix]
    static void MainMenuPageUpdatePostfix(MainMenuMainPage __instance)
    {
        // Stupid fix for localization not existing. Should probably just add it to all languages, but oh well.
        GameObject text = PvPEAKButton.transform.Find("Hinge/Text").gameObject;
        text.GetComponent<LocalizedText>().SetText("Host PEAK Versus");
    }

    //[HarmonyPatch(typeof(GameHandler), nameof(GameHandler.Awake))]
    //[HarmonyPostfix]
    //static void GameHandlerAwakePostfix(GameHandler __instance)
    //{
    //    Plugin.gameHandler = __instance;
    //    Plugin.networking = __instance.gameObject.AddComponent<Networking>();
    //}
}