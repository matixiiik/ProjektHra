using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  DeathScreen.cs
//  Obrazovka smrti. Ukáže se, když hráči (panáčkovi) klesne zdraví na 0.
//  Zastaví hru (Time.timeScale = 0) a nabídne dvě tlačítka:
//    • RESPAWN     — hráč se objeví s VESLICÍ na nejbližším ostrově od místa
//                    smrti. Mince a rozdělaný mega quest zůstanou, ale kořist
//                    (ryby, poklady, náboje) i všechna vylepšení zmizí.
//    • HLAVNÍ MENU — vrátí do hlavního menu.
//
//  Objekt se vytváří sám přes DeathScreen.Show(). Nic se nezapojuje ve scéně.
// ─────────────────────────────────────────────────────────────────────────────

public class DeathScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private static DeathScreen instance;
    private int      deadPlayerIndex;
    private GUIStyle titleStyle, subStyle, buttonStyle;
    private bool     stylesReady;

    /// <summary>Zobrazí obrazovku smrti pro daného hráče.</summary>
    public static void Show(int playerIndex)
    {
        if (IsOpen) return;

        if (instance == null)
        {
            var go = new GameObject("DeathScreen");
            instance = go.AddComponent<DeathScreen>();
        }
        instance.deadPlayerIndex = playerIndex;
        IsOpen         = true;
        Time.timeScale = 0f;
    }

    void OnGUI()
    {
        if (!IsOpen) return;
        EnsureStyles();

        // Tmavě rudé pozadí přes celou obrazovku.
        GUI.color = new Color(0.12f, 0.02f, 0.02f, 0.95f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 460f, h = 320f;
        float px = (Screen.width - w) / 2f;
        float py = (Screen.height - h) / 2f;

        GUILayout.BeginArea(new Rect(px, py, w, h));

        GUILayout.Label("POTOPIL SES", titleStyle);
        GUILayout.Space(6);
        GUILayout.Label("Moře si tě vzalo. Mince ti zůstaly, ale loď,\nnáklad i vylepšení jsou pryč.", subStyle);
        GUILayout.Space(26);

        if (SoundManager.Click(GUILayout.Button("Respawn  —  veslice na nejblizsim ostrove", buttonStyle, GUILayout.Height(56))))
            Respawn();

        GUILayout.Space(12);

        if (SoundManager.Click(GUILayout.Button("Hlavni menu", buttonStyle, GUILayout.Height(56))))
            ToMenu();

        GUILayout.EndArea();
    }

    void Respawn()
    {
        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null) grid.RespawnPlayerAtNearestIsland(deadPlayerIndex);

        // Znovu načti stav hráče (postaví ho na nové místo, ukáže veslici).
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (pc.playerIndex == deadPlayerIndex) pc.ReloadFromData();

        IsOpen         = false;
        Time.timeScale = 1f;
    }

    void ToMenu()
    {
        IsOpen = false;
        // MainMenuManager.Show() si Time.timeScale = 0 nastaví samo.
        MainMenuManager.Show();
    }

    void EnsureStyles()
    {
        if (stylesReady) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.4f, 0.35f) }
        };
        subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.85f, 0.8f, 0.8f) }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = Solid(new Color(0.35f, 0.12f, 0.12f)) },
            hover  = { textColor = Color.white, background = Solid(new Color(0.5f, 0.18f, 0.18f)) }
        };
        stylesReady = true;
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }
}
