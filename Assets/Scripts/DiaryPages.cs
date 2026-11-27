using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  DiaryPages.cs
//  Stránky bratrova deníku — sběratelské listy schované po cestě příběhem.
//  Postupně vysvětlují, proč bratr zůstal sám, kde vzal rodinné dědictví a co
//  se stalo s dědovým synem — bez dlouhých monologů ve finále.
//
//  Kde se která stránka najde (index = bit v GameData.diaryPagesMask):
//    0 — Pirátský ostrov: svitek uvnitř hradeb (MegaIslandMarker)
//    1 — první bedna otevřená po Pirátském ostrově (ChestManager)
//    2 — Hřbitov lodí: svitek na břehu (MegaIslandMarker)
//    3 — vrak Bludného Holanďana (CombatDirector.OnPirateSunk)
//    4 — Poslední ostrov: svitek u obelisku (MegaIslandMarker)
//    5 — bedna otevřená na cestě k Poslednímu ostrovu (ChestManager)
//  Pořadí stránek NEMĚNIT — index se ukládá do savu jako bit.
//
//  Čistě statická třída: texty + pomocné metody nad bitovou maskou. Deník
//  (JournalScreen) je vypisuje, sebrání hlásí toast.
// ─────────────────────────────────────────────────────────────────────────────

public static class DiaryPages
{
    public const int COUNT = 6;

    // Texty stránek — česky a anglicky vedle sebe (stejný index).
    private static readonly string[] TextCs =
    {
        "Den, kdy se loď roztříštila o útesy, si pamatuju hlavně podle zvuku. Praskot dřeva a táta, jak křičí jméno mého bratra. Ne moje.",
        "Na útesu jsem čekal tři dny. Viděl jsem jejich veslici zmizet za obzorem. Pořád jsem si říkal: zítra se pro mě vrátí.",
        "Našli mě piráti, napůl mrtvého hlady. Dali mi jídlo a práci. Za pár let jsem velel vlastní posádce v pevnosti s hradbami.",
        "Truhlička po matce — naše dědictví — byla celou dobu u mě. Táta mi ji ten večer svěřil, ať ji hlídám. Nikdy se pro ni nevrátil.",
        "Potkal jsem mladíka s očima mého bratra. Jeho syna. Chtěl jsem mu všechno říct, ale bouře byla rychlejší než já.",
        "Píšu to, protože vím, že jednou někdo připluje. Kéž by to byl on. A kéž bych mu dokázal odpustit.",
    };
    private static readonly string[] TextEn =
    {
        "The day the ship shattered on the rocks, I remember mostly by the sound. Splintering wood, and Father screaming my brother's name. Not mine.",
        "I waited on the cliff for three days. I watched their rowboat vanish over the horizon. I kept telling myself: tomorrow they'll come back for me.",
        "Pirates found me, half dead from hunger. They gave me food and work. A few years later I commanded my own crew in a walled fortress.",
        "Mother's little chest — our inheritance — was with me all along. Father gave it to me that night to keep safe. He never came back for it.",
        "I met a young man with my brother's eyes. His son. I wanted to tell him everything, but the storm was faster than I was.",
        "I write this because I know that someday someone will sail here. I wish it were him. And I wish I could forgive him.",
    };

    /// <summary>Text stránky v aktuálním jazyce.</summary>
    public static string Text(int index) => Loc.T(TextCs[index], TextEn[index]);

    /// <summary>Má hráč stránku `index`?</summary>
    public static bool Has(GameData d, int index)
        => d != null && index >= 0 && index < COUNT && (d.diaryPagesMask & (1 << index)) != 0;

    /// <summary>Kolik stránek má hráč sebraných.</summary>
    public static int Count(GameData d)
    {
        int n = 0;
        for (int i = 0; i < COUNT; i++) if (Has(d, i)) n++;
        return n;
    }

    /// <summary>
    /// Připíše stránku (když ji hráč ještě nemá) a vrátí true. Ukládání a hlášku
    /// řeší volající — tady se jen mění bitová maska, ať to jde testovat bez scény.
    /// </summary>
    public static bool Collect(GameData d, int index)
    {
        if (d == null || index < 0 || index >= COUNT || Has(d, index)) return false;
        d.diaryPagesMask |= 1 << index;
        return true;
    }

    /// <summary>Text hlášky po sebrání stránky ("Stránka bratrova deníku (2/6) — přečti si ji v Deníku.").</summary>
    public static string FoundToast(GameData d)
    {
        int n = Count(d);
        return Loc.T($"Našel jsi stránku bratrova deníku ({n}/{COUNT}) — přečti si ji v Deníku (Esc → Deník).",
                     $"You found a page of the brother's diary ({n}/{COUNT}) — read it in the Journal (Esc → Journal).");
    }

    /// <summary>Stránka z bedny: po Pirátském ostrově (1) a na cestě k Poslednímu ostrovu (5).
    /// Vrací index stránky, kterou má bedna dát, nebo -1.</summary>
    public static int PageForChest(GameData d)
    {
        if (d == null || d.storyDone || d.storyStep < 2) return -1;
        if (d.megaIndex >= 1 && !Has(d, 1)) return 1;
        if (d.megaIndex >= 2 && !Has(d, 5)) return 5;
        return -1;
    }
}
