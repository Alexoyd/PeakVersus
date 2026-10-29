using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace PvPEAK.Versus;

// Hazard vote, relics, early finish and host options.
public static partial class VersusGame
{
    public const double VoteSeconds = 90;
    public const int RelicsPerBiome = 3;
    public const int RelicPoints = 2;

    // ---------- rising hazard vote ----------

    static void OpenHazardVote(int campfire)
    {
        State.voteCampfire = campfire;
        State.voteDeadline = Now + VoteSeconds;
        State.votes.Clear();
        State.extraVotes.Clear();
        string question = State.hazardMode == HazardMode.Rising ? "le danger monte : on continue ?" : "le danger (brume ou lave) doit-il monter ?";
        Announce($"VOTE : {question} Vote à la boutique [{Plugin.ConfigurationHandler.ShopKey}] du feu (90 s)", 8f);
    }

    public static void MasterVote(string user, bool yes)
    {
        if (!IsMaster || State.voteCampfire < 0) { return; }
        State.votes[user] = yes;
        RefreshRoster();
        if (State.playerTeams.Keys.All(State.votes.ContainsKey))
        {
            CloseHazardVote();
        }
        Broadcast();
    }

    static void CloseHazardVote()
    {
        int score = 0;
        foreach (KeyValuePair<string, bool> vote in State.votes)
        {
            int weight = 1 + (State.extraVotes.TryGetValue(vote.Key, out int extra) ? extra : 0);
            score += vote.Value ? weight : -weight;
        }
        State.voteCampfire = -1;
        if (score > 0)
        {
            State.hazardMode = HazardMode.Rising;
            Announce("Résultat du vote : le danger MONTE (et plus vite)", 6f);
            StartFogNow();
        }
        else if (score < 0)
        {
            State.hazardMode = HazardMode.Paused;
            Announce("Résultat du vote : le danger est FIGÉ", 6f);
        }
        else
        {
            Announce("Égalité au vote : rien ne change", 5f);
        }
        State.chronicle.Add($"Vote du danger : {(score > 0 ? "il monte" : score < 0 ? "figé" : "égalité, rien ne change")}");
    }

    static void StartFogNow()
    {
        OrbFogHandler fog = Singleton<OrbFogHandler>.Instance;
        if (fog != null && !fog.isMoving && !fog.hasArrived)
        {
            fog.photonView.RPC("StartMovingRPC", RpcTarget.All);
        }
    }

    // ---------- relics ----------

    public static void SpawnRelicsAfter(int campfire)
    {
        Campfire? from = Plugin.CampfireList.FirstOrDefault(c => c != null && VersusLocal.CampfireID(c) == campfire);
        if (from == null) { return; }
        Campfire? to = Plugin.CampfireList.FirstOrDefault(c => c != null && (int)c.advanceToSegment == (int)from.advanceToSegment + 1);
        if (to == null) { return; }
        SpawnRelicsBetween(from.transform.position, to.transform.position);
    }

    public static void SpawnRelicsForFirstBiome(Vector3 spawn)
    {
        Campfire? first = Plugin.CampfireList.Where(c => c != null && (int)c.advanceToSegment >= 1).OrderBy(c => (int)c.advanceToSegment).FirstOrDefault();
        if (first == null) { return; }
        SpawnRelicsBetween(spawn, first.transform.position);
    }

    static void SpawnRelicsBetween(Vector3 a, Vector3 b)
    {
        if (!IsMaster || !VersusItems.TryGetBaseItem(VersusItemKind.Relic, out ushort itemID)) { return; }
        Item prefab = SingletonAsset<ItemDatabase>.Instance.itemLookup[itemID];
        Vector3 path = b - a;
        Vector3 side = Vector3.Cross(new Vector3(path.x, 0f, path.z).normalized, Vector3.up);
        int spawned = 0;
        for (int attempt = 0; attempt < 40 && spawned < RelicsPerBiome; attempt++)
        {
            Vector3 along = Vector3.Lerp(a, b, Random.Range(0.2f, 0.85f));
            float offset = Random.Range(25f, 60f) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 probe = along + side * offset + Vector3.up * 120f;
            RaycastHit hit = HelperFunctions.GetGroundPosRaycast(probe, HelperFunctions.LayerType.TerrainMap);
            if (hit.transform == null || Mathf.Abs(hit.point.y - along.y) > 80f) { continue; }
            GameObject go = PhotonNetwork.InstantiateItemRoom(prefab.name, hit.point + Vector3.up * 0.6f, Quaternion.identity);
            Item? item = go != null ? go.GetComponent<Item>() : null;
            if (item == null) { continue; }
            item.photonView.RPC("SetItemInstanceDataRPC", RpcTarget.All, VersusItems.CreateData(VersusItemKind.Relic));
            spawned++;
        }
        Plugin.Logger.LogInfo($"Spawned {spawned} relics");
    }

    public static void MasterDepositRelics(string user, int count)
    {
        if (!IsMaster || count <= 0) { return; }
        AddPoints(user, count * RelicPoints);
        Announce($"{State.NameOf(user)} dépose {count} relique(s) au feu : +{count * RelicPoints} pts", 5f);
        State.chronicle.Add($"{State.NameOf(user)} a rapporté {count} relique(s)");
        Broadcast();
    }

    // ---------- shorter runs ----------

    static void TryStartFinalCountdown(int campfire, int segment)
    {
        if (State.options.biomes <= 0 || segment != State.options.biomes || State.finalCampfire >= 0) { return; }
        State.finalCampfire = campfire;
        State.finalCountdownEnd = Now + Plugin.ConfigurationHandler.PeakCountdown;
        Announce($"Dernier feu atteint ! Fin dans {Plugin.ConfigurationHandler.PeakCountdown} s : restez près du feu pour vous échapper (pacte ou mêlée)", 10f);
    }

    public static bool NearFinalCampfire(Character character)
    {
        Campfire? fire = Plugin.CampfireList.FirstOrDefault(c => c != null && VersusLocal.CampfireID(c) == State.finalCampfire);
        return fire != null && Vector3.Distance(fire.transform.position, character.Center) <= TruceRadius;
    }

    // ---------- host options ----------

    public static void MasterSetOptions(VersusOptions options)
    {
        if (!IsMaster) { return; }
        State.options = options;
        Broadcast();
    }
}
