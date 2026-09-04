using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  ChestManager.cs
//  Bedny na ostrovech. Hráč u bedny stojí pěšky a dá `E` → PlayerController
//  zavolá TryOpen(). Bedna dá pár mincí a (pokud hráč zrovna žádný nemá)
//  MEGA QUEST — "poklad na mapě": mapa s vzdáleným místem na moři, kam se
//  dopluje, vykope se poklad a odměna se vyzvedne v kterémkoli QuestShopu.
//
//  Které bedny už jsou otevřené se pamatuje v GameData.openedChests
//  (klíč "x,y"), takže po znovunačtení zůstanou prázdné.
// ─────────────────────────────────────────────────────────────────────────────

public class ChestManager : MonoBehaviour
{
    public static ChestManager Instance { get; private set; }

    private GridManager grid;

    // Krátká hláška po otevření bedny (vykreslí OnGUI).
    private string toast;
    private float  toastUntil;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        grid = FindFirstObjectByType<GridManager>();
    }

    /// <summary>Je bedna na [x,y] už otevřená (kterýmkoli hráčem)?</summary>
    public bool IsOpened(int x, int y)
    {
        if (grid == null) grid = FindFirstObjectByType<GridManager>();
        string key = x + "," + y;
        return grid.gameData.openedChests.Contains(key)
            || grid.gameData.player2OpenedChests.Contains(key);
    }

    /// <summary>Hráč otevírá bednu na [x,y]. Vrací true, když šlo o bednu.</summary>
    public bool TryOpen(int x, int y, int playerIndex)
    {
        if (grid.GetTileType(x, y) != TileType.Chest) return false;

        string key = x + "," + y;
        var opened = playerIndex == 0 ? grid.gameData.openedChests : grid.gameData.player2OpenedChests;

        if (opened.Contains(key))
        {
            Toast("Tahle bedna už je prázdná.");
            return true; // pořád "vyřízeno" — ať se hráč nezkusí nalodit
        }

        opened.Add(key);

        // Pár mincí rovnou z bedny.
        int loot = Random.Range(60, 160);
        AddCoins(playerIndex, loot);
        string msg = "Bedna otevřena!  +" + loot + " mincí.";

        // Mapa (mega quest) — jen když hráč žádný rozdělaný nemá.
        MegaQuest mq = playerIndex == 0 ? grid.gameData.megaQuest : grid.gameData.player2MegaQuest;
        if (!mq.active)
        {
            AssignTreasureMap(mq, x, y);
            msg += "\nUvnitr byla MAPA! Dopluj na [" + mq.targetX + ", " + mq.targetY + "]\na vykopej poklad (mezernik na tom policku).";
        }
        else
        {
            msg += "\n(Mapu si nech na priste — jednu uz mas rozdelanou.)";
        }
        Toast(msg);
        SoundManager.PlayCoin();

        // Překresli bednu (otevřené víko).
        grid.SetTileType(x, y, TileType.Chest);
        grid.Save();
        grid.NotifyWorldChanged();
        return true;
    }

    // Vylosuje cíl mapy: náhodný směr, 35–70 políček od bedny.
    private void AssignTreasureMap(MegaQuest mq, int fromX, int fromY)
    {
        float ang  = Random.value * Mathf.PI * 2f;
        int   dist = Random.Range(35, 71);
        mq.targetX     = fromX + Mathf.RoundToInt(Mathf.Cos(ang) * dist);
        mq.targetY     = fromY + Mathf.RoundToInt(Mathf.Sin(ang) * dist);
        mq.active      = true;
        mq.dug         = false;
        mq.rewardCoins = Random.Range(800, 1600);
    }

    private void AddCoins(int playerIndex, int amount)
    {
        if (playerIndex == 0) grid.gameData.coins        += amount;
        else                  grid.gameData.player2Coins += amount;
    }

    private void Toast(string text)
    {
        toast      = text;
        toastUntil = Time.unscaledTime + 6f;
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(toast) || Time.unscaledTime > toastUntil) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white }
        };

        float w = 520, h = 90;
        var r = new Rect((Screen.width - w) / 2f, 70f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.85f, 0.3f, 1f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(r, toast, style);
    }
}
