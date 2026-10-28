using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zorro.Core;

namespace PvPEAK.Versus;

public enum VersusItemKind
{
    None = 0,
    Scissors = 1,
    DefectivePiton = 2,
    Magnifier = 3,
    Incense = 4,
    CampfireTrap = 5,
}

// Versus items are vanilla items tagged with a custom key in their instance data.
// The game serializes unknown keys as-is, so the tag travels with inventories, backpacks and drops.
public static class VersusItems
{
    public const DataEntryKey KindKey = (DataEntryKey)201;
    public const DataEntryKey UsesKey = (DataEntryKey)202;
    public const int ScissorsUses = 2;

    static readonly Dictionary<VersusItemKind, ushort> baseItems = new();

    public static string DisplayName(VersusItemKind kind) => kind switch
    {
        VersusItemKind.Scissors => "Ciseaux",
        VersusItemKind.DefectivePiton => "Piton défectueux",
        VersusItemKind.Magnifier => "Loupe",
        VersusItemKind.Incense => "Encens du retour",
        VersusItemKind.CampfireTrap => "Piège de feu",
        _ => "",
    };

    public static string Description(VersusItemKind kind) => kind switch
    {
        VersusItemKind.Scissors => "[E] sur une corde : couper. Accroupi + [E] : saboter (elle lâchera sous un adversaire).",
        VersusItemKind.DefectivePiton => "Se plante comme un piton. Craque puis casse sous le poids d'un adversaire.",
        VersusItemKind.Magnifier => "Tenue en main : révèle cordes sabotées, pitons défectueux et feux piégés. Dans l'inventaire : désamorce les pièges de feu.",
        VersusItemKind.Incense => "Maintenir [clic gauche] 10 s : ranime un joueur mort à l'endroit de sa mort.",
        VersusItemKind.CampfireTrap => "[Clic gauche] près d'un feu : le piège pour l'équipe adverse.",
        _ => "",
    };

    public static VersusItemKind GetKind(ItemInstanceData? data)
    {
        if (data != null && data.TryGetDataEntry<IntItemData>(KindKey, out var value))
        {
            return (VersusItemKind)value.Value;
        }
        return VersusItemKind.None;
    }

    public static VersusItemKind GetKind(Item? item) => item == null ? VersusItemKind.None : GetKind(item.data);

    public static VersusItemKind HeldKind(Character character)
    {
        return character != null && character.data != null ? GetKind(character.data.currentItem) : VersusItemKind.None;
    }

    public static bool HasInInventory(Character character, VersusItemKind kind)
    {
        Player player = character.player;
        if (player == null) { return false; }
        foreach (ItemSlot slot in player.itemSlots)
        {
            if (!slot.IsEmpty() && GetKind(slot.data) == kind) { return true; }
        }
        return HeldKind(character) == kind;
    }

    public static bool HasBingBong(Character character)
    {
        Player player = character.player;
        if (player == null) { return false; }
        foreach (ItemSlot slot in player.itemSlots)
        {
            if (!slot.IsEmpty() && slot.prefab != null && slot.prefab.itemTags.HasFlag(Item.ItemTags.BingBong)) { return true; }
        }
        Item held = character.data.currentItem;
        return held != null && held.itemTags.HasFlag(Item.ItemTags.BingBong);
    }

    public static ItemInstanceData CreateData(VersusItemKind kind)
    {
        ItemInstanceData data = new ItemInstanceData(Guid.NewGuid());
        ItemInstanceDataHandler.AddInstanceData(data);
        data.RegisterNewEntry<IntItemData>(KindKey).Value = (int)kind;
        if (kind == VersusItemKind.Scissors)
        {
            data.RegisterNewEntry<IntItemData>(UsesKey).Value = ScissorsUses;
        }
        return data;
    }

    // Picks the vanilla item each Versus item is built on, by component so it survives item ID changes.
    public static bool TryGetBaseItem(VersusItemKind kind, out ushort itemID)
    {
        if (baseItems.TryGetValue(kind, out itemID)) { return true; }
        Dictionary<ushort, Item> lookup = SingletonAsset<ItemDatabase>.Instance.itemLookup;
        Func<Item, bool>[] matchers = kind switch
        {
            VersusItemKind.Scissors => [i => i.GetComponentInChildren<CompassPointer>(true) != null, i => i.GetComponent<Action_Guidebook>() != null],
            VersusItemKind.DefectivePiton => [i => i.GetComponent<ClimbingSpikeComponent>() != null],
            VersusItemKind.Magnifier => [i => i.GetComponent<Actions_Binoculars>() != null, i => i.GetComponent<Action_ShowBinocularOverlay>() != null],
            VersusItemKind.Incense => [i => i.GetComponent<Action_LightCandle>() != null, i => i.GetComponent<Action_LightLantern>() != null],
            VersusItemKind.CampfireTrap => [i => i.GetComponent<Action_Guidebook>() != null, i => i.GetComponent<Action_Passport>() != null, i => i.GetComponentInChildren<CompassPointer>(true) != null],
            _ => [],
        };
        foreach (Func<Item, bool> matcher in matchers)
        {
            foreach (KeyValuePair<ushort, Item> entry in lookup.OrderBy(e => e.Key))
            {
                if (entry.Value != null && !(entry.Value is Backpack) && matcher(entry.Value))
                {
                    baseItems[kind] = entry.Key;
                    itemID = entry.Key;
                    Plugin.Logger.LogInfo($"Versus item {kind} uses base item {entry.Value.name} ({entry.Key})");
                    return true;
                }
            }
        }
        Plugin.Logger.LogError($"No base item found for Versus item {kind}");
        itemID = 0;
        return false;
    }

    // Removes one use of the held item, emptying its slot when it runs out. Must run on the holder's client.
    public static void ConsumeHeld(Character character, bool useCharges)
    {
        Item item = character.data.currentItem;
        if (item == null) { return; }
        if (useCharges && item.data.TryGetDataEntry<IntItemData>(UsesKey, out var uses) && uses.Value > 1)
        {
            uses.Value--;
            return;
        }
        Optionable<byte> slot = character.refs.items.currentSelectedSlot;
        if (slot.IsSome)
        {
            character.player.EmptySlot(slot);
            character.refs.items.EquipSlot(Optionable<byte>.None);
        }
    }
}
