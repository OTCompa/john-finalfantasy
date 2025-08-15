using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
namespace JohnFinalfantasy.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration configuration;


    private string[] firstName = new string[8];
    private string[] lastName = new string[8];
    private Plugin plugin;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("John Finalfantasy###With a constant ID")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(400, 410);

        configuration = plugin.Configuration;
        for ( int i = 0; i < 8; i++ )
        {
            var name = configuration.PartyNames[i].Split(' ', 2);
            this.firstName[i] = name[0];
            this.lastName[i] = name[1];
        }
    }

    public void Dispose() {

    }

    public override void Draw()
    {
        DrawToggles();
        DrawNameConfig();
    }

    private void DrawToggles() {
        ImGui.Text("Party list and nameplates");
        var self = configuration.EnableForSelf;
        if (ImGui.Checkbox("Self", ref self))
        {
            configuration.EnableForSelf = self;
            configuration.Save();
            this.plugin.Obscurer.ResetPartyList();
            this.plugin.Obscurer.partySize = -1;
        }

        ImGui.SameLine();

        var party = configuration.EnableForParty;
        if (ImGui.Checkbox("Party", ref party))
        {
            configuration.EnableForParty = party;
            configuration.Save();
            this.plugin.Obscurer.ResetPartyList();
            this.plugin.Obscurer.partySize = -1;
        }

        ImGui.Separator();
        ImGui.Text("Additional replacements");
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
        {
            ImGui.SetTooltip("Still requires the corresponding checkboxes above to be enabled.");
        }

        var enableForChatbox = configuration.EnableForChat;
        if (ImGui.Checkbox("Enable for chatbox", ref enableForChatbox))
        {
            configuration.EnableForChat = enableForChatbox;
            configuration.Save();
        }

        ImGui.SameLine();

        var enableForAllText = configuration.EnableForAllText;
        if (ImGui.Checkbox("Enable for all remaining text", ref enableForAllText))
        {
            configuration.EnableForAllText = enableForAllText;
            configuration.Save();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
        {
            ImGui.SetTooltip("All other text like character info window, social party window, cross-world/local linkshell window, etc.\nMay affect performance.");
        }

        ImGui.Separator();
    }

    private void DrawNameConfig()
    {
        using (var partyNameTable = ImRaii.Table("PartyTable", 4))
        {
            ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn("First Name", ImGuiTableColumnFlags.WidthFixed, 105);
            ImGui.TableSetupColumn("Last Name", ImGuiTableColumnFlags.WidthFixed, 105);
            ImGui.TableSetupColumn("Randomize", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            for (var i = 0; i < 8; i++)
            {
                using var id = ImRaii.PushId(i);
                ImGui.TableNextColumn();
                ImGui.Text("\t" + (i + 1).ToString() + "\t");
                ImGui.TableNextColumn();
                ImRaii.ItemWidth(100);
                ImGui.InputText("##First", ref firstName[i], 20);
                ImGui.TableNextColumn();
                ImRaii.ItemWidth(100);
                ImGui.InputText("##Last", ref lastName[i], 20);
                ImGui.TableNextColumn();
                using (var font = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if (ImGui.Button(FontAwesomeIcon.Redo.ToIconString()))
                    {
                        var ret = plugin.Obscurer.currentPartyHandler.GenerateName(i);
                        firstName[i] = ret.Item1;
                        lastName[i] = ret.Item2;
                    }
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
                {
                    ImGui.SetTooltip("Randomize name");
                }
                ImGui.TableNextRow();

            }
        }

        if (ImGui.Button("Save Names"))
        {
            for (var i = 0; i < 8; i++)
            {
                var first = firstName[i];
                var last = lastName[i];
                var name = first + " " + last;
                configuration.PartyNames[i] = name;
            }
            configuration.Save();
            if (configuration.EnableForSelf)
            {
                this.plugin.UpdateSelf("", "");
            }
            if (configuration.EnableForParty)
            {
                this.plugin.UpdateParty("", "");
            }
            this.Toggle();
        }
    }
}
