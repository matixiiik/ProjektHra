using NUnit.Framework;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryLogicTests.cs
//  EditMode testy pravidel příběhu, která se dají ověřit bez scény:
//  kdy je hráč "na mega ostrově" a kdy si po smrti nechá loď a mapu.
// ─────────────────────────────────────────────────────────────────────────────

public class StoryLogicTests
{
    [Test]
    public void NaOstrove_JenKdyzDoplulStoryStep3()
    {
        var d = new GameData { storyIslandActive = true, storyStep = 2, megaIndex = 1, megaTask = 1 };
        // Hřbitov lodí má megaTask 1 už při postavení ostrova — to NESMÍ znamenat "je na ostrově".
        Assert.IsFalse(d.IsOnCurrentMegaIsland);

        d.storyStep = 3;
        Assert.IsTrue(d.IsOnCurrentMegaIsland);
    }

    [Test]
    public void NaOstrove_NeplatiPoDohraniNeboBezOstrova()
    {
        var d = new GameData { storyIslandActive = true, storyStep = 3, storyDone = true };
        Assert.IsFalse(d.IsOnCurrentMegaIsland);

        d = new GameData { storyIslandActive = false, storyStep = 3 };
        Assert.IsFalse(d.IsOnCurrentMegaIsland);
    }

    [Test]
    public void Smrt_LodAMapaZustanouJenBehemPribehu()
    {
        Assert.IsFalse(GridManager.KeepsStoryGearOnDeath(new GameData { storyStep = 0 }));
        Assert.IsFalse(GridManager.KeepsStoryGearOnDeath(new GameData { storyStep = 1 }));
        Assert.IsTrue (GridManager.KeepsStoryGearOnDeath(new GameData { storyStep = 2 }));
        Assert.IsTrue (GridManager.KeepsStoryGearOnDeath(new GameData { storyStep = 3 }));
        Assert.IsFalse(GridManager.KeepsStoryGearOnDeath(new GameData { storyStep = 3, storyDone = true }));
        Assert.IsFalse(GridManager.KeepsStoryGearOnDeath(null));
    }
}
