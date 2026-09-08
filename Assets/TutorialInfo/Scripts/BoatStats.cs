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
        if (level <= 0) return "veslice — pomala, zacinas s ni";
        if (level == 1) return "plachty — normalni rychlost";
        if (level == 2) return "+25% rychlost, rychlejsi tezba";
        return "+50% rychlost, +1 ryba za zatah";
    }
}
