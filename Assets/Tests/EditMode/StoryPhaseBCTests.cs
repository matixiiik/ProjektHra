using NUnit.Framework;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryPhaseBCTests.cs
//  EditMode testy novinek příběhu (fáze B a C, 11/2026): stránky bratrova deníku,
//  trofej po megalodonovi a statistiky na závěrečné obrazovce.
// ─────────────────────────────────────────────────────────────────────────────

public class StoryPhaseBCTests
{
    [Test]
    public void Denik_StrankaSeSebereJenJednou()
    {
        var d = new GameData();
        Assert.AreEqual(0, DiaryPages.Count(d));

        Assert.IsTrue(DiaryPages.Collect(d, 2));
        Assert.IsFalse(DiaryPages.Collect(d, 2)); // podruhé už ne
        Assert.IsTrue(DiaryPages.Has(d, 2));
        Assert.IsFalse(DiaryPages.Has(d, 0));
        Assert.AreEqual(1, DiaryPages.Count(d));
    }

    [Test]
    public void Denik_NeplatnyIndexSeIgnoruje()
    {
        var d = new GameData();
        Assert.IsFalse(DiaryPages.Collect(d, -1));
        Assert.IsFalse(DiaryPages.Collect(d, DiaryPages.COUNT));
        Assert.IsFalse(DiaryPages.Collect(null, 0));
        Assert.AreEqual(0, d.diaryPagesMask);
    }

    [Test]
    public void Denik_BednaDavaStrankuJenBehemCesty()
    {
        var d = new GameData { storyStep = 1 };
        Assert.AreEqual(-1, DiaryPages.PageForChest(d)); // před vyplutím nic

        d.storyStep = 2; d.megaIndex = 0;
        Assert.AreEqual(-1, DiaryPages.PageForChest(d)); // ještě před Pirátským ostrovem nic

        d.megaIndex = 1;
        Assert.AreEqual(1, DiaryPages.PageForChest(d));
        DiaryPages.Collect(d, 1);
        Assert.AreEqual(-1, DiaryPages.PageForChest(d)); // druhá bedna na stejné cestě už nic

        d.megaIndex = 2;
        Assert.AreEqual(5, DiaryPages.PageForChest(d));

        d.storyDone = true;
        Assert.AreEqual(-1, DiaryPages.PageForChest(d)); // po dohrání už ne
    }

    [Test]
    public void Korist_ZubMegalodonaNikdyNepadneNahodne()
    {
        Assert.AreEqual("Zub megalodona", LootItems.Items[LootItems.MEGALODON_TOOTH].nameCs);
        Assert.AreEqual(0, LootItems.Items[LootItems.MEGALODON_TOOTH].weight);

        for (int i = 0; i < 2000; i++)
            Assert.AreNotEqual(LootItems.MEGALODON_TOOTH, LootItems.RollRandomIndex());
    }

    [Test]
    public void Zaver_StatistikyObsahujiPocty()
    {
        var d = new GameData { statFishCaught = 42, statTreasuresDug = 7, pirateKills = 3, playSeconds = 3725f, storyEnding = 1 };
        DiaryPages.Collect(d, 0);
        string all = string.Join("\n", EndScreen.StatLines(d));

        StringAssert.Contains("42", all);
        StringAssert.Contains("7", all);
        StringAssert.Contains("1 h 2 min", all); // 3725 s = 62 min
        StringAssert.Contains("1/6", all);
    }
}
