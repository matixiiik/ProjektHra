using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  MultiplayerManager.cs
//  Zapíná / vypíná split-screen multiplayer pro dva hráče na jedné klávesnici
//  (P1 = WASD, P2 = šipky/numpad).
//
//  Princip: ve scéně je normálně jen hráč 1. Při startu multiplayeru se
//  hráč 1 naklonuje (Instantiate), z kopie se smažou věci, které mají být
//  ve scéně jen jednou (kamera, HUD, menu...), a doplní se druhá kamera + HUD
//  + minimapa pro P2. Obrazovka se rozdělí na dvě poloviny přes Camera.rect.
//
//  Statické metody StartMultiplayer() / Stop() volá menu a pauza.
// ─────────────────────────────────────────────────────────────────────────────

public class MultiplayerManager : MonoBehaviour
{
    /// <summary>Běží teď hra v režimu dvou hráčů? Čtou to skoro všechny ostatní skripty.</summary>
    public static bool IsMultiplayer { get; private set; }

    // Odkaz na jedinou instanci ve scéně (aby statické metody měly na co volat).
    private static MultiplayerManager instance;

    private Camera            p1Camera;
    private Camera            p2Camera;
    private PlayerController  p1Player;
    private PlayerController  p2Player;
    private HUDCounter        p1HUD;
    private HUDCounter        p2HUD;
    private MinimapUIRenderer p2Minimap;

    void Awake() { instance = this; }

    // ── Veřejné API (volá se odjinud) ─────────────────────────────────────────
    public static void StartMultiplayer() { instance?.Setup(); }
    public static void Stop()             { instance?.Teardown(); }

    // ── Zapnutí split screenu ─────────────────────────────────────────────────
    void Setup()
    {
        p1Camera = Camera.main;
        p1Player = FindFirstObjectByType<PlayerController>();
        p1HUD    = FindFirstObjectByType<HUDCounter>();

        if (p1Camera == null || p1Player == null)
        {
            Debug.LogWarning("MultiplayerManager: chybí kamera nebo hráč.");
            return;
        }

        IsMultiplayer = true;

        // P1 kamera → levá polovina obrazovky (x=0, šířka=0.5).
        p1Camera.rect = new Rect(0f, 0f, 0.5f, 1f);

        // P1 HUD → přesunout ke středu (na kraj levé poloviny).
        if (p1HUD != null) p1HUD.UpdateLayout(true);

        // Vytvoř hráče 2 jako kopii hráče 1.
        GameObject p2Go = Instantiate(p1Player.gameObject);
        p2Go.name            = "Player2";
        p2Player             = p2Go.GetComponent<PlayerController>();
        p2Player.playerIndex = 1; // od teď se chová jako P2 (jiné klávesy, jiná ekonomika)

        // Z kopie smaž komponenty, které mají být ve scéně jen jednou.
        // (ShipModelSwitcher se NECHÁVÁ — řídí model lodě P2.)
        // Kamery se ruší jako celé objekty — v URP nejdou smazat jen Camera
        // (závisí na ní UniversalAdditionalCameraData). P2 dostane vlastní kameru níž.
        foreach (var c in p2Go.GetComponentsInChildren<Camera>(true))        Destroy(c.gameObject);
        foreach (var c in p2Go.GetComponentsInChildren<CameraFollow>())      Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<HUDCounter>())        Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<MinimapUIRenderer>()) Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<GameConsole>())       Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<PauseMenu>())         Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<MainMenuManager>())   Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<AudioListener>())     Destroy(c);

        // Vlastní HUD pro P2 (sám si při Start() postaví canvas).
        GameObject p2HudGo = new GameObject("P2HUD");
        p2HUD = p2HudGo.AddComponent<HUDCounter>();
        p2HUD.playerIndex = 1;

        // Vlastní minimapa pro P2 (minimapImage == null → sama si vytvoří canvas vpravo dole).
        GameObject p2MapGo = new GameObject("P2Minimap");
        p2Minimap = p2MapGo.AddComponent<MinimapUIRenderer>();
        p2Minimap.playerIndex = 1;

        // Vlastní kamera pro P2 → pravá polovina obrazovky, jinak stejná jako P1.
        GameObject p2CamGo = new GameObject("P2Camera");
        p2Camera = p2CamGo.AddComponent<Camera>();
        p2Camera.CopyFrom(p1Camera);
        p2Camera.rect  = new Rect(0.5f, 0f, 0.5f, 1f);
        p2Camera.tag   = "Untagged";           // "MainCamera" smí být jen P1
        p2Camera.depth = p1Camera.depth + 1;

        // Postav P2 kameru na stejný odstup od P2 hráče, jaký má P1 kamera od P1 hráče.
        Vector3 camOffset = p1Camera.transform.position - p1Player.transform.position;
        p2CamGo.transform.position = p2Player.transform.position + camOffset;
        p2CamGo.transform.rotation = p1Camera.transform.rotation;
    }

    // ── Vypnutí split screenu (návrat do hlavního menu) ───────────────────────
    void Teardown()
    {
        IsMultiplayer = false;

        // P1 kamera zpět na celou obrazovku.
        if (p1Camera != null) p1Camera.rect = new Rect(0f, 0f, 1f, 1f);

        // P1 HUD zpět do pravého horního rohu.
        if (p1HUD != null) p1HUD.UpdateLayout(false);

        // Smaž všechno, co patřilo P2.
        if (p2Player  != null) Destroy(p2Player.gameObject);
        if (p2Camera  != null) Destroy(p2Camera.gameObject);
        if (p2HUD     != null) Destroy(p2HUD.gameObject);
        if (p2Minimap != null) Destroy(p2Minimap.gameObject);

        p2Camera  = null;
        p2Player  = null;
        p2HUD     = null;
        p2Minimap = null;
    }

    // ── Každý snímek: drž P2 kameru za P2 hráčem se stejným odstupem jako P1 ───
    void LateUpdate()
    {
        if (!IsMultiplayer) return;
        if (p1Camera == null || p2Camera == null || p1Player == null || p2Player == null) return;

        Vector3 camOffset = p1Camera.transform.position - p1Player.transform.position;
        p2Camera.transform.position = p2Player.transform.position + camOffset;
        p2Camera.transform.rotation = p1Camera.transform.rotation;
    }
}
