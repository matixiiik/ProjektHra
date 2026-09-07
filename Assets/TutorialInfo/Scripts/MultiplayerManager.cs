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

    // Kamera hráče 2: myš patří P1 (jeho orbitální kamera), P2 si ji otáčí
    // klávesami Numpad + / - (nebo horními + / -).
    public  float p2TurnSpeed   = 90f; // stupňů za sekundu
    private float p2Yaw;
    private float p2Pitch       = 52f;
    private float p2Distance    = 14f;
    private float p2PivotHeight = 0.8f;

    void Awake() { instance = this; }

    // ── Veřejné API (volá se odjinud) ─────────────────────────────────────────
    public static void StartMultiplayer() { instance?.Setup(); }
    public static void Stop()             { instance?.Teardown(); }

    // Coop: hráč 1 vešel do majáku (additivní scéna). Jeho půlka ukáže interiér,
    // hráč 2 hraje dál. Volá LighthouseInterior po načtení scény.
    public static void BeginLighthouseSplit(Camera interiorCam) => instance?.DoBeginLighthouseSplit(interiorCam);
    // Coop: hráč 1 vyšel z majáku — vrať jeho půlce normální kameru a ovládání.
    public static void EndLighthouseSplit() => instance?.DoEndLighthouseSplit();

    private CameraOrbit p1Orbit; // orbitální kamera hráče 1 (kvůli vypnutí, když je v majáku)

    void DoBeginLighthouseSplit(Camera interiorCam)
    {
        if (interiorCam != null)
        {
            interiorCam.rect  = new Rect(0f, 0f, 0.5f, 1f);           // levá půlka = hráč 1
            interiorCam.depth = (p1Camera != null ? p1Camera.depth : 0);
        }

        // Vypni kameru hráče 1 v herní scéně (ať se nekreslí přes interiér).
        if (p1Camera != null)
        {
            p1Camera.enabled = false;
            AudioListener al = p1Camera.GetComponent<AudioListener>();
            if (al != null) al.enabled = false; // poslouchá teď kamera interiéru
            p1Orbit = p1Camera.GetComponent<CameraOrbit>();
            if (p1Orbit != null) p1Orbit.enabled = false;
        }

        // Zmraž hráče 1 v herní scéně (uvnitř majáku za něj chodí InteriorPlayer).
        if (p1Player != null) p1Player.enabled = false;
    }

    void DoEndLighthouseSplit()
    {
        if (p1Camera != null)
        {
            p1Camera.enabled = true;
            AudioListener al = p1Camera.GetComponent<AudioListener>();
            if (al != null) al.enabled = true;
            if (p1Orbit != null) p1Orbit.enabled = true;
        }
        if (p1Player != null) p1Player.enabled = true;
    }

    // ── Zapnutí split screenu ─────────────────────────────────────────────────
    void Setup()
    {
        if (IsMultiplayer) return; // pojistka proti dvojímu zapnutí (jinak by vzniklo víc kopií P2)

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

        // Hráč 2 má červené tričko místo modrého (ať se hráči na první pohled rozliší).
        if (p2Player.headDot != null)
        {
            Transform body = p2Player.headDot.transform.Find("Body");
            Renderer  br   = body != null ? body.GetComponent<Renderer>() : null;
            if (br != null)
            {
                Color red = new Color(0.75f, 0.16f, 0.13f);
                var mpb = new MaterialPropertyBlock();
                br.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", red);
                mpb.SetColor("_Color", red);
                br.SetPropertyBlock(mpb);
            }
        }

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

        // Převezmi úhel/odstup z orbitální kamery P1, ať P2 začíná stejně natočená
        // (dál už si ji točí sama klávesami).
        CameraOrbit p1Orbit = p1Camera.GetComponent<CameraOrbit>();
        if (p1Orbit != null)
        {
            p2Pitch       = p1Orbit.pitch;
            p2Distance    = p1Orbit.distance;
            p2PivotHeight = p1Orbit.pivotHeight;
            p2Yaw         = p1Orbit.yaw;
        }

        // Hráč 2 se hýbe podle SVÉ kamery, ne podle kamery hráče 1.
        p2Player.viewCamera = p2CamGo.transform;

        PositionP2Camera(); // hned ji postav, ať první snímek nebliká z počátku
    }

    // Postaví kameru hráče 2 za hráče 2 podle p2Yaw / p2Pitch / p2Distance.
    // (Stejná matematika jako CameraOrbit, jen ve světových souřadnicích —
    //  kamera P2 není potomkem hráče.)
    void PositionP2Camera()
    {
        if (p2Camera == null || p2Player == null) return;

        Quaternion rot = Quaternion.Euler(p2Pitch, p2Yaw, 0f);
        Vector3 pivot  = p2Player.transform.position + Vector3.up * p2PivotHeight;
        p2Camera.transform.position = pivot + rot * new Vector3(0f, 0f, -p2Distance);
        p2Camera.transform.rotation = rot;
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

    // ── Každý snímek: drž P2 kameru za P2 hráčem; P2 si ji otáčí klávesami +/- ──
    void LateUpdate()
    {
        if (!IsMultiplayer) return;
        if (p2Camera == null || p2Player == null) return;

        // Myš patří hráči 1. Hráč 2 točí kameru: Numpad + (i horní +) doprava,
        // Numpad - (i horní -) doleva. (Obchod hráče 1 / maják hráči 2 kameru neblokuje.)
        bool uiBlocking = MainMenuManager.IsVisible || GameConsole.IsOpen;
        if (!uiBlocking)
        {
            if (Input.GetKey(KeyCode.KeypadPlus)  || Input.GetKey(KeyCode.Equals)) p2Yaw += p2TurnSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus))  p2Yaw -= p2TurnSpeed * Time.deltaTime;
        }

        PositionP2Camera();
    }
}
