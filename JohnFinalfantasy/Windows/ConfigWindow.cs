using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

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

        Size = new Vector2(400, 365);

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
        var enable = configuration.Enabled;
        if (ImGui.Checkbox("Enable for all text", ref enable))
        {
            configuration.Enabled = enable;
            configuration.Save();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.None))
        {
            ImGui.SetTooltip("All text like chatbox, social party window, cross-world/local linkshell window, etc.\nMay affect performance.");
        }

        var self = configuration.EnableForSelf;
        if (ImGui.Checkbox("Self", ref self))
        {
            configuration.EnableForSelf = self;
            configuration.Save();
            this.plugin.Obscurer.ResetPartyList();
            this.plugin.Obscurer.partySize = -1;
        }

        var party = configuration.EnableForParty;
        if (ImGui.Checkbox("Party", ref party))
        {
            configuration.EnableForParty = party;
            configuration.Save();
            this.plugin.Obscurer.ResetPartyList();
            this.plugin.Obscurer.partySize = -1;
        }
        ImGui.Separator();
        using var partyNameTable = ImRaii.Table("PartyTable", 2);
        for (var i = 0; i < 8; i++)
        {
            ImGui.PushID(i);
            ImGui.TableNextColumn();
            ImGui.Text("\t" + (i + 1).ToString() + "\t");
            ImGui.SameLine();
            ImGui.Text("First");
            ImGui.SameLine();
            ImGui.InputText("##First", ref firstName[i], 20);
            ImGui.SameLine();
            ImGui.TableNextColumn();
            ImGui.Text("Last");
            ImGui.SameLine();
            ImGui.InputText("##Last", ref lastName[i], 20);
            ImGui.TableNextRow();
        }

        ImGui.TableNextColumn();
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
