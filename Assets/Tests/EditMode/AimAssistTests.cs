using NUnit.Framework;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  AimAssistTests.cs
//  EditMode testy výpočtu náměru (AimAssist.ElevationTo) — bez scény, jen matika:
//  cíl ve stejné výšce = vodorovně, dělo na věži = nahoru, cíl níž = dolů.
// ─────────────────────────────────────────────────────────────────────────────

public class AimAssistTests
{
    [Test]
    public void Namer_CilVeStejneVysce_LetiVodorovne()
    {
        Assert.AreEqual(0f, AimAssist.ElevationTo(new Vector3(0f, 0.4f, 0f), new Vector3(10f, 0.4f, 0f)), 0.001f);
    }

    [Test]
    public void Namer_DeloNaVezi_MiriNahoru()
    {
        // Dělo na věži (≈3,3 j) 10 j před přídí (ústí 0,4 j) → atan(2,9 / 10) ≈ 16,2°.
        float e = AimAssist.ElevationTo(new Vector3(0f, 0.4f, 0f), new Vector3(10f, 3.3f, 0f));
        Assert.AreEqual(16.17f, e, 0.05f);
    }

    [Test]
    public void Namer_CilNiz_MiriDolu_AleNejvyse40Stupnu()
    {
        Assert.Less(AimAssist.ElevationTo(new Vector3(0f, 1f, 0f), new Vector3(10f, 0f, 0f)), 0f);
        // Cíl skoro přímo nad hlavní — náměr se ořízne na 40°.
        Assert.AreEqual(40f, AimAssist.ElevationTo(Vector3.zero, new Vector3(0.5f, 10f, 0f)), 0.001f);
    }

    [Test]
    public void Cil_BezNepratelNicNenajde()
    {
        // V EditMode testu nejsou žádní piráti, děla ani věže → žádný cíl, střela letí rovně.
        Assert.IsFalse(AimAssist.FindTarget(Vector3.zero, Vector3.forward, out _));
    }
}
