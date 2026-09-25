# Scry

Browse every prefab in the game and preview it, without anything entering the world.

Press F7 or type /scry in chat to open the panel. Search by the name the game shows or by prefab name, narrow the list to creatures, items, pieces, projectiles, effects, sounds, status effects or everything else, and pick one. Models turn on a stage inside the panel, where you can resize them, show a creature at each star level, play its animations, or show a piece new, worn or broken. Sounds play at your ears, effects play on the stage, where you look or on you, projectiles fly where you look and burst where they land, and a status effect shows its icon, description and visuals on you without ever being applied. Any model can also be shown in the world in front of you and pinned there to compare with others.

Every preview is local to your game. Nothing is spawned into the world, saved, or seen by other players, and all of it is gone when you leave the world or clear it from the panel.

## AI notice

Most of Scry was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Using it

The arrow keys move through the list, Enter plays or shows the selection, Ctrl+F jumps to the search and Escape closes the panel. Drag the preview to turn it and scroll to zoom. Star an entry to keep it in your favourites, and use Copy name to paste a prefab name into another mod's settings. /scry followed by some text opens the panel searching for it, and /scry clear removes every preview from the world.

## Multiplayer

Only you need Scry, and it works on any server. Nothing it does is sent to anyone else.

## Settings

All settings are in BepInEx/config/isimp.Scry.cfg, each with a description.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Scry
