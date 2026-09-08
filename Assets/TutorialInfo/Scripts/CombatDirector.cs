using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  CombatDirector.cs
//  Řídí soubojový systém:
//   • drží spawnutá děla nepřátelských ostrovů (v okolí hráče) a piráty,
//   • jednou za čas přidá pirátskou loď, když hráč pluje po otevřeném moři,
//   • kreslí boss health bar piráta, který zrovna útočí na hráče,
//   • rozdává odměny za potopení piráta / zničení ostrovního děla,
//   • krátké hlášky ("toast") dole na obrazovce.
//
//  Objekt se vytvoří sám (CombatDirector.Ensure(), volá GridManager.Awake) —
//  nic se nezapojuje ve scéně. Stejný princip jako SoundManager.
// ─────────────────────────────────────────────────────────────────────────────

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    private const float ISLAND_SCAN_EVERY = 1.5f;
    private const float ISLAND_KEEP_RANGE = 40f; // dál než tohle se dělo ostrova uklidí
    private const int   MAX_PIRATES       = 2;
    private const float PIRATE_MIN_GAP    = 22f; // min. pauza mezi spawny pirátů
    private const float PIRATE_MAX_GAP    = 42f;
    private const float PIRATE_SPAWN_MIN  = 14f; // jak daleko od hráče pirát vznikne
    private const float PIRATE_SPAWN_MAX  = 20f;

    private GridManager grid;
    private readonly List<PirateShip>          pirates = new List<PirateShip>();
    private readonly List<HostileIslandCannon> cannons = new List<HostileIslandCannon>();

    private float nextIslandScan;
    private float nextPirateSpawn;

    private string toastText;
    private float  toastUntil;

    private GUIStyle bossLabel, toastStyle;

    /// <summary>Vytvoří CombatDirector, pokud ještě není.</summary>
    public static void Ensure()
    {
        if (Instance != null) return;
        var go = new GameObject("CombatDirector");
        Instance = go.AddComponent<CombatDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        grid = FindFirstObjectByType<GridManager>();
        nextPirateSpawn = Time.time + Random.Range(PIRATE_MIN_GAP, PIRATE_MAX_GAP);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (grid == null) { grid = FindFirstObjectByType<GridManager>(); return; }

        pirates.RemoveAll(p => p == null);
        cannons.RemoveAll(c => c == null);

        if (Time.time >= nextIslandScan)
        {
            nextIslandScan = Time.time + ISLAND_SCAN_EVERY;
            ScanHostileIslands();
        }

        if (Time.time >= nextPirateSpawn)
        {
            nextPirateSpawn = Time.time + Random.Range(PIRATE_MIN_GAP, PIRATE_MAX_GAP);
            TrySpawnPirate();
        }
    }

    // ── Nejbližší hráč na vodě — pluje NEBO plave (rozbitá loď). Piráti i děla
    //    míří na něj a klidně ho dorazí, i když už nemá loď. ──────────────────
    public PlayerController NearestSailingPlayer(Vector3 from)
    {
        PlayerController best = null;
        float bestSq = float.MaxValue;
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!pc.IsSailing && !pc.IsSwimming) continue;
            float sq = (pc.transform.position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = pc; }
        }
        return best;
    }

    // ── Cíle pro hráčovy dělové koule ─────────────────────────────────────
    public PirateShip PirateNear(Vector3 pos, float radius)
    {
        foreach (var p in pirates) if (p != null && p.IsNear(pos, radius)) return p;
        return null;
    }

    public HostileIslandCannon CannonNear(Vector3 pos, float radius)
    {
        foreach (var c in cannons) if (c != null && c.IsNear(pos, radius)) return c;
        return null;
    }

    // ── Nepřátelské ostrovy: drž dělo spawnuté, když je ostrov u hráče ─────
    void ScanHostileIslands()
    {
        var player = NearestAnyPlayer();
        if (player == null) return;
        Vector3 pp = player.transform.position;

        foreach (string key in grid.ActiveHostileIslandKeys())
        {
            Vector2Int center = GridManager.KeyToTile(key);
            float dist = Vector2.Distance(new Vector2(center.x, center.y), new Vector2(pp.x, pp.z));

            HostileIslandCannon existing = cannons.Find(c => c != null && c.islandKey == key);

            if (dist > ISLAND_KEEP_RANGE)
            {
                if (existing != null) { Destroy(existing.gameObject); cannons.Remove(existing); }
                continue;
            }

            if (existing == null && grid.TryGetHostileCannonSpot(center, out Vector2Int spot))
                cannons.Add(HostileIslandCannon.Spawn(spot, key));
        }
    }

    // ── Spawn pirátů ──────────────────────────────────────────────────────
    void TrySpawnPirate()
    {
        if (pirates.Count >= MAX_PIRATES) return;

        // Noví piráti se objevují jen když někdo PLUJE V LODI (ne když plave —
        // to už je dost bezmocný a nechceme smyčku smrti).
        PlayerController player = null;
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (pc.IsSailing) { player = pc; break; }
        if (player == null) return;

        Vector3 pp = player.transform.position;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(PIRATE_SPAWN_MIN, PIRATE_SPAWN_MAX);
            int x = Mathf.RoundToInt(pp.x + Mathf.Cos(a) * r);
            int z = Mathf.RoundToInt(pp.z + Mathf.Sin(a) * r);

            TileType t = grid.GetTileType(x, z);
            if (t != TileType.Water && t != TileType.Water_Fish) continue;

            float roll = Random.value;
            int size = roll < 0.55f ? 0 : roll < 0.88f ? 1 : 2;
            pirates.Add(PirateShip.Spawn(new Vector3(x, 0f, z), size));
            return;
        }
    }

    // ── Odměny / hlášky ──────────────────────────────────────────────────
    public void OnPirateSunk(PirateShip p)
    {
        int reward = p.size == 0 ? EconomyConfig.PirateRewardSmall
                   : p.size == 1 ? EconomyConfig.PirateRewardMedium
                   :               EconomyConfig.PirateRewardLarge;
        var pc = NearestAnyPlayer();
        if (pc != null) pc.RewardCoins(reward);
        if (grid != null) { grid.gameData.pirateKills++; grid.Save(); }
        SoundManager.PlayCoin();
        Toast("Pirat potopen!  +" + reward + " minci");
    }

    public void OnIslandCannonDestroyed(string key)
    {
        if (grid != null) { grid.MarkIslandCleared(key); grid.Save(); }
        var pc = NearestAnyPlayer();
        if (pc != null) pc.RewardCoins(EconomyConfig.IslandCannonReward);
        SoundManager.PlayCoin();
        Toast("Ostrovni delo zniceno!  +" + EconomyConfig.IslandCannonReward + " minci");
    }

    public void Toast(string text)
    {
        toastText  = text;
        toastUntil = Time.time + 2.6f;
    }

    /// <summary>Jednotkový směr PRYČ od nejbližšího piráta / ostrovního děla (pro "odplavání" po potopení).</summary>
    public Vector3 AwayFromNearestThreat(Vector3 pos)
    {
        Vector3 nearest = Vector3.zero;
        float bestSq = float.MaxValue;
        foreach (var p in pirates) if (p != null) { float sq = (p.transform.position - pos).sqrMagnitude; if (sq < bestSq) { bestSq = sq; nearest = p.transform.position; } }
        foreach (var c in cannons) if (c != null) { float sq = (c.transform.position - pos).sqrMagnitude; if (sq < bestSq) { bestSq = sq; nearest = c.transform.position; } }

        Vector3 away = pos - nearest; away.y = 0f;
        return away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
    }

    private PlayerController NearestAnyPlayer()
    {
        PlayerController any = null;
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (pc.playerIndex == 0) return pc; // P1 preferovaně
            any = pc;
        }
        return any;
    }

    // ── Boss health bar + toast ──────────────────────────────────────────
    void OnGUI()
    {
        EnsureStyles();

        // Boss bar: nejbližší pirát, který zrovna útočí na nějakého hráče.
        PirateShip boss = null;
        foreach (var p in pirates) if (p != null && p.Engaged) { boss = p; break; }

        if (boss != null)
        {
            float w = 360f, h = 22f;
            float x = (Screen.width - w) / 2f;
            float y = 54f;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 20), Texture2D.whiteTexture);
            GUI.color = new Color(0.75f, 0.15f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(x, y, w * boss.HpFraction, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string name = boss.size == 0 ? "PIRAT (mala lod)" : boss.size == 1 ? "PIRAT (stredni lod)" : "PIRAT (velka lod)";
            GUI.Label(new Rect(x, y + h, w, 18f), name, bossLabel);
        }

        if (Time.time < toastUntil && !string.IsNullOrEmpty(toastText))
            GUI.Label(new Rect(0f, Screen.height - 168f, Screen.width, 26f), toastText, toastStyle);
    }

    void EnsureStyles()
    {
        if (bossLabel != null) return;
        bossLabel = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0.8f) }
        };
        toastStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.9f, 0.55f) }
        };
    }
}
