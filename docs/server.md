# Where Scry runs: alone, hosting, on a dedicated server

What runs in each setting, what the world's server keeps, and what a server without Scry leaves out. The same DLL serves every setting.

Code: `src/Plugin.cs`, `src/Catalog/GroupMapsLink.cs`, `src/Catalog/CompendiumLink.cs`, `src/Catalog/DungeonTraceReader.cs`, `src/Catalog/EventLink.cs`, `src/Core/Settings.cs`.

See also: [messages](network.md), [the world's sources](compendium/sources.md), [dungeon records](locations/dungeon-records.md), [files and settings](operations/files-and-settings.md); all notes in [the index](index.md).

## The settings

| Setting | What runs | Notes |
|---|---|---|
| Alone (a world of one's own) | everything, the server's part in the player's own game | answers come from the game itself, at once |
| Hosting (a player's game runs the world for others) | everything; the host's game is the world's server for those joining | the host's `[4 - World]` settings bind everyone who joins; traces are read on `/scry read traces`, not at each start; the server's records live in the host's cache folder |
| Dedicated server with Scry | on the server: the world's settings sent to every player, answers on explored and shared ground, the census and dungeons in a ground, the records of dungeons walked, the traces read at each start; no patches, no panel | finds no graphics device (`GraphicsDeviceType.Null`) and runs only its messages |
| Dedicated server without Scry | each player's own Scry alone | events between players still pass through; no census, no answers on explored or shared ground, no server records or traces; the compendium's box names what is left out |

Every player plays the same version of Scry: the hello carries the messages' version, and a game with another is named as needing an update and counted as one without Scry.

## What the server keeps

- `BepInEx/cache/isimp.Scry/maps-<world>.dat`: the world's dungeon records, every room any player stood in, the first and last day walked, the rooms traces uncovered, the day an admin last forgot them. Written at most every half minute, flushed to disk with the file before kept as `.bak`; a file that cannot be read is never written over.
- `BepInEx/config/isimp.Scry.cfg`, section *4 - World*: the rules, whether peeking and *Known up to* are allowed, and how far each source of the world may show. ServerSync sends them to every player connected; set there, they win, each player's own choices kept beneath.
- The same file, section *5 - Server*, the server's own and never sent: `ReadTracesOnStart` (read the traces at each start, `GroupMapsLink.KeepRecords`) and `TakeWalks` (keep players' walks; off, `GroupMapsLink.MergeIn` takes none and a list of walks is answered wanting none).

A world moved from a hosting game to a dedicated server leaves the host's records behind; copy `maps-<world>.dat` across with the world to keep them.

## For an admin

- `/scry forget server maps` forgets the world's records; walks last made before that day are not taken back from players' lists.
- `/scry read traces` reads again what was left inside the world's dungeons: chests taken from, things broken, pickups taken, doors opened. The game running the world, a dedicated server or a host, reads them at each start unless `ReadTracesOnStart` is off; what was read stays though a mod resets a dungeon.

Both go on the admin's own connection and are refused for anyone not on the server's admin list.
