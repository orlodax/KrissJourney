using System.Collections.Generic;

namespace KrissJourney.Kriss.Models;

public class Condition                      // condition for the viability of the action. normally an item
{
    public string Type { get; set; }        // is it an item? a previous node to be visited?
    public string Item { get; set; }        // name of the resource
    public string Refusal { get; set; }     // message for condition not met
    public List<Condition> All { get; set; }// every nested condition must hold: use it instead of Type/Item to require more than one thing
}
