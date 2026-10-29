using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Photon.Pun;
using PvPEAK.Model;
using UnityEngine;

namespace PvPEAK.Versus;

public enum VersusProduct
{
    Magnifier = 0,
    Heal = 1,
    Scissors = 2,
    DefectivePiton = 3,
    CampfireTrap = 4,
    RerollModifier = 5,
    Incense = 6,
}

public static class VersusGame
{
    public const float TruceRadius = 15f;
    public const float BetrayalWindow = 15f;
    public const int FairPlayCapPerBiome = 2;
    public const double TrapMinSeconds = 180;
    public const double TrapMaxSeconds = 600;
    public const int CatchUpGap = 8;

    public static VersusState State = new();

    // Master-only bookkeeping that does not need to be synced.
    static readonly Dictionary<string, float> lastHelp = new();

    public static bool Active => Plugin.ItsPvPEAK;
    public static bool IsMaster => PhotonNetwork.IsMasterClient;
    public static string LocalUser => PhotonNetwork.LocalPlayer?.UserId ?? "";

    public static string UserOf(Character character) => character.photonView.Owner.UserId;

    public static string TeamKey(Character character)
    {
        Team team = Plugin.teamTracker.GetPlayerTeam(character.player);
        return team == null ? "" : ColorUtility.ToHtmlStringRGB(team.teamColor);
    }

    public static string TeamName(string team)
    {
        if (team == "") { return "Sans équipe"; }
        return $"<color=#{team}>Équipe {ColorName(team)}</color>";
    }

    // Scout colours are arbitrary RGB values: name them by hue so players can read them.
    public static string ColorName(string team)
    {
        if (!ColorUtility.TryParseHtmlString("#" + team, out Color color)) { return team; }
        Color.RGBToHSV(color, out float h, out float s, out float v);
        if (v < 0.2f) { return "Noire"; }
        if (s < 0.2f) { return v > 0.8f ? "Blanche" : "Grise"; }
        float deg = h * 360f;
        if (deg < 15f || deg >= 340f) { return "Rouge"; }
        if (deg < 45f) { return "Orange"; }
        if (deg < 70f) { return "Jaune"; }
        if (deg < 160f) { return "Verte"; }
        if (deg < 200f) { return "Turquoise"; }
        if (deg < 255f) { return "Bleue"; }
        if (deg < 290f) { return "Violette"; }
        return "Rose";
    }

    // Solo test mode lets your own traps, ropes and pitons hit you.
    public static bool IsEnemy(string teamA, string teamB) => teamA != teamB || State.soloTest;
    public static bool IsEnemyUser(string a, string b) => a != b && IsEnemy(State.TeamOf(a), State.TeamOf(b)) || (a == b && State.soloTest);

    public static double Now => PhotonNetwork.Time;

    public static void Reset()
    {
        State = new VersusState();
        lastHelp.Clear();
    }

    // ---------- helpers ----------

    static void RefreshRoster()
    {
        foreach (Character character in PlayerHandler.GetAllPlayerCharacters())
        {
            if (character == null || character.isBot) { continue; }
            string user = UserOf(character);
            State.playerTeams[user] = TeamKey(character);
            State.playerNames[user] = character.photonView.Owner.NickName;
        }
    }

    public static void Broadcast()
    {
        if (!IsMaster) { return; }
        RefreshRoster();
        string json = JsonConvert.SerializeObject(State);
        VersusNet.ToAll(nameof(VersusNet.RPC_SyncState), json);
    }

    public static void Announce(string text, float duration = 5f, string onlyUser = "")
    {
        VersusNet.ToAll(nameof(VersusNet.RPC_Announce), text, duration, onlyUser);
    }

    public static void MasterNote(string text)
    {
        if (!IsMaster) { return; }
        State.chronicle.Add(text);
        Broadcast();
    }

    static void AddPoints(string user, int amount)
    {
        State.wallets[user] = Mathf.Max(0, State.Wallet(user) + amount);
    }

    static IEnumerable<string> TeamMembers(string team) => State.playerTeams.Where(p => p.Value == team).Select(p => p.Key);

    static Character? CharacterOf(string user)
    {
        return Plugin.player_mapping.TryGetValue(user, out Player player) && player != null ? player.character : null;
    }

    static BiomeModifier RollModifier()
    {
        BiomeModifier[] all = [BiomeModifier.Storm, BiomeModifier.Silence, BiomeModifier.Fog, BiomeModifier.SoftGravity, BiomeModifier.GoldFever, BiomeModifier.TrapSale];
        BiomeModifier next;
        do { next = all[Random.Range(0, all.Length)]; } while (next == State.modifier && all.Length > 1);
        return next;
    }

    public static string ModifierName(BiomeModifier modifier) => modifier switch
    {
        BiomeModifier.Storm => "Tempête : les voix adverses portent plus loin",
        BiomeModifier.Silence => "Silence : plus de voix entre équipes",
        BiomeModifier.Fog => "Brouillard : noms des adversaires masqués",
        BiomeModifier.SoftGravity => "Gravité molle : chutes moins douloureuses",
        BiomeModifier.GoldFever => "Fièvre de l'or : points des feux doublés",
        BiomeModifier.TrapSale => "Pièges en promo : piège de feu à 2 pts",
        _ => "Aucun",
    };

    public static string TrapName(TrapType type) => type switch
    {
        TrapType.Hiccups => "Hoquet",
        TrapType.ItchingPowder => "Poil à gratter",
        TrapType.SpringLog => "Bûche-ressort",
        TrapType.Famine => "Fringale",
        TrapType.TellTaleSmoke => "Fumée révélatrice",
        _ => "?",
    };

    // ---------- master handlers ----------

    public static void MasterArrival(string user, int campfire, int weight, bool alive, bool hasMagnifier, bool hasBingBong)
    {
        if (!IsMaster) { return; }
        RefreshRoster();
        if (!State.arrivals.TryGetValue(user, out List<int> list))
        {
            list = new List<int>();
            State.arrivals[user] = list;
        }
        if (list.Contains(campfire))
        {
            if (State.soloTest)
            {
                CheckTrap(user, State.TeamOf(user), campfire, hasMagnifier);
                Broadcast();
            }
            return;
        }
        bool firstAnyone = !State.arrivals.Values.Any(l => l.Contains(campfire));
        list.Add(campfire);
        string team = State.TeamOf(user);
        string name = State.NameOf(user);

        State.perjuredUntilArrival.Remove(user);
        State.fairPlayThisBiome[user] = 0;

        if (firstAnyone)
        {
            State.modifier = RollModifier();
            Announce($"Nouveau biome — {ModifierName(State.modifier)}", 8f);
        }

        if (alive)
        {
            int points = Mathf.Clamp(weight, 1, 3) * (State.modifier == BiomeModifier.GoldFever ? 2 : 1);
            AddPoints(user, points);
            Announce($"Feu de camp atteint : +{points} pts", 4f, user);
        }
        else
        {
            Announce("Arrivé au feu en étant mort : pas de points", 4f, user);
        }

        if (hasBingBong)
        {
            AddPoints(user, 2);
            Announce($"{name} rapporte Bing Bong au feu : +2 pts", 5f);
            State.chronicle.Add($"{name} a ramené Bing Bong au feu");
        }

        // Team complete at this campfire?
        if (team != "" && TeamMembers(team).All(m => State.arrivals.TryGetValue(m, out var l) && l.Contains(campfire)))
        {
            if (!State.campfireTeamsComplete.TryGetValue(campfire, out List<string> complete))
            {
                complete = new List<string>();
                State.campfireTeamsComplete[campfire] = complete;
            }
            if (!complete.Contains(team))
            {
                complete.Add(team);
                if (complete.Count == 1)
                {
                    State.teamPots[team] = State.Pot(team) + 1;
                    Announce($"{TeamName(team)} arrive complète la première : +1 pt d'équipe", 5f);
                }
                else
                {
                    CatchUp(campfire);
                }
            }
        }

        CheckTrap(user, team, campfire, hasMagnifier);

        // Effects end when the team reaches a new campfire after the minimum duration
        State.effects.RemoveAll(e => e.team == team && Now - e.start >= TrapMinSeconds);

        Broadcast();
    }

    static void CheckTrap(string user, string team, int campfire, bool hasMagnifier)
    {
        string name = State.NameOf(user);
        TrapRecord? trap = State.traps.FirstOrDefault(t => t.campfire == campfire);
        if (trap != null && IsEnemy(team, trap.team))
        {
            State.traps.Remove(trap);
            if (hasMagnifier)
            {
                AddPoints(user, 1);
                MasterGiveVersusItem(user, VersusItemKind.CampfireTrap, refund: 0);
                Announce($"{name} a désamorcé un piège de feu ({TrapName(trap.type)}) : +1 pt et piège récupéré !", 6f);
                State.chronicle.Add($"{name} a désamorcé le piège « {TrapName(trap.type)} » de l'{TeamName(trap.team)}");
            }
            else
            {
                State.effects.RemoveAll(e => e.team == team);
                State.effects.Add(new EffectRecord { team = team, type = trap.type, start = Now });
                Announce($"PIÈGE ! L'{TeamName(team)} subit « {TrapName(trap.type)} » jusqu'au prochain feu", 8f);
                State.chronicle.Add($"L'{TeamName(team)} est tombée dans le piège « {TrapName(trap.type)} » de l'{TeamName(trap.team)}");
                VersusNet.ToAll(nameof(VersusNet.RPC_TrapBurst), team, (int)trap.type);
            }
        }

    }

    static void CatchUp(int campfire)
    {
        List<string> teams = State.playerTeams.Values.Distinct().Where(t => t != "").ToList();
        if (teams.Count < 2) { return; }
        string leader = teams.OrderByDescending(State.TeamTotal).First();
        string trailer = teams.OrderBy(State.TeamTotal).First();
        if (State.TeamTotal(leader) - State.TeamTotal(trailer) < CatchUpGap) { return; }
        foreach (string member in TeamMembers(trailer))
        {
            AddPoints(member, 2);
        }
        Announce($"Rattrapage : l'{TeamName(trailer)} reçoit +2 pts par joueur", 6f);
    }

    public static void MasterTick()
    {
        if (!IsMaster) { return; }
        if (State.effects.RemoveAll(e => Now - e.start >= TrapMaxSeconds) > 0)
        {
            Broadcast();
        }
    }

    public static void MasterHelp(string helper, string target, bool inDanger)
    {
        if (!IsMaster) { return; }
        RefreshRoster();
        lastHelp[helper + "|" + target] = Time.time;
        if (!IsEnemyUser(helper, target) || helper == target || !inDanger) { return; }
        string helperName = State.NameOf(helper);
        string targetName = State.NameOf(target);

        DebtRecord? owed = State.FindDebt(helper, target);
        if (owed != null)
        {
            State.debts.Remove(owed);
            AddPoints(helper, 2);
            AddPoints(target, 1);
            Announce($"{helperName} a rendu sa dette à {targetName} : +1 pt chacun (et +1 pour le geste)", 6f);
            State.chronicle.Add($"{helperName} a remboursé sa dette d'honneur envers {targetName}");
        }
        else
        {
            int earned = State.fairPlayThisBiome.TryGetValue(helper, out int v) ? v : 0;
            if (earned < FairPlayCapPerBiome)
            {
                AddPoints(helper, 1);
                State.fairPlayThisBiome[helper] = earned + 1;
                Announce($"Beau jeu ! Tu as aidé {targetName} : +1 pt", 4f, helper);
            }
            if (!State.HasDebtBetween(helper, target))
            {
                State.debts.Add(new DebtRecord { debtor = target, creditor = helper });
                Announce($"Tu as une dette d'honneur envers {helperName}", 5f, target);
                State.chronicle.Add($"{helperName} a aidé son adversaire {targetName}");
            }
        }
        Broadcast();
    }

    public static void MasterAttack(string attacker, string target, int kind)
    {
        if (!IsMaster) { return; }
        bool recentHelp = lastHelp.TryGetValue(attacker + "|" + target, out float when) && Time.time - when <= BetrayalWindow;
        bool owesTarget = State.FindDebt(attacker, target) != null;
        if (!(recentHelp || owesTarget) || attacker == target) { return; }
        string attackerName = State.NameOf(attacker);
        string targetName = State.NameOf(target);
        int lost = State.fairPlayThisBiome.TryGetValue(attacker, out int v) ? v : 0;
        AddPoints(attacker, -lost);
        State.fairPlayThisBiome[attacker] = 0;
        State.perjuredUntilArrival[attacker] = Now;
        string how = kind == 1 ? "en le tirant par la cheville" : "en le poussant";
        Announce($"TRAHISON ! {attackerName} a trahi {targetName} {how}", 8f);
        State.chronicle.Add($"TRAHISON : {attackerName} a trahi {targetName} {how}");
        Broadcast();
    }

    public static void MasterMission(string user, string mission)
    {
        if (!IsMaster) { return; }
        AddPoints(user, 2);
        Announce($"Mission secrète réussie : {mission} (+2 pts)", 5f, user);
        State.chronicle.Add($"{State.NameOf(user)} a réussi sa mission secrète : {mission}");
        Broadcast();
    }

    public static void MasterFall(string user, float meters)
    {
        if (!IsMaster || meters <= State.highestFall) { return; }
        State.highestFall = meters;
        State.highestFallBy = user;
        Broadcast();
    }

    public static int Price(VersusProduct product) => product switch
    {
        VersusProduct.Magnifier => 2,
        VersusProduct.Heal => 2,
        VersusProduct.Scissors => 3,
        VersusProduct.DefectivePiton => 3,
        VersusProduct.CampfireTrap => State.modifier == BiomeModifier.TrapSale ? 2 : 4,
        VersusProduct.RerollModifier => 4,
        VersusProduct.Incense => 10,
        _ => 99,
    };

    public static bool PaidByTeam(VersusProduct product) => product == VersusProduct.CampfireTrap || product == VersusProduct.RerollModifier;

    public static string ProductName(VersusProduct product) => product switch
    {
        VersusProduct.Magnifier => "Loupe",
        VersusProduct.Heal => "Petit soin",
        VersusProduct.Scissors => "Ciseaux",
        VersusProduct.DefectivePiton => "Piton défectueux",
        VersusProduct.CampfireTrap => "Piège de feu",
        VersusProduct.RerollModifier => "Relancer le modificateur",
        VersusProduct.Incense => "Encens du retour",
        _ => "?",
    };

    public static void MasterPurchase(string user, VersusProduct product)
    {
        if (!IsMaster) { return; }
        RefreshRoster();
        int price = Price(product);
        string team = State.TeamOf(user);
        if (product == VersusProduct.Incense && State.incenseSold)
        {
            Announce("L'encens du retour a déjà été vendu dans cette partie", 4f, user);
            return;
        }
        bool fromTeam = PaidByTeam(product);
        int funds = fromTeam ? State.Pot(team) : State.Wallet(user);
        if (funds < price)
        {
            Announce($"Pas assez de points ({funds}/{price}{(fromTeam ? ", cagnotte d'équipe" : "")})", 4f, user);
            return;
        }
        if (fromTeam) { State.teamPots[team] = funds - price; } else { AddPoints(user, -price); }

        switch (product)
        {
            case VersusProduct.Heal:
                VersusNet.ToAll(nameof(VersusNet.RPC_ApplyHeal), user);
                break;
            case VersusProduct.RerollModifier:
                State.modifier = RollModifier();
                Announce($"Modificateur relancé — {ModifierName(State.modifier)}", 6f);
                break;
            default:
                VersusItemKind kind = product switch
                {
                    VersusProduct.Magnifier => VersusItemKind.Magnifier,
                    VersusProduct.Scissors => VersusItemKind.Scissors,
                    VersusProduct.DefectivePiton => VersusItemKind.DefectivePiton,
                    VersusProduct.CampfireTrap => VersusItemKind.CampfireTrap,
                    _ => VersusItemKind.Incense,
                };
                if (!MasterGiveVersusItem(user, kind, refund: 0))
                {
                    if (fromTeam) { State.teamPots[team] = State.Pot(team) + price; } else { AddPoints(user, price); }
                    Announce("Inventaire plein : achat remboursé", 4f, user);
                    Broadcast();
                    return;
                }
                if (product == VersusProduct.Incense) { State.incenseSold = true; }
                break;
        }
        Announce($"Acheté : {ProductName(product)}", 3f, user);
        Broadcast();
    }

    public static bool MasterGiveVersusItem(string user, VersusItemKind kind, int refund)
    {
        if (!IsMaster) { return false; }
        if (!VersusItems.TryGetBaseItem(kind, out ushort itemID)) { return false; }
        if (!Plugin.player_mapping.TryGetValue(user, out Player player) || player == null) { return false; }
        bool added = player.AddItem(itemID, VersusItems.CreateData(kind), out ItemSlot _);
        if (!added && refund > 0) { AddPoints(user, refund); }
        return added;
    }

    public static void MasterGiveVanilla(string user, ushort itemID)
    {
        if (!IsMaster) { return; }
        if (Plugin.player_mapping.TryGetValue(user, out Player player) && player != null)
        {
            player.AddItem(itemID, null!, out ItemSlot _);
        }
    }

    public static void MasterGivePoints(string from, string to, int amount)
    {
        if (!IsMaster || amount <= 0 || from == to) { return; }
        if (State.Wallet(from) < amount)
        {
            Announce("Pas assez de points à donner", 3f, from);
            return;
        }
        AddPoints(from, -amount);
        AddPoints(to, amount);
        Announce($"{State.NameOf(from)} t'a donné {amount} pt(s)", 4f, to);
        Announce($"Tu as donné {amount} pt(s) à {State.NameOf(to)}", 3f, from);
        Broadcast();
    }

    public static void MasterArmTrap(string user, int campfire)
    {
        if (!IsMaster) { return; }
        RefreshRoster();
        if (State.traps.Any(t => t.campfire == campfire))
        {
            Announce("Ce feu est déjà piégé", 3f, user);
            return;
        }
        TrapType type = (TrapType)Random.Range(0, 5);
        State.traps.Add(new TrapRecord { campfire = campfire, team = State.TeamOf(user), type = type });
        Announce($"Feu piégé : « {TrapName(type)} » attend l'équipe adverse", 5f, user);
        State.chronicle.Add($"{State.NameOf(user)} a piégé un feu de camp (« {TrapName(type)} »)");
        Broadcast();
    }

    public static void MasterMarkSabotage(int ropeView, string team)
    {
        if (!IsMaster) { return; }
        State.sabotagedRopes[ropeView] = team;
        Broadcast();
    }

    public static void MasterClearSabotage(int ropeView)
    {
        if (!IsMaster) { return; }
        if (State.sabotagedRopes.Remove(ropeView)) { Broadcast(); }
    }

    public static void MasterMarkPiton(int pitonView, string team)
    {
        if (!IsMaster) { return; }
        State.defectivePitons[pitonView] = team;
        Broadcast();
    }

    public static void MasterClearPiton(int pitonView)
    {
        if (!IsMaster) { return; }
        if (State.defectivePitons.Remove(pitonView)) { Broadcast(); }
    }

    public static void MasterGodPoints(string user, int amount)
    {
        if (!IsMaster) { return; }
        AddPoints(user, amount);
        string team = State.TeamOf(user);
        State.teamPots[team] = State.Pot(team) + Mathf.Max(0, amount / 2);
        Broadcast();
    }

    public static void MasterSoloTest(bool on)
    {
        if (!IsMaster) { return; }
        State.soloTest = on;
        Announce(on ? "Mode test solo : tes propres pièges te ciblent" : "Mode test solo désactivé", 4f);
        Broadcast();
    }

    public static void MasterSetModifier(BiomeModifier modifier)
    {
        if (!IsMaster) { return; }
        State.modifier = modifier;
        Announce($"Modificateur — {ModifierName(modifier)}", 5f);
        Broadcast();
    }

    // Called on the master right before the game ends.
    public static void MasterEndGame()
    {
        if (!IsMaster) { return; }
        RefreshRoster();
        List<Character> players = PlayerHandler.GetAllPlayerCharacters().Where(c => c != null && !c.isBot).ToList();
        List<Character> escaped = players.Where(Character.CheckWinCondition).ToList();
        HashSet<string> escapedUsers = new(escaped.Select(UserOf));
        List<string> teams = State.playerTeams.Values.Distinct().Where(t => t != "").ToList();
        List<string> completeTeams = teams.Where(t => TeamMembers(t).All(escapedUsers.Contains)).ToList();

        string verdict;
        if (escaped.Count == 0)
        {
            verdict = "Personne ne s'est échappé : tout le monde a perdu.";
        }
        else if (escaped.Count == players.Count && teams.Count > 1)
        {
            verdict = "PACTE DES GRIMPEURS ! Tout le monde s'est échappé ensemble : victoire partagée, gg à tous.";
        }
        else if (completeTeams.Count == 1)
        {
            verdict = $"Victoire de l'{TeamName(completeTeams[0])} !";
        }
        else
        {
            string best = teams.OrderByDescending(State.TeamTotal).First();
            verdict = $"Extraction mixte : victoire aux points de l'{TeamName(best)} ({State.TeamTotal(best)} pts).";
        }
        State.chronicle.Add(verdict);
        State.finished = true;
        Announce(verdict, 15f);
        Broadcast();
    }
}
