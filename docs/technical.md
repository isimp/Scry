# Technical notes

How Scry works, what it copies from the game, and what to do when the game updates. Scry runs only in the game client (`BepInProcess("valheim.exe")`); a dedicated server never loads it. Nothing it shows is networked or saved, so it needs nothing from the server and no other player sees it.

## Layout of the code

`src/Model` holds everything that needs no game: kinds, search and suggestions, the list's groups, the rules for preview copies, adjustments, looks and outfits, favourites and history, links, leftovers, place names and the startup check's fingerprints. The tests in `tests/Scry.Tests` cover that part and nothing else. `src/Catalog` reads the game into the model, `src/Preview` makes and plays the copies, `src/UI` draws the panel, and `src/Patches` holds the few Harmony patches and the startup check.

## The catalog

The catalog is read once per world, a few milliseconds each frame, starting ten seconds after the world is entered, so it is usually ready before the panel is first opened. Opened earlier, the panel says how far the reading has got, and the reading takes a bigger share of each frame while the panel waits. Nothing shows until all of it is read, since an entry's kind, links and groups are only known then.

It holds every prefab registered with the scene, every status effect in the object database, every raid the world has switched on, and every prefab reached through an effect list. Most sounds and many visual effects are never registered on their own: they hang off the effect lists of the items, attacks, pieces, creatures, status effects and interface that play them, so those lists are followed, including effects that point at further effects.

Leaving a world lets go of everything read of it. Each part of Scry that keeps something of a world registers how it forgets it (`WorldCaches`), and a test over Scry's own source fails when a store of world things is never emptied or its class never registers.

A prefab is the game's own when it was in the scene's lists before any mod ran, read by a first-priority prefix on `ZNetScene.Awake`, a status effect the same way on `ObjectDB.Awake`, and a raid on `RandEventSystem.Awake`. An effect is the game's own when anything of the game plays it.

### Kinds and groups

An entry's kind comes from its components, what it does before how it looks: anything with a character is a creature, then projectiles, items and pieces. What is chopped, mined, picked or grown, or breaks into drops, is a resource. Something with only sound is a sound, particles or anything only ever played from an effect list are an effect, and the rest is other.

A mod that makes the game's own prefabs buildable (*MoreVanillaBuildPrefabs* adds a `Piece` to hundreds of them) does not change their kind. Such a mod leaves its `Piece` switched off on the prefab, which none of the game's own pieces is, so a ruin wall stays what the game made it and its facts say the mod makes it buildable. A crop planted with the cultivator is a piece, since it is built like one.

Each kind's tab groups its list by what the game itself says: items by their item type (weapons by the skill they train, armour by slot), pieces by the hammer's tabs and then by tool, creatures by faction, resources by how they are gathered, effects and sounds by the names of the effect lists that play them, projectiles by what fires them, and status effects by what gives them. Raids are one list, each named by the message the game shows when it starts. What is no prefab is kept under a key of its own kind (`EntryKeys`: a status effect's "se:" and a raid's "raid:" and its name), so favourites, recent and links never mistake it for a prefab of the same name. Items only creatures carry, which nothing a player meets gives, go last under *Carried by creatures*.

## Search and keys

The search matches names and takes terms (`kind:`, `has:`, `biome:`, `mod:`, `playedby:`, `station:` and `in:`), each value matched anywhere in what it names, spaces left out. Suggestions come from an index of every value each key can take, built on a worker thread once the catalog is read, each with how many entries it finds. The best one shows faint after the text; Tab takes it, and further presses cycle through the rest.

Enter takes a suggestion while one is offered for a word being typed. Otherwise it does the selection's main thing, as a double click does: a sound or effect plays, a projectile flies, a status effect shows on the person, a creature makes its first attack, a wearable item is put on or taken off, and a tree, log, rock or piece is felled or broken. In the clip and effect filters Enter plays the first match. Escape leaves a text box first and closes the panel the second time; Ctrl+F goes to the search.

IMGUI's text field uses up any key that types no character, so Enter is taken before the field is drawn. It leaves Tab alone, and an unused Tab moves Unity's keyboard focus on, so Tab is taken after it. The inventory opens on Tab without checking for text boxes, so while one of the panel's boxes has the keyboard Scry releases that key press first (`ZInput.ResetButtonStatus`), as the game does with a key it has handled.

## Preview copies

Every preview is a copy of the prefab made under an inactive holder, so none of its scripts wake. While it sleeps, everything is removed except what draws, animates, lights or sounds, plus a few of the game's effect scripts that need neither a network view nor a character (light flicker, random sound clips, timed destruction, fades). Scripts go first, then joints, then colliders and bodies, respecting what each component requires. Scripts added by mods are always removed, since what they need cannot be known. Only then is the copy placed and woken. It has no network view, so nothing of it is sent or saved.

Star levels, wear and the other looks the game switches by script are read from the untouched prefab and applied to the matching parts of the copy, found by their place in the hierarchy. A prefab of a hundred and fifty parts or more, such as the person, is stripped once and kept asleep, four at most, and later copies are made from that.

## Looks and gear

Where the game switches a prefab's look by script, one kind of look is offered: a plant's growth, a fire's state, a portal open or shut, a door, a chest, a windmill turning, a smelter or fermenter at work, a crafting station in use, a saddle, a pickable picked, a humanoid's gear, or an item's style. Each is set as the game's own method sets it (`Fireplace.UpdateState`, `Door.SetState`, `Smelter.UpdateState`, `ItemStyle.Setup` and so on), and those methods are among the ones the startup check fingerprints.

Gear goes on as `VisEquipment.AttachItem` puts it on: an `attach` part hangs on its bone, and an `attach_skin` part is bound to the body's bones. Armour made for a skeleton with a different number of bones is left off rather than drawn twisted. A creature that rolls its gear (`Humanoid.GiveDefaultItems`) gets a row for each list it rolls from, and one weapon in hand at a time, as its AI holds one in a fight; the items in hand set its stance as `Humanoid.SetupAnimationState` does.

An item shown worn goes on a copy of the player the same way, over whatever is kept on, and chest and leg armour paint the body with their own textures, as the game swaps them.

## Animations

The animator's switches only move it between states under conditions the game sets together, and those cannot be read at run time, so a switch pulled on its own often does nothing. Scry instead plays each clip of the controller directly on the copy through a playable graph, and destroys the graph when the clip ends.

A clip's events are answered by Scry when every event the creature's clips send is one the game's `CharacterAnimEvent` or `AnimationEffect` knows: effects play at the bone they name, props hang on their bone, parts show and hide, and a strike plays its attack. When a clip sends anything else, as some mods' clips do, its events stay off, since an event nobody hears makes Unity log an error each time.

Footsteps are not events in the game: its walk animations set a value that changes sign as a foot comes down, which a clip played on its own does not set. So while a moving clip plays, the feet named by the prefab's `FootStep` are watched, and a step sounds where one comes down, chosen from the creature's step table for the ground picked in the panel.

### What the animator plays

Which clip a trigger or switch leads to cannot be read either. So once per creature (and for the person, once per weapon stance) each is pulled on a hidden copy and watched beside a run left alone, both seeded alike since the game picks idle clips at random (`RandomIdle`). The watch takes about four milliseconds a frame, stops when another entry is chosen, and waits while the panel is closed. A watch that fails is tried once more, then given up and told in the log.

An attack is watched as the game watches one (`Humanoid.InAttack`): every clip played while in a state tagged "attack" is the attack's, in order. The same watch finds where the game's own actions lead: jumping, eating, sleeping and waking, being alerted, dying, staggering, spawning, swimming and flying. Where the watch could not see a swim or a jump at all, clips named for them are paired by their names and listed apart under *Found by name*.

### Attacks and what clips play

The game gives creatures their attacks as items and starts each by an animator trigger, so an attack plays through its clips. As its first clip starts, its start and trail effects play where the attack comes from (`Attack.GetAttackOrigin`); when a clip says it strikes, its trigger plays, and its hit plays where it lands: a swing's where a target within its attack range would stand, an area attack's in its middle, a throw's where the thrown thing lands. An attack none of whose clips says when it strikes strikes halfway through its first.

A clip one of the game's actions leads to plays the effects the game plays with that action. Some clips the game plays nothing with, yet something is heard around them, and those are listed under *Heard around it*: an alert call after waking, the idle sound with idle clips (`BaseAI.DoIdleSound`), and a hit with a stagger.

## Effects

What an effect list's chip does is one rule (`ChipPlan`): a creature's death with a ragdoll falls as the ragdoll; a list the game plays when destroying the prefab destroys the copy; a tree's hit shakes its trunk; a jump, death, alert, waking, eating or block sets the animator as the game does; an item's attack plays in the clip the person swings it in; a list the game plays with a clip plays that clip; anything else just plays.

A destroyed copy is hidden while what it leaves falls, then stands again. What falls is what the game leaves: debris thrown as the game's `Gibber` throws it, the piece's own parts when it breaks apart (`Destructible.CreateFragments`), a tree's log tipped away from you beside its stump (`TreeBase.SpawnLog`), a log's halves (`TreeLog.Destroy`), what a rock leaves in its place (`Destructible.Destroy`), and what a drop table rolls.

Falling copies keep their bodies and colliders on Scry's own layer, which meets only the ground and itself, so in the world they land on the real ground while players, creatures, arrows and the game's checks pass through them. A body is pushed out of what it overlaps no faster than the game allows (1 m/s), so a log's halves part gently. On the stage they land on an invisible ground under the model.

What a model plays is shown at the size the model is shown, so at twice the size its hits, steps and effects are twice as big. A sound or effect lists under *Plays in* the effect lists it is part of, lists alike merged into one row. A play button stays lit until everything it started has finished, and a second click stops it.

A status effect with something to see shows on a person on the stage, its start effects placed where the game places them on a character. The status effect itself is never applied.

The *Volume* slider under *Adjust* plays the selection from silent to twice the game's loudness and goes back to the game's loudness with every new selection, so a sound turned up never carries over to one that plays as soon as it is selected. An audio source cannot go above full and the game's sound script sets its volume on every play, so Scry's copies carry a small audio filter (`OnAudioFilterRead`) that scales the samples and clips them at full scale. The game's own sounds are left alone.

## What the details tell

With the catalog, Scry reads where things live and where they come from: the running spawn systems' lists, spawn points, raids, nests, the world's vegetation, every drop table on any prefab (an item tells its share of a roll in each, how many rolls and how often, as `DropTable.GetDropList` rolls it), every station that turns one item into another, what producers make, what traders sell, and which boss each altar summons with what. Items learn what they are used for: recipes, pieces, stations, fuel and what eats them. Status effects learn what gives them. Rows longer than twenty show eighteen until asked for the rest.

Where a detail follows one of the game's rules, it is worked out as the game's code does, and that code is among what the startup check fingerprints. An item's facts follow its tooltip (`ItemDrop.ItemData.GetTooltip`); creature drops follow `CharacterDrop.GenerateDropList`, which leaves the top of each range out and doubles marked drops per star; sight follows `BaseAI.CanSeeTarget`, which measures the view angle to either side of forward, so the field is twice that angle; a creature turns on what it sees nearer than its alert range, less while the target sneaks, and gives up a chase beyond its chase distance from where it spawned (`MonsterAI.UpdateAI`, `MonsterAI.UpdateTarget`); a weak spot's resistances take the place of the body's for a hit that lands there (`Character.GetDamageModifiers`); a piece's support comes from calling the game's own `WearNTear.GetMaterialProperties`, is lost per metre between pieces as `WearNTear.UpdateSupport` loses it, and rain, ash and snow wear it as `WearNTear.UpdateWear` does; a spawner checks every two seconds and spawns once its timer is past its interval, so a 10 s interval spawns every 12 s (`SpawnArea.UpdateSpawn`), picks by weight (`SpawnArea.SelectWeightedPrefab`) and rolls each further star on its own (`SpawnArea.SpawnOne`); raids are rolled every so many minutes at a chance and one is picked among those that can start (`RandEventSystem.UpdateRandomEvent`, `StartRandomEvent`), come for a player in their biomes with a base value of three within 20 m where they need a base (`GetValidEventPoints`, `CheckBase`, `EffectArea.GetBaseValue`), and bring each creature every so often at a chance while fewer than its cap are near (`SpawnSystem.UpdateSpawnList`); stars add health as `Character.SetupMaxHealth` and damage as `Attack.GetLevelDamageFactor`; upgrade kits are asked for only at the upgrade station (`CraftingStation.m_upgrader`), and comfort counts only the best piece of each group (`SE_Rested.CalculateComfortLevel`).

The game's plain damage, which no resistance lessens (`HitData.ApplyResistance`), is called "true". Numbers are the prefab's own. A world whose settings change them says so and by how much, without changing the figures.

A status effect lists what differs from a fresh one of its type, and a stats effect tells its stats as the game's tooltip does (`SE_Stats.GetTooltipString`). Which mod added a prefab comes from Jotunn's registry, read by reflection so Scry does not depend on Jotunn; otherwise it is the mod shipping the asset bundle the prefab came from.

## Locations

Where things are found in the world's locations and dungeon rooms is read only on request: with *Find in locations*, with `/scry locations`, or by itself when `ReadLocationsAutomatically` is on. The game keeps locations and rooms as soft references to asset bundles it loads while building a zone, and loading its 550 or so at once stalls the game for seconds. So one at a time is loaded in the background, read three milliseconds a frame, and released: two to three minutes in all, about two seconds of it Scry's own work. Only names are kept. Nothing shows until all is read, so a reading can be stopped at any point and leaves the world as it was.

A place goes by the name the game shows on entering it (`Teleport.m_enterText`, "Burial Chambers") or discovering it (`Location.m_discoverLabel`). Otherwise an altar goes by its boss ("The Elder's altar") and a camp by its trader ("Haldor's camp"), and anything else by its prefab name in words, without variant numbers and the makers' tags, with creatures called as the game calls them ("GoblinCamp2" is a Fuling camp). Each place says its biomes from the world's location list. A dungeon room goes by the dungeons built with its kind of room (`DungeonGenerator.m_themes` against `Room.m_theme`), so a crypt's rooms and its entrance are one place. The `in:` search looks at a place's name, not its biome.

## Links and leftovers

Besides effect lists, prefabs point at one another in many ways, and each becomes a link shown at both ends under its own heading: sounds an animation clip names, footsteps, the items a humanoid may carry, any prefab or item a field names (a projectile an attack fires, a creature's saddle, a door's key), armour sets, station upgrades, ammo, and the status effects prefabs give. A prefab the scene does not register has no entry, yet many do their work through one (a staff's projectile raising a troll); what its own fields name is linked from its user instead.

What a prefab leaves behind is paired with it: a creature's ragdoll, a tree's log and stump, a log's halves, a rock's broken version and the debris of anything destroyed. A leftover is named after its owner ("Troll · ragdoll"), takes its owner's kind so it sits beside it in the list, and links to it. One shared by many owners is named for what it is and how many leave it ("Debris of 30").

## The stage and the world

The turntable is a copy far above the world on a layer the game does not use, with its own camera rendering into a texture the panel draws and its own lights. The world's sun and main camera ignore that layer, and fog and ambient light are set for the stage only while its camera renders. The camera frames what the copy draws; a creature stands on the bottom of its capsule, as the game stands it. The stage copy is taken down while the panel is closed.

On the stage, an effect and what a model plays are heard as if beside you, and a model's own built-in sounds are muted. While a copy stands in the world, sound comes from there instead, and the stage's copies have their sound scripts switched off, since the game lets only so many of one sound play at once (`AudioMan.RequestPlaySound`).

A model shown in the world stands where you were looking, facing you. Effects play there or on you. Projectiles are flown with the prefab's gravity and a chosen speed, and a ray stops them at the first ground or building, where their hit plays. Sounds are copies on the camera, kept while they play, so long clips such as music play to the end and can be skipped through. The game's sound script picks a clip at random a frame after the copy wakes, so to play one variant, the copy's list is narrowed to that clip first. Location music is started by the game's `MusicLocation` when you come near, so Scry starts it instead, at your music volume.

## Input

While the panel is open, `TextInput.IsVisible` answers yes. The game already stops moving, looking, attacking and opening its menu while a text box is up, and frees the cursor. The camera zoom reads the wheel regardless, so `ZInput.Internal_GetMouseScrollWheel`, through which every wheel read of the game passes, answers nothing while the panel is open. The block lasts one frame past closing, so the Escape that closed the panel does not open the game's menu.

The game asks for mouse look apart from movement (`PlayerController.TakeInput`), so holding the right mouse button outside the panel answers only that question yes and captures the cursor as in play. Walking is allowed whenever none of the panel's boxes has the keyboard, and `Player.SetControls` never receives attack, block or dodge while the panel is open. `WalkWhileOpen` and `LookWithRightMouse` turn these off.

## Files and settings

The settings are in `BepInEx/config/isimp.Scry.cfg`: the key that opens the panel (`OpenKey`), its scale, whether the search has the keyboard on opening, walking and looking while open, how long before a tip shows, how many entries *Recent* keeps, how long after entering a world the catalog is read, whether the locations are read by themselves, the model's spinning, whether a sound plays when selected, and the diagnostics. Favourites and the panel's state (its place in both views, the stage's look, the list's width and which sections are folded) are kept in `BepInEx/config/isimp.Scry/`, so they go with the profile. Favourites of prefabs not in the game right now are kept.

## Game updates

Most of what Scry shows is read from the game as it runs, so new creatures, items and mods' prefabs appear without a change to Scry. Some previews and details follow the rules of the game's own methods, and a few features reach private members by name. The first time Scry reads the catalog in a world it checks all of these and writes one line to the log when all is as expected, and otherwise a line for each part that is missing (its feature is off) or changed (it may be slightly wrong).

The check covers the private members Scry reaches by name, each of its patches, the animation events the game answers, the layers falling copies land on, the effect scripts copies keep, the status effects and prefab it looks up by name, and the code of over a hundred game methods whose rules Scry follows. A method's code is compared by its shape (`IlShape`): the sequence of its steps, leaving out what they name, which the game numbers anew in every build, so only a rewritten method counts as changed. A coroutine's code is read from the state machine the compiler makes for it. The list is `Compatibility.Watched`; a mod that rewrites one of these methods shows it as changed too.

`tools/shapes` keeps the list current. From the repository's folder, with `VALHEIM_DIR` set (or `--dll` and the path to `assembly_valheim.dll`), `dotnet run --project tools/shapes -c Release -- --check` lists only the methods that changed, with their old and new shapes, and those that are gone. `--update` writes the changed shapes into `Compatibility.cs` and leaves the missing ones to be fixed by hand. Given `Type.Method` names instead, it prints each overload's shape, for adding a method to the list.

A public member that is renamed or removed fails where it is used, so every part of Scry's work is caught on its own: each part of each prefab while reading, each step of reading, each part of an entry's details, each section of the panel and each preview. Patch bodies live in methods of their own, called inside a try, because Mono compiles a whole method before running it and a missing member would otherwise fail before its try begins. A failure that means the game changed is told once as the feature it turns off, and the panel shows a *Game changed* chip whose tip names every feature that is off. Any other failure is most likely one odd prefab, usually a mod's, costs only that prefab's part, and is counted in one summary after reading.

After an update, three things tell what needs doing: building against the new assemblies fails on every public member that changed; the check's line in the log names the private members, patches and methods that changed; and the summary after reading shows anything that now fails on many prefabs. For each changed method, read its new code against the rules Scry follows from it, then run `--update`.

## Logging

With `LogPreviews` off (the default), Scry writes only the check's line, how long reading the catalog and the locations took, and anything that failed. With it on, the log also tells what each effect list and clip played and whether each part was seen or heard, what each creature's animator was seen to do and its gear, what destroyed things leave and where fallen things came to rest, any frame in which Scry's own work took 20 ms or more (split by part, with about how much memory each part allocated and where a memory cleanup of the runtime ran), and every half minute how many cleanups ran and how much of the allocation was Scry's. `/scry dump` writes every renderer of the stage copy to the log, for finding out why part of a model does not show.

## Limits

A mod prefab whose look is built by its own scripts may preview bare. Locations cannot be previewed. The stage is lit by its own lights, so materials that depend on the world's lighting can look different there. A few things stand in for what the game does with a target and are approximations: where a swing's hit lands, an attack with no strike marked in its clips, steps heard where a foot comes down, one projectile thrown for a burst, and clips paired only by their names.

## Building

The project builds with the .NET SDK against the game's assemblies. Set `VALHEIM_DIR` to the game folder if it is not a standard Steam install, then run `dotnet build`, which also copies the DLL into the local Gale profile (`-p:DeployDir=<folder>` puts it elsewhere). `dotnet test tests/Scry.Tests` runs the tests for everything in `src/Model`.
