# Kriss' Journey — Copilot Instructions

## Project Overview

Kriss' Journey is a **terminal-based interactive fiction game** written in C#/.NET. It adapts a source novel into a playable text adventure running entirely in the console. The story follows Kriss, a modern person transported to Noi-Hert, a decaying human colony on a distant planet, who must journey toward the legendary city of Ayonn with companions Corolla, Smiurl, Theo, and later the telepaths Math and Efeliah. The full novel continues beyond the current game content (chapters 1–10) through Ayonn's liberation from a tyrant, a southern sea voyage, encounters with underwater mutant civilizations, and culminates at the city of Saberinne where Kriss discovers his telekinetic destiny and his soulmate — the immortal warrior-queen Saberinne — before being returned to Earth.

## Architecture

- **Solution**: `KrissJourney.sln` with projects `Kriss` (game), `Publish` (publishing tool), `Tests`.
- **Story data**: JSON files in `Kriss/Chapters/` (c1.json through c11.json). Each is a `Chapter` containing an array of `NodeBase`-derived nodes.
- **Node types** (polymorphic via `NodeJsonConverter`):
  - `StoryNode` — linear narrative, press key to advance
  - `ChoiceNode` — branching decisions with arrow-key selection
  - `DialogueNode` — recursive conversation trees with optional player replies
  - `ActionNode` — text parser (verb + optional object), classic IF style with TAB help
  - `FightNode` — QTE combat with oscillating cursor timing mechanic
  - `MiniGame01` — extends ActionNode for a telepathy guessing game
- **Models**: `Chapter`, `DialogueLine`, `Reply`, `Choice`, `Action`, `ActionObject`, `Condition`, `Effect`, `Encounter`, `Foe`, `Prowess`, `Status`, `EnCharacter`
- **Services**: `GameEngine` (orchestrates chapters/nodes/state), `StatusManager` (persistence), `TerminalFacade` (abstracts Console for testability), `NodeJsonConverter` (polymorphic deserialization)
- **Helpers**: `Typist` (text rendering with flowing/instant modes, color codes, pause markers), `ProwessHelper` (combat stats per chapter)

## JSON Content Conventions

### Text Markup (rendered by `Typist.RenderText`)
- `#` = dramatic pause (~1 second). Multiple `##` = stacked pauses. This is NOT a comment — it's a timing directive.
- `$X` = inline color switch where X is a letter mapped to `ConsoleColor`:
  - `$C` DarkCyan (narrator default), `$c` Cyan (Kriss), `$R` Red (Corolla), `$Y` Yellow (Smiurl), `$B` Blue (Theo), `$g` DarkGreen (Efeliah), `$m` DarkMagenta (Math), `$M` Magenta (Elder/Chief), `$W` White (emphasis), `$D` DarkGray (meta/instructions), `$G` Green (Saberinne/mysterious voice), `$r` DarkRed, `$y` DarkYellow, `$d` Gray, `$K` Black
- `<<text>>` = telepathic speech (also set via `istelepathy: true` on dialogue lines)
- `$c<<thought>>$C` = Kriss's inner monologue

### Node Structure
- Each node has: `id` (unique within chapter), `type`, `recap` (internal note), `text`, optional `alttext` (shown on revisit), `childid` (next node), `isVisited`, `isLast`, `isClosing`
- **Choice nodes**: `choices[]` with `desc`, `childid`, optional `condition`, `effect`, `refusal`, `ishidden`, `unhide`, `isnotrepeatable`
- **Dialogue nodes**: `dialogues[]` with `actor` (EnCharacter), `line`, `precomment`, `comment`, `linename`/`nextline` (jump labels), `replies[]`, `childid`, `break`, `istelepathy`
- **Action nodes**: `actions[]` with `verbs[]`, `objects[]` (each with `objs[]`, `answer`, `childid`, `condition`, `effect`), fallback `answer`
- **Fight nodes**: `encounter` with `foes[]` (name, health, damage, attacksPerRound), `victoryMessage`, `defeatMessage`, QTE params (`qtecycles`, `qtelength`, `qtespeedfactor`, `qtewidth`)

### State System
- `Condition`: gates on inventory item or visited node (`type: "isNodeVisited"` with `item` = node ID as string)
- `Effect`: `gainitem` adds a string to inventory
- `Status`: tracks `Inventory` (string list) and `VisitedNodes` (chapter ID → node ID list)

## Narrative Style (for content work)

> **Source fidelity is non-negotiable.** Every scene, event, sensory detail, and character reaction in the adaptation must come directly from the source novel. Do not invent scenes, fill gaps with atmospheric set-pieces, or add sensory details the source doesn't describe — however vivid and well-crafted they would be. When the source is silent on a moment, keep the adaptation equally sparse. If in doubt, ask the author rather than inventing.

- **Second person, present tense**: "You walk," "You see"
- Narrator voice is literary but conversational — vivid sensory detail without pretension
- Characters have distinct voices: Theo is terse, Smiurl excitable and fearful, Corolla sardonic yet warm, Elder Long absent-minded and wise, Efeliah gentle and perceptive, Saberinne regal with ancient authority
- Inner monologue: `$c<<sardonic thought>>$C`
- Game Over messages should have dark humor and personality
- Use `#` pauses deliberately for dramatic timing, comic beats, and reveals
- Modern references (sneakers, pizza, eggs and bacon) contrast the fantasy setting intentionally
- Saberinne's dialogue uses `$G` (Green); her telepathic communion is narrated through `<<>>` and emotional/sensory description rather than conventional speech
- Psychic/telekinetic moments are narrated through physical sensation (burning hands, skull pressure, rage channeling) — never clinical
- Corolla's rare emotional breaks (Øder backstory, injuries) are devastating because they contrast with her usual composure

## Full Story Arc (Source Novel Summary)

The game currently adapts chapters 1–10 of the source novel. The complete story arc is:

1. **Arrival & the Horde** (c1–c4): Kriss transported to Noi-Hert by lightning, discovers raided village, captured by Horde, saved by Corolla/Smiurl/Theo, Chief killed by Ayonn's lights, Kriss takes his sword
2. **Journey to Ayonn** (c5–c10): World-building, desert crossing, Seer's Rock (Elder Long, Math, Efeliah join), Oxengutter fights, the Mazerock
3. **Ayonn: Liberation** (unadapted): Break through psychic shield, imprisoned by Joe the Tyrant who uses ancient Projector for mind-control, freed by Theo's strength, ally with rebels (led by Riff), infiltrate Municipal building, Math/Efeliah disable Projector
4. **Backstory: Øder** (unadapted): Corolla revealed as last princess of destroyed mountain city Øder; she and Theo joined Horde for survival after its destruction
5. **Southern Journey** (unadapted): Coastal town, battle with the Edzzen (insane hostile dwarves), escape to solar-powered ship
6. **Sea Voyage & Underwater** (unadapted): Storm sinks ship; captured by Sea Mutants (genetically engineered aquatic humanoids with underwater city); Kriss discovers telekinesis punching through metal door; escape through flooding glass tunnels
7. **City of Saberinne** (unadapted): Besieged white marble city; Kriss dream-flies over army; epic duel with armored warrior revealed as immortal queen Saberinne — his psychic soulmate
8. **Climax & Return** (unadapted): Mind-merge defeats army with storms/tornado; Kriss sent back to Earth; wakes from lightning strike; returns to school heartbroken; new student "Sabrina" arrives — Saberinne reincarnated

### Key Characters (Full Novel)

- **Riff**: Rebel leader in Ayonn, brave and passionate
- **Joe the Tyrant**: Despotic ruler of Ayonn, uses Projector for mind-control (offstage)
- **The Edzzen**: Colony of ancient insane hostile dwarves in a coastal town
- **Sea Mutants**: Blue-green aquatic humanoids, genetically engineered, with thriving underwater civilization
- **Saberinne**: Immortal warrior-queen of the southern city, millennia old, supreme telepath, Kriss's soulmate. Uses `$G` color. Becomes "Sabrina" on Earth

### Key Locations (Full Novel)

- **Ayonn interior**: Cells, Municipal building, Projector room (psychic machine with two control seats)
- **Øder**: Destroyed mountain city, Corolla and Theo's homeland
- **Edzzen coastal town**: Decaying port built from ship parts, lighthouse on promontory
- **Southern Sea**: Crystal clear, rich marine ecosystem, solar-powered ships
- **Sea Mutant underwater city**: Glass tunnels, coral gardens, schools, mini-sub transport, geothermal infrastructure
- **City of Saberinne**: White marble city in southern valley; elliptical dueling hall, palace, gardens, hedge maze

## Code Conventions

- C# with .NET, `System.Text.Json` for serialization (case-insensitive, `PropertyNameCaseInsensitive = true`)
- `TerminalFacade` static class wraps all Console I/O — never call `Console.*` directly in game code
- `Typist` handles all text rendering — use `RenderText`, `FlowingText`, `InstantText`, `RenderLine`, `WaitForKey`
- Node classes inherit `NodeBase`, override `Load()`, call `Init()` then `AdvanceToNext(childId)`
- JSON chapters are embedded resources read by `GameEngine`
- Build: `dotnet build Kriss/KrissJourney.Kriss.csproj`
- Tests: `dotnet test Tests/KrissJourney.Tests.csproj`
- Steam integration exists via `SteamManager`

## Important Patterns

- Nodes are linked by `childid` references within the same chapter — always verify target nodes exist
- `IsVisited` controls whether text flows (typewriter) or renders instantly
- The climbing puzzle (c6) and Mazerock (c10) use networks of Choice nodes as spatial navigation
- Fight difficulty scales via `ProwessHelper.GetProwess(chapterId)`
- `EnCharacter` enum maps character names to console colors via `CharacterExtensions.Color()`
