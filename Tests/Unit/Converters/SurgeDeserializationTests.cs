using System.Text.Json;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Converters;

/// <summary>
/// Confirms the "surge" node type round-trips through <see cref="NodeJsonConverter"/> and
/// <see cref="JsonHelper.Options"/> the same way every other node type does. The fixture
/// payload lives here in test code rather than under Kriss/Chapters: c1-c10 are canon and
/// this issue does not touch any chapter file, and the content issues that will actually
/// place Surges in the story (6, 7, 12, 16, 17) land separately.
/// </summary>
[TestClass]
public class SurgeDeserializationTests
{
    // Modelled on instance #2 from issue 4's table (the psychic door): a fixed, authored row
    // and a routed failure, which exercises every field on SurgeChallenge at once.
    const string SurgeJson = """
    {
        "id": 42,
        "type": "surge",
        "recap": "test fixture - the psychic door",
        "text": "You reach for the door.",
        "childid": 43,
        "challenge": {
            "sequencelength": 4,
            "sequence": "L U R D",
            "drainrate": 12.5,
            "restore": 5,
            "penalty": 9,
            "startingrage": 80,
            "maxrage": 120,
            "successmessage": "The door gives.",
            "failuremessage": "The door holds.",
            "failurechildid": 44,
            "duetpattern": ".S",
            "duetbeat": 0.25
        }
    }
    """;

    [TestMethod]
    public void SurgeJson_DeserializesIntoASurgeNode_WithChallengePopulated()
    {
        NodeBase node = JsonSerializer.Deserialize<NodeBase>(SurgeJson, JsonHelper.Options);

        SurgeNode surge = node as SurgeNode;
        Assert.IsNotNull(surge, "A node JSON payload with \"type\": \"surge\" must deserialize into a Surge.");

        Assert.AreEqual(42, surge.Id);
        Assert.AreEqual(43, surge.ChildId);
        Assert.AreEqual("You reach for the door.", surge.Text);

        Assert.IsNotNull(surge.Challenge, "The nested \"challenge\" object must populate Surge.Challenge.");
        Assert.AreEqual(4, surge.Challenge.SequenceLength);
        Assert.AreEqual("L U R D", surge.Challenge.Sequence);
        Assert.AreEqual(12.5f, surge.Challenge.DrainRate);
        Assert.AreEqual(5f, surge.Challenge.Restore);
        Assert.AreEqual(9f, surge.Challenge.Penalty);
        Assert.AreEqual(80f, surge.Challenge.StartingRage);
        Assert.AreEqual(120f, surge.Challenge.MaxRage);
        Assert.AreEqual("The door gives.", surge.Challenge.SuccessMessage);
        Assert.AreEqual("The door holds.", surge.Challenge.FailureMessage);
        Assert.AreEqual(44, surge.Challenge.FailureChildId);
        Assert.AreEqual(".S", surge.Challenge.DuetPattern);
        Assert.AreEqual(0.25f, surge.Challenge.DuetBeat);
    }

    [TestMethod]
    public void SurgeJson_WithoutADuetBeat_DefaultsToThreeTenthsOfASecond()
    {
        const string json = """{"id": 1, "type": "surge", "text": "test", "childid": 2, "challenge": {"duetpattern": ".S"}}""";

        NodeBase node = JsonSerializer.Deserialize<NodeBase>(json, JsonHelper.Options);

        SurgeNode surge = node as SurgeNode;
        Assert.IsNotNull(surge?.Challenge);
        Assert.AreEqual(0.3f, surge.Challenge.DuetBeat);
    }

    /// <summary>
    /// "bufferinput" decides what a press on one of Saberinne's glyphs is worth, and every
    /// Surge authored before it existed was tuned against the swallowing behaviour, so an
    /// absent key has to keep meaning false rather than picking up the new one.
    /// </summary>
    [TestMethod]
    public void BufferInputDeserializesFromJson()
    {
        const string withBuffer = """{"id": 1, "type": "surge", "text": "test", "childid": 2, "challenge": {"duetpattern": ".S", "bufferinput": true}}""";
        const string withoutBuffer = """{"id": 1, "type": "surge", "text": "test", "childid": 2, "challenge": {"duetpattern": ".S"}}""";

        SurgeNode buffered = JsonSerializer.Deserialize<NodeBase>(withBuffer, JsonHelper.Options) as SurgeNode;
        SurgeNode unbuffered = JsonSerializer.Deserialize<NodeBase>(withoutBuffer, JsonHelper.Options) as SurgeNode;

        Assert.IsNotNull(buffered?.Challenge);
        Assert.IsNotNull(unbuffered?.Challenge);
        Assert.IsTrue(buffered.Challenge.BufferInput, "\"bufferinput\": true must reach SurgeChallenge.BufferInput.");
        Assert.IsFalse(unbuffered.Challenge.BufferInput, "An absent \"bufferinput\" must stay false.");
    }

    [TestMethod]
    public void SurgeJson_WithoutAChallengeObject_DeserializesWithNullChallenge()
    {
        // The wrapper is optional in the JSON (Surge.Load defaults it with `Challenge ??= new()`),
        // so the converter must not choke on a bare "surge" node either.
        const string json = """{"id": 1, "type": "surge", "text": "test", "childid": 2}""";

        NodeBase node = JsonSerializer.Deserialize<NodeBase>(json, JsonHelper.Options);

        SurgeNode surge = node as SurgeNode;
        Assert.IsNotNull(surge);
        Assert.IsNull(surge.Challenge);
    }

    [TestMethod]
    public void CanConvert_Surge_ReturnsTrue()
    {
        NodeJsonConverter converter = new();

        Assert.IsTrue(converter.CanConvert(typeof(SurgeNode)));
    }
}
