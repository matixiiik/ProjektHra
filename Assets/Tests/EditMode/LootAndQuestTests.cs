using NUnit.Framework;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  LootAndQuestTests.cs
//  EditMode testy pro pirátskou kořist (LootItems) a posouvání questů
//  (ActiveQuest.AddProgress). Obojí jsou čistá data bez scény.
// ─────────────────────────────────────────────────────────────────────────────

public class LootAndQuestTests
{
    [Test]
    public void Loot_Count_ChybejiciPolozkaJeNula()
    {
        // Starý save pole loot nemá (nebo je kratší) → počet musí být 0, ne výjimka.
        var ps = new PlayerState();
        Assert.AreEqual(0, LootItems.Count(ps, 0));
        Assert.AreEqual(0, LootItems.Count(ps, LootItems.Items.Length - 1));

        ps.loot = null;
        Assert.AreEqual(0, LootItems.Count(ps, 2));
    }

    [Test]
    public void Loot_Add_DoplniSeznamAVratiSoucty()
    {
        var ps = new PlayerState();
        LootItems.Add(ps, 3, 2);
        LootItems.Add(ps, 0, 1);

        Assert.AreEqual(2, LootItems.Count(ps, 3));
        Assert.AreEqual(1, LootItems.Count(ps, 0));
        Assert.AreEqual(3, LootItems.TotalCount(ps));
        Assert.AreEqual(2 * LootItems.Items[3].price + LootItems.Items[0].price, LootItems.TotalValue(ps));
    }

    [Test]
    public void Loot_Add_NejdePodNulu()
    {
        var ps = new PlayerState();
        LootItems.Add(ps, 1, 1);
        LootItems.Add(ps, 1, -5); // prodej víc kusů, než má → zůstane 0
        Assert.AreEqual(0, LootItems.Count(ps, 1));
    }

    [Test]
    public void Loot_TryDrop_SanceNulaNicNeda_SanceJednaVzdyDa()
    {
        var ps = new PlayerState();
        Assert.AreEqual(-1, LootItems.TryDrop(ps, 0f));
        Assert.AreEqual(0, LootItems.TotalCount(ps));

        int idx = LootItems.TryDrop(ps, 1f);
        Assert.That(idx, Is.InRange(0, LootItems.Items.Length - 1));
        Assert.AreEqual(1, LootItems.TotalCount(ps));
    }

    [Test]
    public void Loot_RollRandomIndex_VzdyPlatnyIndex()
    {
        Random.InitState(1234);
        for (int i = 0; i < 500; i++)
            Assert.That(LootItems.RollRandomIndex(), Is.InRange(0, LootItems.Items.Length - 1));
    }

    [Test]
    public void Loot_TryDrop_PosuneQuestNaKorist()
    {
        var ps = new PlayerState();
        ps.activeQuest = new ActiveQuest { hasQuest = true, questType = ActiveQuest.TYPE_LOOT, target = 2 };

        LootItems.TryDrop(ps, 1f);
        Assert.AreEqual(1, ps.activeQuest.progress);
    }

    [Test]
    public void Quest_AddProgress_JenSpravnyTyp()
    {
        var q = new ActiveQuest { hasQuest = true, questType = ActiveQuest.TYPE_PIRATES, target = 3 };
        q.AddProgress(ActiveQuest.TYPE_FISH, 5);   // jiný typ → nic
        Assert.AreEqual(0, q.progress);

        q.AddProgress(ActiveQuest.TYPE_PIRATES, 1);
        Assert.AreEqual(1, q.progress);
    }

    [Test]
    public void Quest_AddProgress_NepretecePresCil()
    {
        var q = new ActiveQuest { hasQuest = true, questType = ActiveQuest.TYPE_FISH, target = 10, progress = 9 };
        q.AddProgress(ActiveQuest.TYPE_FISH, 4);
        Assert.AreEqual(10, q.progress);
        Assert.IsTrue(q.IsComplete);
    }

    [Test]
    public void Quest_AddProgress_BezQuestuNic()
    {
        var q = new ActiveQuest { hasQuest = false, questType = ActiveQuest.TYPE_CHESTS, target = 2 };
        q.AddProgress(ActiveQuest.TYPE_CHESTS, 1);
        Assert.AreEqual(0, q.progress);
    }

    [Test]
    public void Quest_Reset_VynulujeVecnouOdmenu()
    {
        var q = new ActiveQuest { hasQuest = true, rewardItem = ActiveQuest.REWARD_UPGRADE };
        q.Reset();
        Assert.AreEqual(ActiveQuest.REWARD_NONE, q.rewardItem);
    }

    [Test]
    public void Quest_StarySaveBezNovychPoli_NactenJakoBezOdmeny()
    {
        // Save z doby před věcnými odměnami: pole rewardItem chybí → 0 = jen mince.
        string json = "{\"hasQuest\":true,\"questType\":0,\"target\":5,\"progress\":2,\"reward\":30}";
        var q = JsonUtility.FromJson<ActiveQuest>(json);
        Assert.AreEqual(ActiveQuest.REWARD_NONE, q.rewardItem);
        Assert.AreEqual(2, q.progress);
    }
}
