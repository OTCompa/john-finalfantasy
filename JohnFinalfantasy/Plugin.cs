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
using static Lumina.Data.Parsing.Uld.NodeData;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualBasic;

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

    private const string CommandConfig = "/jfconfig";
    private const string CommandWho = "jfconfig";
    private const string CommandToggleSelf = "/jfself";
    private const string CommandToggleParty = "/jfparty";
    private const string CommandToggleAll = "/jfall";
    private const string DebugCommandUpdateParty = "/updateplist";
    private const string DebugCommandUpdateSelf = "/updateself";
    private const string DebugCommandResetParty = "/resetplist";

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

        CommandManager.AddHandler(CommandConfig, new CommandInfo(ToggleSettings)
        {
            HelpMessage = "Toggle John Finalfantasy's config UI"
        });

        CommandManager.AddHandler(CommandWho, new CommandInfo(ToggleWho)
        {
            HelpMessage = "Toggle the who's who UI"
        });

        CommandManager.AddHandler(CommandToggleSelf, new CommandInfo(ToggleSelf)
        {
            HelpMessage = "Toggle the obscurer for yourself"
        });

        CommandManager.AddHandler(CommandToggleParty, new CommandInfo(ToggleParty)
        {
            HelpMessage = "Toggle the obscurer for your party"
        });

        CommandManager.AddHandler(CommandToggleAll, new CommandInfo(SetAll)
        {
            HelpMessage = "Turn the obscurer on/off.\n Example: \"/jfall on\" or \"jfall off\""
        });

        CommandManager.AddHandler(DebugCommandUpdateSelf, new CommandInfo(UpdateSelf)
        {
            HelpMessage = "Force update yourself, debug command"
        });

        CommandManager.AddHandler(DebugCommandUpdateParty, new CommandInfo(UpdateParty)
        {
            HelpMessage = "Force update the party list, debug command"
        });

        CommandManager.AddHandler(DebugCommandResetParty, new CommandInfo(ResetParty)
        {
            HelpMessage = "Force reset the party list, debug command"
        });


        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

    }

    public void Dispose()
    {

        CommandManager.RemoveHandler(DebugCommandResetParty);
        CommandManager.RemoveHandler(DebugCommandUpdateParty);
        CommandManager.RemoveHandler(DebugCommandUpdateSelf);
        CommandManager.RemoveHandler(CommandToggleAll);
        CommandManager.RemoveHandler(CommandToggleParty);
        CommandManager.RemoveHandler(CommandToggleSelf);
        CommandManager.RemoveHandler(CommandWho);
        CommandManager.RemoveHandler(CommandConfig);
        //CommandManager.RemoveHandler("/testcommand");

        Obscurer.Dispose();
        Functions.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

    }

    private unsafe void TestCommand(string command, string args)
    {
        var msg = new SeString(new TextPayload("\u0002\u001a\u0002\u0002\u0003\u0002\u0012\u0002?\u0003"));
        Service.ChatGUi.Print(new XivChatEntry { Message = msg, Type = XivChatType.Echo });
        /*
        var hudParty = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        var textNode = hudParty->PartyMember[1].Name->NodeText;
        Service.PluginLog.Debug(textNode.ToString());
        var matched = this.Obscurer.MatchHudTextNode(hudParty->PartyMember[1].Name->NodeText);
        Service.PluginLog.Debug("test2");
        var matches = matched[0].Groups;
        Service.PluginLog.Debug(matches[1].Value);
        Service.PluginLog.Debug(matches[2].Value);
        Service.PluginLog.Debug("test3");
        */
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
