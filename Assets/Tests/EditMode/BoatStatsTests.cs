using NUnit.Framework;

// ─────────────────────────────────────────────────────────────────────────────
//  BoatStatsTests.cs
//  EditMode testy pro BoatStats — statické tabulky podle úrovně lodě.
// ─────────────────────────────────────────────────────────────────────────────

public class BoatStatsTests
{
    [TestCase(0, 0.75f)]
    [TestCase(1, 1.00f)]
    [TestCase(2, 1.25f)]
    [TestCase(3, 1.50f)]
    public void SpeedMultiplier_PodleUrovneLodi(int level, float expected)
    {
        Assert.AreEqual(expected, BoatStats.SpeedMultiplier(level), 0.001f);
    }

    [Test]
    public void HasCannon_VeslicNemaDelo()
    {
        Assert.IsFalse(BoatStats.HasCannon(0));
        Assert.IsTrue(BoatStats.HasCannon(1));
    }

    [Test]
    public void CannonDamage_RosteSUrovni()
    {
        Assert.AreEqual(0f, BoatStats.CannonDamage(0));
        Assert.Less(BoatStats.CannonDamage(1), BoatStats.CannonDamage(2));
        Assert.Less(BoatStats.CannonDamage(2), BoatStats.CannonDamage(3));
    }

    [Test]
    public void FishBonus_JenVelkaLodDavaBonus()
    {
        Assert.AreEqual(0, BoatStats.FishBonus(2));
        Assert.AreEqual(1, BoatStats.FishBonus(3));
    }
}
