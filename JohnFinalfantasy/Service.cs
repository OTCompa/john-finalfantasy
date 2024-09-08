using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace JohnFinalfantasy;

internal class Service
{
    [PluginService] internal static IPartyList PartyList { get; set; }
    [PluginService] internal static IPluginLog PluginLog { get; private set; }
    [PluginService] internal static IGameInteropProvider gameInteropProvider { get; set; }
    [PluginService] internal static IClientState ClientState { get; set; }
    [PluginService] internal static IFramework Framework { get; set; }
    [PluginService] internal static IGameGui GameGui { get; set; }
    [PluginService] internal static IChatGui ChatGUi { get; set; }

    internal static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
    }
}
