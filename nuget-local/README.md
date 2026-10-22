# Local NuGet feed

`scripts/pack.sh` writes the framework `.nupkg` files into this folder. It is configured as the
`rayforge-local` package source in the repository's `NuGet.config`, so `dotnet new rayforge-game` and a
sample build can restore the exact framework version without a project reference.

The folder is kept in git with this README and `.gitkeep`; the `.nupkg` files are generated output.
