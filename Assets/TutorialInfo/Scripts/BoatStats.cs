using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  BoatStats.cs
//  Na jednom místě: co která úroveň lodě umí. Loď se kupuje v obchodě
//  (UpgradeShopManager) a KAŽDÁ vyšší úroveň je znatelně lepší než předchozí.
//
//  Úrovně (gameData.shipLevel / player2ShipLevel):
//     0 = veslice ("boat row small")  — s tou hra začíná, je pomalá
//     1 = malá plachetnice            — normální rychlost
//     2 = střední loď                 — +25 % rychlost, rychlejší těžba
//     3 = velká loď                   — +50 % rychlost, +1 ryba za zátah
//
//  Je to statická pomocná třída (žádný stav, žádný objekt ve scéně) — čte z ní
//  PlayerController (rychlost, rybaření, těžba) i obchod (popisky).
// ─────────────────────────────────────────────────────────────────────────────

public static class BoatStats
{
    public const int MaxHealth = 100; // strop zdraví lodě i hráče

    /// <summary>Poškození jednoho výstřelu z děla dané lodě. 0 = loď dělo nemá (veslice).</summary>
    public static float CannonDamage(int level)
    {
        if (level <= 0) return 0f;   // veslice — bez děla
        if (level == 1) return 0.5f; // malá loď — 1 slabé dělo
        if (level == 2) return 1.0f; // střední loď
        return 2.0f;                 // velká loď — pořádná děla
    }

    /// <summary>Má loď dané úrovně vůbec dělo (dá se z ní střílet)?</summary>
    public static bool HasCannon(int level) => CannonDamage(level) > 0f;

    /// <summary>Násobič rychlosti plavby podle úrovně lodě (pěší chůze se netýká).</summary>
    public static float SpeedMultiplier(int level)
    {
        if (level <= 0) return 0.75f; // veslice — líná loďka na vesla
        if (level == 1) return 1.00f; // malá plachetnice
        if (level == 2) return 1.25f; // střední loď
        return 1.50f;                 // velká loď
    }

    /// <summary>Kolik ryb navíc hráč uloví za jeden zátah (přičítá se k bonusu z prutu).</summary>
    public static int FishBonus(int level) => level >= 3 ? 1 : 0;

    /// <summary>Násobič délky těžby pokladu (menší = rychleji). Střední+ loď má lepší navijáky.</summary>
    public static float MiningMultiplier(int level) => level >= 2 ? 0.8f : 1f;

    /// <summary>Krátký popis výhody dané úrovně (do obchodu).</summary>
    public static string Perk(int level)
    {
        if (level <= 0) return "veslice — pomala, bez dela";
        if (level == 1) return "+rychlost, 1 delo (0.5 posk.)";
        if (level == 2) return "+25% rychlost, rychlejsi tezba, delo 1.0";
        return "+50% rychlost, +1 ryba, delo 2.0";
    }
}
