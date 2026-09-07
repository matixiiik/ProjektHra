using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseInterior.cs
//  Řídí scénu vnitřku majáku. Je na jednom objektu ve scéně LighthouseInterior.
//
//  • Zajistí, že existuje GameSession s daty (kdyby se scéna spustila přímo
//    z editoru pro test, načte se ze save).
//  • V coopu (scéna je načtená ADITIVNĚ vedle herní) posune celý interiér
//    daleko od oceánu a řekne MultiplayerManageru, ať rozdělí kamery.
//  • Kreslí malý ukazatel mincí.
//  • Statická ExitToIsland() vrátí hráče zpět na ostrov.
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseInterior : MonoBehaviour
{
    // Kam se v coopu posune celý interiér, aby ho kamera oceánu neviděla.
    private static readonly Vector3 CoopOffset = new Vector3(5000f, 0f, 0f);

    private GUIStyle coinStyle;

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

    // Coop: interiér je načtený vedle herní scény. Posuň ho pryč od oceánu
    // a nech MultiplayerManager dát ho tomu hráči, co vešel, na jeho půlku.
    private void SetUpCoopSplit()
    {
        int who = LighthouseManager.PendingPlayerIndex;
        if (who < 0) who = 0;

        Scene scene = gameObject.scene;

        Camera interiorCam = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            root.transform.position += CoopOffset;
            if (interiorCam == null)
            {
                Camera c = root.GetComponent<Camera>();
                if (c == null) c = root.GetComponentInChildren<Camera>(true);
                if (c != null) interiorCam = c;
            }
        }

        // Pochozí hráč: nastav mu, kterého hráče ovládá, a posuň střed pochozí
        // plochy na ten offset (pohybuje se s mezemi kolem středu).
        var ip = FindFirstObjectByType<InteriorPlayer>();
        if (ip != null)
        {
            ip.ownerPlayerIndex = who;
            ip.SetAreaCenter(CoopOffset);
        }

        MultiplayerManager.BeginLighthouseSplit(interiorCam, who);
    }

    /// <summary>Odejít z majáku ven na ostrov (volá dveře v interiéru).</summary>
    public static void ExitToIsland()
    {
        GameSession.Instance.Save(); // ulož nákupy

        // Coop: jen odečti scénu majáku, herní scéna (a hráč 2) běží dál.
        if (MultiplayerManager.IsMultiplayer)
        {
            if (LighthouseManager.Instance != null) LighthouseManager.Instance.ExitCoop();
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

        // V coopu je interiér na půlce obrazovky toho hráče — mince tam.
        bool p2Inside = MultiplayerManager.IsMultiplayer && LighthouseManager.InsidePlayerIndex == 1;
        int  coinsP1  = GameSession.Instance != null && GameSession.Instance.Data != null ? GameSession.Instance.Data.coins : 0;
        int  coinsP2  = GameSession.Instance != null && GameSession.Instance.Data != null ? GameSession.Instance.Data.player2Coins : 0;
        float labelX  = p2Inside ? Screen.width * 0.5f + 20f : 20f;
        int   coins   = p2Inside ? coinsP2 : coinsP1;
        GUI.Label(new Rect(labelX, 16, 300, 30), "Mince: " + coins, coinStyle);
    }
}
