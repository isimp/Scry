# Commands

Every command is typed in the chat as shown, or in the game's console (F5) without the slash, and needs no cheats. Scry answers in the chat, its reply led by its name.

## Opening and searching

`/scry` alone opens the panel, or closes it. `/scry` with any other words opens it searching for them, so `/scry troll` opens the panel on what holds "troll".

## Places and dungeons

`/scry locations` reads where things are found in the world's locations and dungeons, in the background, taking a few minutes; `/scry locations stop` stops it. The same reading starts from *Find where things are* in the Locations list, and by itself in every world while *Every world* is on.

`/scry forget` forgets what Scry kept of the locations and their floors, and reads them afresh, as *Look again* does.

`/scry map` looks at your explored map again for the compendium, as *Look at the map again* in the compendium's box does.

`/scry forget maps` forgets your character's own maps of the dungeons of this world: the rooms you walked, and the marks on the large map.

## Your compendium

`/scry forget chests` forgets what your character counted chests holding, the figures on a chest's page, so they count afresh. The chests you opened stay opened and never count a second time, so the figures start again from the chests you open next. It mends a fault of earlier versions, which counted a chest again after every world load.

## For an admin of a server

`/scry forget server maps` forgets the server's records of the dungeons walked in this world, everyone's; walks last made before that day are not taken back when players join.

`/scry read traces` has the server read again what was left inside the world's dungeons: chests taken from, things broken, pickups taken, doors opened. The game running the world, a dedicated server or a host, reads them at each start unless its `ReadTracesOnStart` is off (see [server settings](server-settings.md)). On a world hosted from the game, this command is the only way they are read.

## Looking into Scry

`/scry mods` shows what each mod adds and what Scry links for it.

`/scry clear` removes every preview Scry stood in the world, should one be left behind.

`/scry monitor` shows or hides a small box with what Scry costs each frame and what it holds, for finding faults.

`/scry dump` writes every part of the model on the stage to the log, where it sits and what it draws, for finding out why part of it does not show.

More: https://github.com/isimp/Scry
