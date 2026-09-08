using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  EconomyConfig.cs
//  VŠECHNA ČÍSLA EKONOMIKY NA JEDNOM MÍSTĚ. Kdykoli je potřeba doladit ceny,
//  odměny nebo výkupy, mění se to tady (ne po scénách v inspektoru).
//
//  Základní myšlenka vyvážení:
//   • 1 ryba = 1 mince, 1 poklad = 5 mincí (drobný, ale jistý příjem)
//   • questy, piráti, bedny a mega quest jsou hlavní zdroj větších peněz
//   • velká loď stojí 1000 → je to dlouhodobější cíl
//   • každý ostrov má trošku jiné NÁKUPNÍ ceny (ne výkup) — někde levněji,
//     jinde dráž, ať se hráči vyplatí porovnávat
// ─────────────────────────────────────────────────────────────────────────────

public static class EconomyConfig
{
    // ── Výkup (prodej kořisti v questshopu) — všude stejný ──────────────────
    public const int FishPrice        = 1;  // za 1 rybu
    public const int TreasurePrice    = 5;  // za 1 poklad
    public const int SellBonusPerItem = 2;  // navíc za kus po splnění mega questu
                                            // (drženo nízko — u ryb za 1 minci je
                                            //  i +2 velký skok; +2 = ryba za 3,
                                            //  poklad za 7)

    // ── Základní nákupní ceny (per ostrov se násobí PriceMultiplier) ────────
    public const int SpeedUpgrade  = 160;
    public const int RodUpgrade    = 110;
    public const int MiningUpgrade = 130;
    public const int ShipSmall     = 180;   // veslice → malá plachetnice
    public const int ShipMedium    = 450;   // malá → střední
    public const int ShipLarge     = 1000;  // střední → velká
    public const int AmmoPack      = 45;    // balíček munice
    public const int AmmoPackSize  = 12;    // nábojů v balíčku
    public const int MapItem       = 130;   // mapa (šipka k nejbližšímu ostrovu)

    // ── Odměny ─────────────────────────────────────────────────────────────
    public const int PirateRewardSmall  = 40;
    public const int PirateRewardMedium = 110;
    public const int PirateRewardLarge  = 260;
    public const int IslandCannonReward = 90;
    public const int ChestCoinsMin      = 40;
    public const int ChestCoinsMax      = 120;
    public const int MegaQuestCoinsMin  = 400;
    public const int MegaQuestCoinsMax  = 900;

    // ── Oprava lodě ────────────────────────────────────────────────────────
    public const int RepairCostPerHp   = 2;   // oprava naplavané lodě (u mola nebo v obchodě)
    public const int WreckRepairCost    = 160; // vytáhnout a spravit ROZBITOU loď (v obchodě)

    // ── Per-ostrov cenový násobič ──────────────────────────────────────────
    /// <summary>
    /// Násobič NÁKUPNÍCH cen podle "cenového levelu" ostrova (0–20) → 0.8× až 1.2×.
    /// Rozsah je symetrický kolem 1.0, takže základní ceny výše jsou průměr —
    /// nejlevnější ostrov (level 0) prodává za 80 %, nejdražší (level 20) za 120 %.
    /// </summary>
    public static float PriceMultiplier(int islandPriceLevel)
        => 0.8f + Mathf.Clamp(islandPriceLevel, 0, 20) * 0.02f;

    /// <summary>Deterministický cenový level ostrova (0–20) z pozice jeho majáku.</summary>
    public static int IslandPriceLevel(int lighthouseX, int lighthouseY)
    {
        unchecked
        {
            int h = lighthouseX * 73856093 ^ lighthouseY * 19349663;
            return (h & 0x7fffffff) % 21;
        }
    }

    /// <summary>Zaokrouhlená nákupní cena pro daný ostrov.</summary>
    public static int Price(int baseCost, int islandPriceLevel)
        => Mathf.Max(1, Mathf.RoundToInt(baseCost * PriceMultiplier(islandPriceLevel)));
}
