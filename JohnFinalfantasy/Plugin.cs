using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using JohnFinalfantasy.Windows;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace JohnFinalfantasy;

public sealed class Plugin : IDalamudPlugin
{
    private DalamudPluginInterface PluginInterface { get; init; }
    private ICommandManager CommandManager { get; init; }
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("John Finalfantasy");
    private ConfigWindow ConfigWindow { get; init; }

    private Service service { get; init; }
    internal GameFunctions Functions { get; init; }
    internal Obscurer Obscurer { get; init; }
    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] ICommandManager commandManager,
        [RequiredVersion("1.0")] ITextureProvider textureProvider)
    {

        PluginInterface = pluginInterface;
        CommandManager = commandManager;

        Service.Initialize(pluginInterface);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        this.Functions = new GameFunctions(this);
        this.Obscurer = new Obscurer(this);

        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(ConfigWindow);

        /*
        CommandManager.AddHandler("/testcommand", new CommandInfo(TestCommand)
        {
            HelpMessage = "test debug"
        });
        */

        CommandManager.AddHandler("/jfconfig", new CommandInfo(ToggleSettings) { 
            HelpMessage = "Toggle John Finalfantasy's config UI"
        });


        CommandManager.AddHandler("/updateplist", new CommandInfo(UpdateParty)
        {
            HelpMessage = "update plist"
        });
        
        CommandManager.AddHandler("/resetplist", new CommandInfo(ResetParty)
        {
            HelpMessage = "reset plist"
        });

        CommandManager.AddHandler("/updateself", new CommandInfo(UpdateSelf)
        {
            HelpMessage = "update self"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

    }

    public void Dispose()
    {

        CommandManager.RemoveHandler("/updateself");
        CommandManager.RemoveHandler("/resetplist");
        CommandManager.RemoveHandler("/updateplist");
        CommandManager.RemoveHandler("/jfconfig");
        //CommandManager.RemoveHandler("/testcommand");
        
        Obscurer.Dispose();
        Functions.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

    }

    private unsafe void TestCommand(string command, string args)
    {
        
        var pMemberAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
        foreach (var member in Service.PartyList)
        {
            var objectId = member.ObjectId;
            string name = member.Name.ToString();
            Service.PluginLog.Debug(name + " " + objectId.ToString());
            var objectIdAH = pMemberAgentHud->ObjectId;
            string nameAH = Marshal.PtrToStringUTF8((nint)pMemberAgentHud->Name);
            Service.PluginLog.Debug(nameAH + " " + objectIdAH.ToString());
        }
    }
   
    internal void UpdateParty(string command, string args)
    {
        this.Obscurer.UpdatePartyList();
    }
    
    internal void ResetParty(string command, string args)
    {
        this.Obscurer.ResetPartyList();
    }
    
    internal void UpdateSelf(string command, string args)
    {
        this.Obscurer.UpdateSelf();
    }
    internal void ToggleSettings(string command, string args)
    {
        ToggleConfigUI();
    }
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
}
