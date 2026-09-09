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
//  Když má obchod otevřený TENHLE hráč, jeho ovládání se vypne (ať se nehýbe
//  pod menu). Obchod druhého hráče (jsou v majáku spolu) ho nezmrazí.
// ─────────────────────────────────────────────────────────────────────────────

public class InteriorPlayer : MonoBehaviour
{
    public float moveSpeed  = 3.5f;

    [Tooltip("Poloměr kruhové pochozí plochy (kolem středu místnosti).")]
    public float areaRadius = 3.4f;

    [Tooltip("Vnitřní poloměr — kolem středu je sloup, tam hráč nesmí.")]
    public float innerRadius = 0f;

    // Kterého hráče tenhle panáček ovládá. Nastaví LighthouseInterior při vstupu.
    [HideInInspector] public int ownerPlayerIndex = 0;

    // Střed pochozí plochy. Sólo = počátek (0,0). V coopu je interiér posunutý
    // daleko od oceánu, tak LighthouseInterior nastaví střed na ten posun.
    private Vector3 areaCenter = Vector3.zero;
    public void SetAreaCenter(Vector3 c) => areaCenter = c;

    private InteriorInteractable nearest; // co je zrovna v dosahu (kvůli nápovědě)
    private GUIStyle promptStyle;

    // Obchody v majáku (P1 a P2 jsou spolu v jedné místnosti).
    private UpgradeShopManager shopUpgrade;
    private QuestShopManager   shopQuest;

    // Mám JÁ otevřený obchod? (Obchod druhého hráče mě nezmrazí.)
    private bool MyShopOpen()
    {
        if (shopUpgrade == null)
            foreach (var u in FindObjectsByType<UpgradeShopManager>(FindObjectsSortMode.None))
                if (u.gameObject.scene == gameObject.scene) { shopUpgrade = u; break; }
        if (shopQuest == null)
            foreach (var q in FindObjectsByType<QuestShopManager>(FindObjectsSortMode.None))
                if (q.gameObject.scene == gameObject.scene) { shopQuest = q; break; }

        return (shopUpgrade != null && shopUpgrade.IsOpenForBuyer(ownerPlayerIndex))
            || (shopQuest   != null && shopQuest.IsOpenForBuyer(ownerPlayerIndex));
    }

    // Jsem hráč 1 (nebo sólo)?
    private bool IsP1 => ownerPlayerIndex == 0;

    // Vrátí stisk klávesy podle hráče: hráč 1 dostane p1Key, hráč 2 p2Key.
    private bool MoveKey(KeyCode p1Key, KeyCode p2Key)
        => IsP1 ? Input.GetKey(p1Key) : Input.GetKey(p2Key);

    // ── Kenney model postavičky (idle/walk) ────────────────────────────────
    private bool     figureBuilt;
    private Animator figureAnimator;

    private void EnsureFigure()
    {
        if (figureBuilt) return;
        figureBuilt = true;

        // P2 v coopu vzniká jako klon → zahoď případný zděděný model.
        var stale = transform.Find("CharModel");
        if (stale != null) DestroyImmediate(stale.gameObject);

        Color tint = ownerPlayerIndex == 1
            ? new Color(0.82f, 0.24f, 0.20f)   // P2 — červené
            : new Color(0.32f, 0.46f, 0.72f);  // P1 — modré

        var model = CharacterModel.TryBuild(transform, "character-male-a",
            CharacterModel.DEFAULT_SCALE, tint, "PlayerAnim");
        if (model == null) return;

        figureAnimator = CharacterModel.GetAnimator(model);

        // Schovej původní scénické díly postavičky (mají materiál "InteriorPlayer").
        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.transform.IsChildOf(model.transform)) continue;
            if (mr.sharedMaterial != null && mr.sharedMaterial.name.Contains("InteriorPlayer"))
                mr.enabled = false;
        }
    }

    void Update()
    {
        EnsureFigure();

        if (MyShopOpen() || GameConsole.IsOpen)
        {
            if (figureAnimator != null) figureAnimator.SetFloat("Speed", 0f);
            return;
        }

        // Pohyb po rovině (X = doprava, Z = dopředu). Hráč 1 = WASD, hráč 2 = šipky.
        float h = 0f, v = 0f;
        if (MoveKey(KeyCode.A, KeyCode.LeftArrow))  h -= 1f;
        if (MoveKey(KeyCode.D, KeyCode.RightArrow)) h += 1f;
        if (MoveKey(KeyCode.S, KeyCode.DownArrow))  v -= 1f;
        if (MoveKey(KeyCode.W, KeyCode.UpArrow))    v += 1f;

        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        if (figureAnimator != null)
            figureAnimator.SetFloat("Speed", dir.sqrMagnitude > 0.01f ? moveSpeed : 0f);

        Vector3 pos = transform.position + dir * moveSpeed * Time.deltaTime;

        // Kruhové omezení plochy (maják je kulatý) — drž hráče mezi innerRadius
        // (sloup uprostřed) a areaRadius (stěna).
        Vector3 fromCenter = new Vector3(pos.x - areaCenter.x, 0f, pos.z - areaCenter.z);
        float dist = fromCenter.magnitude;
        if (dist > areaRadius)                    fromCenter = fromCenter.normalized * areaRadius;
        else if (innerRadius > 0f && dist < innerRadius && dist > 0.001f)
                                                 fromCenter = fromCenter.normalized * innerRadius;
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
            if (it.gameObject.scene != gameObject.scene) continue; // jen z mojí kopie majáku
            float d = Vector3.Distance(transform.position, it.transform.position);
            if (d <= it.range && d < bestDist) { best = it; bestDist = d; }
        }
        return best;
    }

    void OnGUI()
    {
        if (nearest == null || MyShopOpen()) return;

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
