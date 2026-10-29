using System.Collections.Generic;
using System.Linq;

namespace PvPEAK.Versus;

public enum TrapType
{
    Hiccups = 0,      // petits sauts réguliers
    ItchingPowder = 1, // l'endurance fond par à-coups
    SpringLog = 2,    // éjection immédiate, puis secousses
    Famine = 3,       // la faim monte doucement
    TellTaleSmoke = 4, // la position de l'équipe est révélée aux adversaires
    FrozenFire = 5,   // les feux ne réchauffent ni ne soignent plus l'équipe
}

public enum BiomeModifier
{
    None = 0,
    Storm = 1,      // les voix adverses portent plus loin
    Silence = 2,    // plus de voix entre équipes
    Fog = 3,        // noms des adversaires masqués
    SoftGravity = 4, // dégâts de chute réduits
    GoldFever = 5,  // points des feux doublés
    TrapSale = 6,   // pièges de feu à moitié prix
}

public enum HazardMode
{
    Vanilla = 0,
    Rising = 1,
    Paused = 2,
}

// Host settings, kept across games in the same lobby.
public class VersusOptions
{
    public bool spawnSeparated = false;
    public int biomes = 0;              // 0 = all the way to the peak, otherwise stop after N biomes
    public bool carryScores = false;
    public int startTruceSeconds = 90;  // same-spawn mode only
}

public class DebtRecord
{
    public string debtor = "";
    public string creditor = "";
}

public class TrapRecord
{
    public int campfire;
    public string team = "";
    public TrapType type;
}

public class EffectRecord
{
    public string team = "";
    public TrapType type;
    public double start;
}

// Whole-game state. The master client owns it and broadcasts it as JSON after every change.
public class VersusState
{
    public Dictionary<string, int> wallets = new();
    public Dictionary<string, int> teamPots = new();
    public Dictionary<string, string> playerTeams = new();
    public Dictionary<string, string> playerNames = new();
    public Dictionary<string, int> fairPlayThisBiome = new();
    public Dictionary<string, List<int>> arrivals = new();
    public Dictionary<int, List<string>> campfireTeamsComplete = new();
    public Dictionary<string, double> perjuredUntilArrival = new();
    public List<DebtRecord> debts = new();
    public List<TrapRecord> traps = new();
    public List<EffectRecord> effects = new();
    public Dictionary<int, string> sabotagedRopes = new();
    public Dictionary<int, string> defectivePitons = new();
    public List<string> chronicle = new();
    public BiomeModifier modifier = BiomeModifier.None;
    public bool incenseSold = false;
    public bool soloTest = false;
    public bool finished = false;
    public VersusOptions options = new();

    // Rising hazard vote, opened at each new campfire
    public HazardMode hazardMode = HazardMode.Vanilla;
    public int voteCampfire = -1;
    public double voteDeadline;
    public Dictionary<string, bool> votes = new();
    public Dictionary<string, int> extraVotes = new();

    // Early finish when the run is shorter than the whole mountain
    public int finalCampfire = -1;
    public double finalCountdownEnd;
    public float highestFall = 0f;
    public string highestFallBy = "";

    public int Wallet(string user) => wallets.TryGetValue(user, out int v) ? v : 0;
    public int Pot(string team) => teamPots.TryGetValue(team, out int v) ? v : 0;
    public string TeamOf(string user) => playerTeams.TryGetValue(user, out string t) ? t : "";
    public string NameOf(string user) => playerNames.TryGetValue(user, out string n) ? n : "?";
    public bool IsPerjured(string user) => perjuredUntilArrival.ContainsKey(user);

    public int TeamTotal(string team)
    {
        return Pot(team) + playerTeams.Where(p => p.Value == team).Sum(p => Wallet(p.Key));
    }

    public DebtRecord? FindDebt(string debtor, string creditor)
    {
        return debts.FirstOrDefault(d => d.debtor == debtor && d.creditor == creditor);
    }

    public bool HasDebtBetween(string a, string b)
    {
        return debts.Any(d => (d.debtor == a && d.creditor == b) || (d.debtor == b && d.creditor == a));
    }
}
