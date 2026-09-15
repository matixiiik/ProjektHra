using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ─────────────────────────────────────────────────────────────────────────────
//  CameraFollowTests.cs
//  PlayMode testy pro CameraFollow — potřebují skutečný běh LateUpdate přes
//  víc snímků (yield return null), to EditMode neumí.
// ─────────────────────────────────────────────────────────────────────────────

public class CameraFollowTests
{
    private GameObject targetGO;
    private GameObject cameraGO;
    private CameraFollow follow;

    [SetUp]
    public void SetUp()
    {
        targetGO = new GameObject("Target");
        targetGO.transform.position = Vector3.zero;

        cameraGO = new GameObject("Camera");
        cameraGO.transform.position = new Vector3(0f, 5f, -10f);
        follow = cameraGO.AddComponent<CameraFollow>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(targetGO);
        Object.Destroy(cameraGO);
    }

    [UnityTest]
    public IEnumerator PoNastaveniCileSiZapamatujeOdstup()
    {
        follow.SetTarget(targetGO.transform);
        yield return null; // prvni LateUpdate spocita offset

        Vector3 offset = cameraGO.transform.position - targetGO.transform.position;
        Assert.AreEqual(new Vector3(0f, 5f, -10f), offset);
    }

    [UnityTest]
    public IEnumerator PoPresunuCileDrziStejnyOdstup()
    {
        follow.SetTarget(targetGO.transform);
        yield return null; // zapamatuje si odstup

        Vector3 offsetBefore = cameraGO.transform.position - targetGO.transform.position;

        targetGO.transform.position = new Vector3(20f, 0f, 30f);
        yield return null; // LateUpdate posune kameru za cilem

        Vector3 offsetAfter = cameraGO.transform.position - targetGO.transform.position;
        Assert.AreEqual(offsetBefore.x, offsetAfter.x, 0.001f);
        Assert.AreEqual(offsetBefore.y, offsetAfter.y, 0.001f);
        Assert.AreEqual(offsetBefore.z, offsetAfter.z, 0.001f);
    }
}
