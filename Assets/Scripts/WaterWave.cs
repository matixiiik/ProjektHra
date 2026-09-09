using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  WaterWave.cs
//  Jemné pohupování vodní dlaždice nahoru a dolů (sinusovka), aby voda
//  nebyla úplně plochá. Fáze houpání se počítá ze souřadnic dlaždice, takže
//  sousední dlaždice se nehoupou přesně stejně — dohromady to vypadá
//  jako vlnění hladiny, ne jako jedna deska co poskakuje celá najednou.
// ─────────────────────────────────────────────────────────────────────────────

public class WaterWave : MonoBehaviour
{
    public float amplitude = 0.04f; // jak vysoko/nízko se dlaždice houpe
    public float speed     = 1.2f;  // rychlost houpání

    private float baseY;
    private float phase;

    void Start()
    {
        baseY = transform.localPosition.y;
        // Fáze podle světové pozice — sousední dlaždice houpou mimo takt.
        phase = transform.position.x * 0.7f + transform.position.z * 1.3f;
    }

    void Update()
    {
        float y = baseY + Mathf.Sin(Time.time * speed + phase) * amplitude;
        Vector3 p = transform.localPosition;
        transform.localPosition = new Vector3(p.x, y, p.z);
    }
}
