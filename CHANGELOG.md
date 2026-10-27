# Changelog

## 0.3.2

A place shown on the stage stands on the ground it digs for itself. A tar pit sits in its bowl with its tar settled in it, small tar floating and big deposits on the pit's floor, and a Burial Chambers' entrance in its stair pit, where before the flat stage floor covered them.

Under *Strict*, a chain names each step once you saw it go to the step next to it, so it reads the same on every page, and a hen seen laying is paired with its egg.

Hildir's Sealed Tower is no longer marked on the map as a dungeon nobody walked. Once you stand in it, it is marked as a place you were.

Things that are one thing to a player are one row: the three tar pits, the Burial Chambers, a creature and its kinds, an item and the copies creatures wear. The page switches among the kinds you met and sets them side by side where they differ, under *Strict* each kept back as its own page keeps it back. A kind you never met is not shown or counted.

An item made by a recipe the game holds several times over, as the blackmetal tower shield was, shows its cost once.

A fish's page tells where it lives and how deep the water must be, when it comes and leaps, how it finds a float and what drives it off, its fight by stars with the raw fish each gives, and what a catch brings besides. Under *Strict* the biting and the fight show once you caught one.

The Creatures tab can group by biome or by what taming allows, and the Items tab by biome, with a switch above the list; each tab keeps its own choice.

A person's animations follow what it holds: the clips it moves, blocks and attacks in with that, and none of other gear's, which the filter still finds, saying what they are held with. A creature no longer lists the attacks of its kin that nothing it carries starts. Clips are grouped by what they are for: attacks, moving with what is held, blocking, jumping and swimming, getting hurt, emotes, riding and resting, and working.

A creature whose death hurts what stands near, as the Writhan's and the lava blob's blasts do, has its death as a line of its attacks with what it deals, played by the clip it dies in. An attack the game plays no animation for says so, and why.

A page's clips are kept from one session to the next, so they show at once the next time; the person's own stance is worked out first, and the rest takes far fewer steps. `/scry forget clips` or *Work out again* works them out afresh.

A creature shows stars only where something in the world can give it some. Lox, Greylings, Bats, Blobs, Wraiths, summons and many more say they never come with any, and a mod's creature no spawn names keeps two stars marked unsure.

## 0.3.1

The compendium follows what the game itself shows. A recipe's materials, a piece's cost or a store's wares come with the thing once the game lists them for your character, under *Loose* and *Strict* alike.

A creature is seen up close within 10 m, where the game names it and marks its stars, and is kept with the stars and gear it had. Under *Strict* the stage shows it only so, and an attack you saw begun up close counts as one taken.

What *Strict* keeps back shows as a soft smudge, a name not yet met as a dash. Each page's title row counts what is left to find out, and `is:left` finds the pages that still have some.

Drops show what you saw fall and how often, and what all your characters saw stands faint beside your own counts.

*Learned* lists what your character met and the numbers it found out, newest first, a day spent in another world named by it.

Playing together, what you learn depends on where you stand and what you did, not on whose game runs the thing. A death seen by its remains counts even where that game has no Scry, and a kill the game credits you with is yours.

The dungeon marks on the large map carry a glyph for the dungeon's kind and stand on its entrance. The settings size them, choose which show and put yours on the minimap.

The stage is filmed only while something on it changes, and the dungeon you walk no longer flickers on it while the panel is open. Smaller fixes throughout.

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
