using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  LootItems.cs
//  Kořist z pirátů — "neviditelný inventář". Když hráč potopí pirátskou loď
//  nebo porazí pirátskou stráž na ostrově, má šanci (DROP_CHANCE = 25 %), že
//  mu padne jeden předmět. Předměty se nikde nezobrazují ve světě, jen se
//  připíšou do PlayerState.loot a dají se prodat ve VÝKUPNĚ v majáku (a plní
//  quest "získej kořist").
//
//  Seznam předmětů je pevný (pole Items) — index předmětu je zároveň index
//  v PlayerState.loot, proto se pořadí NESMÍ měnit (jen přidávat na konec).
//  Vzácnost řeší "váha" (weight): čím vyšší, tím častěji padá.
// ─────────────────────────────────────────────────────────────────────────────

public static class LootItems
{
    /// <summary>Šance, že po poraženém pirátovi padne předmět (0..1).</summary>
    public const float DROP_CHANCE = 0.25f;

    // Jeden druh kořisti: české a anglické jméno, výkupní cena a váha (vzácnost).
    public struct Item
    {
        public string nameCs, nameEn;
        public int    price;
        public int    weight;

        public Item(string cs, string en, int price, int weight)
        {
            nameCs = cs; nameEn = en; this.price = price; this.weight = weight;
        }

        public string Name => Loc.T(nameCs, nameEn);
    }

    // POŘADÍ NEMĚNIT — index je uložený v savu (PlayerState.loot[index]).
    public static readonly Item[] Items =
    {
        new Item("Lahev starého rumu",  "Bottle of old rum",   25, 30),
        new Item("Pirátský kompas",     "Pirate compass",      40, 25),
        new Item("Dalekohled",          "Spyglass",            45, 20),
        new Item("Pirátská šavle",      "Pirate cutlass",      55, 15),
        new Item("Zlatý prsten",        "Golden ring",         70,  7),
        new Item("Stříbrný kalich",     "Silver chalice",     110,  3),
    };

    /// <summary>Kolik kusů předmětu `index` hráč má (chybějící položka = 0).</summary>
    public static int Count(PlayerState ps, int index)
    {
        if (ps == null || ps.loot == null || index < 0 || index >= ps.loot.Count) return 0;
        return ps.loot[index];
    }

    /// <summary>Přidá (nebo odebere zápornou hodnotou) kusy předmětu; seznam si sám doplní na potřebnou délku.</summary>
    public static void Add(PlayerState ps, int index, int amount)
    {
        if (ps == null || index < 0 || index >= Items.Length) return;
        if (ps.loot == null) ps.loot = new List<int>();
        while (ps.loot.Count <= index) ps.loot.Add(0);
        ps.loot[index] = Mathf.Max(0, ps.loot[index] + amount);
    }

    /// <summary>Celkový počet kusů kořisti, co hráč nese.</summary>
    public static int TotalCount(PlayerState ps)
    {
        int sum = 0;
        for (int i = 0; i < Items.Length; i++) sum += Count(ps, i);
        return sum;
    }

    /// <summary>Kolik by hráč dostal, kdyby prodal všechnu kořist.</summary>
    public static int TotalValue(PlayerState ps)
    {
        int sum = 0;
        for (int i = 0; i < Items.Length; i++) sum += Count(ps, i) * Items[i].price;
        return sum;
    }

    /// <summary>Vyhodí všechnu kořist (smrt hráče — stejně jako ryby a poklady).</summary>
    public static void Clear(PlayerState ps)
    {
        if (ps != null && ps.loot != null) ps.loot.Clear();
    }

    /// <summary>Vylosuje index předmětu podle vah (vzácné věci padají méně často).</summary>
    public static int RollRandomIndex()
    {
        int total = 0;
        foreach (var it in Items) total += it.weight;

        int roll = Random.Range(0, total);
        for (int i = 0; i < Items.Length; i++)
        {
            roll -= Items[i].weight;
            if (roll < 0) return i;
        }
        return 0;
    }

    /// <summary>
    /// Hod kostkou po poraženém pirátovi: s šancí `chance` dá hráči jeden předmět,
    /// posune quest "získej kořist" a vrátí jeho index. Když nic nepadne, vrací -1.
    /// </summary>
    public static int TryDrop(PlayerState ps, float chance)
    {
        if (ps == null || Random.value >= chance) return -1;

        int index = RollRandomIndex();
        Add(ps, index, 1);
        ps.activeQuest.AddProgress(ActiveQuest.TYPE_LOOT, 1);
        return index;
    }
}
