using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using JohnFinalfantasy.Windows;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ImGuiNET;

namespace JohnFinalfantasy;

public sealed class Plugin : IDalamudPlugin
{
    private DalamudPluginInterface PluginInterface { get; init; }
    private ICommandManager CommandManager { get; init; }
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("John Finalfantasy");
    private ConfigWindow ConfigWindow { get; init; }
    private WhoWindow WhoWindow { get; init; }

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
        WhoWindow = new WhoWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(WhoWindow);

        /*
        CommandManager.AddHandler("/testcommand", new CommandInfo(TestCommand)
        {
            HelpMessage = "test debug"
        });
        */

        CommandManager.AddHandler("/jfconfig", new CommandInfo(ToggleSettings)
        {
            HelpMessage = "Toggle John Finalfantasy's config UI"
        });

        CommandManager.AddHandler("/jfwho", new CommandInfo(ToggleWho)
        {
            HelpMessage = "Toggle the who's who UI"
        });

        CommandManager.AddHandler("/jfself", new CommandInfo(ToggleSelf)
        {
            HelpMessage = "Toggle the obscurer for yourself"
        });

        CommandManager.AddHandler("/jfparty", new CommandInfo(ToggleParty)
        {
            HelpMessage = "Toggle the obscurer for your party"
        });

        CommandManager.AddHandler("/jfall", new CommandInfo(SetAll)
        {
            HelpMessage = "Turn the obscurer on/off.\n Example: \"/jfall on\" or \"jfall off\""
        });

        CommandManager.AddHandler("/updateplist", new CommandInfo(UpdateParty)
        {
            HelpMessage = "Force update the party list"
        });

        CommandManager.AddHandler("/resetplist", new CommandInfo(ResetParty)
        {
            HelpMessage = "Force reset the party list"
        });

        CommandManager.AddHandler("/updateself", new CommandInfo(UpdateSelf)
        {
            HelpMessage = "Force update yourself"
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
        CommandManager.RemoveHandler("/jfall");
        CommandManager.RemoveHandler("/jfparty");
        CommandManager.RemoveHandler("/jfself");
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

    private void ToggleSelf(string command, string args)
    {
        this.Configuration.EnableForSelf = !this.Configuration.EnableForSelf;
        this.Configuration.Save();
        this.Obscurer.ResetPartyList();
        this.Obscurer.partySize = -1;

    }

    private void ToggleParty(string command, string args)
    {
        this.Configuration.EnableForParty = !this.Configuration.EnableForParty;

        this.Configuration.Save();
        this.Obscurer.ResetPartyList();
        this.Obscurer.partySize = -1;
    }

    private void SetAll(string command, string args)
    {
        var splitArgs = args.Split(' ');
        switch (splitArgs[0])
        {
            case "on":
                this.Configuration.EnableForSelf = true;
                this.Configuration.EnableForParty = true;
                this.Obscurer.ResetPartyList();
                this.Obscurer.partySize = -1;
                break;
            case "off":
                this.Configuration.EnableForSelf = false;
                this.Configuration.EnableForParty = false;
                this.Obscurer.ResetPartyList();
                this.Obscurer.partySize = -1;
                break;
            default:
                var chatMsg = new SeString(new TextPayload("Invalid argument."));
                Service.ChatGUi.Print(new XivChatEntry { Message = chatMsg, Type = XivChatType.Echo });
                break;
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
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    internal void ToggleSettings(string command, string args) => ConfigWindow.Toggle();
    private void ToggleWho(string command, string args) => WhoWindow.ToggleWho();
}
