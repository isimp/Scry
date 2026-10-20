# Building

How to build Scry and its self-test, and where the build puts them.

See also: [releasing](../releasing.md), [game updates](game-updates.md); all notes in [the index](../index.md).

## Building

The project builds with the .NET SDK against the game's assemblies. Set `VALHEIM_DIR` to the game folder if it is not a standard Steam install, then run `dotnet build`, which also copies the DLL into the local Gale profile (`-p:DeployDir=<folder>` puts it elsewhere); `dotnet build selftest` builds and copies the self-test beside it, building Scry first. Both build against the same references (`GameReferences.props`). `dotnet test tests/Scry.Tests` runs the tests for everything in `src/Model`.
