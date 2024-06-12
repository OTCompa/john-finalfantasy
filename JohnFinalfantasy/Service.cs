using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace JohnFinalfantasy;

internal class Service
{
    [PluginService] internal static IPartyList PartyList { get; set; }
    [PluginService] internal static IPluginLog PluginLog { get; private set; }
    [PluginService] internal static IGameInteropProvider gameInteropProvider { get; set; }
    [PluginService] internal static IDataManager DataManager { get; set; }
    [PluginService] internal static IClientState ClientState { get; set; }
    [PluginService] internal static IFramework Framework { get; set; }
    [PluginService] internal static IGameGui GameGui { get; set; }
    internal unsafe static AgentHUD* AgentHud => AgentModule.Instance()->GetAgentHUD();

    internal static void Initialize(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
    }
}
