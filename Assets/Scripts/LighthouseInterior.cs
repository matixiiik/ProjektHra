using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseInterior.cs
//  Řídí scénu vnitřku majáku.
//
//  • Zajistí, že existuje GameSession s daty (kdyby se scéna spustila přímo
//    z editoru pro test, načte se ze save).
//  • COOP: scéna je načtená ADITIVNĚ jednou a jsou v ní OBA hráči zároveň —
//    jeden modrý (P1), druhý červený (P2). Každý ovládá svou postavičku a
//    má vlastní kameru na svojí půlce obrazovky. První hráč použije
//    postavičku + kameru přímo ze scény, druhý dostane jejich kopii.
//  • Kreslí malý ukazatel mincí.
//  • Statická ExitToIsland(playerIndex) vrátí hráče zpět na ostrov.
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseInterior : MonoBehaviour
{
    public static LighthouseInterior Instance { get; private set; }

    // Kam se v coopu posune celý interiér, aby ho kamera oceánu neviděla.
    private static readonly Vector3 CoopOffset = new Vector3(5000f, 0f, 0f);

    // Barvy trička podle hráče (stejné jako venku na ostrově).
    private static readonly Color P1Color = new Color(0.30f, 0.45f, 0.65f);
    private static readonly Color P2Color = new Color(0.75f, 0.16f, 0.13f);

    // Postavičky a kamery jednotlivých hráčů (coop).
    private readonly InteriorPlayer[] figures = new InteriorPlayer[2];
    private readonly Camera[]         cams    = new Camera[2];

    private GUIStyle coinStyle;

    void Awake()
    {
        Instance = this;

        if (GameSession.Instance == null || GameSession.Instance.Data == null)
        {
            SaveManager.CurrentSlot = PlayerPrefs.GetInt("LastSlot", 0);
            GameSession.Ensure().SetData(SaveManager.LoadGame());
        }

        SoundManager.PlayDoor(); // vrznutí dveří

        if (MultiplayerManager.IsMultiplayer)
            SetUpCoopFirstPlayer();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Coop: interiér je načtený vedle herní scény. Posuň ho pryč od oceánu a
    // nastav prvního hráče (postavička + kamera přímo ze scény).
    private void SetUpCoopFirstPlayer()
    {
        int who = LighthouseManager.PendingPlayerIndex;
        if (who < 0) who = 0;

        Scene scene = gameObject.scene;

        Camera         cam = null;
        InteriorPlayer ip  = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            root.transform.position += CoopOffset;

            if (cam == null)
            {
                Camera c = root.GetComponent<Camera>();
                if (c == null) c = root.GetComponentInChildren<Camera>(true);
                if (c != null) cam = c;
            }
            if (ip == null)
            {
                InteriorPlayer p = root.GetComponent<InteriorPlayer>();
                if (p == null) p = root.GetComponentInChildren<InteriorPlayer>(true);
                if (p != null) ip = p;
            }
        }

        if (ip != null)
        {
            ip.ownerPlayerIndex = who;
            ip.SetAreaCenter(CoopOffset);
            ip.transform.position = CoopOffset + SpawnOffset(who);
            TintFigure(ip.gameObject, who == 1 ? P2Color : P1Color);
            figures[who] = ip;
        }

        cams[who] = cam;
        MultiplayerManager.BeginLighthouseSplit(cam, who);
    }

    /// <summary>COOP: přidej do majáku druhého hráče (kopie postavičky + kamery).</summary>
    public void AddPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex > 1 || figures[playerIndex] != null) return;

        int other = 1 - playerIndex;
        InteriorPlayer template = figures[other];
        Camera         camTpl   = cams[other];
        if (template == null || camTpl == null) return;

        // Kopie postavičky (do TÉHLE scény majáku, ne do herní).
        GameObject figGo = Instantiate(template.gameObject);
        SceneManager.MoveGameObjectToScene(figGo, gameObject.scene);
        figGo.SetActive(true);
        InteriorPlayer ip = figGo.GetComponent<InteriorPlayer>();
        ip.ownerPlayerIndex = playerIndex;
        ip.SetAreaCenter(CoopOffset);
        figGo.transform.position = CoopOffset + SpawnOffset(playerIndex);
        TintFigure(figGo, playerIndex == 1 ? P2Color : P1Color);
        figures[playerIndex] = ip;

        // Kopie kamery (taky do scény majáku).
        GameObject camGo = Instantiate(camTpl.gameObject);
        SceneManager.MoveGameObjectToScene(camGo, gameObject.scene);
        camGo.SetActive(true);
        Camera cam = camGo.GetComponent<Camera>();
        cams[playerIndex] = cam;

        MultiplayerManager.BeginLighthouseSplit(cam, playerIndex);
    }

    /// <summary>COOP: odeber z majáku postavičku + kameru daného hráče.</summary>
    public void RemovePlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex > 1) return;

        MultiplayerManager.EndLighthouseSplit(playerIndex);

        if (figures[playerIndex] != null) Destroy(figures[playerIndex].gameObject);
        if (cams[playerIndex]    != null) Destroy(cams[playerIndex].gameObject);
        figures[playerIndex] = null;
        cams[playerIndex]    = null;
    }

    // Kde v místnosti hráč začne (aby si dva nestáli na sobě).
    private static Vector3 SpawnOffset(int playerIndex)
        => new Vector3(playerIndex == 1 ? 1.3f : -1.3f, 0f, -0.4f);

    // Obarví "tělo" postavičky (díly s materiálem InteriorPlayerMat) přes MaterialPropertyBlock.
    private static void TintFigure(GameObject figure, Color color)
    {
        foreach (var r in figure.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.sharedMaterial == null || !r.sharedMaterial.name.Contains("InteriorPlayer")) continue;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color);
            mpb.SetColor("_Color", color);
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>Odejít z majáku ven na ostrov. playerIndex = kdo odchází (0/1).</summary>
    public static void ExitToIsland(int playerIndex)
    {
        GameSession.Instance.Save(); // ulož nákupy

        // Coop: jen odeber toho hráče (poslední zavře scénu).
        if (MultiplayerManager.IsMultiplayer)
        {
            if (LighthouseManager.Instance != null) LighthouseManager.Instance.ExitCoop(playerIndex);
            return;
        }

        // Sólo: plné přepnutí zpátky do herní scény.
        GameSession.ReturningFromLighthouse = true;  // ať hlavní menu nevyskočí
        if (LighthouseManager.Instance != null) LighthouseManager.Instance.Exit();
        SceneManager.LoadScene("SampleScene");
    }

    void OnGUI()
    {
        if (coinStyle == null)
            coinStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) }
            };

        var data = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (data == null) return;

        if (MultiplayerManager.IsMultiplayer)
        {
            // Ukaž mince každého hráče, co je uvnitř, na jeho půlce obrazovky.
            if (figures[0] != null) DrawCoins(0, data.coins);
            if (figures[1] != null) DrawCoins(1, data.player2Coins);
        }
        else
        {
            DrawCoins(0, data.coins);
        }
    }

    private void DrawCoins(int who, int coins)
    {
        float x = who == 1 ? Screen.width * 0.5f + 20f : 20f;
        float y = MultiplayerManager.IsMultiplayer ? 52f : 16f; // pod souřadnicemi z herního HUD
        GUI.Label(new Rect(x, y, 300, 30), "Mince: " + coins, coinStyle);
    }
}
