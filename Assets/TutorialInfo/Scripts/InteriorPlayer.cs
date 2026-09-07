using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  InteriorPlayer.cs
//  Jednoduchá chůze postavičky uvnitř majáku. Není to mřížka jako venku —
//  hráč se pohybuje plynule po KRUHOVÉ podlaze (maják je válec).
//
//  Klávesy podle toho, kterého hráče panáček zastupuje:
//    ownerPlayerIndex 0 = hráč 1 / sólo → W A S D, E
//    ownerPlayerIndex 1 = hráč 2 (jen split screen) → šipky, Numpad1
//  Šipky NIKDY neovládají hráče 1.
//
//  Když je otevřený obchod (UpgradeShopManager.AnyShopOpen), ovládání se
//  vypne, ať se hráč nehýbe pod menu.
// ─────────────────────────────────────────────────────────────────────────────

public class InteriorPlayer : MonoBehaviour
{
    public float moveSpeed  = 3.5f;

    [Tooltip("Poloměr kruhové pochozí plochy (kolem středu místnosti).")]
    public float areaRadius = 3.4f;

    // Kterého hráče tenhle panáček ovládá. Nastaví LighthouseInterior při vstupu.
    [HideInInspector] public int ownerPlayerIndex = 0;

    // Střed pochozí plochy. Sólo = počátek (0,0). V coopu je interiér posunutý
    // daleko od oceánu, tak LighthouseInterior nastaví střed na ten posun.
    private Vector3 areaCenter = Vector3.zero;
    public void SetAreaCenter(Vector3 c) => areaCenter = c;

    private InteriorInteractable nearest; // co je zrovna v dosahu (kvůli nápovědě)
    private GUIStyle promptStyle;

    // Jsem hráč 1 (nebo sólo)?
    private bool IsP1 => ownerPlayerIndex == 0;

    // Vrátí stisk klávesy podle hráče: hráč 1 dostane p1Key, hráč 2 p2Key.
    private bool MoveKey(KeyCode p1Key, KeyCode p2Key)
        => IsP1 ? Input.GetKey(p1Key) : Input.GetKey(p2Key);

    void Update()
    {
        if (UpgradeShopManager.AnyShopOpen || GameConsole.IsOpen) return;

        // Pohyb po rovině (X = doprava, Z = dopředu). Hráč 1 = WASD, hráč 2 = šipky.
        float h = 0f, v = 0f;
        if (MoveKey(KeyCode.A, KeyCode.LeftArrow))  h -= 1f;
        if (MoveKey(KeyCode.D, KeyCode.RightArrow)) h += 1f;
        if (MoveKey(KeyCode.S, KeyCode.DownArrow))  v -= 1f;
        if (MoveKey(KeyCode.W, KeyCode.UpArrow))    v += 1f;

        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 pos = transform.position + dir * moveSpeed * Time.deltaTime;

        // Kruhové omezení plochy (maják je kulatý) — drž hráče v poloměru areaRadius.
        Vector3 fromCenter = new Vector3(pos.x - areaCenter.x, 0f, pos.z - areaCenter.z);
        if (fromCenter.magnitude > areaRadius) fromCenter = fromCenter.normalized * areaRadius;
        pos.x = areaCenter.x + fromCenter.x;
        pos.z = areaCenter.z + fromCenter.z;
        transform.position = pos;

        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 12f * Time.deltaTime);

        // Najdi nejbližší bod zájmu v dosahu.
        nearest = FindNearestInteractable();

        // E (hráč 1) / Numpad1 (hráč 2) → interakce.
        bool interact = IsP1 ? Input.GetKeyDown(KeyCode.E) : Input.GetKeyDown(KeyCode.Keypad1);
        if (nearest != null && interact)
            nearest.Trigger(ownerPlayerIndex);
    }

    private InteriorInteractable FindNearestInteractable()
    {
        InteriorInteractable best = null;
        float bestDist = float.MaxValue;

        foreach (var it in FindObjectsByType<InteriorInteractable>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, it.transform.position);
            if (d <= it.range && d < bestDist) { best = it; bestDist = d; }
        }
        return best;
    }

    void OnGUI()
    {
        if (nearest == null || UpgradeShopManager.AnyShopOpen) return;

        if (promptStyle == null)
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

        // V coopu je interiér na půlce obrazovky toho hráče — nápovědu tam vycentruj.
        float cx = Screen.width * 0.5f;
        if (MultiplayerManager.IsMultiplayer)
            cx = ownerPlayerIndex == 1 ? Screen.width * 0.75f : Screen.width * 0.25f;

        var r = new Rect(cx - 200f, Screen.height - 70f, 400f, 30f);
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(r, nearest.prompt, promptStyle);
    }
}
