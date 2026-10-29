using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using PvPEAK.GameObjects;
using PvPEAK.Model;
using PvPEAK.Patches;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static BepInEx.BepInDependency;

namespace PvPEAK;

// This BepInAutoPlugin attribute comes from the Hamunii.BepInEx.AutoPlugin
// NuGet package, and it will generate the BepInPlugin attribute for you!
// For more info, see https://github.com/Hamunii/BepInEx.AutoPlugin
[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;
    public static ConfigurationHandler ConfigurationHandler;
    private readonly Harmony _harmony = new(Id);
    public static List<Campfire> CampfireList = new();
    public static List<RespawnChest> StatueList = new();
    public static bool IsAfterAwake = false;
    //public static bool HasHostStarted = false;
    //public static NetworkingOld networking = new();
    public static ItemTracker itemTracker = new();
    public static TeamTracker teamTracker = new();
    public static Team? touchedStatueTeam = null;
    public static Dictionary<Campfire, List<Backpack>> campfireBackpackMapping = new();
    public static Dictionary<Campfire, List<Item>> campfireFoodMapping = new();
    public static TMP_FontAsset fontAsset = null;
    public static Dictionary<Character, float> deathMapping = new();
    public static Team? winningTeam = null;
    public static bool ItsPvPEAK = false;
    // Going from a uuid to a player is weirdly annoying, so cache it here
    public static Dictionary<string, Player> player_mapping = new();
    //public static bool isHost = false;
    public static bool leavingLobby = false;
    //public static GameHandler gameHandler;
    //public static Networking networking;
    public static bool CheatsEnabled = false;
    public static InputAction _toggleCheatsAction;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {Id} is loaded!");
        ConfigurationHandler = new ConfigurationHandler(Config);
        Versus.VersusLocal.Create();
        //NetworkingUtilities.MAX_PLAYERS = ConfigurationHandler.MaxPlayers;
        // Harmony.DEBUG = true;

        //Extra marshmallow and backpack patches
        SafePatch(typeof(CampfireAwakePatch));
        SafePatch(typeof(OnPlayerLeftRoomPatch));
        SafePatch(typeof(OnPlayerEnteredRoomPatch));
        Plugin.Logger.LogInfo("Marshmallow patches successful!");


        //In-game message patches
        SafePatch(typeof(PlayerConnectionLogPatch));
        SafePatch(typeof(InitializationPatch));
        SafePatch(typeof(LeaveLobbyPatch));
        Plugin.Logger.LogInfo("Player connection log patches successful!");

        //PvPeak Patches
        SafePatch(typeof(SingleItemSpawnerTrySpawnItemsPatch));
        SafePatch(typeof(StartGamePatch));
        SafePatch(typeof(PlayerAddItemPatch));
        SafePatch(typeof(ItemInteractablePatch));
        SafePatch(typeof(ItemInteractPatch));
        SafePatch(typeof(NetworkingPatch));
        SafePatch(typeof(StatuePatch));
        SafePatch(typeof(CampfireZonePatch));
        SafePatch(typeof(RopePatch));
        SafePatch(typeof(EndGamePatch));
        SafePatch(typeof(MassAfflictionPatch));
        SafePatch(typeof(ProgressPatch));
        SafePatch(typeof(ScoutmasterPatch));
        SafePatch(typeof(MoraleBoostPatch));
        SafePatch(typeof(Versus.VersusPatches));

        Utility.CreateLocalization();

        Plugin.Logger.LogInfo("PvPeak patches successful!");

        //SafePatch(typeof(SkipIntroPatch));
        //SafePatch(typeof(PeakGamingPatch));
        //_toggleCheatsAction = new InputAction("ToggleCheats", (InputActionType)1, "<Keyboard>/k", (string)null, (string)null, (string)null);
        //_toggleCheatsAction.performed += delegate
        //{
        //    ToggleCheats();
        //};
        //_toggleCheatsAction.Enable();

    }

    private void SafePatch(System.Type patchClass)
    {
        try
        {
            _harmony.PatchAll(patchClass);
        }
        catch (System.Exception e)
        {
            Logger.LogError($"Patches in {patchClass.Name} failed, the rest of the mod keeps loading: {e}");
        }
    }

    public static void ToggleCheats()
    {
        CheatsEnabled = !CheatsEnabled;
    }

    public static void Reset()
    {
        campfireBackpackMapping.Clear();
        campfireFoodMapping.Clear();
        CampfireList.Clear();
        IsAfterAwake = false;
        //HasHostStarted = false;
        itemTracker = new();
        teamTracker = new();
        touchedStatueTeam = null;
        deathMapping.Clear();
        winningTeam = null;
    }
}
