# Changelog

## 0.3.0

Scry keeps a compendium for each character, chosen at the top of the panel and offered once on a new character. *Loose* shows only what the character has met; *Strict* also keeps back how things connect, and the numbers the game never shows, until the character found them out. The *?* in the compendium's box explains it in the game, and `is:new`, `is:seen` and `is:walked` search what the character met.

A page's *You* box tells when the character first saw a thing and came to know it, and how its fights went. *Learned* lists what it learned day by day, and *Known up to* opens every tier below the bosses it has beaten.

A character played before is caught up from what the game keeps of it and from its explored map.

Where friends have Scry, what their game runs near you counts for you as it would alone, and a notice names those whose games lack it.

Each world can show more as known, source by source in the compendium's box: explored ground, shared maps, marked places, places on every map. With Scry on the server, the compendium also learns what only the server knows, and the server can set its rules for everyone, which a notice tells once on joining. Explored ground shows its places through its own row, which can be turned off, as the map reaches 100 m around you whatever you saw, and a place counts as seen only once your eye has a clear line to it.

The gear in the title row holds the panel's settings, written to the config file as they are chosen, among them *Stage* set to *Fast* for big dungeons on weaker machines. The spin's speed and the lights below a dungeon's cut are in the View box, reading locations in every world is beside *Find where things are*, and the map's marks are in the compendium's box.

Dungeons are laid out room by room, with a plan to go into each room. The dungeon you are in shows as it was saved, your walks are kept and marked on the large map, and a server reads what was left inside dungeons walked before Scry.

Skills and traders have tabs of their own, and every figure a skill changes shows what it comes to at your level.

Pages tell more: weapon combos, a windmill's wind, what mods set or add to an item and its tooltip, stars' trophy chances as in Valheim 1.0.20, and readable names for all the game leaves unnamed.

What locations hold and where their floors are is kept between sessions, and big places are built and read over several frames.

A website opened from a mod's page no longer keeps Steam showing Valheim as running, the first step of a combo plays again, and the pointer comes back where it was after looking around.

## 0.2.0

Locations have a tab of their own: every location and dungeon room the world can place, grouped by biome, each telling where it is placed and what it holds. A location stands on the turntable as a new zone would roll it, its creatures included, and *Roll again* rolls it anew.

A dungeon or camp is built on the turntable as an example layout, with its floor plan in the corner, and a click on a room goes to it. Places can be cut open floor by floor with the ruler beside the stage, Page Up and Page Down, or Shift with the wheel; a place with nothing over its floors to take away, such as a ring of standing stones or a wreck, has no ruler and no cut. The rooms below a floor opened are dimmed and unlit; `LightDimmedRooms` keeps their lights.

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
