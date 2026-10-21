# Messages between games

Every message Scry sends between players' games and the world's server, who sends and who takes it, and what each side checks. The messages ride the game's routed messages, so a server without Scry passes them on all the same; the asks about a player's ground and the two an admin alone may send go on the asker's own connection.

Code: `src/Catalog/EventLink.cs`, `src/Catalog/CompendiumLink.cs`, `src/Catalog/GroundAsks.cs`, `src/Model/GroundAskPace.cs`, `src/Catalog/DungeonsInGround.cs`, `src/Catalog/GroupMapsLink.cs`, `src/Model/EventsBetweenGames.cs`, `src/Model/Packed.cs`, `src/Model/WalkList.cs`, `src/Model/ExploredGrid.cs`.

See also: [server](server.md), [playing together](compendium/together.md), [the world's sources](compendium/sources.md), [dungeon records](locations/dungeon-records.md); all notes in [the index](index.md).

## How far another game is trusted

A routed message names its sender as the sending game wrote it, and the server passes it on as it is (`ZRoutedRpc.RouteRPC`), as the game trusts every connected game alike. Within that, Scry holds to these rules:

- A message is registered anew on each connection, as the game makes its routed messages anew for each.
- What only the world's server answers is taken only from the world's server (`EventLink.WorldSession`): map and shared-ground answers, the census, dungeon records, dungeons in a ground, answers on walks wanted, forgetting and traces.
- An event is taken only from the game that ran it (its game id is the sender's), and counted once however often it comes (`EventLedger`).
- Forgetting the server's records and reading the traces go on the asker's own connection (`ZRpc`), registered on each player's connection as it appears, where the server reads its admin list for that connection; a routed message naming an admin would not do.
- The asks about a player's ground (explored map, shared ground, dungeons in a ground) go on the asker's own connection too, and the server answers each player, by the connection's platform id, a few at once and then one a minute, dungeons one in five minutes, passing over what comes sooner and saying so once in its log (`GroundAsks`, `GroundAskPace`). A game asking with made-up ground, cell by cell, so learns little in an hour; on routed messages a pace kept by sender could be dodged, or spent on another player's behalf.
- Packed bytes are unpacked only up to the most a message of their kind holds (`Packed.Unpack`): a map grid 8,192 pixels a side, a megabyte of walks or records sent to the server, 16 MB of dungeons answered by it. The game's own unpacking takes whatever the bytes stand for.
- Counts read from a message are bounded before anything is made for them (kinds of place, walks listed, wanted ids, the self-test's events and echoes).
- Of every walk a game sends, the server keeps one only of a location the world places where the walk's id says, of that location, with the rooms its map holds and no day later than today (`DungeonMaps.HeldBy`).
- Every handler runs inside `Guard.Run`, so a message that fails costs only itself.

What a game with a changed Scry can still do, and the game itself allows as much: send events that make others' books count what they never saw (an event cannot be checked against what really ran); claim a ground it never explored and learn which kinds of place, and which dungeons with their entrances, lie in it, at the pace above; light rooms of a real dungeon nobody walked.

## The messages

| Message | From, to | What | Checked |
|---|---|---|---|
| `Scry_Hello` | every game, to everybody; answered to the one asking | the messages' version (`ScryPeers.Messages`), whether it asks for one back | own hello passed over |
| `Scry_Event` | the game that ran an event, to its doers and those within 30 m whose games said hello | one event (`EventWire`) | from the game that ran it, counted once |
| `Scry_EventEcho`, `Scry_EventRun`, `Scry_EventRan`, `Scry_EventTakenAsk`, `Scry_EventTakenAnswer` | the self-test, to and from the world's game | made-up events run and echoed for the self-test | bounded; made-up things only; reach the self-test's book alone |
| `Scry_MapAsk`, `Scry_MapAnswer` | a player, to the server on its own connection, and back | the explored ground packed (`ExploredGrid`); the kinds of place in it, with how many | grid bounded; paced by connection; answer from the server only |
| `Scry_SharedMapAsk`, `Scry_SharedMapAnswer` | as above | ground from shared maps; the kinds in it | as above |
| `Scry_CensusAsk`, `Scry_CensusAnswer` | a player, to the server, and back | how many of each kind of place the world holds, never where | answer from the server only, count bounded |
| `Scry_DungeonsAsk`, `Scry_DungeonsAnswer` | a player, to the server on its own connection, and back | a ground and kinds of dungeon; the dungeons lying in it, with rooms where the world keeps them | grid and answer bounded; paced by connection; answer from the server only |
| `Scry_MapWalked` | a player, to the server | the record of a dungeon walked | unpacked bounded; kept only as `DungeonMaps.HeldBy` allows |
| `Scry_GroupAsk`, `Scry_GroupAnswer` | a player, to the server, and back | the server's records of a location's dungeons | answer from the server only |
| `Scry_WalksList`, `Scry_WalksWanted` | a player, to the server, and back | the walks a player has (`WalkList`); the ids the server wants | list bounded; answer from the server only |
| `Scry_GroupForget` | an admin, to the server, on its own connection | forget the world's records | admin by the connection |
| `Scry_GroupForgot` | the server, to the admin | how many it forgot, or refused | from the server only |
| `Scry_ReadTraces` | an admin, to the server, on its own connection | read the traces in the world's dungeons again | admin by the connection |
| `Scry_TracesRead` | the server, to the admin | how many dungeons, rooms and rooms on the way, or refused | from the server only |

ServerSync (vendored in `src/ServerSync`) sends the world's settings, section *4 - World*, from the server to every player; see [the world's sources](compendium/sources.md) and [server](server.md).
