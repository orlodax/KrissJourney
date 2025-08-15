namespace KrissJourney.Kriss.Models;

/// <summary>
/// Represents how strong Kriss is.
/// </summary>
/// <param name="MaxHealth">Your HP.</param>
/// <param name="BaseDamage">Your attack power.</param>
/// <param name="RageBonus">Your rage bonus. It is meant to be a function of how often you get hit.</param>
/// <param name="FuryBonus">Your fury bonus. It is meant to trigger when you or a party member reaches 10% health, or other narratively major distressing situation.</param>
/// <param name="QteSpeedFactor">How fast the QTEs are.</param>
/// <param name="QteWidth">How wide the QTEs are.</param>
public record struct Prowess(
    int MaxHealth,
    int BaseDamage,
    int RageBonus,
    int FuryBonus,
    float QteSpeedFactor,
    int QteWidth);
