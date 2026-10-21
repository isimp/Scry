# Scry's notes

How Scry works, which of the game's rules it follows, and what to do when the game updates. Scry runs in the game client (`BepInProcess("valheim.exe")`); the same DLL loads on a dedicated server (`BepInProcess("valheim_server.exe")`), where, finding no graphics device, it patches nothing and draws nothing and runs only its side of the messages with the players' games ([server](server.md)). Nothing Scry shows is spawned or saved into the world, and no other player sees it.

Each note covers one area, starts with what it covers, names the source files it is about on its `Code:` line, and links its neighbours. Player-facing pages (installing, the compendium's rules, the server's settings, the commands) are in [guide](guide/install.md).

## Where to start

1. [Architecture](architecture.md): the layers, ownership, how failures go. Read before changing code.
2. [Standards](standards.md): the rules every change keeps, and the tests holding each.
3. The note of the area below.
4. [The self-test](operations/selftest.md): what to run in game to see a change work, and which parts to pick.

## Every note

| Note | Covers |
|---|---|
| [architecture.md](architecture.md) | layers, failures, caches, ownership |
| [standards.md](standards.md) | wording, numbers, skin, doc and source tests |
| [catalog.md](catalog.md) | reading the game, kinds, groups, names |
| [search.md](search.md) | search terms, suggestions, help, header, keys |
| [previews.md](previews.md) | copies, looks, gear, animations, attacks, effects |
| [stage.md](stage.md) | the stage, backdrops, biome ground, in the world |
| [details/pages.md](details/pages.md) | page layout, topics, header, links, leftovers |
| [details/game-rules.md](details/game-rules.md) | the game's rules pages follow, raids, biomes |
| [details/skills.md](details/skills.md) | the skills tab and skill lines |
| [details/sources-and-mods.md](details/sources-and-mods.md) | where things come from, mods, the mod report |
| [locations/reading.md](locations/reading.md) | reading places, their creatures, runestones, music |
| [locations/floors.md](locations/floors.md) | floors found and kept, the cut |
| [locations/examples.md](locations/examples.md) | example dungeons, plan, floors and levels, building |
| [locations/plan-box.md](locations/plan-box.md) | the plan box, rooms peeked and gone into, the room card |
| [locations/this-dungeon.md](locations/this-dungeon.md) | the dungeon one is in, walks, fog, maps kept |
| [locations/dungeon-records.md](locations/dungeon-records.md) | marks on the map, the server's records, traces |
| [compendium/book.md](compendium/book.md) | the book, tiers and the floor, cost |
| [compendium/learning.md](compendium/learning.md) | watchers, catch-up, the explored map |
| [compendium/together.md](compendium/together.md) | events between games, who one plays with |
| [compendium/sources.md](compendium/sources.md) | what a world shows as known, the server's rules |
| [compendium/showing.md](compendium/showing.md) | Loose and Strict, what is kept back, tallies |
| [network.md](network.md) | every message, who sends it, what is checked |
| [server.md](server.md) | alone, hosting, dedicated, what the server keeps |
| [operations/files-and-settings.md](operations/files-and-settings.md) | files, settings, log, resource monitor |
| [operations/game-updates.md](operations/game-updates.md) | the startup check, shapes, stubs |
| [operations/selftest.md](operations/selftest.md) | runs and their picks, parts, pictures |
| [operations/limits.md](operations/limits.md) | what Scry knowingly leaves out |
| [operations/building.md](operations/building.md) | building |
| [releasing.md](releasing.md) | releases and the stores |

## To do something

- A game update turned a feature off: [game-updates](operations/game-updates.md), then the note of that feature.
- Add a way the compendium learns: [learning](compendium/learning.md), then a row in the learning-path part of [the self-test](operations/selftest.md).
- Change what Loose or Strict shows: [showing](compendium/showing.md), [sources](compendium/sources.md).
- Add or change a message between games: [network](network.md), [server](server.md).
- Change a page's rows or wording: [pages](details/pages.md), [standards](standards.md).
- Follow another of the game's rules on a page: [game rules](details/game-rules.md); the startup check must watch the method ([game-updates](operations/game-updates.md)).
- Change how dungeons are laid out or shown: [examples](locations/examples.md), [plan box](locations/plan-box.md), [floors](locations/floors.md).
- Change the dungeon one is in, walks or marks: [this dungeon](locations/this-dungeon.md), [dungeon records](locations/dungeon-records.md).
- Make Scry cost less: [architecture](architecture.md) (timed parts), [files and settings](operations/files-and-settings.md) (the monitor), and the cost parts of [the self-test](operations/selftest.md).
- Release: [releasing](releasing.md).
