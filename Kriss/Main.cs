global using static KrissJourney.Kriss.Services.TerminalFacade;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using KrissJourney.Kriss;
using KrissJourney.Kriss.Services;

Console.Title = "KRISS' JOURNEY";

#region Bind Command Line Arguments
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--debug")
        CommandLineOptions.IsDebug = true;
    else if (args[i] == "--skipSteam")
        CommandLineOptions.SkipSteam = true;
    else if (args[i] == "--chapter" && i + 1 < args.Length && int.TryParse(args[i + 1], out int chapter))
        CommandLineOptions.Chapter = chapter;
    else if (args[i] == "--node" && i + 1 < args.Length && int.TryParse(args[i + 1], out int node))
        CommandLineOptions.Node = node;
}
#endregion

#region Steam API Initialization
SteamManager.Initialize(() =>
{
    // This will be called when the application quits
    Debug.WriteLine("Game shutting down - cleaning up Steam resources");
});

// Set up a simple callback runner because the game doesn't have a dedicated update loop
_ = Task.Run(async () =>
{
    while (SteamManager.Initialized)
    {
        SteamManager.RunCallbacks();
        await Task.Delay(15);
    }
});

// Register an event to handle application exit
AppDomain.CurrentDomain.ProcessExit += (s, e) =>
{
    SteamManager.Shutdown();
};

// Wait for SteamManager to finish initializing before continuing
// Because for some reason the Steam library prints stuff to the console (but only on Mac)!
if (OperatingSystem.IsMacOS())
    await Task.Delay(3000);

Console.Clear();
#endregion

new GameEngine(new StatusManager())
    .Run();