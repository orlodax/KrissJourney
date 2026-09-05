namespace KrissJourney.Kriss.Models;

/// <summary>
/// Tuning for a <see cref="Nodes.SurgeNode"/>, the rage-powered telekinesis minigame.
/// Mirrors the way <see cref="Encounter"/> parameterises a fight: everything one instance
/// needs sits in a single nested JSON object ("challenge"), so the whole difficulty curve
/// across the story lives in the chapters and never in the code.
/// Rage is an abstract quantity: <see cref="MaxRage"/> is a full bar, zero is a failed
/// Surge, and <see cref="DrainRate"/> is measured per second of real time.
/// </summary>
public class SurgeChallenge
{
    /// <summary>How many glyphs the row holds. Ignored when <see cref="Sequence"/> is set.</summary>
    public int SequenceLength { get; set; } = 8;

    /// <summary>
    /// Optional fixed row, for a scene that has to name the directions in its own prose -
    /// Math and Efeliah calling the keys aloud at the psychic door. Accepts "L", "U", "R",
    /// "D" or the arrow glyphs themselves, and ignores anything else, so "L U R D" and
    /// "LURD" are the same row. Left unset, the row is generated at random, which is the
    /// normal case.
    /// </summary>
    public string Sequence { get; set; }

    /// <summary>
    /// Rage lost per second, continuously. This is the clock, and the reason the Surge is
    /// the opposite of a Fight: standing still costs you.
    /// </summary>
    public float DrainRate { get; set; } = 25f;

    /// <summary>
    /// Rage regained per correct arrow. Momentum is the only thing that puts rage back in
    /// the bar, so the tuning must keep this worth more than the time it takes to press.
    /// </summary>
    public float Restore { get; set; } = 6f;

    /// <summary>
    /// Rage lost per wrong arrow. A wrong arrow never advances the row - which is what
    /// makes blind mashing fail - and never resets it either.
    /// </summary>
    public float Penalty { get; set; } = 8f;

    /// <summary>Rage the player starts with. Clamped into 0..<see cref="MaxRage"/>.</summary>
    public float StartingRage { get; set; } = 100f;

    /// <summary>A full bar. <see cref="Restore"/> never pushes rage above this.</summary>
    public float MaxRage { get; set; } = 100f;

    /// <summary>Flown after the row is completed, before <c>childid</c>.</summary>
    public string SuccessMessage { get; set; }

    /// <summary>Flown when the rage empties.</summary>
    public string FailureMessage { get; set; }

    /// <summary>
    /// Where a failed Surge goes. A Surge is never a Game Over: left unset, the player
    /// simply goes again on the spot, which is what most instances want. Set it when
    /// failing is part of the story - at Ayonn's shield every failed push costs a
    /// companion, so each attempt is its own Surge node routing its failure into the next
    /// beat of the scene.
    /// </summary>
    public int? FailureChildId { get; set; }

    /// <summary>
    /// Duet mask, cycled over the row: "S" marks a glyph as Saberinne's, anything else is
    /// Kriss's, so ".S" alternates. Hers are not the player's to press. They render in her
    /// green ($G), resolve on their own once the caret reaches them, and put back exactly
    /// the rage a correct input would - she carries her share of the working rather than
    /// costing him anything, which is the whole point of the mind-merge.
    /// </summary>
    public string DuetPattern { get; set; }

    /// <summary>
    /// Seconds Saberinne takes over each of her glyphs, one per beat through a run of them.
    /// The default is the tempo of brisk play, so her share lands on the same pulse the
    /// player is already producing; raise it to make her more deliberate, and it stops
    /// reading as a shared rhythm and starts reading as dead time somewhere past half a
    /// second. No <see cref="DuetPattern"/>, no beat.
    /// </summary>
    public float DuetBeat { get; set; } = 0.3f;

    /// <summary>
    /// What happens to a press that lands on one of Saberinne's glyphs. Left false the
    /// press is swallowed - no penalty, nothing spent, nothing advanced. Set true it is
    /// held in a single slot instead (latest press wins) and spent the instant the caret
    /// comes back to Kriss, exactly as if it had been pressed then: right advances and
    /// restores, wrong takes the ordinary penalty. The two read differently in the hands -
    /// on an alternating pattern a player in tempo keeps pressing through her beats - so
    /// the choice is per instance. No <see cref="DuetPattern"/>, no buffer.
    /// </summary>
    public bool BufferInput { get; set; }
}
