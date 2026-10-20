# Server settings

A server with Scry, a dedicated one or a game hosting, can set what the compendium allows in section *4 - World* of `BepInEx/config/isimp.Scry.cfg`. The server sends these to every player connected; while connected, the server's values win, each player's own choices kept beneath them for when they play elsewhere. Left as written on first start, every choice is each player's. On a dedicated server, change them with the server stopped, then start it again.

## The settings

`Rules` is the compendium's rules everyone plays by: `Off`, `Loose`, `Strict`, or `PlayersChoose` (the default), which leaves them to each player ([the rules](rules.md)).

`Peeking` (true by default) is whether players may peek, every page showing whole until the panel closes.

`KnownUpTo` (true by default) is whether players may raise *Known up to*, showing everything of the tiers below the bosses they beat as known.

The rest cap how far each source of the world may show in the compendium's box; a player choosing above the cap gets the cap.

`ExploredUpTo` and `MarkedUpTo`, for *Ground you explored* and *Places marked for you*: `Seen`, `Known` or `WithInsides` (the default).

`SharedMapsUpTo` and `OnEveryMapUpTo`, for *Shared maps* and *Places on every map*: `Off`, `Seen`, `Known` or `WithInsides` (the default).

`WholeWorldUpTo`, for *The whole world*: `Off` or `Counted` (the default).

## Examples

For a group that wants to find everything itself: `Rules = Strict`, `Peeking = false`, `KnownUpTo = false`, `SharedMapsUpTo = Seen`, `OnEveryMapUpTo = Seen`, `WholeWorldUpTo = Off`.

For a relaxed group that shares what it finds: leave `Rules` at `PlayersChoose` and the caps at their defaults, so a map shared at a cartography table can show its dungeons with what anyone walked inside.

## What players see

On joining, a notice under the panel's header says once what the server sets, such as "This server plays Strict; peeking and Known up to are off.", with *Show the rules* opening the compendium's guide; it is told again only when the server's settings change. The compendium's box shows a rule or a level the server set locked, with a lock after its caption and a tip saying the server keeps it so; levels above a cap are dimmed. Players without Scry join and play as before.

More: https://github.com/isimp/Scry
