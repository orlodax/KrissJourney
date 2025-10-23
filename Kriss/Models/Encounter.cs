using System.Collections.Generic;

namespace KrissJourney.Kriss.Models;

public class Encounter
{
    public IEnumerable<Foe> Foes { get; set; }
    public string DefeatMessage { get; set; }
    public string VictoryMessage { get; set; }
    public int QteCycles { get; set; } = 3;
    public int QteLength { get; set; } = 20;
    public float QteSpeedFactor { get; set; } = 1f;
    public int QteWidth { get; set; } = 2;
}

public class Foe
{
    public string Name { get; set; }
    public int Health { get; set; }
    public int Damage { get; set; }
    public int AttacksPerRound { get; set; } = 1;
}
