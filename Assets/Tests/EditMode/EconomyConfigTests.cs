using NUnit.Framework;

// ─────────────────────────────────────────────────────────────────────────────
//  EconomyConfigTests.cs
//  EditMode testy pro EconomyConfig — čistá čísla, žádná scéna potřeba.
// ─────────────────────────────────────────────────────────────────────────────

public class EconomyConfigTests
{
    [Test]
    public void PriceMultiplier_JeNaKrajichRozsahu()
    {
        Assert.AreEqual(0.8f, EconomyConfig.PriceMultiplier(0), 0.001f);
        Assert.AreEqual(1.2f, EconomyConfig.PriceMultiplier(20), 0.001f);
    }

    [Test]
    public void PriceMultiplier_OrezavaMimoRozsah()
    {
        Assert.AreEqual(EconomyConfig.PriceMultiplier(0), EconomyConfig.PriceMultiplier(-5), 0.001f);
        Assert.AreEqual(EconomyConfig.PriceMultiplier(20), EconomyConfig.PriceMultiplier(99), 0.001f);
    }

    [Test]
    public void Price_ZaokrouhlujeAMinimalneJedna()
    {
        Assert.AreEqual(80, EconomyConfig.Price(100, 0));
        Assert.AreEqual(120, EconomyConfig.Price(100, 20));
        Assert.AreEqual(1, EconomyConfig.Price(0, 20)); // Mathf.Max(1, ...) — nikdy 0
    }

    [Test]
    public void IslandPriceLevel_JeVzdyVRozsahu0Az20()
    {
        for (int x = -50; x <= 50; x += 7)
        {
            for (int y = -50; y <= 50; y += 11)
            {
                int level = EconomyConfig.IslandPriceLevel(x, y);
                Assert.GreaterOrEqual(level, 0);
                Assert.LessOrEqual(level, 20);
            }
        }
    }

    [Test]
    public void IslandPriceLevel_JeDeterministicky()
    {
        int a = EconomyConfig.IslandPriceLevel(123, -45);
        int b = EconomyConfig.IslandPriceLevel(123, -45);
        Assert.AreEqual(a, b);
    }
}
