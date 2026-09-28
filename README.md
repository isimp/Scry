# Scry

Browse every prefab in the game and preview it, without anything entering the world.

Press F7 or type /scry in chat to open the panel. Search by the name the game shows or by prefab name, pick a kind (creatures, items, pieces, resources, projectiles, effects, sounds, status effects or the rest), and each kind's list is grouped the way the game itself sorts things: items by type and armour by slot, pieces by the hammer's tabs, creatures by faction, resources by how they are gathered, effects and sounds by what they are for. Groups fold away with a click.

Models turn on a stage inside the panel, where they can be resized, shown at each star level, played in any of their animations, or shown new, worn or broken. A creature attacks with the sounds and effects it makes, falls as its ragdoll, and wears any weapons and armour it can roll; a tree is felled and its log rolls away; a status effect shows its visuals on a person. Sounds play at your ears, as loud as the game plays them or, with the Volume slider, anywhere from silent to twice as loud, back to the game's own loudness for each new selection; effects play on the stage, where you look or on you, and projectiles fly where you look. Any model can also be shown in the world where you look, and pinned there to compare with others.

Below the stage, the details tell what the game knows of the selection: an item's stats as its tooltip gives them, its recipe and uses, a creature's health and drops with stars, its attacks and senses, what a resource takes to gather and what it gives, a piece's cost and comfort, what a station makes and burns, what a trader sells, how a boss is summoned, what a status effect changes, where things spawn or grow, and which mod added them. Most things the details name can be clicked to go to them, and much of what a prefab is tied to, such as what a creature carries or the sounds its animations make, is linked from both sides.

Every preview is local to your game. Nothing is spawned into the world, saved, or seen by other players, and all of it is gone when you leave the world or press Clear at the top of the panel.

## AI notice

Most of Scry was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Using it

The arrow keys move through the list and Enter does the obvious thing with the selection: a sound or effect plays, a creature attacks, an item is worn, a tree is felled or a piece breaks, a projectile flies. In a filter box Enter plays what it shows first. Ctrl+F jumps to the search, Escape leaves a text box, and Escape again closes the panel. Drag the preview to turn it and scroll to zoom. Star an entry to keep it in your favourites, and use Copy name to paste a prefab name into another mod's settings. The search understands terms such as kind:creature, biome:swamp, mod:name and station:forge3, and suggests them and their values as you type (Tab or Enter completes); the ? button next to it explains them all. Find in locations, at the top of the panel, reads where things are found in the world's locations and dungeons, which takes a few minutes in the background; a setting has it done by itself in every world.

The compact view moves the panel to the side of the screen, to watch a preview in the world. You can walk while the panel is open, as long as you are not typing in it, and hold the right mouse button outside it to look around; both can be turned off. /scry followed by some text opens the panel searching for it, /scry clear removes every preview from the world, and /scry locations reads the locations and dungeons. Should a game update change something Scry relies on, a Game changed note beside the panel's title names the features that are off until Scry is updated; everything else keeps working.

## Multiplayer

Only you need Scry, and it works on any server. Nothing it does is sent to anyone else.

## Settings

All settings are in BepInEx/config/isimp.Scry.cfg, each with a description: among them whether the search box takes the keyboard on opening, whether you can walk and look around while the panel is open, and how fast the model turns. Favourites and the panel's place are kept beside it, in BepInEx/config/isimp.Scry.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Scry
