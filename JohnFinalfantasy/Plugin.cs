using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using JohnFinalfantasy.Windows;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace JohnFinalfantasy;

public sealed class Plugin : IDalamudPlugin
{
    private enum CommandAction
    {
        Off,
        On,
        Toggle
    };

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    public Configuration Configuration { get; init; }
    internal GameFunctions Functions { get; init; }
    internal Obscurer Obscurer { get; init; }

    public readonly WindowSystem WindowSystem = new("John Finalfantasy");
    private ConfigWindow ConfigWindow { get; init; }
    private WhoWindow WhoWindow { get; init; }

    private const string CommandPrimary = "/jf";
    private const string CommandConfig = "/jfconfig";
    private const string CommandWho = "/jfwho";
#if DEBUG
    private const string DebugCommandUpdateParty = "/updateplist";
    private const string DebugCommandUpdateSelf = "/updateself";
    private const string DebugCommandResetParty = "/resetplist";
    private const string DebugCommandTestString = "/jfteststring";
#endif
    public Plugin()
    {
        Service.Initialize(PluginInterface);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        this.Functions = new GameFunctions(this);
        this.Obscurer = new Obscurer(this);

        ConfigWindow = new ConfigWindow(this);
        WhoWindow = new WhoWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(WhoWindow);

        CommandManager.AddHandler(CommandPrimary, new CommandInfo(PrimaryCommandHandler)
        {
            HelpMessage = "Toggle individual John Finalfantasy settings"
        });

        CommandManager.AddHandler(CommandConfig, new CommandInfo(ToggleSettings)
        {
            HelpMessage = "Toggle John Finalfantasy's config UI"
        });

        CommandManager.AddHandler(CommandWho, new CommandInfo(ToggleWho)
        {
            HelpMessage = "Toggle the who's who UI"
        });

#if DEBUG
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

        CommandManager.AddHandler(DebugCommandTestString, new CommandInfo(TestString)
        {
            HelpMessage = "Test string"
        });
#endif
        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

    }

    public void Dispose()
    {
#if DEBUG
        CommandManager.RemoveHandler(DebugCommandResetParty);
        CommandManager.RemoveHandler(DebugCommandUpdateParty);
        CommandManager.RemoveHandler(DebugCommandUpdateSelf);
#endif
        CommandManager.RemoveHandler(CommandWho);
        CommandManager.RemoveHandler(CommandConfig);

        Obscurer.Dispose();
        Functions.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

    }

    public void PrimaryCommandHandler(string command, string args)
    {
        var splitArgs = args.Split(' ');
        var action = CommandAction.Toggle;
        if (splitArgs.Length > 1)
        {
            switch(splitArgs[1])
            {
                case "on":
                    action = CommandAction.On;
                    break;
                case "off":
                    action = CommandAction.Off;
                    break;
                default:
                    var chatMsg = new SeString(new TextPayload("Invalid argument."));
                    Service.ChatGui.Print(new XivChatEntry { Message = chatMsg, Type = XivChatType.Echo });
                    return;
            }
        }
        switch(splitArgs[0])
        {
            case "self":
                HandleSubArgs(Configuration.EnableForSelf, action, out var selfRes);
                Configuration.EnableForSelf = selfRes;
                Configuration.Save();
                break;
            case "party":
                HandleSubArgs(Configuration.EnableForParty, action, out var partyRes);
                Configuration.EnableForParty = partyRes;
                Configuration.Save();
                break;
            case "chat":
                HandleSubArgs(Configuration.EnableForChat, action, out var chatRes);
                Configuration.EnableForChat = chatRes;
                Configuration.Save();
                break;
            case "remaining":
                HandleSubArgs(Configuration.EnableForAllText, action, out var remainingRes);
                Configuration.EnableForAllText = remainingRes;
                Configuration.Save();
                break;
            default:
                Usage();
                break;
        }
    }

    private void HandleSubArgs(bool config, CommandAction action, out bool res)
    {
        switch (action)
        {
            case CommandAction.Off:
                res = false;
                break;
            case CommandAction.On:
                res = true;
                break;
            case CommandAction.Toggle:
                res = !config;
                break;
            default:
                res = false;
                break;
        }
    }

    private void Usage()
    {
        var chatMsg = new SeString(new TextPayload(
            """
            Usage:
                /jf [setting] [OPTIONAL:state]
                Available settings: self, party, chat, remaining
                Available states: [blank] (toggle), on, off
                Examples:
                Toggle self masking: /jf self
                Turn chatbox masking on: /jf chat on
            """
        ));
        Service.ChatGui.Print(new XivChatEntry { Message = chatMsg, Type = XivChatType.Echo });
    }

    internal void UpdateParty(string command, string args) => Obscurer.UpdatePartyList();
    internal void ResetParty(string command, string args) => Obscurer.ResetPartyList();
    internal void UpdateSelf(string command, string args) => Obscurer.UpdatePartyListForSelf();
    private void TestString(string command, string args)
    {
        var chatMsg = new SeString(new TextPayload("Test \x02\x1A\x02\x01\x03 String"));
        Service.ChatGui.Print(new XivChatEntry { Message = chatMsg, Type = XivChatType.Echo });
    }
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    internal void ToggleSettings(string command, string args) => ConfigWindow.Toggle();
    private void ToggleWho(string command, string args) => WhoWindow.ToggleWho();
}
