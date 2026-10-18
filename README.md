# Scry

Look up anything in Valheim and see it, without spawning a thing. Press F7 or type /scry in chat, search for a creature, item, piece, sound, effect or location, and it appears on a turntable inside the panel.

![A camp on the Scry stage with its plan and what its rooms hold](https://raw.githubusercontent.com/isimp/Scry/main/docs/images/screenshot.webp)

## AI notice

Most of Scry was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## What it does

Creatures can be shown at every star level, in any of their animations and with the gear they can roll, and they attack with their own sounds and effects. Trees can be felled, pieces shown new, worn or broken, sounds and effects played, and projectiles fired where you look. Any model can also be placed in the world in front of you and pinned there to compare with others.

Locations and dungeon rooms stand on the turntable as a new zone would roll them, their creatures included, and *Roll again* rolls them anew. A dungeon or camp is built as an example layout with its floor plan in the corner, and places can be cut open floor by floor. Raids, biomes, skills and mods have tabs of their own: what a raid brings and for whom, a biome's weathers, music and what lives there, what each skill changes at no skill, at your level and at the top, and what each mod adds.

Below the preview, Scry tells what the game knows about it: an item's stats and recipe, a creature's health, attacks and drops, what a station makes, where things spawn and what drops them, where the world places a location and what it holds, which mod added them, and which mods hook into what drops or spawns. Wherever your skills change a figure, what it comes to at your level shows faint under it, such as a weapon's hits and their stamina, a bow's draw, a creature's sight while you sneak or a workbench's making time. Almost everything named there can be clicked to go to it. Every mod has a page of its own in the *Mods* tab, and with the list showing only what mods added, *Mod report* sums them all up: what it adds, what Scry links for its stations and tools, and what it could not place. *Read all locations* reads what every dungeon and other location holds, so things tell where they are found, which takes a few minutes in the background. Scry keeps what it read, so the next time it is known at once, and only what an update changed is read again; *Read again* on the Locations tab starts afresh.

The compendium, switched on with *Compendium* in the title row, shows only what the character you play has come to know: a creature once fought or tamed, an item once held, made, eaten, its recipe unlocked or its card shown, anything once used, a place once stepped into and a dungeon once entered. What was only seen stands on the turntable with its page closed. *Loose* shows a known page whole, while *Strict* shows of how a thing connects only what your character saw, such as the drops it saw fall or the ore it put into a smelter, and keeps back the numbers the game never shows until your character found them out, such as an attack's damage until it hit you or a creature's health until you killed one. A page opens with what your character did with the thing and how it came to know it. *Floor* shows everything of the biomes whose bosses your character has beaten as known, *Learned* lists what came lately, and the eye shows everything until the panel closes. A character played before is caught up from what the game keeps of it and from its explored map. Each character keeps its own choice and its own knowledge, in `BepInEx/cache/isimp.Scry/compendium`.

Everything Scry shows stays on your own screen. Nothing is spawned or saved into the world, and nothing is seen by other players. It works on any server, and servers do not need it; only for catching the compendium up from the explored map on a dedicated server does Scry ask the server's Scry, if it has one. *Clear world* at the top of the panel takes back whatever you placed.

## Using it

The arrow keys move through the list and Enter plays the selection. The search suggests terms such as `kind:creature`, `biome:swamp` and `station:forge` as you type, and the ? button beside it explains them all. Star an entry to keep it in your favourites, or use *Copy name* to paste its prefab name into another mod's settings.

*Compact* moves the panel to the side of the screen, so you can walk and watch a preview in the world. Hold the right mouse button outside the panel to look around.

## Settings

All settings are in `BepInEx/config/isimp.Scry.cfg`, each with a description. They include the key, the panel's size, walking and looking around while it is open, and reading locations by itself in every world. Favourites and the panel's layout are kept in `BepInEx/config/isimp.Scry`.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Scry
