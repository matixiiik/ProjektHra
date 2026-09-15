using NUnit.Framework;

// ─────────────────────────────────────────────────────────────────────────────
//  GameDataTests.cs
//  EditMode testy pro ActiveQuest / MegaQuest — obyčejné [Serializable] třídy,
//  jdou vytvořit přímo bez scény ani save souboru.
// ─────────────────────────────────────────────────────────────────────────────

public class GameDataTests
{
    [Test]
    public void ActiveQuest_IsComplete_PodleProgresu()
    {
        var q = new ActiveQuest { hasQuest = true, target = 10, progress = 9 };
        Assert.IsFalse(q.IsComplete);
        q.progress = 10;
        Assert.IsTrue(q.IsComplete);
    }

    [Test]
    public void ActiveQuest_IsComplete_FalseKdyzNemaQuest()
    {
        // hranicni pripad: 0 >= 0 by bez kontroly hasQuest proslo jako splnene
        var q = new ActiveQuest { hasQuest = false, target = 0, progress = 0 };
        Assert.IsFalse(q.IsComplete);
    }

    [Test]
    public void ActiveQuest_Reset_VynulujeVse()
    {
        var q = new ActiveQuest
        {
            hasQuest = true, questType = 1, description = "Ulov 10 ryb",
            target = 10, progress = 5, cost = 20, reward = 60, multiplier = 3
        };
        q.Reset();
        Assert.IsFalse(q.hasQuest);
        Assert.AreEqual(0, q.questType);
        Assert.AreEqual("", q.description);
        Assert.AreEqual(0, q.target);
        Assert.AreEqual(0, q.progress);
        Assert.AreEqual(0, q.cost);
        Assert.AreEqual(0, q.reward);
        Assert.AreEqual(0, q.multiplier);
    }

    [Test]
    public void MegaQuest_Reset_VynulujeVse()
    {
        var mq = new MegaQuest
        {
            active = true, targetX = 100, targetY = -50,
            dug = true, rewardCoins = 500, grantsHistoricalTreasure = true
        };
        mq.Reset();
        Assert.IsFalse(mq.active);
        Assert.AreEqual(0, mq.targetX);
        Assert.AreEqual(0, mq.targetY);
        Assert.IsFalse(mq.dug);
        Assert.AreEqual(0, mq.rewardCoins);
        Assert.IsFalse(mq.grantsHistoricalTreasure);
    }
}
