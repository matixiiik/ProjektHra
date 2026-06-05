using UnityEngine;

public class MultiplayerManager : MonoBehaviour
{
    public static bool IsMultiplayer { get; private set; }

    private static MultiplayerManager instance;

    private Camera           p1Camera;
    private Camera           p2Camera;
    private PlayerController p1Player;
    private PlayerController p2Player;

    void Awake() { instance = this; }

    // ── Veřejné API ───────────────────────────────────────────────────────────

    public static void StartMultiplayer() { instance?.Setup(); }

    public static void Stop() { instance?.Teardown(); }

    // ── Nastavení split screenu ───────────────────────────────────────────────

    void Setup()
    {
        p1Camera = Camera.main;
        p1Player = FindFirstObjectByType<PlayerController>();

        if (p1Camera == null || p1Player == null)
        {
            Debug.LogWarning("MultiplayerManager: chybí kamera nebo hráč.");
            return;
        }

        IsMultiplayer = true;

        // P1 kamera → levá polovina
        p1Camera.rect = new Rect(0f, 0f, 0.5f, 1f);

        // Vytvoř P2 hráče (kopie P1)
        GameObject p2Go = Instantiate(p1Player.gameObject);
        p2Go.name = "Player2";
        p2Player  = p2Go.GetComponent<PlayerController>();
        p2Player.playerIndex = 1;

        // Odstraň komponenty které mají být jen jednou v scéně
        foreach (var c in p2Go.GetComponentsInChildren<HUDCounter>())        Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<MinimapUIRenderer>()) Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<ShipModelSwitcher>()) Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<GameConsole>())        Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<PauseMenu>())          Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<MainMenuManager>())    Destroy(c);
        foreach (var c in p2Go.GetComponentsInChildren<AudioListener>())      Destroy(c);

        // Vytvoř P2 kameru → pravá polovina, stejná nastavení jako P1
        GameObject p2CamGo = new GameObject("P2Camera");
        p2Camera = p2CamGo.AddComponent<Camera>();
        p2Camera.CopyFrom(p1Camera);
        p2Camera.rect  = new Rect(0.5f, 0f, 0.5f, 1f);
        p2Camera.tag   = "Untagged";
        p2Camera.depth = p1Camera.depth + 1;
    }

    void Teardown()
    {
        IsMultiplayer = false;

        if (p1Camera != null) p1Camera.rect = new Rect(0f, 0f, 1f, 1f);
        if (p2Player != null) Destroy(p2Player.gameObject);
        if (p2Camera != null) Destroy(p2Camera.gameObject);

        p2Camera = null;
        p2Player = null;
    }

    // ── Každá kamera sleduje svého hráče se stejným offsetem ─────────────────

    void LateUpdate()
    {
        if (!IsMultiplayer) return;
        if (p1Camera == null || p1Player == null || p2Camera == null || p2Player == null) return;

        // Spočítej offset kamery vůči hráči (výška + úhel kamery)
        Vector3 camOffset = p1Camera.transform.position - p1Player.transform.position;

        // P1 kamera zůstává tam kde je (sleduje ji jiný systém / je child hráče)
        // P2 kamera aplikuje stejný offset na P2 hráče → každý je ve středu své strany
        p2Camera.transform.position = p2Player.transform.position + camOffset;
        p2Camera.transform.rotation = p1Camera.transform.rotation;
    }
}
