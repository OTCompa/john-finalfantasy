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
    private Configuration Configuration;


    private string[] firstName = new string[8];
    private string[] lastName = new string[8];

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("A Wonderful Configuration Window###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoCollapse;

        Size = new Vector2(450, 420);

        Configuration = plugin.Configuration;
        for ( int i = 0; i < 8; i++ )
        {
            var name = Configuration.PartyNames[i].Split(' ', 2);
            this.firstName[i] = name[0];
            this.lastName[i] = name[1];
        }
    }

    public void Dispose() {

    }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        var configValue = Configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Checkbox("Random Config Bool", ref configValue))
        {
            Configuration.SomePropertyToBeSavedAndWithADefault = configValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            Configuration.Save();
        }

        var movable = Configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            Configuration.IsConfigWindowMovable = movable;
            Configuration.Save();
        }

        var enable = Configuration.Enabled;
        if (ImGui.Checkbox("Enable", ref enable))
        {
            Configuration.Enabled = enable;
            Configuration.Save();
        }

        var self = Configuration.EnableForSelf;
        if (ImGui.Checkbox("Self", ref self))
        {
            Configuration.EnableForSelf = self;
            Configuration.Save();
        }

        var party = Configuration.EnableForParty;
        if (ImGui.Checkbox("Party", ref party))
        {
            Configuration.EnableForParty = party;
            Configuration.Save();
        }

        using var partyNameTable = ImRaii.Table("fps_input_settings", 2);
        for (var i = 0; i < 8; i++)
        {
            ImGui.PushID(i);
            ImGui.TableNextColumn();
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
                Configuration.PartyNames[i] = name;
            }
        }
        
    }
}
