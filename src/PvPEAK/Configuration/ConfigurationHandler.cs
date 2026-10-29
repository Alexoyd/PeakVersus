using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PvPEAK;

public class ConfigurationHandler
{
    private ConfigFile _config;
    public InputAction MenuAction { get; set; }

    public ConfigEntry<int> ConfigItemSafeDistance;
    public ConfigEntry<string> ConfigPushKey;
    public ConfigEntry<string> ConfigAnkleKey;
    public ConfigEntry<string> ConfigShopKey;
    public ConfigEntry<string> ConfigGodMenuKey;
    public ConfigEntry<string> ConfigChronicleKey;
    public ConfigEntry<string> ConfigOptionsKey;
    public ConfigEntry<float> ConfigPushForce;
    public ConfigEntry<float> ConfigAnkleForce;
    public ConfigEntry<bool> ConfigTeamVoiceInGame;
    public ConfigEntry<int> ConfigPeakCountdown;
    public ConfigEntry<bool> ConfigGodMenuEnabled;

    public int ItemSafeDistance => ConfigItemSafeDistance.Value;
    public string PushKey => ConfigPushKey.Value;
    public string AnkleKey => ConfigAnkleKey.Value;
    public string ShopKey => ConfigShopKey.Value;
    public string GodMenuKey => ConfigGodMenuKey.Value;
    public string ChronicleKey => ConfigChronicleKey.Value;
    public string OptionsKey => ConfigOptionsKey.Value;
    public float PushForce => ConfigPushForce.Value;
    public float AnkleForce => ConfigAnkleForce.Value;
    public bool TeamVoiceInGame => ConfigTeamVoiceInGame.Value;
    public int PeakCountdown => ConfigPeakCountdown.Value;
    public bool GodMenuEnabled => ConfigGodMenuEnabled.Value;


    public ConfigurationHandler(ConfigFile configFile)
    {
        _config = configFile;

        Plugin.Logger.LogInfo("ConfigurationHandler initialising");

        ConfigItemSafeDistance = _config.Bind
        (
            "General",
            "Item Safe Distance",
            15,
            "The maximum distance you must be from a campfire to steal another team's items, in world units."
        );

        ConfigPushKey = _config.Bind("Controls", "Push Key", "F", "Touche pour pousser le joueur que tu regardes (nom de touche Unity Input System).");
        ConfigAnkleKey = _config.Bind("Controls", "Ankle Grab Key", "G", "Touche pour attraper la cheville d'un joueur qui grimpe juste au-dessus de toi.");
        ConfigShopKey = _config.Bind("Controls", "Shop Key", "B", "Touche pour ouvrir la boutique (près d'un feu de camp).");
        ConfigGodMenuKey = _config.Bind("Controls", "God Menu Key", "F8", "Touche du menu de test (god mode).");
        ConfigChronicleKey = _config.Bind("Controls", "Chronicle Key", "F7", "Touche pour afficher scores et chronique.");
        ConfigOptionsKey = _config.Bind("Controls", "Options Key", "F6", "Touche des options de partie (hôte).");
        ConfigPushForce = _config.Bind("Tuning", "Push Force", 600f, "Force appliquée par une poussée.");
        ConfigAnkleForce = _config.Bind("Tuning", "Ankle Grab Force", 500f, "Force appliquée quand on tire une cheville.");
        ConfigPeakCountdown = _config.Bind("Tuning", "Peak Countdown", 75, "Secondes de compte à rebours au sommet (pacte ou mêlée).");
        ConfigTeamVoiceInGame = _config.Bind("Voice", "Team Voice In Game", true, "true : tu entends ton coéquipier en jeu à toute distance. false : coéquipier muet en jeu (si vous êtes sur Discord).");
        ConfigGodMenuEnabled = _config.Bind("Debug", "God Menu Enabled", true, "Active le menu de test (F8).");

        Plugin.Logger.LogInfo("ConfigurationHandler: Item Safe Distance Loaded: " + ConfigItemSafeDistance.Value);

        Plugin.Logger.LogInfo("ConfigurationHandler initialised");
    }
}
