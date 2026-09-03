using System;

// ─────────────────────────────────────────────────────────────────────────────
//  TileData.cs
//  Základní datové typy pro jedno políčko herní mřížky (mapy).
//  Nejsou to komponenty (MonoBehaviour) — jen "hloupá" data, která se ukládají
//  do save souboru přes GameData.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Druh políčka. Číslo (int) se ukládá do save, proto se hodnoty nesmí měnit
/// (jinak by se staré savy načetly špatně).
/// </summary>
public enum TileType
{
    Empty       = 0, // nepoužívá se, jen výchozí hodnota
    Water       = 1, // obyčejná voda — dá se přes ni plout
    Water_Fish  = 2, // voda s rybami — dá se tu rybařit
    Treasure    = 3, // políčko s pokladem — dá se tu těžit
    Harbor      = 4, // pevnina ostrova — chodí se po ní pěšky
    Pier        = 5, // molo — přechod mezi lodí a pevninou
    UpgradeShop = 6, // starý obchod s vylepšeními (2x2) — nové ostrovy už ho nestaví, drží se kvůli starým savům
    QuestShop   = 7, // starý obchod s questy (2x2) — dtto
    Lighthouse  = 8  // maják na ostrově — pěšky u něj `E` = vejít dovnitř (scéna s obchody)
}

/// <summary>
/// Stav jednoho konkrétního políčka. Ukládá se do save souboru.
/// </summary>
[Serializable]
public class TileStatus
{
    public int  type;          // druh políčka (přetypováno z TileType)
    public bool isExplored;    // true = hráč sem už doplul a odkryl mlhu
    public int  fishRemaining; // kolik ryb na políčku ještě zbývá (jen u Water_Fish)

    public TileStatus(int type)
    {
        this.type = type;
    }
}
