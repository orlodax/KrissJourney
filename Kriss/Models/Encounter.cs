using System.Collections.Generic;

namespace KrissJourney.Kriss.Models;

public class Encounter
{
    public IEnumerable<Foe> Foes { get; set; }
    public string DefeatMessage { get; set; }
    public string VictoryMessage { get; set; }
}

public class Foe
{
    public string Name { get; set; }
    public int Health { get; set; }
    public int Damage { get; set; }
    public int AttacksPerRound { get; set; }
}
