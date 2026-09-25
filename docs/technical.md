# Technical notes

## The catalog

The catalog is read once per world, the first time the panel opens, and the time it took is written to the log. It holds every prefab registered with the scene, every status effect in the object database, and every prefab reached through an effect list. Most sounds and many visual effects are never registered on their own: they hang off the effect lists of the items, attacks, pieces, creatures, status effects and interface that play them, so those lists are followed, including effects that point at further effects. Effects that belong only to locations, such as boss altars and runestones, are not reached, because locations are loaded from their bundles on demand and walking them would force the loads.

Each entry's kind comes from its components. What a prefab does outranks how it looks: anything with a character is a creature, then projectiles, items and pieces. Something that only has sound is a sound. Particles with nothing solid, or anything drawn that is only ever played from an effect list, is an effect. Everything else, such as trees, rocks and spawners, is other. Entries with nothing to draw, light or hear are listed but marked as having nothing to preview.

A prefab is from the game when it was in the scene's lists before any mod ran, read by a first-priority prefix on `ZNetScene.Awake`; status effects the same way on `ObjectDB.Awake`. An effect is from the game when any game prefab, status effect or the interface uses it.

## Effects

Effect lists sometimes point at whole models: the ragdoll that replaces a creature when it dies, creatures it splits into, items, skinned bodies and pieces that fly apart under physics. A preview copy keeps no physics, so these would only stand frozen beside the preview; playing an effect leaves them out.

## Preview copies

Every preview is a copy of the prefab made under an inactive holder, so none of its scripts wake. While it sleeps everything is taken off except what draws, animates, lights or sounds: renderers, meshes, particle systems, animators, lights, audio sources, level-of-detail groups, cloth, projectors, and a few of the game's own effect scripts that need neither a network view nor a character (light flicker, random sound clips, timed destruction, fades, billboards and decals). Scripts go first, then joints, then colliders and bodies, and a component another still needs is only removed after it. Scripts added by mods are always removed, since what they need cannot be known. Only then is the copy moved into place and woken. It has no network view, so no other player sees it and nothing of it is saved. Animations play in place and send no events.

Star levels and wear are switched by scripts the copy no longer has, so their settings are read from the untouched prefab and applied to the matching parts of the copy, found by their position in the hierarchy.

Animations are played clip by clip rather than through the animator's switches. The switches only move the animator between states under conditions the game sets together (waking only while asleep, jumping only in the air), and those conditions cannot be read at run time, so a switch pulled on its own often does nothing. Each clip in the animator's controller is instead played directly on the copy through a playable graph; when it ends or is stopped the graph is destroyed and the animator carries on as before.

A status effect offers every effect list it carries that has something in it: its start effects are shown on you until taken off, and the others, such as stop, tick or break, play once. One with none says so.

## Looks and gear

Some prefabs have several looks that the game switches by script, and one kind is offered per prefab, in this order: a plant's growth (the grown plant is a prefab of its own, so it is copied instead), a fire's state (the enabled, low, high, full, half and empty objects of `Fireplace`, set as `Fireplace.UpdateState` sets them), a pickable's picked state (its hidden-when-picked object), a humanoid's gear, and an item's style (set through the game's material manager as `ItemStyle.Setup` does).

Gear is put on as `VisEquipment.AttachItem` puts it on: each item's `attach` part hangs on the matching hand or head bone, and an `attach_skin` part is bound to the body's bones. A creature always carrying the same things offers one gear look with its default items and the first of each random weapon, armour and shield; one given one of several sets offers each set. A prefab whose body mesh is chosen at run time, such as the player, gets the first of its models. Armour made for a skeleton with a different number of bones is left off rather than drawn twisted. An item shown worn is put on a copy of the player the same way, and chest and leg armour also paint the body with their own textures, as the game swaps them. /scry dump writes every renderer of the stage copy to the log, for finding out why part of a model does not show.

## Knowledge

With the catalog, Scry reads where things live and where they come from: the world spawn lists of the running spawn systems (biome, time of day, levels, group size, required key), raids, nests and spawn points among the registered prefabs, and the world's vegetation (biome and altitude). Items learn what drops them from creatures' drop lists, what they are picked from, every drop table on any prefab, every list that turns one item into another (smelters, kilns, fermenters, cooking stations, and mods' that work the same way), and what loaded traders sell. Status effects learn what gives them: any item, attack or part of a prefab that refers to them, or names them. Spawn points sit inside dungeons and locations, which the game only loads nearby, so their biome is not known. Biomes found this way are searchable with biome:.

Which mod added a prefab is read from Jotunn's registry when Jotunn is installed, by reflection, so Scry does not depend on it. For the rest, the loaded asset bundle holding an asset of the prefab's name is found, and the mod shipping a bundle of that name, as a file in its folder or embedded in its assembly, is named. A prefab made in code, or a bundle named unlike anything its mod ships, stays unnamed.

What a status effect changes is every number, switch or choice on it that differs from a fresh status effect of the same kind, plus its damage modifiers, so status effects from mods show too.

## The stage

The turntable is a copy far above the world on a layer the game does not name or use. It has its own camera, which renders into a texture the panel draws, and its own lights. The world's sun and the main camera are told to ignore that layer, and fog and ambient light are set for the stage only while its camera renders, then restored. The camera frames the copy from the size of what it draws, with particles left out when there is anything else. Effects on the stage are heard as if beside you; anything else on the stage is muted. The stage copy is taken down while the panel is closed.

## In the world

A model shown in the world stands where the camera was looking when it was placed, facing you. Effects play at the same spot or attached to you, and are removed after ten seconds if they do not end themselves. Projectiles lose their own script, so they are flown instead with the prefab's gravity and a chosen speed, and a ray along the path stops them at the first ground or building and plays their hit effects there. The ray only looks. Status effects show their start effects attached to you as the game would attach them, and their stop effects when taken off; the status effect itself is never added. Sounds are copies attached to the camera, kept for as long as something on them plays or is paused, so long clips such as music play to the end and can be skipped through by setting the audio source's position. Most sounds hold several clips and the game's sound script picks one at random each time it plays, when it starts a frame after the copy wakes. To play one variant on purpose, the copy's list is narrowed to that clip before then, so it still plays with the sound's own volume, pitch variation and mix; parts of the same sound that do not hold the clip are kept quiet. The panel lights the clip that is actually playing, so a random play shows which variant was heard. Skipping through or pausing a sound hands its end to Scry: the prefab's timed destruction and the sound script's scheduled fade are called off, since both count time played rather than the place in the clip. Location music does not play on its own; the game's `MusicLocation` starts it when you come near, so Scry starts it instead, at your music volume.

## Input

While the panel is open, `TextInput.IsVisible` answers yes. The game already stops moving, looking, attacking and opening its menu while a text box is up, and frees the cursor, so that covers all of it. The camera zoom reads the wheel regardless, so the one method all of the game's wheel reads go through, `ZInput.Internal_GetMouseScrollWheel`, answers nothing while the panel is open. The block lasts one frame past closing, so the Escape that closed the panel does not also open the game's menu.

Holding the right mouse button, pressed outside the panel, looks around. The game asks for mouse look separately from movement and actions (`PlayerController.TakeInput(look: true)`), so only that question is answered yes while the button is held, and the cursor is captured as in normal play. Walking, blocking and attacking stay off.

The compact view is a slim column at the side of the screen without the turntable, which is not rendered while it is in use. Both views remember their own place and size. The character can walk while the panel is open: the movement half of `TakeInput` is answered yes whenever none of the panel's text boxes has the keyboard, and any click lets go of the keyboard unless it lands on a text box. The full view opens with the search focused, the compact view does not. While the panel is open, `Player.SetControls` never receives attack, block or dodge, so clicks on the panel and the look button stay harmless.

## Files

Favourites and the panel's place on screen are kept in `Scry` inside the game's own data folder (on Windows `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\Scry`), outside the BepInEx folder, so a mod manager replacing a profile's configs leaves them alone. Favourites of prefabs that are not in the game right now are kept.

## Limits

Humanoid creatures are shown without their weapons and armour, which the game puts on through a script that needs a network view. A mod prefab whose look is built by its own scripts may preview bare. Locations cannot be previewed. The stage is lit by its own lights, so materials that depend on the world's lighting can look different there than in the world.

## Building

The project builds with the .NET SDK against the game's assemblies. Set `VALHEIM_DIR` to the game folder if it is not a standard Steam install, then run `dotnet build`, which also copies the DLL into the local Gale profile. `dotnet test tests/Scry.Tests` runs the tests for the parts that need no game: kinds, search, the preview copy's rules, modifiers, favourites and origins.
