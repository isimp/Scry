# Scry

A compendium of Valheim that grows with your character. Every creature, item, piece, place and dungeon you meet gets a page, told by the game's own data and rules, while the rest stays unknown until you find it. Press F7 or type `/scry` in the chat to open it. Nothing is ever spawned.

![A camp on the Scry stage with its plan and what its rooms hold](https://raw.githubusercontent.com/isimp/Scry/main/docs/images/screenshot.webp)

## AI notice

Most of Scry was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## The compendium

Turn it on for a character with *Compendium* at the top of the panel. A creature becomes known once fought or tamed, an item once held or made, a place once stepped into, a dungeon once entered; what was only seen stands on the stage with its page closed, telling how to come to know it. *Loose* shows a known page whole but names only what your character has met. *Strict* also keeps back how things connect until your character saw them go together, and the numbers the game never shows until it found them out, such as an attack's damage until it hit you. A page's *You* box tells when your character first saw the thing and came to know it, and how its fights went; *Learned* lists what it learned, day by day; *Known up to* opens every tier below the bosses it has beaten. A character played before is caught up from what the game keeps of it and from its explored map.

## Exploring

Dungeons you walk are kept as you walked them, room by room, and marked on Valheim's large map; a click on a mark shows the dungeon in Scry, its rooms on a plan you can go into. Each world can show more as known, source by source: the ground you explored, maps shared at a cartography table, places a Vegvisir or a rune stone marked, the places every map shows. Places tell where they lie in your ground and what they hold.

## Playing together

Where your friends have Scry, what their game runs near you, a kill, a drop or a pick, counts for you as it would alone. With Scry on the server too, the compendium learns what only the server knows, such as how many of each place the world holds and which dungeons anyone on the server walked, and the server can set the compendium's rules for everyone.

## Looking things up

Anything can also be looked at on the stage inside the panel: creatures at every star level, in any animation, with their gear and their attacks' sounds and effects; trees felled, pieces worn or broken, projectiles fired; locations and example dungeons as a new zone would roll them, cut open level by level. Every figure tells the game's rules behind it, what your skills make of it, which mods add or change it, and almost everything named can be clicked to go to it. The search takes terms such as `kind:creature`, `biome:swamp` and `station:forge`, and the ? beside it explains them all. With the compendium off, Scry is a lookup of everything the game and its mods hold.

## Installing

Install from a mod manager. A server does not need Scry; with the same package in a dedicated server's plugins, or a player hosting the world, it does the server's part. How to set up a server, the compendium's rules in full and the server's settings are at https://github.com/isimp/Scry/blob/main/docs/guide/install.md

## Settings

Settings are in `BepInEx/config/isimp.Scry.cfg`, each with a description; a server's are in its section *4 - World*. Favourites and the panel's layout are kept in `BepInEx/config/isimp.Scry`, each character's knowledge in `BepInEx/cache/isimp.Scry/compendium`. Every `/scry` command is described at https://github.com/isimp/Scry/blob/main/docs/guide/commands.md

More, with technical notes and build instructions: https://github.com/isimp/Scry
