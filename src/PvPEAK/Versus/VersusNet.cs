using Newtonsoft.Json;
using Photon.Pun;
using UnityEngine;

namespace PvPEAK.Versus;

// Lives on every Player object next to its PhotonView, like PvPEAK's Networking component.
// Requests go to the master client, which owns VersusState and broadcasts it back.
public class VersusNet : MonoBehaviourPun
{
    public PhotonView view = null!;

    public static VersusNet? Local
    {
        get
        {
            Player player = Player.localPlayer;
            return player != null ? player.GetComponent<VersusNet>() : null;
        }
    }

    public static VersusNet? ForUser(string userId)
    {
        return Plugin.player_mapping.TryGetValue(userId, out Player player) && player != null ? player.GetComponent<VersusNet>() : null;
    }

    public void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    public static void ToMaster(string method, params object[] args)
    {
        VersusNet? local = Local;
        if (local == null) { return; }
        local.view.RPC(method, RpcTarget.MasterClient, args);
    }

    public static void ToAll(string method, params object[] args)
    {
        VersusNet? local = Local;
        if (local == null) { return; }
        local.view.RPC(method, RpcTarget.All, args);
    }

    // ---------- Master -> everyone ----------

    [PunRPC]
    public void RPC_SyncState(string json)
    {
        VersusGame.State = JsonConvert.DeserializeObject<VersusState>(json) ?? new VersusState();
    }

    [PunRPC]
    public void RPC_Announce(string text, float duration, string onlyUser)
    {
        if (onlyUser != "" && onlyUser != VersusGame.LocalUser) { return; }
        VersusHud.Toast(text, duration);
    }

    [PunRPC]
    public void RPC_BreakPiton(int viewID)
    {
        PhotonView pitonView = PhotonView.Find(viewID);
        if (pitonView == null) { return; }
        ClimbHandle handle = pitonView.GetComponent<ClimbHandle>();
        if (handle == null) { return; }
        GamefeelHandler.instance.AddPerlinShakeProximity(handle.transform.position, 4f);
        handle.Break();
    }

    [PunRPC]
    public void RPC_TrapBurst(string team, int type)
    {
        VersusLocal.OnTrapBurst(team, (TrapType)type);
    }

    [PunRPC]
    public void RPC_ApplyHeal(string user)
    {
        if (user != VersusGame.LocalUser) { return; }
        Character me = Character.localCharacter;
        if (me == null) { return; }
        me.refs.afflictions.SubtractStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.3f);
        me.refs.afflictions.SubtractStatus(CharacterAfflictions.STATUSTYPE.Hunger, 0.3f);
        me.AddStamina(0.5f);
    }

    // ---------- Attacker -> target's own client ----------

    [PunRPC]
    public void RPC_Pushed(Vector3 force, string attacker)
    {
        VersusLocal.OnPushed(force, attacker);
    }

    [PunRPC]
    public void RPC_AnkleGrabbed(Vector3 direction, string attacker)
    {
        VersusLocal.OnAnkleGrabbed(direction, attacker);
    }

    [PunRPC]
    public void RPC_Zip()
    {
        VersusHud.Toast("Bruit de fermeture éclair dans ton dos…", 3f);
    }

    // ---------- Anyone -> master ----------

    [PunRPC] public void RPC_ReportArrival(string user, int campfire, int weight, bool alive, bool hasMagnifier, bool hasBingBong) => VersusGame.MasterArrival(user, campfire, weight, alive, hasMagnifier, hasBingBong);
    [PunRPC] public void RPC_ReportHelp(string helper, string target, bool inDanger) => VersusGame.MasterHelp(helper, target, inDanger);
    [PunRPC] public void RPC_ReportAttack(string attacker, string target, int kind) => VersusGame.MasterAttack(attacker, target, kind);
    [PunRPC] public void RPC_ReportMission(string user, string mission) => VersusGame.MasterMission(user, mission);
    [PunRPC] public void RPC_ReportFall(string user, float meters) => VersusGame.MasterFall(user, meters);
    [PunRPC] public void RPC_ReportPickpocket(string thief, string victim) => VersusGame.MasterNote($"{VersusGame.State.NameOf(thief)} a fouillé le sac de {VersusGame.State.NameOf(victim)}");
    [PunRPC] public void RPC_Note(string text) => VersusGame.MasterNote(text);
    [PunRPC] public void RPC_ReportDeath(string user) => VersusGame.MasterNote($"{VersusGame.State.NameOf(user)} est mort");
    [PunRPC] public void RPC_RequestPurchase(string user, int product) => VersusGame.MasterPurchase(user, (VersusProduct)product);
    [PunRPC] public void RPC_RequestGivePoints(string from, string to, int amount) => VersusGame.MasterGivePoints(from, to, amount);
    [PunRPC] public void RPC_ArmTrap(string user, int campfire) => VersusGame.MasterArmTrap(user, campfire);
    [PunRPC] public void RPC_MarkSabotage(int ropeView, string team) => VersusGame.MasterMarkSabotage(ropeView, team);
    [PunRPC] public void RPC_ClearSabotage(int ropeView) => VersusGame.MasterClearSabotage(ropeView);
    [PunRPC] public void RPC_MarkPiton(int pitonView, string team) => VersusGame.MasterMarkPiton(pitonView, team);
    [PunRPC] public void RPC_ClearPiton(int pitonView) => VersusGame.MasterClearPiton(pitonView);
    [PunRPC] public void RPC_GodGive(string user, int kind) => VersusGame.MasterGiveVersusItem(user, (VersusItemKind)kind, refund: 0);
    [PunRPC] public void RPC_GodGiveVanilla(string user, int itemID) => VersusGame.MasterGiveVanilla(user, (ushort)itemID);
    [PunRPC] public void RPC_GodPoints(string user, int amount) => VersusGame.MasterGodPoints(user, amount);
    [PunRPC] public void RPC_GodSoloTest(bool on) => VersusGame.MasterSoloTest(on);
    [PunRPC] public void RPC_GodModifier(int modifier) => VersusGame.MasterSetModifier((BiomeModifier)modifier);
}
