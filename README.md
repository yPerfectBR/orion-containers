# Orion Containers

Runtime **container** implementation (slot grid, `Show`/`Close`, content sync). Opt-in.

- **Manifest id:** `orion:containers`
- **Provides:** `orion:containers`

Block chests and barrels are handled by the separate **orion:block_containers** plugin.

## Build

```bash
dotnet build OrionContainers.csproj -c Release
```

Deploy `plugin.json` and `orion.containers.dll` under `plugins/orion:containers/`.

## CI

GitHub Actions builds the plugin, runs `PackageReferenceTests`, checks out [OrionServerBE](https://github.com/OrionBedrock/OrionServerBE), and smoke-boots the server with this plugin loaded.
