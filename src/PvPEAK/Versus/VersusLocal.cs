using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using Zorro.Core;

namespace PvPEAK.Versus;

public enum MissionType
{
    PushEnemy,
    GrabEnemyAnkle,
    Pickpocket,
    GetCarried,
    SurviveBigFall,
    BringBingBong,
}

// Everything the local player does or suffers, run every frame on a DontDestroyOnLoad object.
public class VersusLocal : MonoBehaviour
{
    public static VersusLocal? Instance;

    // Tuning
    const float PushRange = 2.5f;
    const float PushStamina = 0.25f;
    const float PushCooldown = 1.5f;
    const float AnkleStamina = 0.3f;
    const float AnkleCooldown = 3f;
    const float AnkleReactionWindow = 0.7f;
    const float AnkleImmunity = 4f;
    const float IncenseChannel = 10f;
    const float ArrivalRadius = 10f;
    const float RopeCrackAfter = 2.5f;
    const float RopeBreakAfter = 3.5f;
    const float PitonCrackAfter = 0.8f;
    const float PitonBreakAfter = 2f;

    // Shared tracking used by patches
    public static readonly Dictionary<Character, Vector3> lastGroundPosition = new();
    public static readonly Dictionary<Character, Vector3> deathPosition = new();

    float pushCooldownUntil;
    float ankleCooldownUntil;
    float ankleImmuneUntil;
    float anklePendingUntil = -1f;
    Vector3 ankleDirection;
    string ankleAttacker = "";
    public float incenseProgress;
    public float scissorsProgress;
    public bool scissorsSabotage;
    Rope? scissorsRope;
    float nextTint;
    readonly HashSet<int> tintedItems = new();
    float ropeTimer;
    bool ropeCracked;
    float pitonTimer;
    bool pitonCracked;
    float nextArrivalCheck;
    float nextEffectTick;
    float nextMasterTick;
    readonly HashSet<int> reportedArrivals = new();
    readonly Dictionary<Character, float> helpTimers = new();
    readonly Dictionary<string, float> helpReportedAt = new();

    // Missions
    public MissionType? mission;
    bool missionDone;
    float carriedDistance;
    Vector3 lastCarriedPos;
    bool airborne;
    float airborneMaxY;

    public static readonly List<string> magnifierLines = new();

    public static void Create()
    {
        if (Instance != null) { return; }
        GameObject go = new GameObject("PeakVersus");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<VersusLocal>();
        go.AddComponent<VersusHud>();
    }

    public static void ResetLocal()
    {
        lastGroundPosition.Clear();
        deathPosition.Clear();
        if (Instance == null) { return; }
        Instance.reportedArrivals.Clear();
        Instance.helpTimers.Clear();
        Instance.helpReportedAt.Clear();
        Instance.mission = null;
        Instance.missionDone = false;
        Instance.incenseProgress = 0f;
    }

    static bool KeyPressed(string keyName)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) { return false; }
        if (!System.Enum.TryParse(keyName, true, out Key key) || key == Key.None) { return false; }
        return keyboard[key].wasPressedThisFrame;
    }

    public static bool InTruce(Vector3 position) => Utility.DistanceToCampfire(position, out Campfire _) <= VersusGame.TruceRadius;

    static float StaminaFraction(Character character)
    {
        return character.data.currentStamina / Mathf.Max(0.01f, character.GetMaxStamina());
    }

    static string MyTeam => Character.localCharacter != null ? VersusGame.TeamKey(Character.localCharacter) : "";

    static bool IsCampfireUsable(Campfire campfire)
    {
        return campfire != null && campfire.transform.parent != null && !campfire.transform.parent.name.ToLower().Contains("wings");
    }

    public static int CampfireID(Campfire campfire)
    {
        PhotonView view = campfire.GetComponent<PhotonView>();
        return view != null ? view.ViewID : campfire.GetInstanceID();
    }

    void Update()
    {
        // God and chronicle keys work as soon as a run exists, even outside Versus for solo testing.
        if (KeyPressed(Plugin.ConfigurationHandler.GodMenuKey)) { VersusHud.ToggleGod(); }
        if (KeyPressed(Plugin.ConfigurationHandler.ChronicleKey)) { VersusHud.ToggleChronicle(); }

        UpdateTints();
        Character me = Character.localCharacter;
        if (me == null || !VersusGame.Active) { return; }

        foreach (Character character in Character.AllCharacters)
        {
            if (character != null && character.data.isGrounded && !character.data.dead)
            {
                lastGroundPosition[character] = character.Center;
            }
        }

        if (Time.time > nextMasterTick)
        {
            nextMasterTick = Time.time + 5f;
            VersusGame.MasterTick();
        }

        if (mission == null) { AssignMission(); }

        if (KeyPressed(Plugin.ConfigurationHandler.ShopKey)) { VersusHud.ToggleShop(); }

        if (me.data.dead) { incenseProgress = 0f; return; }

        if (KeyPressed(Plugin.ConfigurationHandler.PushKey)) { TryPush(me); }
        if (KeyPressed(Plugin.ConfigurationHandler.AnkleKey)) { TryAnkleGrab(me); }

        UpdateAnkleReaction(me);
        UpdateHeldItem(me);
        UpdateSabotagedRope(me);
        UpdateDefectivePiton(me);
        UpdateArrivals(me);
        UpdateEffects(me);
        UpdateMissions(me);

        if (VersusGame.State.modifier == BiomeModifier.SoftGravity)
        {
            me.refs.movement.CapFallDamage(0.25f, 0.5f);
        }
    }

    // ---------- push ----------

    void TryPush(Character me)
    {
        if (Time.time < pushCooldownUntil || me.data.isClimbingAnything || !me.data.fullyConscious) { return; }
        Camera cam = Camera.main;
        if (cam == null) { return; }
        Character? target = null;
        foreach (RaycastHit hit in Physics.RaycastAll(cam.transform.position, cam.transform.forward, PushRange + 2f, LayerMask.GetMask("Character")).OrderBy(h => h.distance))
        {
            if (CharacterRagdoll.TryGetCharacterFromCollider(hit.collider, out Character c) && c != null && c != me)
            {
                if (Vector3.Distance(me.Center, c.Center) <= PushRange + 0.5f) { target = c; }
                break;
            }
        }
        if (target == null) { return; }
        if (InTruce(me.Center) || InTruce(target.Center))
        {
            VersusHud.Toast("Trêve du feu de camp : pas de poussée ici", 2f);
            return;
        }
        if (me.data.currentStamina < PushStamina)
        {
            VersusHud.Toast("Trop épuisé pour pousser", 2f);
            return;
        }
        me.UseStamina(PushStamina, true, true);
        pushCooldownUntil = Time.time + PushCooldown;
        Vector3 force = (cam.transform.forward + Vector3.up * 0.25f).normalized * Plugin.ConfigurationHandler.PushForce;
        string targetUser = VersusGame.UserOf(target);
        VersusNet? net = VersusNet.ForUser(targetUser);
        net?.view.RPC(nameof(VersusNet.RPC_Pushed), target.photonView.Owner, force, VersusGame.LocalUser);
        if (VersusGame.IsEnemy(MyTeam, VersusGame.TeamKey(target))) { CompleteMission(MissionType.PushEnemy); }
    }

    public static void OnPushed(Vector3 force, string attacker)
    {
        Character me = Character.localCharacter;
        if (me == null || me.data.dead) { return; }
        if (me.data.isClimbingAnything && StaminaFraction(me) >= 0.3f)
        {
            // Holds on: the push only drains stamina.
            me.UseStamina(0.15f, true, true);
            GamefeelHandler.instance.AddPerlinShakeProximity(me.Center, 2f);
            VersusHud.Toast($"{VersusGame.State.NameOf(attacker)} te bouscule !", 2f);
        }
        else
        {
            if (me.data.isClimbingAnything) { me.refs.climbing.StopAnyClimbing(); }
            me.Fall(1.2f);
            me.AddForce(force);
            VersusHud.Toast($"{VersusGame.State.NameOf(attacker)} t'a poussé !", 2f);
        }
        VersusNet.ToMaster(nameof(VersusNet.RPC_ReportAttack), attacker, VersusGame.LocalUser, 0);
    }

    // ---------- ankle grab ----------

    void TryAnkleGrab(Character me)
    {
        if (Time.time < ankleCooldownUntil || !me.data.fullyConscious) { return; }
        if (!me.data.isGrounded && !me.data.isClimbingAnything) { return; }
        Character? best = null;
        float bestDistance = float.MaxValue;
        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c == me || c.isBot || c.data.dead) { continue; }
            if (!c.data.isClimbing && !c.data.isRopeClimbing) { continue; }
            float dy = c.Center.y - me.Center.y;
            Vector3 flat = c.Center - me.Center;
            flat.y = 0f;
            if (dy < 0.3f || dy > 2.8f || flat.magnitude > 2f) { continue; }
            if (Vector3.Distance(me.Center, c.Center) < bestDistance)
            {
                best = c;
                bestDistance = Vector3.Distance(me.Center, c.Center);
            }
        }
        if (best == null)
        {
            VersusHud.Toast("Personne à attraper juste au-dessus de toi", 1.5f);
            return;
        }
        if (InTruce(me.Center))
        {
            VersusHud.Toast("Trêve du feu de camp : pas d'attaque ici", 2f);
            return;
        }
        if (me.data.currentStamina < AnkleStamina)
        {
            VersusHud.Toast("Trop épuisé pour attraper une cheville", 2f);
            return;
        }
        me.UseStamina(AnkleStamina, true, true);
        ankleCooldownUntil = Time.time + AnkleCooldown;
        Vector3 direction = me.data.lookDirection;
        direction.y = 0f;
        direction = direction.sqrMagnitude < 0.01f ? -me.transform.forward : direction.normalized;
        string targetUser = VersusGame.UserOf(best);
        VersusNet.ForUser(targetUser)?.view.RPC(nameof(VersusNet.RPC_AnkleGrabbed), best.photonView.Owner, direction, VersusGame.LocalUser);
        VersusHud.Toast($"Tu attrapes la cheville de {VersusGame.State.NameOf(targetUser)} !", 2f);
        if (VersusGame.IsEnemy(MyTeam, VersusGame.TeamKey(best))) { CompleteMission(MissionType.GrabEnemyAnkle); }
    }

    public static void OnAnkleGrabbed(Vector3 direction, string attacker)
    {
        if (Instance == null) { return; }
        Character me = Character.localCharacter;
        if (me == null || me.data.dead || Time.time < Instance.ankleImmuneUntil || Instance.anklePendingUntil > 0f) { return; }
        Instance.anklePendingUntil = Time.time + AnkleReactionWindow;
        Instance.ankleDirection = direction;
        Instance.ankleAttacker = attacker;
        VersusHud.Toast($"{VersusGame.State.NameOf(attacker)} t'attrape la cheville ! SAUTE pour te dégager !", AnkleReactionWindow + 0.3f);
    }

    void UpdateAnkleReaction(Character me)
    {
        if (anklePendingUntil < 0f) { return; }
        if (me.input.jumpWasPressed && StaminaFraction(me) >= 0.15f)
        {
            me.UseStamina(0.15f, true, true);
            anklePendingUntil = -1f;
            ankleImmuneUntil = Time.time + AnkleImmunity;
            VersusHud.Toast("Coup de pied ! Tu t'es dégagé", 2f);
            return;
        }
        if (Time.time < anklePendingUntil) { return; }
        anklePendingUntil = -1f;
        ankleImmuneUntil = Time.time + AnkleImmunity;
        me.refs.climbing.StopAnyClimbing();
        me.Fall(2f);
        me.AddForce(ankleDirection * Plugin.ConfigurationHandler.AnkleForce + Vector3.down * 150f);
        VersusNet.ToMaster(nameof(VersusNet.RPC_ReportAttack), ankleAttacker, VersusGame.LocalUser, 1);
    }

    // ---------- held Versus items ----------

    void UpdateHeldItem(Character me)
    {
        VersusItemKind held = VersusItems.HeldKind(me);
        if (held != VersusItemKind.Incense) { incenseProgress = 0f; }
        if (held != VersusItemKind.Scissors) { scissorsProgress = 0f; }
        magnifierLines.Clear();

        switch (held)
        {
            case VersusItemKind.Incense:
                UpdateIncense(me);
                break;
            case VersusItemKind.Scissors:
                UpdateScissors(me);
                break;
            case VersusItemKind.CampfireTrap:
                if (me.input.usePrimaryWasPressed) { ArmTrap(me); }
                break;
            case VersusItemKind.Magnifier:
                ScanWithMagnifier(me);
                break;
        }
    }

    static Rope? RopeInSight()
    {
        Camera cam = Camera.main;
        if (cam == null) { return null; }
        RaycastHit[] hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 4f, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
        {
            Rope rope = hit.collider.GetComponentInParent<Rope>();
            if (rope != null && rope.attachmenState == Rope.ATTACHMENT.anchored && !rope.isHelicopterRope)
            {
                return rope;
            }
        }
        return null;
    }

    void UpdateScissors(Character me)
    {
        bool sabotage = me.input.usePrimaryIsPressed;
        bool cut = me.input.useSecondaryIsPressed && !sabotage;
        if (!cut && !sabotage)
        {
            scissorsProgress = 0f;
            return;
        }
        Rope? rope = RopeInSight();
        if (rope == null)
        {
            if (me.input.usePrimaryWasPressed || me.input.useSecondaryWasPressed) { VersusHud.Toast("Vise une corde ancrée, à moins de 3 m", 2f); }
            scissorsProgress = 0f;
            return;
        }
        if (InTruce(rope.transform.position) || InTruce(me.Center))
        {
            if (me.input.usePrimaryWasPressed || me.input.useSecondaryWasPressed) { VersusHud.Toast("Trêve du feu de camp : pas de sabotage ici", 2f); }
            scissorsProgress = 0f;
            return;
        }
        if (rope != scissorsRope || sabotage != scissorsSabotage)
        {
            scissorsRope = rope;
            scissorsSabotage = sabotage;
            scissorsProgress = 0f;
        }
        scissorsProgress += Time.deltaTime;
        if (scissorsProgress < (sabotage ? 3f : 2f)) { return; }
        scissorsProgress = 0f;
        if (sabotage)
        {
            VersusNet.ToMaster(nameof(VersusNet.RPC_MarkSabotage), rope.photonView.ViewID, MyTeam);
            VersusHud.Toast("Corde sabotée : elle lâchera sous un adversaire", 3f);
        }
        else
        {
            rope.photonView.RPC("Detach_Rpc", RpcTarget.AllBuffered, 0f);
            VersusHud.Toast("Corde coupée !", 2f);
        }
        VersusItems.ConsumeHeld(me, useCharges: true);
    }

    void UpdateTints()
    {
        if (Time.time < nextTint) { return; }
        nextTint = Time.time + 1f;
        foreach (Item item in FindObjectsByType<Item>(FindObjectsSortMode.None))
        {
            if (item == null || item.data == null || tintedItems.Contains(item.GetInstanceID())) { continue; }
            Color? tint = VersusItems.Tint(VersusItems.GetKind(item));
            if (tint == null) { continue; }
            tintedItems.Add(item.GetInstanceID());
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.materials)
                {
                    foreach (string property in new[] { "_BaseColor", "_Color", "_MainColor", "_Tint" })
                    {
                        if (material.HasProperty(property)) { material.SetColor(property, Color.Lerp(material.GetColor(property), tint.Value, 0.75f)); }
                    }
                }
            }
        }
    }

    void UpdateIncense(Character me)
    {
        if (!me.input.usePrimaryIsPressed)
        {
            incenseProgress = 0f;
            return;
        }
        List<Character> dead = Character.AllCharacters.Where(c => c != null && !c.isBot && c != me && c.data.dead).ToList();
        if (dead.Count == 0)
        {
            if (me.input.usePrimaryWasPressed) { VersusHud.Toast("Personne à ranimer", 2f); }
            return;
        }
        if (Singleton<PeakHandler>.Instance != null && Singleton<PeakHandler>.Instance.summonedHelicopter)
        {
            if (me.input.usePrimaryWasPressed) { VersusHud.Toast("L'encens est interdit pendant le compte à rebours final", 3f); }
            return;
        }
        if (me.data.fallSeconds > 0f)
        {
            incenseProgress = 0f;
            VersusHud.Toast("Incantation interrompue !", 2f);
            return;
        }
        incenseProgress += Time.deltaTime;
        if (incenseProgress < IncenseChannel) { return; }
        incenseProgress = 0f;

        string myTeam = MyTeam;
        Character target = dead.OrderByDescending(c => VersusGame.TeamKey(c) == myTeam).First();
        Vector3 position = target.Center;
        if (deathPosition.TryGetValue(target, out Vector3 died)) { position = died; }
        if (lastGroundPosition.TryGetValue(target, out Vector3 ground) && ground.y - position.y > 10f) { position = ground; }
        target.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, position + Vector3.up, true, -1);
        VersusItems.ConsumeHeld(me, useCharges: false);
        VersusNet.ToMaster(nameof(VersusNet.RPC_ReportHelp), VersusGame.LocalUser, VersusGame.UserOf(target), true);
        VersusNet.ToMaster(nameof(VersusNet.RPC_Note), $"{VersusGame.State.NameOf(VersusGame.LocalUser)} a ranimé {VersusGame.State.NameOf(VersusGame.UserOf(target))} avec l'encens du retour");
        VersusHud.Toast($"{VersusGame.State.NameOf(VersusGame.UserOf(target))} revient à la vie !", 4f);
    }

    void ArmTrap(Character me)
    {
        Campfire? nearest = Plugin.CampfireList.Where(IsCampfireUsable).OrderBy(c => Vector3.Distance(c.transform.position, me.Center)).FirstOrDefault();
        if (nearest == null || Vector3.Distance(nearest.transform.position, me.Center) > 6f)
        {
            VersusHud.Toast("Approche-toi d'un feu de camp pour le piéger", 2f);
            return;
        }
        int id = CampfireID(nearest);
        if (VersusGame.State.traps.Any(t => t.campfire == id))
        {
            VersusHud.Toast("Ce feu est déjà piégé", 2f);
            return;
        }
        VersusNet.ToMaster(nameof(VersusNet.RPC_ArmTrap), VersusGame.LocalUser, id);
        VersusItems.ConsumeHeld(me, useCharges: false);
    }

    void ScanWithMagnifier(Character me)
    {
        string myTeam = MyTeam;
        VersusState state = VersusGame.State;
        foreach (KeyValuePair<int, string> rope in state.sabotagedRopes)
        {
            PhotonView view = PhotonView.Find(rope.Key);
            if (view == null) { continue; }
            float distance = Vector3.Distance(view.transform.position, me.Center) * CharacterStats.unitsToMeters;
            if (distance <= 40f) { magnifierLines.Add($"Corde sabotée à {distance:0} m{(rope.Value == myTeam ? " (la tienne)" : "")}"); }
        }
        foreach (KeyValuePair<int, string> piton in state.defectivePitons)
        {
            PhotonView view = PhotonView.Find(piton.Key);
            if (view == null) { continue; }
            float distance = Vector3.Distance(view.transform.position, me.Center) * CharacterStats.unitsToMeters;
            if (distance <= 40f) { magnifierLines.Add($"Piton défectueux à {distance:0} m{(piton.Value == myTeam ? " (le tien)" : "")}"); }
        }
        foreach (TrapRecord trap in state.traps)
        {
            Campfire? campfire = Plugin.CampfireList.FirstOrDefault(c => c != null && CampfireID(c) == trap.campfire);
            if (campfire == null) { continue; }
            float distance = Vector3.Distance(campfire.transform.position, me.Center) * CharacterStats.unitsToMeters;
            if (distance <= 80f) { magnifierLines.Add($"Feu piégé à {distance:0} m{(trap.team == myTeam ? " (ton piège)" : "")}"); }
        }
        if (magnifierLines.Count == 0) { magnifierLines.Add("Rien de suspect à proximité"); }
    }

    // ---------- sabotaged ropes and defective pitons ----------

    void UpdateSabotagedRope(Character me)
    {
        Rope rope = me.data.heldRope;
        if (!me.data.isRopeClimbing || rope == null)
        {
            ropeTimer = 0f;
            ropeCracked = false;
            return;
        }
        int id = rope.photonView.ViewID;
        if (!VersusGame.State.sabotagedRopes.TryGetValue(id, out string team) || !VersusGame.IsEnemy(MyTeam, team)) { return; }
        ropeTimer += Time.deltaTime;
        if (ropeTimer > RopeCrackAfter && !ropeCracked)
        {
            ropeCracked = true;
            GamefeelHandler.instance.AddPerlinShakeProximity(me.Center, 3f);
            VersusHud.Toast("CRAC ! La corde est entaillée…", 2f);
        }
        if (ropeTimer > RopeBreakAfter)
        {
            ropeTimer = 0f;
            rope.photonView.RPC("Detach_Rpc", RpcTarget.AllBuffered, 0f);
            VersusNet.ToMaster(nameof(VersusNet.RPC_ClearSabotage), id);
            VersusNet.ToMaster(nameof(VersusNet.RPC_Note), $"{VersusGame.State.NameOf(VersusGame.LocalUser)} a grimpé sur une corde sabotée");
        }
    }

    void UpdateDefectivePiton(Character me)
    {
        ClimbHandle handle = me.data.currentClimbHandle;
        PhotonView? view = handle != null ? handle.GetComponentInParent<PhotonView>() : null;
        if (view == null)
        {
            pitonTimer = 0f;
            pitonCracked = false;
            return;
        }
        if (pitonTimer == 0f) { Plugin.Logger.LogInfo($"Hanging on handle view {view.ViewID}, defective: {VersusGame.State.defectivePitons.ContainsKey(view.ViewID)}"); }
        if (!VersusGame.State.defectivePitons.TryGetValue(view.ViewID, out string team) || !VersusGame.IsEnemy(MyTeam, team))
        {
            pitonTimer = -1f;
            return;
        }
        if (pitonTimer < 0f) { pitonTimer = 0f; }
        pitonTimer += Time.deltaTime;
        if (pitonTimer > PitonCrackAfter && !pitonCracked)
        {
            pitonCracked = true;
            GamefeelHandler.instance.AddPerlinShakeProximity(me.Center, 3f);
            VersusHud.Toast("Le piton craque !", 2f);
        }
        if (pitonTimer > PitonBreakAfter)
        {
            pitonTimer = 0f;
            VersusNet.ToAll(nameof(VersusNet.RPC_BreakPiton), view.ViewID);
            VersusNet.ToMaster(nameof(VersusNet.RPC_ClearPiton), view.ViewID);
            VersusNet.ToMaster(nameof(VersusNet.RPC_Note), $"{VersusGame.State.NameOf(VersusGame.LocalUser)} s'est accroché à un piton défectueux");
        }
    }

    // ---------- campfires ----------

    void UpdateArrivals(Character me)
    {
        if (Time.time < nextArrivalCheck) { return; }
        nextArrivalCheck = Time.time + 0.5f;
        foreach (Campfire campfire in Plugin.CampfireList)
        {
            if (!IsCampfireUsable(campfire)) { continue; }
            int id = CampfireID(campfire);
            if (reportedArrivals.Contains(id)) { continue; }
            if (Vector3.Distance(campfire.transform.position, me.Center) > ArrivalRadius) { continue; }
            reportedArrivals.Add(id);
            bool hasBingBong = VersusItems.HasBingBong(me);
            VersusNet.ToMaster(nameof(VersusNet.RPC_ReportArrival), VersusGame.LocalUser, id, (int)campfire.advanceToSegment, !me.data.dead,
                VersusItems.HasInInventory(me, VersusItemKind.Magnifier), hasBingBong);
            if (hasBingBong) { CompleteMission(MissionType.BringBingBong); }
            // A new biome starts: new secret mission.
            mission = null;
        }
    }

    // ---------- trap effects ----------

    public static void OnTrapBurst(string team, TrapType type)
    {
        Character me = Character.localCharacter;
        if (me == null || me.data.dead || VersusGame.TeamKey(me) != team) { return; }
        if (type == TrapType.SpringLog && !me.data.isClimbingAnything)
        {
            me.Fall(1f);
            me.AddForce((Vector3.up * 2f + Random.insideUnitSphere).normalized * 900f);
            VersusHud.Toast("BOING ! La bûche-ressort t'éjecte", 3f);
        }
    }

    void UpdateEffects(Character me)
    {
        if (Time.time < nextEffectTick) { return; }
        nextEffectTick = Time.time + Random.Range(6f, 10f);
        string myTeam = MyTeam;
        foreach (EffectRecord effect in VersusGame.State.effects.Where(e => e.team == myTeam))
        {
            // Effects never knock you off a wall: they only fire on the ground.
            bool safe = me.data.isGrounded && !me.data.isClimbingAnything;
            switch (effect.type)
            {
                case TrapType.Hiccups:
                    if (safe)
                    {
                        me.AddForce(Vector3.up * 350f);
                        VersusHud.Toast("Hic !", 1f);
                    }
                    break;
                case TrapType.ItchingPowder:
                    if (safe)
                    {
                        me.UseStamina(0.12f, true, true);
                        VersusHud.Toast("Ça gratte !", 1.5f);
                    }
                    break;
                case TrapType.SpringLog:
                    if (safe && Random.value < 0.4f)
                    {
                        me.AddForce(Vector3.up * 500f);
                        VersusHud.Toast("La bûche-ressort te poursuit…", 1.5f);
                    }
                    break;
                case TrapType.Famine:
                    me.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Hunger, 0.02f);
                    VersusHud.Toast("Ton ventre gargouille…", 1.5f);
                    break;
                case TrapType.TellTaleSmoke:
                    VersusHud.Toast("Ta fumée colorée trahit ta position", 1.5f);
                    break;
            }
        }
    }

    // ---------- help detection (called from the Reach patch on the helped player's client) ----------

    public void NoteHelpedBy(Character helper)
    {
        Character me = Character.localCharacter;
        if (me == null || helper == null || helper == me) { return; }
        helpTimers.TryGetValue(helper, out float timer);
        timer += Time.deltaTime;
        helpTimers[helper] = timer;
        if (timer < 0.4f) { return; }
        helpTimers[helper] = 0f;
        string helperUser = VersusGame.UserOf(helper);
        if (helpReportedAt.TryGetValue(helperUser, out float last) && Time.time - last < 20f) { return; }
        helpReportedAt[helperUser] = Time.time;
        bool inDanger = me.data.isClimbing || StaminaFraction(me) < 0.35f || me.data.passedOut || me.refs.afflictions.isStruggling;
        VersusNet.ToMaster(nameof(VersusNet.RPC_ReportHelp), helperUser, VersusGame.LocalUser, inDanger);
    }

    // ---------- missions ----------

    void AssignMission()
    {
        mission = (MissionType)Random.Range(0, System.Enum.GetValues(typeof(MissionType)).Length);
        missionDone = false;
        carriedDistance = 0f;
        VersusHud.Toast($"Mission secrète : {MissionText(mission.Value)}", 6f);
    }

    public static string MissionText(MissionType type) => type switch
    {
        MissionType.PushEnemy => "pousser un adversaire",
        MissionType.GrabEnemyAnkle => "attraper la cheville d'un adversaire",
        MissionType.Pickpocket => "fouiller le sac d'un adversaire",
        MissionType.GetCarried => "te faire porter sur 20 m",
        MissionType.SurviveBigFall => "tomber de plus de 15 m et survivre",
        MissionType.BringBingBong => "arriver au prochain feu avec Bing Bong",
        _ => "?",
    };

    public static void CompleteMission(MissionType type)
    {
        if (Instance == null || Instance.mission != type || Instance.missionDone) { return; }
        Instance.missionDone = true;
        VersusNet.ToMaster(nameof(VersusNet.RPC_ReportMission), VersusGame.LocalUser, MissionText(type));
    }

    void UpdateMissions(Character me)
    {
        if (me.data.isCarried)
        {
            if (lastCarriedPos != Vector3.zero) { carriedDistance += Vector3.Distance(lastCarriedPos, me.Center) * CharacterStats.unitsToMeters; }
            lastCarriedPos = me.Center;
            if (carriedDistance >= 20f) { CompleteMission(MissionType.GetCarried); }
        }
        else
        {
            lastCarriedPos = Vector3.zero;
        }

        bool inAir = !me.data.isGrounded && !me.data.isClimbingAnything && !me.data.isCarried;
        if (inAir)
        {
            if (!airborne) { airborne = true; airborneMaxY = me.Center.y; }
            airborneMaxY = Mathf.Max(airborneMaxY, me.Center.y);
        }
        else if (airborne)
        {
            airborne = false;
            float meters = (airborneMaxY - me.Center.y) * CharacterStats.unitsToMeters;
            if (meters > 8f) { VersusNet.ToMaster(nameof(VersusNet.RPC_ReportFall), VersusGame.LocalUser, meters); }
            if (meters > 15f && !me.data.dead) { CompleteMission(MissionType.SurviveBigFall); }
        }
    }
}
