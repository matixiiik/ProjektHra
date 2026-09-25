using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Loc.cs
//  Jednoduchá lokalizace hry: čeština / angličtina.
//
//  Princip: obě verze textu stojí vedle sebe přímo v kódu —
//      Loc.T("Nová hra", "New game")
//  Žádné externí tabulky ani klíče, takže je hned vidět, co se kde zobrazí.
//  Delší texty (dialogy) se předávají jako pole stránek: Loc.Pick(csPages, enPages).
//
//  Jazyk se ukládá do PlayerPrefs ("Lang"), NE do savu — je to nastavení
//  zařízení, ne postupu ve hře, a formát savu se tím nemění.
//  Přepíná se v hlavním menu a v pauze (Loc.Toggle()).
//
//  Pozor na věty s čísly: čeština skloňuje podle počtu ("1 mince", "3 mince",
//  "5 mincí"), angličtina jen "1 coin / 5 coins". Na to je Loc.Plural.
// ─────────────────────────────────────────────────────────────────────────────

public enum Language { Czech = 0, English = 1 }

public static class Loc
{
    private const string PrefsKey = "Lang";

    private static int lang = -1; // -1 = ještě nenačteno z PlayerPrefs

    /// <summary>Vyvolá se po změně jazyka — posluchači si překreslí své texty.</summary>
    public static event System.Action OnLanguageChanged;

    /// <summary>Aktuální jazyk (líně načtený, protože menu běží dřív než cokoli jiného).</summary>
    public static Language Current
    {
        get
        {
            if (lang < 0) lang = PlayerPrefs.GetInt(PrefsKey, (int)Language.Czech);
            return (Language)lang;
        }
    }

    /// <summary>Je zapnutá angličtina? (Pro věty s proměnnými: Loc.En ? $"…" : $"…".)</summary>
    public static bool En => Current == Language.English;

    /// <summary>Nastaví jazyk, uloží ho a dá vědět posluchačům.</summary>
    public static void Set(Language language)
    {
        if (Current == language) return;

        lang = (int)language;
        PlayerPrefs.SetInt(PrefsKey, lang);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    /// <summary>Přepne češtinu ↔ angličtinu (tlačítko v menu).</summary>
    public static void Toggle() => Set(En ? Language.Czech : Language.English);

    /// <summary>Krátký text: česky, anglicky.</summary>
    public static string T(string cs, string en) => En ? en : cs;

    /// <summary>Delší text po stránkách (dialogy): pole česky, pole anglicky.</summary>
    public static string[] Pick(string[] cs, string[] en) => En ? en : cs;

    /// <summary>Slovo "mince" ve správném tvaru podle počtu (1 mince / 3 mince / 5 mincí / coins).</summary>
    public static string CoinsWord(int n) => Plural(n, "mince", "mince", "mincí", "coin", "coins");

    /// <summary>
    /// Podstatné jméno podle počtu. Čeština má tři tvary (1 / 2–4 / 5+),
    /// angličtina dva (1 / ostatní). Příklad:
    ///   Loc.Plural(n, "mince", "mince", "mincí", "coin", "coins")
    /// </summary>
    public static string Plural(int n, string cs1, string cs2to4, string cs5plus, string en1, string enMany)
    {
        if (En) return n == 1 ? en1 : enMany;

        int abs = Mathf.Abs(n);
        if (abs == 1)             return cs1;
        if (abs >= 2 && abs <= 4) return cs2to4;
        return cs5plus;
    }
}
