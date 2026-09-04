using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseInterior.cs
//  Řídí scénu vnitřku majáku. Je na jednom objektu v scéně LighthouseInterior.
//
//  • Zajistí, že existuje GameSession s daty (kdyby se scéna spustila přímo
//    z editoru pro test, načte se ze save).
//  • Kreslí malý ukazatel mincí (aby hráč viděl, kolik má na nákupy).
//  • Statická ExitToIsland() vrátí hráče zpět na ostrov.
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseInterior : MonoBehaviour
{
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
    }

    /// <summary>Odejít z majáku ven na ostrov (volá dveře v interiéru).</summary>
    public static void ExitToIsland()
    {
        GameSession.Instance.Save();                 // ulož nákupy
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

        int coins = GameSession.Instance != null && GameSession.Instance.Data != null
            ? GameSession.Instance.Data.coins : 0;
        GUI.Label(new Rect(20, 16, 300, 30), "Mince: " + coins, coinStyle);
    }
}
