# ServerSync (vendored)

`ConfigSync.cs` is **not ours**. It is blaxxun's ServerSync, taken unmodified from

    https://raw.githubusercontent.com/blaxxun-boop/ServerSync/master/ConfigSync.cs

the same file Hoard, Sparring and Vigil compile in. Licence: **MIT-0**, public-domain-equivalent,
no attribution required, so it may simply be compiled in. It is the way most Valheim mods sync
config: what a world's server sets is sent to every player as they join, the server's value
winning while they are connected, the player's own kept on disk beneath it.

Scry uses it for what the world's server allows the compendium (`Settings`, section
`4 - World`): the rules everyone plays by, peeking, *Known up to*, and how far each source of the
world may show. `ModRequired` is off, so players without Scry join as before, and the entries are
locked, so only an admin changes them in game.

**Keep it unmodified.** Updating should be a wholesale replacement of that one file. What it needs
beyond itself lives in `Scry.csproj`, `GameReferences.props` and `.editorconfig`, not in the file:

- `<Nullable>annotations</Nullable>`, so its `?` annotations parse without turning null-state
  warnings on for the rest of the project.
- `System.IO.Compression`, which it uses to compress config payloads.
- `Unity.TextMeshPro`, for the one place it rewrites the connect-error screen.
- `generated_code = true` for `src/ServerSync/` in `.editorconfig`, so the code analysis rules
  Scry follows leave it alone; the standards tests compile it in without reading it.

What it does: it patches `ZNet.Awake`, `OnNewConnection`, `RPC_PeerInfo`, `Shutdown`,
`ZRpc.HandlePackage` and `ConfigEntryBase.Get/SetSerializedValue`, registers its own routed RPC per
mod, and briefly wraps the connection socket so config arrives before a client finishes joining.
It touches no files beyond BepInEx's own `configFile.Save()`, opens no sockets of its own, and makes
no web requests. Admin status is decided server-side from Valheim's admin list.
