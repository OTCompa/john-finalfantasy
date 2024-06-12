using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
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
    private MainWindow MainWindow { get; init; }

    private Service service { get; init; }
    internal GameFunctions Functions { get; init; }
    private Obscurer Obscurer { get; init; }
    public Plugin(
        [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("1.0")] ICommandManager commandManager,
        [RequiredVersion("1.0")] ITextureProvider textureProvider)
    {

        PluginInterface = pluginInterface;
        CommandManager = commandManager;

        Service.Initialize(pluginInterface);
        //this.NameRepository = new NameRepository(this);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        this.Functions = new GameFunctions(this);
        this.Obscurer = new Obscurer(this);
        // you might normally want to embed resources and load them from the manifest stream
        var file = new FileInfo(Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png"));

        // ITextureProvider takes care of the image caching and dispose
        var goatImage = textureProvider.GetTextureFromFile(file);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImage);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler("/testcommand", new CommandInfo(TestCommand)
        {
            HelpMessage = "test debug"
        });

        CommandManager.AddHandler("/updateplist", new CommandInfo(UpdateParty)
        {
            HelpMessage = "update plist"
        });
        
        CommandManager.AddHandler("/resetplist", new CommandInfo(ResetParty)
        {
            HelpMessage = "reset plist"
        });
        
        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {

        CommandManager.RemoveHandler("/resetplist");
        CommandManager.RemoveHandler("/updateplist");
        CommandManager.RemoveHandler("/testcommand");

        if (Obscurer.stateChanged)
        {
            Obscurer.ResetPartyList();
        }
        
        Obscurer.Dispose();
        Functions.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();

    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
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
   
    private void UpdateParty(string command, string args)
    {
        this.Obscurer.UpdatePartyList();
    }
    
    private void ResetParty(string command, string args)
    {
        this.Obscurer.ResetPartyList();
    }
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}