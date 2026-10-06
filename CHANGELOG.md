# Changelog

## 0.2.0

Locations have a tab of their own: every location and dungeon room the world can place, grouped by biome, each telling where it is placed and what it holds. A location stands on the turntable as a new zone would roll it, its creatures included, and *Roll again* rolls it anew.

A dungeon or camp is built on the turntable as an example layout, with its floor plan in the corner, and a click on a room goes to it. Places can be cut open floor by floor with the ruler beside the stage, Page Up and Page Down, or Shift with the wheel. The rooms below a floor opened are dimmed and unlit; `LightDimmedRooms` keeps their lights.

Raids, biomes, spawners and mods have tabs of their own: what a raid brings and for whom, a biome's weathers, music and what lives there, a spawner's creatures and pace, and what each mod adds and which of the game's rules it hooks into.

*In the game* is laid out by topic, what a page is looked at for first, with tables where numbers change with something, such as a creature's attacks and stars or a weapon's damage by quality. An attack in a table plays when clicked.

Pages tell much more: a creature's senses, breeding, taming and riding, a piece's support, weather and placement rules, what gear resists and changes while worn, what burning, poison, frost and lightning do, fish and bait, keys and doors, and what follows a boss's fall. Pages of one kind show the same rows, saying none where that is the answer.

Loot is told by what it is worth: what nothing else gives is marked *only here*, what a trader buys is marked with its coins, then the rest, the least likely first. An item tells its odds in every chest, rock or tree that gives it.

A creature that comes only during a world event or after a boss's fall, such as Fimbulvinter's or the Charred, is listed apart from where it lives.

What a rock, vein or tree is broken with lists the pickaxes or axes that break it, the weakest first.

Each page names the mods hooking into what it tells, and what Scry is not sure of is marked with a `~` that says why. Drops seen in play are remembered from session to session.

The search takes more terms (`is:`, `weak:`, `resists:`, `immune:`, `damage:`, `skill:`, `drops:`, `from:`, `needs:`, `gives:` and `spawns:`), names in quotes, a comma for either of two values, and a name typed one letter off. Its help has an example of each.

The stage zooms toward the pointer and moves with a right-drag, and the *Ground* backdrop stands a model on its biome's own ground under its sky, its footsteps sounding on it. Volume, size, speed and Repeat sit beside the selection's name.

When part of Scry is off because the game changed, a strip under the header says what in plain words. *Find in locations* is called *Read all locations*. Numbers show their thousands with commas, and `/scry monitor` shows what Scry costs each frame.

## 0.1.0

First release.
