# Changelog

## 0.3.0

The compendium shows only what the character playing has come to know. *Compendium* in the title row switches it *Off*, *Loose* or *Strict*, chosen for each character and kept with it, and a new character starts with it off. A creature is known once fought or tamed, and seen as far as its size makes it out; an item once held, made or eaten, its recipe unlocked or its tooltip card shown in a chest, the inventory or a store; anything once used, as a friend's portal, bed or ship; a resource once hit or picked; a place once stepped into and a dungeon once entered; a raid once lived through; a trader once its store is opened. What a known thing plays, leaves behind or gives comes along with it, but not what one only seen does. Anything in the world is seen by one rule, creatures, trees, rocks, buildings, pickables, nests and items on the ground alike: in view with nothing solid between, as far as its size makes it out. Biomes under the ground explored are seen, and so are the places a Vegvisir marks. A page opens with a box titled *You*: what the game counted of it for the character, as kills, things made or picked up, foods eaten and pieces built, and how, on which day and in which biome it came to know it. What was only seen stands on the turntable with its page closed, telling how to come to know it. Lists and search hold only what was met, each tab counting what was found of all there is.

*Loose* shows a known page whole. *Strict* shows of how a known thing connects only what the character saw go together: the drops it saw fall, where it met a creature or picked a thing up, what it made at a station, what built a piece, what hit it, what a creature ate, what a trader sells, what went into a station and came out of it, what a tree or rock really dropped, the offering that called a boss, the bait a fish took. A known creature's damage grid keeps back how it takes each type until the game's popup on a hit told it, and other numbers the game never shows wait until the character found them out, each saying what shows it: an attack's damage until it hit you, health until you killed one, taming, pregnancy, growing up and growing back until you saw it, a weak spot until a hit found it, a tree's or rock's health until you broke one, what stars change until you saw a starred one fall, a spawner's pool and pace until you saw it spawn, a producer's pace until you collected from one. What a chest holds is counted the first time you open each one, what a bush gives besides as you pick it, and a biome's weathers as you see them come. Drops and what trees and rocks give show what the character saw fall, how many and how often, not the game's chances, on their own page and in the lines of an item's page telling where it comes from, and where a creature spawns or a plant grows shows only for the biomes it was met in. A section that left something out says faintly at its end how many more were not yet seen, never which, and names of things never met read ??? in a page's text and its tooltip card.

*Floor* shows everything of a biome as known once its boss has fallen for the character, tier by tier as far as the bosses beaten in a row, and can be lowered again; what it shows is marked in the list, on its page, and on every chip and link going to it from another page. Each thing's tier is worked out from how it is come by, so iron gear belongs to the Swamp, black metal to the Plains and a guardian power to its boss.

A character played before is caught up from what the game keeps of it: what it carried, made, ate and picked, the recipes and pieces it unlocked, its kills, trophies and biomes, and the guardian power it chose. Its explored map counts too, the kinds of place lying in ground it explored counting as seen, read again each session and told in the notice as news; on a dedicated server that needs Scry on the server, which tells only which kinds of place lie there, never where. *Read the map again* above the Locations list, or `/scry map`, reads it anew. What a character learns is collected even while the compendium is off, so turning it on later shows everything met meanwhile, and each character's knowledge is kept in `BepInEx/cache/isimp.Scry/compendium`.

*Learned* lists what the character learned lately, newest first, including what it found out of something *Strict* held back, and the eye beside the mode shows everything until the panel closes. Under *Strict* a page's *You* box ends with what is still to find out and what to do for each.

What *Read all locations* reads is kept, so the next time it is known as soon as the world's catalog is. After an update of the game only the locations it changed are read again, and after a change of mods or of Scry all of them, in the background, with what was known shown meanwhile. `/scry locations` reads them all again.

Where a place's floors are is kept too, so a dungeon's rooms no longer each stall a frame to be measured when its example is built again, in this session or the next. Showing what the locations hold is spread over a few frames, so neither entering a world nor finishing a reading stalls one.

*Read again* above the Locations tab's list, or `/scry forget`, forgets everything Scry kept and reads the locations afresh, for when what is shown seems out of date.

A place zoomed out on the stage keeps all its rooms; the game's level of detail dropped their pieces one by one as they grew small. Locations and dungeon rooms no longer list animations, which were their creatures' or their doors' rather than their own.

A greatsword's, an axe's or a knife's first attack plays its clip again, as do the attacks of creatures that pick one of several animations, such as Bonemass's punch or Yagluth's stomp. After looking around with the right mouse button, the pointer is back where it was rather than in the middle of the screen.

What equipping something changes on you is noted as you play, whichever mod made it so, and the item's page shows it by quality under *Wearing*: the carry weight, armour, movement, resistances and effects a backpack or other mod gear gives only once worn.

An item that a mod's settings are named after, such as a backpack whose size and carry bonus a mod sets in a section of its own, shows those settings on its page under *Set by mods*, as the mod's config holds them now.

An item whose tooltip in the game mods add lines to shows that tooltip on its page under *Tooltip in the game*, at each quality, as a card laid out like the details, the lines the mods added tinted, and the mods hooking the tooltip named under it, each going to its page.

A creature's *With stars* table gives each drop that stars make only likelier a line of its chance at each star, as Valheim 1.0.20 does for every trophy, where it said they doubled in number.

A weapon's attacks table has a line for each step of its combo, the last as the game deals it, each playing its own swing, and says how the combo goes and what starts it over. A windmill's page tells what wind it needs: none below the least, full at the strongest, any facing; what blocks it within 30 m of its hub; when its blades hurt; and how much time away it makes up.

A mod's website opened from its page no longer keeps Steam showing Valheim as running after the game is closed when the browser was not open before.

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
