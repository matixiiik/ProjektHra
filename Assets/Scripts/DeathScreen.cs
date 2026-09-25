using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  DeathScreen.cs
//  Obrazovka smrti. Ukáže se, když hráči (panáčkovi) klesne zdraví na 0.
//  Nabízí dvě tlačítka:
//    • RESPAWN     — hráč se objeví s VESLICÍ na nejbližším ostrově od místa
//                    smrti. Mince a rozdělaný mega quest zůstanou, ale kořist
//                    (ryby, poklady, náboje) i všechna vylepšení zmizí.
//    • HLAVNÍ MENU — vrátí do hlavního menu. To je sdílená, hru ukončující
//                    akce (stejně jako "Hlavní menu" v pauze) — ukončí hru
//                    i druhému hráči.
//
//  Per-hráč modal jako MapScreen / obchody: sama NEpauzuje čas (v sólu se
//  o to postará SoloPause stejně jako u obchodu/mapy/dialogu — viz
//  SoloPause.modal), jen zmrazí a zobrazí toho hráče, co umřel. Ve split
//  screenu druhý hráč hraje dál, stejně jako když má někdo otevřený obchod.
//  Umí být otevřená současně pro oba hráče (umřou-li oba najednou).
//
//  Objekt se vytváří sám přes DeathScreen.Show(). Nic se nezapojuje ve scéně.
// ─────────────────────────────────────────────────────────────────────────────

public class DeathScreen : MonoBehaviour
{
    private static DeathScreen instance;
    private readonly bool[] openFor = new bool[2]; // openFor[0] = P1, openFor[1] = P2

    /// <summary>Je obrazovka smrti otevřená pro KTERÉHOKOLI hráče? V sólu je to
    /// totéž co IsOpenFor(0) — SoloPause podle toho pauzuje čas jako u obchodu.</summary>
    public static bool IsOpen => instance != null && (instance.openFor[0] || instance.openFor[1]);

    /// <summary>Má TENHLE hráč zrovna otevřenou obrazovku smrti? (blokuje jemu
    /// pohyb i poškození — druhého hráče ne, ten hraje dál.)</summary>
    public static bool IsOpenFor(int playerIndex) => instance != null && instance.openFor[playerIndex];

    /// <summary>Zobrazí obrazovku smrti pro daného hráče.</summary>
    public static void Show(int playerIndex)
    {
        if (instance == null)
        {
            var go = new GameObject("DeathScreen");
            instance = go.AddComponent<DeathScreen>();
        }
        instance.openFor[playerIndex] = true;
    }

    private GUIStyle titleStyle, subStyle, buttonStyle;
    private bool     stylesReady;

    void OnGUI()
    {
        if (!IsOpen) return;
        EnsureStyles();

        for (int i = 0; i < 2; i++)
            if (openFor[i]) DrawFor(i);
    }

    // Sólo = celá obrazovka, split screen = jen půlka toho hráče, co umřel.
    void DrawFor(int playerIndex)
    {
        float halfX = 0f, halfW = Screen.width;
        if (MultiplayerManager.IsMultiplayer)
        {
            halfW = Screen.width * 0.5f;
            halfX = playerIndex == 1 ? Screen.width * 0.5f : 0f;
        }

        // Tmavě rudé pozadí přes tuhle půlku obrazovky.
        GUI.color = new Color(0.12f, 0.02f, 0.02f, 0.95f);
        GUI.DrawTexture(new Rect(halfX, 0, halfW, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = Mathf.Min(460f, halfW - 40f), h = 320f;
        float px = halfX + (halfW - w) / 2f;
        float py = (Screen.height - h) / 2f;

        GUILayout.BeginArea(new Rect(px, py, w, h));

        GUILayout.Label(Loc.T("POTOPIL SES", "YOU SANK"), titleStyle);
        GUILayout.Space(6);
        // Zalomení řádků obstará wordWrap stylu (v angličtině je text delší).
        GUILayout.Label(Loc.T("Moře si tě vzalo. Mince ti zůstaly, ale loď, náklad i vylepšení jsou pryč.",
                              "The sea has claimed you. You keep your coins, but your boat, cargo and upgrades are gone."), subStyle);
        GUILayout.Space(26);

        if (SoundManager.Click(GUILayout.Button(Loc.T("Respawn  —  veslice na nejbližším ostrově", "Respawn  —  a rowboat at the nearest island"), buttonStyle, GUILayout.Height(56))))
            Respawn(playerIndex);

        GUILayout.Space(12);

        if (SoundManager.Click(GUILayout.Button(Loc.T("Hlavní menu", "Main Menu"), buttonStyle, GUILayout.Height(56))))
            ToMenu(playerIndex);

        GUILayout.EndArea();
    }

    void Respawn(int playerIndex)
    {
        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null) grid.RespawnPlayerAtNearestIsland(playerIndex);

        // Znovu načti stav hráče (postaví ho na nové místo, ukáže veslici).
        foreach (var pc in PlayerController.All)
            if (pc.playerIndex == playerIndex) pc.ReloadFromData();

        openFor[playerIndex] = false;
    }

    void ToMenu(int playerIndex)
    {
        openFor[playerIndex] = false;
        // MainMenuManager.Show() zastaví hru (Time.timeScale = 0) samo — je to
        // sdílená akce, co ukončí hru i druhému hráči (stejně jako "Hlavní menu"
        // v pauze).
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
