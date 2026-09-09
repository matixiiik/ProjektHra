using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  WorkIndicator.cs
//  Kroužek nad lodí, který se postupně "dokresluje" během rybaření / těžby
//  a ukazuje, jak daleko je práce hotová (0 % → 100 %).
//  Kreslí se pomocí LineRenderer jako oblouk z bodů po kružnici.
//  Skript je na stejném objektu jako PlayerController.
// ─────────────────────────────────────────────────────────────────────────────

public class WorkIndicator : MonoBehaviour
{
    private PlayerController player; // odkud se čte stav práce (IsWorking, WorkProgress)
    private LineRenderer     lr;     // čára, kterou kreslíme kroužek
    private Material         arcMaterial; // vlastní materiál čáry (uklidíme ho v OnDestroy)

    private const float RADIUS   = 0.45f; // poloměr kroužku
    private const float HEIGHT   = 1.8f;  // výška nad lodí
    private const int   SEGMENTS = 36;    // na kolik dílků je plná kružnice rozdělená

    void Start()
    {
        player = GetComponent<PlayerController>();

        // Vytvoř podřízený objekt, na kterém bude LineRenderer.
        var child = new GameObject("WorkArc");
        child.transform.SetParent(transform);
        child.transform.localPosition = new Vector3(0, HEIGHT, 0);

        // Nastav vzhled čáry.
        lr = child.AddComponent<LineRenderer>();
        lr.useWorldSpace   = false;                 // body jsou relativní k objektu
        lr.widthMultiplier = 0.07f;
        lr.numCapVertices  = 4;                     // zaoblené konce
        arcMaterial        = new Material(Shader.Find("Sprites/Default"));
        lr.material        = arcMaterial;
        lr.startColor = lr.endColor = new Color(1f, 0.85f, 0.1f); // žlutá
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.enabled           = false;              // schovaný, dokud se nepracuje
    }

    void OnDestroy()
    {
        // Ukliď materiál, který jsme si vytvořili (jinak zůstane v paměti).
        if (arcMaterial != null) Destroy(arcMaterial);
    }

    void Update()
    {
        // Když se nepracuje, kroužek schovej a nic nepočítej.
        if (!player.IsWorking)
        {
            lr.enabled = false;
            return;
        }

        lr.enabled = true;

        // Kolik bodů oblouku vykreslit podle postupu práce (0..1).
        int points = Mathf.Max(2, Mathf.RoundToInt(SEGMENTS * player.WorkProgress) + 1);
        lr.positionCount = points;

        // Rozmísti body po kružnici. Start je nahoře (-90°), pokračuje dokola.
        for (int i = 0; i < points; i++)
        {
            float angle = ((float)i / SEGMENTS) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * RADIUS, 0f, Mathf.Sin(angle) * RADIUS));
        }
    }
}
