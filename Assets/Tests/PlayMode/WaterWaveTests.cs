using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ─────────────────────────────────────────────────────────────────────────────
//  WaterWaveTests.cs
//  PlayMode testy pro WaterWave — houpani se pocita v Update() ze skutecneho
//  Time.time, takze to jde overit jen behem opravdoveho behu snimku po snimku.
// ─────────────────────────────────────────────────────────────────────────────

public class WaterWaveTests
{
    [UnityTest]
    public IEnumerator HoupaniZustavaVRozsahuAmplitudy()
    {
        var tileGO = new GameObject("Tile");
        tileGO.transform.position = new Vector3(3f, 0f, 5f); // zacina na Y=0
        var wave = tileGO.AddComponent<WaterWave>();
        wave.amplitude = 0.04f;

        for (int i = 0; i < 15; i++)
        {
            yield return null;
            float y = tileGO.transform.localPosition.y;
            Assert.LessOrEqual(Mathf.Abs(y), wave.amplitude + 0.0001f,
                "Dlazdice se nesmi houpat vic, nez dovoluje amplituda");
        }

        Object.Destroy(tileGO);
    }

    [UnityTest]
    public IEnumerator SousedniDlazdiceMajiRuznouFazi()
    {
        var goA = new GameObject("TileA");
        goA.transform.position = new Vector3(0f, 0f, 0f);
        goA.AddComponent<WaterWave>();

        var goB = new GameObject("TileB");
        goB.transform.position = new Vector3(10f, 0f, 0f);
        goB.AddComponent<WaterWave>();

        yield return null;
        yield return null;

        Assert.AreNotEqual(goA.transform.localPosition.y, goB.transform.localPosition.y,
            "Ruzna faze (podle souradnic) musi dat ruznou vysku ve stejnem snimku");

        Object.Destroy(goA);
        Object.Destroy(goB);
    }
}
