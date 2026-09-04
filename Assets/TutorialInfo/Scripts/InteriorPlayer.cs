using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  InteriorPlayer.cs
//  Jednoduchá chůze postavičky uvnitř majáku. Není to mřížka jako venku —
//  hráč se pohybuje plynule po podlaze (WASD / šipky). `E` = interakce
//  s nejbližším InteriorInteractable v dosahu.
//
//  Když je otevřený obchod (UpgradeShopManager.AnyShopOpen), ovládání se
//  vypne, ať se hráč nehýbe pod menu.
// ─────────────────────────────────────────────────────────────────────────────

public class InteriorPlayer : MonoBehaviour
{
    public float moveSpeed = 3.5f;

    [Tooltip("Meze podlahy (kolem počátku), aby hráč nevyšel skrz zeď.")]
    public Vector2 areaHalfSize = new Vector2(3.2f, 3.2f);

    private InteriorInteractable nearest; // co je zrovna v dosahu (kvůli nápovědě)
    private GUIStyle promptStyle;

    void Update()
    {
        if (UpgradeShopManager.AnyShopOpen || GameConsole.IsOpen) return;

        // Pohyb po rovině (X = doprava, Z = dopředu).
        float h = 0f, v = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1f;

        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 pos = transform.position + dir * moveSpeed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, -areaHalfSize.x, areaHalfSize.x);
        pos.z = Mathf.Clamp(pos.z, -areaHalfSize.y, areaHalfSize.y);
        transform.position = pos;

        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), 12f * Time.deltaTime);

        // Najdi nejbližší bod zájmu v dosahu.
        nearest = FindNearestInteractable();

        // E / Numpad1 → interakce.
        if (nearest != null && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Keypad1)))
            nearest.Trigger();
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

        var r = new Rect(Screen.width / 2f - 200f, Screen.height - 70f, 400f, 30f);
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(r, nearest.prompt, promptStyle);
    }
}
