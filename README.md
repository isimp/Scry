# Scry

Browse every prefab in the game and preview it, without anything entering the world.

Press F7 or type /scry in chat to open the panel. Search by the name the game shows or by prefab name, narrow the list to creatures, items, pieces, projectiles, effects, sounds, status effects or everything else, and pick one. Models turn on a stage inside the panel, where you can resize them, show a creature at each star level, play any of its animations, or show a piece new, worn or broken. Sounds play at your ears, either picked at random the way the game does or one variant at a time, effects play on the stage, where you look or on you, projectiles fly where you look and burst where they land, and a status effect shows its icon, description and visuals on you without ever being applied. Any model can also be shown in the world where you look and pinned there to compare with others.

Creatures show the weapons and armour they carry, in any combination they can roll, fires can be lit or put out, portals opened, plants shown growing or grown, bushes picked, and items in each of their styles. Every effect a prefab has can be played on it: a troll dies and falls as a ragdoll, a wall breaks apart, a tree is felled. A sound or effect shows every list it plays in, who plays it and what plays along, and plays the list together. The stage turns the model or holds it still, can be made taller, has front, side and top views, keeps whatever an effect throws in the picture, and has lighting presets, a sky, a floor ruled in metres with the model's size, and a person to stand beside it for size. An animation can be paused and scrubbed like a sound. Every section of the panel folds away and stays folded, and back and forward buttons (or the mouse's own) retrace the entries shown. Chips that go to another entry are tinted in the colour of its kind. What a prefab leaves behind, such as a troll's ragdoll or an oak's log, is named after it and listed beside it. A creature can fall as its ragdoll at any time, and its effects play the animation that goes with them, such as a stagger with a hit. Weapons, armour and capes can be shown worn by that person, and kept on while you try on more. The details tell what the game knows: an item's stats and recipe, a creature's health, weaknesses and drops, a piece's cost and comfort, what a status effect changes, where things spawn or grow, what drops, sells or makes them, what gives a status effect, and which mod added them. Clicking an ingredient, a drop, a station, a status effect or anything else the details name goes to it. For admins, the panel writes the game's own command to spawn the selection or give yourself an item, ready to paste into the console.

Every preview is local to your game. Nothing is spawned into the world, saved, or seen by other players, and all of it is gone when you leave the world or press Clear at the top of the panel.

## AI notice

Most of Scry was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Using it

The arrow keys move through the list, Enter plays or shows the selection, Ctrl+F jumps to the search and Escape closes the panel. Drag the preview to turn it and scroll to zoom. Star an entry to keep it in your favourites, and use Copy name to paste a prefab name into another mod's settings. Hover a name that does not fit to read all of it. The search understands terms such as kind:creature, has:aoe, biome:swamp, mod:name, used:troll and station:forge3, and a minus leaves things out; the ? button next to it explains them. Recent lists what you looked at last.

To watch a preview in the world, switch the panel to its compact view, which moves it to the side of the screen. You can walk around while the panel is open, as long as you are not typing in its search, and hold the right mouse button outside the panel to look around. Attacking and blocking stay off while the panel is open. Closing the panel leaves every preview where it is until you press Clear. A playing sound shows where it is in its clip, and the bar can be dragged to skip through long ones such as music. /scry followed by some text opens the panel searching for it, and /scry clear removes every preview from the world.

## Multiplayer

Only you need Scry, and it works on any server. Nothing it does is sent to anyone else.

## Settings

All settings are in BepInEx/config/isimp.Scry.cfg, each with a description.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Scry
