using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  WorkIndicator.cs
//  Kroužek nad lodí, který se postupně "dokresluje" během rybaření / těžby
//  a ukazuje, jak daleko je práce hotová (0 % → 100 %).
//  Kreslí se pomocí LineRenderer jako oblouk z bodů po kružnici; kroužek se
//  natáčí čelem ke kameře hráče (billboard), aby při pohybu kamery vypadal pořád
//  jako kolečko, které se nabíhá od 12 hodin.
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

        // Rozmísti body po kružnici v rovině X/Y objektu (ta se v LateUpdate natáčí
        // ke kameře). Start je nahoře (12 hodin) a jde po směru hodinových ručiček.
        for (int i = 0; i < points; i++)
        {
            float angle = Mathf.PI * 0.5f - ((float)i / SEGMENTS) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * RADIUS, Mathf.Sin(angle) * RADIUS, 0f));
        }
    }

    // Kroužek je "billboard" — vždy natočený čelem ke kameře daného hráče, takže se při
    // otáčení kamerou nezkresluje do elipsy a start zůstává nahoře na obrazovce.
    // LateUpdate: až po tom, co se kamera pohnula.
    void LateUpdate()
    {
        if (lr == null || !lr.enabled) return;

        Transform cam = player.viewCamera != null ? player.viewCamera
                      : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null) return;

        lr.transform.rotation = cam.rotation;
    }
}
