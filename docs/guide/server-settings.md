# Server settings

A server with Scry, a dedicated one or a game hosting, can set what the compendium allows in section *4 - World* of `BepInEx/config/isimp.Scry.cfg`. The server sends these to every player connected; while connected, the server's values win, each player's own choices kept beneath them for when they play elsewhere. Left as written on first start, every choice is each player's. On a dedicated server, change them with the server stopped, then start it again.

## The settings

`Rules` is the compendium's rules everyone plays by: `Off`, `Loose`, `Strict`, or `PlayersChoose` (the default), which leaves them to each player ([the rules](rules.md)).

`Peeking` (true by default) is whether players may peek, at the page shown until they go to another or at every page until the panel closes.

`KnownUpTo` (true by default) is whether players may raise *Known up to*, showing everything of the tiers below the bosses they beat as known.

`SeenReach` (10 by default) is how near, in metres, a creature, an item, a piece or a resource in view counts as seen, a creature's stars, what it carries and its attacks begun kept for the compendium: as far as the game names a creature and marks its stars over it. At 0 no stars, gear or attacks are kept, and things are seen by their size alone. Under *Strict* the stage shows a creature only with the stars and gear it was seen with so; 0 keeps none.

`SeenOnMapReach` (50 by default) is how far away, in metres, a player's sight remembers where an ore deposit, a pickable or a place stands, for *Show on map*, things smaller than a person seen nearer. At 0 nothing is remembered, and nothing stands on the map, shown or kept, but the dungeons a player walked.

`PlacesOnMap` (`Squares` by default) is how the server tells players the places lying in ground they explored or saw on a shared map but never came near: `Off`, not at all, players seeing only what they saw themselves; `Squares`, the 128 m square each stands in, so they know one stands somewhere inside, never where; `Dots`, where each stands, as a hollow dot. A player whose Scry is older than this setting is told squares on `Dots`. It replaces the earlier `SquaresOnMap` switch: one left off reads as `Off`.

The rest cap how far each source of the world may show in the compendium's box; a player choosing above the cap gets the cap.

`ExploredUpTo`, for *Ground you explored*: `Off`, `Seen`, `Known` or `WithInsides` (the default). `MarkedUpTo`, for *Places marked for you*: `Off`, `Seen`, `Known` or `WithInsides` (the default).

`SharedMapsUpTo` and `OnEveryMapUpTo`, for *Shared maps* and *Places on every map*: `Off`, `Seen`, `Known` or `WithInsides` (the default).

`WholeWorldUpTo`, for *The whole world*: `Off` or `Counted` (the default).

Two more, in the section `[5 - Server]`, count only on the server and are never sent to players. `ReadTracesOnStart` (on by default) has the game running the world, a dedicated server or a host, read what was left inside the world's dungeons each time the world starts, a few milliseconds a frame, so rooms emptied before anyone's walk was kept count as walked on this server; off, they are read only when an admin types `/scry read traces`. `TakeWalks` (on by default) keeps the dungeon walks players send and tells them to everyone as walked on this server; off, the server keeps no one's walks, each player keeps their own, and the others see only what the traces show.

The server answers each player's asks about their ground (which places and dungeons lie in it) a few at once and then about one a minute, so a changed game cannot map the world by asking cell by cell; a player's own game never asks faster than that.

## Examples

For a group that wants to find everything itself: `Rules = Strict`, `Peeking = false`, `KnownUpTo = false`, `SharedMapsUpTo = Seen`, `OnEveryMapUpTo = Seen`, `WholeWorldUpTo = Off`.

For a relaxed group that shares what it finds: leave `Rules` at `PlayersChoose` and the caps at their defaults, so a map shared at a cartography table can show its dungeons with what anyone walked inside.

## What players see

On joining, a notice under the panel's header says once what the server sets, such as "This server plays Strict; peeking and Known up to are off.", with *Show the rules* opening the compendium's guide; it is told again only when the server's settings change. The compendium's box shows a rule or a level the server set locked, with a lock after its caption and a tip saying the server keeps it so; levels above a cap are dimmed. The settings' *Compendium* section shows the same rows, a line after a lock under each saying what the server keeps from it. Playing alone or hosting, that line is where these are set instead, each under the row it limits: *Your world plays* for `Rules`, *Your world allows it* for `KnownUpTo`, *Your world allows up to* for each source's cap; `Peeking`, `SeenReach` and the rest stand in the settings' *This server* section. Players without Scry join and play as before.

![The notice told once on joining a world whose server sets the rules](../images/server-rules-notice.webp)

![The compendium's box under the server's rules, what it sets locked](../images/box-under-server-rules.webp)

More: https://github.com/isimp/Scry
