using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  InteriorInteractable.cs
//  Jeden "bod zájmu" ve vnitřku majáku, ke kterému hráč přijde a dá E.
//   • UpgradeShop → otevře obchod s vylepšeními
//   • QuestShop   → otevře obchod s questy
//   • Exit        → odejít ven na ostrov
//
//  InteriorPlayer si najde nejbližší takový objekt v dosahu a zavolá Trigger().
// ─────────────────────────────────────────────────────────────────────────────

// Pozn.: NOVÉ hodnoty přidávej jen NA KONEC — čísla se serializují do scény
// (ExitDoor apod.), přeházení by rozbilo existující objekty.
public enum InteriorAction { UpgradeShop, QuestShop, Exit, QuestShopSell }

public class InteriorInteractable : MonoBehaviour
{
    public InteriorAction action;

    [Tooltip("Na jakou vzdálenost (v metrech) sem hráč dosáhne.")]
    public float range = 1.6f;

    [Tooltip("Záložní text nápovědy (skutečný text se skládá podle akce a jazyka, viz GetPrompt).")]
    public string prompt = "E — otevřít";

    /// <summary>
    /// Text nápovědy v aktuálním jazyce, podle akce bodu a klávesy hráče (E / Numpad 1).
    /// Text `prompt` uložený ve scéně je jen záloha pro případ nové akce.
    /// </summary>
    public string GetPrompt(bool isPlayer1)
    {
        string key = isPlayer1 ? "E" : "Numpad 1";
        switch (action)
        {
            case InteriorAction.UpgradeShop:   return key + " — " + Loc.T("obchod s vylepšeními",     "upgrade shop");
            case InteriorAction.QuestShop:     return key + " — " + Loc.T("obchod s questy",          "quest shop");
            case InteriorAction.QuestShopSell: return key + " — " + Loc.T("výkupna (prodej kořist)",  "trading post (sell your loot)");
            case InteriorAction.Exit:          return key + " — " + Loc.T("ven na ostrov",            "back out to the island");
        }
        return prompt;
    }

    /// <summary>Vykoná akci tohoto bodu za daného hráče (0 = P1/sólo, 1 = P2 ve split).</summary>
    public void Trigger(int playerIndex)
    {
        switch (action)
        {
            case InteriorAction.UpgradeShop:
                var us = FindInMyScene<UpgradeShopManager>();
                if (us != null) us.Open(playerIndex);
                break;

            case InteriorAction.QuestShop:
                var qs = FindInMyScene<QuestShopManager>();
                if (qs != null) qs.Open(playerIndex, sellMode: false);
                break;

            case InteriorAction.QuestShopSell:
                var qss = FindInMyScene<QuestShopManager>();
                if (qss != null) qss.Open(playerIndex, sellMode: true);
                break;

            case InteriorAction.Exit:
                LighthouseInterior.ExitToIsland(playerIndex);
                break;
        }
    }

    // Najde komponentu přednostně ve STEJNÉ scéně jako tenhle bod (kvůli coopu,
    // kde vedle sebe běží scéna majáku i herní scéna a každá má svůj obchod).
    private T FindInMyScene<T>() where T : MonoBehaviour
    {
        foreach (var m in FindObjectsByType<T>(FindObjectsSortMode.None))
            if (m.gameObject.scene == gameObject.scene) return m;
        return FindFirstObjectByType<T>();
    }
}
