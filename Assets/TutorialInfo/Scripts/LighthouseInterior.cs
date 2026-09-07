using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseInterior.cs
//  Řídí (jednu kopii) scény vnitřku majáku. Je na jednom objektu ve scéně.
//
//  • Zajistí, že existuje GameSession s daty (kdyby se scéna spustila přímo
//    z editoru pro test, načte se ze save).
//  • V coopu (scéna je načtená ADITIVNĚ vedle herní) posune celý interiér
//    daleko od oceánu i od druhé kopie a řekne MultiplayerManageru, ať dá
//    interiér tomu hráči na jeho půlku obrazovky. Můžou být uvnitř oba naráz —
//    každý má vlastní kopii scény.
//  • Kreslí malý ukazatel mincí.
//  • Statická ExitToIsland(playerIndex) vrátí hráče zpět na ostrov.
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseInterior : MonoBehaviour
{
    private GUIStyle coinStyle;

    // Kterého hráče je tahle kopie interiéru (0 = P1/sólo, 1 = P2). Coop.
    private int who = 0;

    // Kam se v coopu posune tahle kopie interiéru (různě pro P1 a P2, ať se nepřekrývají).
    private static Vector3 OffsetFor(int playerIndex)
        => new Vector3(playerIndex == 1 ? 10000f : 5000f, 0f, 0f);

    void Awake()
    {
        // Data by tu měla být z GameSession (přežila přechod scény). Když ne
        // (přímé spuštění scény), načti poslední save.
        if (GameSession.Instance == null || GameSession.Instance.Data == null)
        {
            SaveManager.CurrentSlot = PlayerPrefs.GetInt("LastSlot", 0);
            GameSession.Ensure().SetData(SaveManager.LoadGame());
        }

        SoundManager.PlayDoor(); // vrznutí dveří — hráč právě vešel dovnitř

        if (MultiplayerManager.IsMultiplayer)
            SetUpCoopSplit();
    }

    // Coop: tahle kopie interiéru je načtená vedle herní scény. Posuň ji pryč
    // od oceánu (a od druhé kopie) a nech MultiplayerManager dát ji tomu hráči.
    private void SetUpCoopSplit()
    {
        who = LighthouseManager.PendingPlayerIndex;
        if (who < 0) who = 0;

        Vector3 off = OffsetFor(who);
        Scene scene = gameObject.scene;

        Camera         interiorCam = null;
        InteriorPlayer ip          = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            root.transform.position += off;

            if (interiorCam == null)
            {
                Camera c = root.GetComponent<Camera>();
                if (c == null) c = root.GetComponentInChildren<Camera>(true);
                if (c != null) interiorCam = c;
            }
            if (ip == null)
            {
                InteriorPlayer p = root.GetComponent<InteriorPlayer>();
                if (p == null) p = root.GetComponentInChildren<InteriorPlayer>(true);
                if (p != null) ip = p;
            }
        }

        // Pochozí hráč: kterého hráče ovládá + střed pochozí plochy na ten offset.
        if (ip != null)
        {
            ip.ownerPlayerIndex = who;
            ip.SetAreaCenter(off);
        }

        MultiplayerManager.BeginLighthouseSplit(interiorCam, who);
    }

    /// <summary>Odejít z majáku ven na ostrov. playerIndex = kdo odchází (0/1).</summary>
    public static void ExitToIsland(int playerIndex)
    {
        GameSession.Instance.Save(); // ulož nákupy

        // Coop: jen odečti kopii scény toho hráče, zbytek běží dál.
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

        // V coopu je tahle kopie interiéru na půlce obrazovky svého hráče — mince tam.
        bool  p2   = MultiplayerManager.IsMultiplayer && who == 1;
        var   data = GameSession.Instance != null ? GameSession.Instance.Data : null;
        int   coins = data == null ? 0 : (p2 ? data.player2Coins : data.coins);
        float labelX = p2 ? Screen.width * 0.5f + 20f : 20f;
        // Kousek níž, ať to nekryje souřadnice z herního HUD (levý horní roh).
        float labelY = MultiplayerManager.IsMultiplayer ? 52f : 16f;
        GUI.Label(new Rect(labelX, labelY, 300, 30), "Mince: " + coins, coinStyle);
    }
}
