using UnityEngine;

public class WorkIndicator : MonoBehaviour
{
    private PlayerController player;
    private LineRenderer lr;

    private const float RADIUS = 0.45f;
    private const float HEIGHT = 1.8f;
    private const int SEGMENTS = 36;

    void Start()
    {
        player = GetComponent<PlayerController>();

        var child = new GameObject("WorkArc");
        child.transform.SetParent(transform);
        child.transform.localPosition = new Vector3(0, HEIGHT, 0);

        lr = child.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.widthMultiplier = 0.07f;
        lr.numCapVertices = 4;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = new Color(1f, 0.85f, 0.1f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
    }

    void Update()
    {
        if (!player.IsWorking)
        {
            lr.enabled = false;
            return;
        }

        lr.enabled = true;

        int points = Mathf.Max(2, Mathf.RoundToInt(SEGMENTS * player.WorkProgress) + 1);
        lr.positionCount = points;

        for (int i = 0; i < points; i++)
        {
            float angle = ((float)i / SEGMENTS) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * RADIUS, 0f, Mathf.Sin(angle) * RADIUS));
        }
    }
}
