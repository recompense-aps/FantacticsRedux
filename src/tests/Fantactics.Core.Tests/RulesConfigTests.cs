using System.Text.Json;
using System.Text.Json.Nodes;
using Fantactics.Core.Rules;
using Fantactics.Core.Serialization;

namespace Fantactics.Core.Tests;

/// <summary>Loading rules configs: named characters and their checks (RacesAndUnits §2.5).</summary>
public class RulesConfigTests
{
    [Fact]
    public void TheUniquesAreNamedCharacters()
    {
        UnitDefinition grukk = RulesConfig.Default.Units["WarLord"];

        Assert.Equal(("Grukk", "Skullsplitter", "War Lord"), (grukk.Name, grukk.Epithet, grukk.Role));
        Assert.Equal("Grukk Skullsplitter", grukk.FullName);
        Assert.Equal("Ysolde of the Deepwood", RulesConfig.Default.Units["Druid"].FullName);
        Assert.Equal("Mother Gristle", RulesConfig.Default.Units["Shaman"].FullName);
        Assert.All(RulesConfig.Default.Units.Values.Where(unit => unit.Unique), unit => Assert.NotNull(unit.Name));
    }

    [Theory]
    [InlineData("Grunt")]
    [InlineData("Archer")]
    public void CommonUnitsHaveNoName(string type)
    {
        UnitDefinition unit = RulesConfig.Default.Units[type];

        Assert.Null(unit.Name);
        Assert.Null(unit.FullName);
    }

    [Fact]
    public void TheDefaultRulesRoundTripWithTheSameHash()
    {
        RulesConfig loaded = RulesConfig.FromJson(JsonSerializer.Serialize(RulesConfig.Default, CoreJson.Options));

        Assert.Equal(RulesConfig.Default.Hash, loaded.Hash);
    }

    [Fact]
    public void AUniqueWithoutANameIsRejected()
    {
        string json = Edit(units => units["WarLord"]!.AsObject().Remove("name"));

        JsonException error = Assert.Throws<JsonException>(() => RulesConfig.FromJson(json));
        Assert.Contains("'WarLord' is unique", error.Message);
    }

    [Theory]
    [InlineData("Grukk")]
    [InlineData("grukk")]
    public void TwoUnitsWithOneNameAreRejected(string name)
    {
        string json = Edit(units => units["Grunt"]!["name"] = name);

        JsonException error = Assert.Throws<JsonException>(() => RulesConfig.FromJson(json));
        Assert.Contains("share the name", error.Message);
    }

    [Fact]
    public void AnEpithetWithoutANameIsRejected()
    {
        string json = Edit(units => units["Grunt"]!["epithet"] = "the Unready");

        JsonException error = Assert.Throws<JsonException>(() => RulesConfig.FromJson(json));
        Assert.Contains("'Grunt' has an epithet or role but no name", error.Message);
    }

    [Fact]
    public void ACommonUnitMayHaveAName()
    {
        string json = Edit(units => units["Grunt"]!["name"] = "Gob");

        Assert.Equal("Gob", RulesConfig.FromJson(json).Units["Grunt"].FullName);
    }

    /// <summary>The default rules as JSON, with <paramref name="edit"/> applied to the <c>units</c> object.</summary>
    private static string Edit(Action<JsonObject> edit)
    {
        JsonObject rules = JsonSerializer.SerializeToNode(RulesConfig.Default, CoreJson.Options)?.AsObject()
            ?? throw new InvalidOperationException();
        edit(rules["units"]?.AsObject() ?? throw new InvalidOperationException());
        return rules.ToJsonString();
    }
}
