using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace PvPEAK.Versus;

// Plain IMGUI overlay: toasts, points HUD, shop, chronicle and the god menu used for solo testing.
public class VersusHud : MonoBehaviour
{
    static VersusHud? instance;

    readonly List<(string text, float until)> toasts = new();
    bool shopOpen;
    bool godOpen;
    bool chronicleOpen;
    bool infiniteStamina;
    string vanillaFilter = "";
    Vector2 godScroll;
    Vector2 chronicleScroll;
    Rect shopRect = new(40, 160, 380, 460);
    Rect godRect = new(440, 80, 460, 640);
    Rect chronicleRect = new(920, 80, 520, 600);
    GUIStyle? toastStyle;
    GUIStyle? boxStyle;
    GUIStyle? labelStyle;
    GUIStyle? headStyle;

    void Awake() => instance = this;

    public static void Toast(string text, float duration)
    {
        if (instance == null) { return; }
        instance.toasts.RemoveAll(t => t.text == text);
        instance.toasts.Add((text, Time.time + duration));
        if (instance.toasts.Count > 6) { instance.toasts.RemoveAt(0); }
    }

    public static void ToggleShop()
    {
        if (instance == null) { return; }
        Character me = Character.localCharacter;
        if (!instance.shopOpen && (me == null || !VersusLocal.InTruce(me.Center)))
        {
            Toast("La boutique n'est ouverte qu'aux feux de camp", 2f);
            return;
        }
        instance.shopOpen = !instance.shopOpen;
    }

    public static void ToggleGod()
    {
        if (instance == null || !Plugin.ConfigurationHandler.GodMenuEnabled) { return; }
        instance.godOpen = !instance.godOpen;
    }

    public static void ToggleChronicle()
    {
        if (instance == null) { return; }
        instance.chronicleOpen = !instance.chronicleOpen;
    }

    bool AnyWindowOpen => shopOpen || godOpen || chronicleOpen;
    public static bool IsAnyWindowOpen => instance != null && instance.AnyWindowOpen;

    void Update()
    {
        toasts.RemoveAll(t => Time.time > t.until);
        Character me = Character.localCharacter;
        if (shopOpen && (me == null || !VersusLocal.InTruce(me.Center))) { shopOpen = false; }
        if (infiniteStamina && me != null && !me.data.dead) { me.AddStamina(1f); }
        if (VersusGame.State.finished) { chronicleOpen = true; }
    }

    void EnsureStyles()
    {
        if (toastStyle != null) { return; }
        toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
        toastStyle.normal.textColor = new Color(1f, 0.95f, 0.8f);
        boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 15, wordWrap = true };
        boxStyle.normal.textColor = Color.white;
        labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, wordWrap = true };
        headStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        headStyle.normal.textColor = new Color(1f, 0.3f, 0.25f);
    }

    void OnGUI()
    {
        EnsureStyles();
        DrawToasts();
        if (godOpen) { godRect = GUILayout.Window(98401, godRect, DrawGod, "PEAK Versus — menu de test"); }

        Character me = Character.localCharacter;
        if (me == null || !VersusGame.Active) { return; }

        DrawHud(me);
        DrawWorldLabels(me);
        if (shopOpen) { shopRect = GUILayout.Window(98402, shopRect, DrawShop, "Boutique du feu de camp"); }
        if (chronicleOpen) { chronicleRect = GUILayout.Window(98403, chronicleRect, DrawChronicle, "Scores et chronique"); }
    }

    void DrawToasts()
    {
        float y = 90f;
        foreach ((string text, float _) in toasts)
        {
            Rect rect = new(Screen.width / 2f - 450f, y, 900f, 34f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, text, toastStyle);
            y += 38f;
        }
    }

    void DrawHud(Character me)
    {
        VersusState state = VersusGame.State;
        string user = VersusGame.LocalUser;
        string team = VersusGame.TeamKey(me);
        List<string> lines = new()
        {
            $"<b>PEAK Versus</b>  {VersusGame.TeamName(team)}",
            $"Tes points : <b>{state.Wallet(user)}</b>   Cagnotte d'équipe : <b>{state.Pot(team)}</b>",
            $"Biome : {VersusGame.ModifierName(state.modifier)}",
        };
        foreach (DebtRecord debt in state.debts.Where(d => d.debtor == user)) { lines.Add($"<color=#ffcc66>Tu dois une faveur à {state.NameOf(debt.creditor)}</color>"); }
        foreach (DebtRecord debt in state.debts.Where(d => d.creditor == user)) { lines.Add($"{state.NameOf(debt.debtor)} te doit une faveur"); }
        foreach (TrapRecord trap in state.traps.Where(t => t.team == team))
        {
            Campfire? campfire = Plugin.CampfireList.FirstOrDefault(c => c != null && VersusLocal.CampfireID(c) == trap.campfire);
            string where = campfire != null ? $" à {Vector3.Distance(campfire.transform.position, me.Center) * CharacterStats.unitsToMeters:0} m" : "";
            lines.Add($"<color=#ffaa55>Ton équipe a piégé un feu{where} : « {VersusGame.TrapName(trap.type)} »</color>");
        }
        foreach (EffectRecord effect in state.effects.Where(e => e.team == team)) { lines.Add($"<color=#ff8866>Piège subi : {VersusGame.TrapName(effect.type)}</color>"); }
        if (state.IsPerjured(user)) { lines.Add("<color=#ff4444><b>PARJURE</b> jusqu'au prochain feu</color>"); }
        if (VersusLocal.Instance?.mission is MissionType mission) { lines.Add($"<color=#aaddff>Mission secrète : {VersusLocal.MissionText(mission)}</color>"); }
        VersusItemKind held = VersusItems.HeldKind(me);
        if (held != VersusItemKind.None) { lines.Add($"<color=#ccffcc>{VersusItems.DisplayName(held)} : {VersusItems.Description(held)}</color>"); }
        foreach (string line in VersusLocal.magnifierLines) { lines.Add($"<color=#ffee88>{line}</color>"); }
        if (VersusLocal.Instance != null && VersusLocal.Instance.scissorsProgress > 0f)
        {
            lines.Add($"{(VersusLocal.Instance.scissorsSabotage ? "Sabotage" : "Coupe")} : {VersusLocal.Instance.scissorsProgress:0.0} / {(VersusLocal.Instance.scissorsSabotage ? 3 : 2)} s");
        }
        if (VersusLocal.Instance != null && VersusLocal.Instance.incenseProgress > 0f) { lines.Add($"Incantation : {VersusLocal.Instance.incenseProgress:0.0} / 10 s"); }
        lines.Add($"<size=12>[{Plugin.ConfigurationHandler.PushKey}] pousser  [{Plugin.ConfigurationHandler.AnkleKey}] cheville  [{Plugin.ConfigurationHandler.ShopKey}] boutique  [{Plugin.ConfigurationHandler.ChronicleKey}] scores</size>");
        string text = string.Join("\n", lines);
        float height = boxStyle!.CalcHeight(new GUIContent(text), 420f) + 8f;
        GUI.Box(new Rect(12f, 12f, 420f, height), text, boxStyle);
    }

    void DrawWorldLabels(Character me)
    {
        Camera cam = Camera.main;
        if (cam == null) { return; }
        VersusState state = VersusGame.State;
        string myTeam = VersusGame.TeamKey(me);
        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c == me || c.isBot || c.data.dead) { continue; }
            string user = VersusGame.UserOf(c);
            string team = VersusGame.TeamKey(c);
            bool smoked = team != myTeam && state.effects.Any(e => e.team == team && e.type == TrapType.TellTaleSmoke);
            bool perjured = state.IsPerjured(user);
            if (!smoked && !perjured) { continue; }
            Vector3 screen = cam.WorldToScreenPoint(c.Center + Vector3.up * 1.6f);
            if (screen.z < 0f) { continue; }
            float distance = Vector3.Distance(me.Center, c.Center) * CharacterStats.unitsToMeters;
            string label = perjured ? "PARJURE" : "";
            if (smoked) { label += (label == "" ? "" : "\n") + $"<color=#c080ff>FUMÉE {distance:0} m</color>"; }
            GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 30f, 200f, 50f), label, headStyle);
        }
    }

    void DrawShop(int id)
    {
        VersusState state = VersusGame.State;
        string user = VersusGame.LocalUser;
        Character me = Character.localCharacter;
        string team = me != null ? VersusGame.TeamKey(me) : "";
        GUILayout.Label($"Tes points : <b>{state.Wallet(user)}</b>   Cagnotte d'équipe : <b>{state.Pot(team)}</b>", labelStyle);
        foreach (VersusProduct product in System.Enum.GetValues(typeof(VersusProduct)))
        {
            if (product == VersusProduct.Incense && state.incenseSold) { continue; }
            string source = VersusGame.PaidByTeam(product) ? " (équipe)" : "";
            if (GUILayout.Button($"{VersusGame.ProductName(product)} — {VersusGame.Price(product)} pts{source}"))
            {
                VersusNet.ToMaster(nameof(VersusNet.RPC_RequestPurchase), user, (int)product);
            }
        }
        GUILayout.Space(8);
        GUILayout.Label("Donner 1 point à :", labelStyle);
        foreach (KeyValuePair<string, string> player in state.playerNames.Where(p => p.Key != user))
        {
            if (GUILayout.Button($"{player.Value} ({VersusGame.TeamName(state.TeamOf(player.Key))})"))
            {
                VersusNet.ToMaster(nameof(VersusNet.RPC_RequestGivePoints), user, player.Key, 1);
            }
        }
        if (GUILayout.Button("Fermer")) { shopOpen = false; }
        GUI.DragWindow();
    }

    void DrawChronicle(int id)
    {
        VersusState state = VersusGame.State;
        chronicleScroll = GUILayout.BeginScrollView(chronicleScroll);
        foreach (string team in state.playerTeams.Values.Distinct())
        {
            GUILayout.Label($"<b>{VersusGame.TeamName(team)}</b> — total {state.TeamTotal(team)} pts (cagnotte {state.Pot(team)})", labelStyle);
            foreach (KeyValuePair<string, string> member in state.playerTeams.Where(p => p.Value == team))
            {
                GUILayout.Label($"   {state.NameOf(member.Key)} : {state.Wallet(member.Key)} pts{(state.IsPerjured(member.Key) ? " — PARJURE" : "")}", labelStyle);
            }
        }
        GUILayout.Space(6);
        if (state.highestFallBy != "") { GUILayout.Label($"Plus grosse chute : {state.NameOf(state.highestFallBy)} ({state.highestFall:0} m)", labelStyle); }
        foreach (DebtRecord debt in state.debts) { GUILayout.Label($"Dette non rendue : {state.NameOf(debt.debtor)} → {state.NameOf(debt.creditor)}", labelStyle); }
        GUILayout.Space(6);
        GUILayout.Label("<b>Chronique</b>", labelStyle);
        foreach (string entry in state.chronicle) { GUILayout.Label("• " + entry, labelStyle); }
        GUILayout.EndScrollView();
        if (GUILayout.Button("Fermer")) { chronicleOpen = false; }
        GUI.DragWindow();
    }

    void GiveVanillaButton(string label, System.Func<Item, bool> match)
    {
        if (!GUILayout.Button(label)) { return; }
        KeyValuePair<ushort, Item> entry = SingletonAsset<ItemDatabase>.Instance.itemLookup.OrderBy(e => e.Key).FirstOrDefault(e => e.Value != null && match(e.Value));
        if (entry.Value == null)
        {
            Toast($"Objet introuvable : {label}", 2f);
            return;
        }
        VersusNet.ToMaster(nameof(VersusNet.RPC_GodGiveVanilla), VersusGame.LocalUser, (int)entry.Key);
    }

    void DrawGod(int id)
    {
        string user = VersusGame.LocalUser;
        if (!PhotonNetwork.InRoom)
        {
            GUILayout.Label("Lance d'abord une partie (bouton « Host PEAK Versus »).", labelStyle);
            if (GUILayout.Button("Fermer")) { godOpen = false; }
            GUI.DragWindow();
            return;
        }
        if (!VersusGame.Active)
        {
            GUILayout.Label("<color=#ffaa55>Cette partie n'est pas en mode Versus : les objets Versus seront donnés mais leurs effets ne marcheront pas.</color>", labelStyle);
        }
        GUILayout.Label("<b>Objets Versus</b>", labelStyle);
        GUILayout.BeginHorizontal();
        foreach (VersusItemKind kind in new[] { VersusItemKind.Scissors, VersusItemKind.DefectivePiton, VersusItemKind.Magnifier })
        {
            if (GUILayout.Button(VersusItems.DisplayName(kind))) { VersusNet.ToMaster(nameof(VersusNet.RPC_GodGive), user, (int)kind); }
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        foreach (VersusItemKind kind in new[] { VersusItemKind.Incense, VersusItemKind.CampfireTrap })
        {
            if (GUILayout.Button(VersusItems.DisplayName(kind))) { VersusNet.ToMaster(nameof(VersusNet.RPC_GodGive), user, (int)kind); }
        }
        if (GUILayout.Button("Bing Bong"))
        {
            ushort bingBong = SingletonAsset<ItemDatabase>.Instance.itemLookup.FirstOrDefault(e => e.Value != null && e.Value.itemTags.HasFlag(Item.ItemTags.BingBong)).Key;
            VersusNet.ToMaster(nameof(VersusNet.RPC_GodGiveVanilla), user, (int)bingBong);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("<b>Objets du jeu utiles aux tests</b>", labelStyle);
        GUILayout.BeginHorizontal();
        GiveVanillaButton("Piton normal", i => i.GetComponent<ClimbingSpikeComponent>() != null);
        GiveVanillaButton("Corde", i => i.GetComponent<RopeSpool>() != null);
        GiveVanillaButton("Lance-corde", i => i.GetComponent<RopeShooter>() != null);
        GiveVanillaButton("Effigie de scout", i => i.GetComponent<ScoutEffigy>() != null);
        GUILayout.EndHorizontal();

        GUILayout.Label("<b>Triche</b>", labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+5 points")) { VersusNet.ToMaster(nameof(VersusNet.RPC_GodPoints), user, 5); }
        infiniteStamina = GUILayout.Toggle(infiniteStamina, "Endurance infinie");
        GUILayout.EndHorizontal();
        bool solo = GUILayout.Toggle(VersusGame.State.soloTest, "Test solo : mes pièges, cordes et pitons me ciblent");
        if (solo != VersusGame.State.soloTest) { VersusNet.ToMaster(nameof(VersusNet.RPC_GodSoloTest), solo); }
        GUILayout.BeginHorizontal();
        foreach (BiomeModifier modifier in System.Enum.GetValues(typeof(BiomeModifier)))
        {
            if (GUILayout.Button(modifier.ToString())) { VersusNet.ToMaster(nameof(VersusNet.RPC_GodModifier), (int)modifier); }
        }
        GUILayout.EndHorizontal();
        Character me = Character.localCharacter;
        if (me != null && GUILayout.Button("Simuler mon arrivée au feu le plus proche (+ déclenche son piège)"))
        {
            Campfire? nearest = Plugin.CampfireList.Where(c => c != null).OrderBy(c => Vector3.Distance(c.transform.position, me.Center)).FirstOrDefault();
            if (nearest != null)
            {
                VersusNet.ToMaster(nameof(VersusNet.RPC_ReportArrival), user, VersusLocal.CampfireID(nearest), (int)nearest.advanceToSegment, !me.data.dead,
                    VersusItems.HasInInventory(me, VersusItemKind.Magnifier), VersusItems.HasBingBong(me));
            }
        }

        GUILayout.Label("<b>Objets du jeu</b> (filtre)", labelStyle);
        vanillaFilter = GUILayout.TextField(vanillaFilter);
        godScroll = GUILayout.BeginScrollView(godScroll, GUILayout.Height(260));
        foreach (KeyValuePair<ushort, Item> entry in SingletonAsset<ItemDatabase>.Instance.itemLookup.OrderBy(e => e.Value != null ? e.Value.name : ""))
        {
            if (entry.Value == null) { continue; }
            string name = entry.Value.name;
            if (vanillaFilter != "" && !name.ToLower().Contains(vanillaFilter.ToLower())) { continue; }
            if (GUILayout.Button($"{name} ({entry.Key})")) { VersusNet.ToMaster(nameof(VersusNet.RPC_GodGiveVanilla), user, (int)entry.Key); }
        }
        GUILayout.EndScrollView();
        if (GUILayout.Button("Fermer")) { godOpen = false; }
        GUI.DragWindow();
    }
}
