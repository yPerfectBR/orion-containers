using Orion.PluginContracts;

namespace OrionContainers;

/// <summary>
/// Opt-in container storage/UI runtime (<see cref="Orion.Containers.Container"/>).
/// Block chests/barrels live in OrionBlockContainers.
/// </summary>
public sealed class OrionContainersPlugin : IOrionPlugin
{
    public string Id => "orion:containers";

    public Version Version { get; } = new(1, 0, 0);

    public void Load(IPluginLoadContext context) => _ = context;

    public void OnEnable(IPluginContext context) => _ = context;

    public void OnWorldInitialize(IWorldInitContext context) => _ = context;

    public void OnDisable(IPluginContext context) => _ = context;
}
