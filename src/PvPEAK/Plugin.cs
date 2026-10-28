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
[BepInDependency(SoftDependencyFix.Plugin.Id)]
[BepInDependency(PEAKUnlimited.Plugin.Id, DependencyFlags.SoftDependency)]
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
        //NetworkingUtilities.MAX_PLAYERS = ConfigurationHandler.MaxPlayers;
        // Harmony.DEBUG = true;

        //Extra marshmallow and backpack patches
        _harmony.PatchAll(typeof(CampfireAwakePatch));
        _harmony.PatchAll(typeof(OnPlayerLeftRoomPatch));
        _harmony.PatchAll(typeof(OnPlayerEnteredRoomPatch));
        Plugin.Logger.LogInfo("Marshmallow patches successful!");


        //In-game message patches
        _harmony.PatchAll(typeof(PlayerConnectionLogPatch));
        _harmony.PatchAll(typeof(InitializationPatch));
        _harmony.PatchAll(typeof(LeaveLobbyPatch));
        Plugin.Logger.LogInfo("Player connection log patches successful!");

        //PvPeak Patches
        _harmony.PatchAll(typeof(SingleItemSpawnerTrySpawnItemsPatch));
        _harmony.PatchAll(typeof(StartGamePatch));
        _harmony.PatchAll(typeof(PlayerAddItemPatch));
        _harmony.PatchAll(typeof(ItemInteractablePatch));
        _harmony.PatchAll(typeof(ItemInteractPatch));
        _harmony.PatchAll(typeof(NetworkingPatch));
        _harmony.PatchAll(typeof(StatuePatch));
        _harmony.PatchAll(typeof(CampfireZonePatch));
        _harmony.PatchAll(typeof(RopePatch));
        _harmony.PatchAll(typeof(EndGamePatch));
        _harmony.PatchAll(typeof(MassAfflictionPatch));
        _harmony.PatchAll(typeof(ProgressPatch));
        _harmony.PatchAll(typeof(ScoutmasterPatch));
        _harmony.PatchAll(typeof(MoraleBoostPatch));
        _harmony.PatchAll(typeof(Versus.VersusPatches));
        Versus.VersusLocal.Create();
        Utility.CreateLocalization();

        Plugin.Logger.LogInfo("PvPeak patches successful!");

        //_harmony.PatchAll(typeof(SkipIntroPatch));
        //_harmony.PatchAll(typeof(PeakGamingPatch));
        //_toggleCheatsAction = new InputAction("ToggleCheats", (InputActionType)1, "<Keyboard>/k", (string)null, (string)null, (string)null);
        //_toggleCheatsAction.performed += delegate
        //{
        //    ToggleCheats();
        //};
        //_toggleCheatsAction.Enable();

        Chainloader.PluginInfos.TryGetValue(PEAKUnlimited.Plugin.Id, out PluginInfo PEAKUnlimitedInfo);
        if (PEAKUnlimitedInfo != null)
        {
            Logger.LogInfo($"PEAKUnlimited found: {PEAKUnlimitedInfo}. Patching for compatibility.");
            _harmony.PatchAll(typeof(PeakUnlimitedCompatPatch));
        }
        else
        {
            Logger.LogInfo($"PEAKUnlimited not found; not patching");
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
