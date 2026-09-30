using NUnit.Framework;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  SaveMigrationTests.cs
//  EditMode testy migrace starého savu (ploché "coins"/"player2Coins" …) na
//  GameData.players[0]/[1] — SaveManager.MigrateIfNeeded. Používají skutečný
//  JsonUtility, stejně jako SaveManager.LoadGame.
// ─────────────────────────────────────────────────────────────────────────────

public class SaveMigrationTests
{
    // Načte JSON stejnou cestou jako SaveManager.LoadGame (parse + migrace).
    static GameData Load(string json) => SaveManager.ParseAndMigrate(json);

    [Test]
    public void StarySaveVerze1_PresunePlochaPoleDoPlayers()
    {
        var d = Load("{\"saveVersion\":1,\"coins\":42,\"fishCount\":7,\"shipLevel\":2,"
                   + "\"playerGridX\":-571,\"playerGridY\":-304,"
                   + "\"player2Coins\":9,\"player2ShipLevel\":1,\"player2GridX\":5}");

        Assert.AreEqual(42, d.players[0].coins);
        Assert.AreEqual(7, d.players[0].fishCount);
        Assert.AreEqual(2, d.players[0].shipLevel);
        Assert.AreEqual(-571, d.players[0].gridX);
        Assert.AreEqual(-304, d.players[0].gridY);

        Assert.AreEqual(9, d.players[1].coins);
        Assert.AreEqual(1, d.players[1].shipLevel);
        Assert.AreEqual(5, d.players[1].gridX);
    }

    [Test]
    public void Migrace_ZvedneSaveVersionNaAktualni()
    {
        var d = Load("{\"saveVersion\":1,\"coins\":1}");
        Assert.AreEqual(GameData.CURRENT_SAVE_VERSION, d.saveVersion);
    }

    [Test]
    public void Migrace_HraciSeNeprolinaji()
    {
        var d = Load("{\"saveVersion\":1,\"coins\":100,\"player2Coins\":5}");
        Assert.AreEqual(100, d.players[0].coins);
        Assert.AreEqual(5, d.players[1].coins);
    }

    [Test]
    public void AktualniSave_MigraciNepouzije()
    {
        // Save už ve verzi 2: staré ploché pole se ignoruje, platí players[].
        var d = Load("{\"saveVersion\":2,\"coins\":999,"
                   + "\"players\":[{\"coins\":3},{\"coins\":4}]}");
        Assert.AreEqual(3, d.players[0].coins);
        Assert.AreEqual(4, d.players[1].coins);
    }

    [Test]
    public void SaveBezPoleSaveVersion_SeMigruje()
    {
        // Nejstarší savy (před verzováním) pole saveVersion vůbec nemají.
        var d = Load("{\"coins\":77}");
        Assert.AreEqual(77, d.players[0].coins);
    }

    [Test]
    public void NovaHra_MaDvaHraceAAktualniVerzi()
    {
        var d = new GameData();
        Assert.AreEqual(2, d.players.Length);
        Assert.IsNotNull(d.players[0]);
        Assert.IsNotNull(d.players[1]);
        Assert.AreEqual(GameData.CURRENT_SAVE_VERSION, d.saveVersion);
    }
}
