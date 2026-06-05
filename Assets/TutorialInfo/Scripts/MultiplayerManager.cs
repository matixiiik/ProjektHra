using UnityEngine;

public class MultiplayerManager : MonoBehaviour
{
    public static bool IsMultiplayer { get; private set; }

    private static MultiplayerManager instance;

    private Camera            p1Camera;
    private Camera            p2Camera;
    private PlayerController  p1Player;
    private PlayerController  p2Player;
    private HUDCounter        p1HUD;
    private HUDCounter        p2HUD;
    private MinimapUIRenderer p2Minimap;

    void Awake() { instance = this; }

    // ── Veřejné API ───────────────────────────────────────────────────────────
    public static void StartMultiplayer() { instance?.Setup(); }
    public static void Stop()             { instance?.Teardown(); }

    // ── Nastavení split screenu ───────────────────────────────────────────────
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

        // P1 kamera → levá polovina
        p1Camera.rect = new Rect(0f, 0f, 0.5f, 1f);

        // P1 HUD → přesunout do levé poloviny
        if (p1HUD != null) p1HUD.UpdateLayout(true);

        // Vytvoř P2 hráče (kopie P1)
        GameObject p2Go = Instantiate(p1Player.gameObject);
        p2Go.name    = "Player2";
        p2Player     = p2Go.GetComponent<PlayerController>();
        p2Player.playerIndex = 1;

        // Odstraň komponenty které mají být jen jednou v scéně
        // ShipModelSwitcher PONECHAT — řídí P2 loď samostatně
        foreach (var c in p2Go.GetComponentsInChildren<Camera>())            Destroy(c);  // P2 nesmí mít vlastní kameru
        foreach (var c in p2Go.GetComponentsInChildren<CameraFollow>())      Destroy(c);  // ani CameraFollow
        foreach (var c in p2Go.GetComponentsInChildren<HUDCounter>())        Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<MinimapUIRenderer>()) Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<GameConsole>())        Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<PauseMenu>())          Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<MainMenuManager>())    Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<AudioListener>())      Destroy(c);

        // Vytvoř P2 HUD
        GameObject p2HudGo = new GameObject("P2HUD");
        p2HUD = p2HudGo.AddComponent<HUDCounter>();
        p2HUD.playerIndex = 1;
        // Start() se zavolá příští snímek → HUDCounter si sám postaví canvas

        // Vytvoř P2 minimapu (auto-canvas v bottom-right)
        GameObject p2MapGo = new GameObject("P2Minimap");
        p2Minimap = p2MapGo.AddComponent<MinimapUIRenderer>();
        p2Minimap.playerIndex = 1;
        // minimapImage == null → MinimapUIRenderer.Start() vytvoří canvas automaticky

        // Vytvoř P2 kameru → pravá polovina, stejné parametry jako P1
        GameObject p2CamGo = new GameObject("P2Camera");
        p2Camera = p2CamGo.AddComponent<Camera>();
        p2Camera.CopyFrom(p1Camera);
        p2Camera.rect  = new Rect(0.5f, 0f, 0.5f, 1f);
        p2Camera.tag   = "Untagged";
        p2Camera.depth = p1Camera.depth + 1;

        // Nastav počáteční pozici P2 kamery (stejný offset jako P1 kamera od P1 hráče)
        Vector3 camOffset = p1Camera.transform.position - p1Player.transform.position;
        p2CamGo.transform.position = p2Player.transform.position + camOffset;
        p2CamGo.transform.rotation = p1Camera.transform.rotation;
    }

    void Teardown()
    {
        IsMultiplayer = false;

        // P1 kamera zpět na celou obrazovku
        if (p1Camera != null) p1Camera.rect = new Rect(0f, 0f, 1f, 1f);

        // P1 HUD zpět na pravý kraj
        if (p1HUD != null) p1HUD.UpdateLayout(false);

        if (p2Player  != null) Destroy(p2Player.gameObject);
        if (p2Camera  != null) Destroy(p2Camera.gameObject);
        if (p2HUD     != null) Destroy(p2HUD.gameObject);
        if (p2Minimap != null) Destroy(p2Minimap.gameObject);

        p2Camera  = null;
        p2Player  = null;
        p2HUD     = null;
        p2Minimap = null;
    }

    // ── P2 kamera sleduje P2 hráče se stejným offsetem jako P1 kamera P1 hráče
    void LateUpdate()
    {
        if (!IsMultiplayer) return;
        if (p1Camera == null || p2Camera == null || p1Player == null || p2Player == null) return;

        Vector3 camOffset = p1Camera.transform.position - p1Player.transform.position;
        p2Camera.transform.position = p2Player.transform.position + camOffset;
        p2Camera.transform.rotation = p1Camera.transform.rotation;
    }
}
