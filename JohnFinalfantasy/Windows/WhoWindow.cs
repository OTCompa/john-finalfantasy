using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace JohnFinalfantasy.Windows
{
    internal unsafe class WhoWindow : Window, IDisposable
    {
        private Plugin plugin;
        private bool collapse = true;
        public WhoWindow(Plugin plugin) : base("John Finalfantasy###JohnWho")
        {
            this.plugin = plugin;
            Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse;
            Size = new Vector2(400, 200);
        }

        public override void Draw()
        {
            if (collapse)
            {
                ImGui.GetStateStorage().SetInt(ImGui.GetID("Who's Who###whoheader"), 0);
                collapse = false;
            }
            if (ImGui.CollapsingHeader("Who's Who###whoheader"))
            {
                string list = "";
                int i = 1;
                foreach (var entry in plugin.Obscurer.replacements)
                {
                    list += i.ToString() + ". " + entry.Value + ": " + entry.Key + "\n";
                    i++;
                }
                ImGui.Text(list);
            }
        }


        public void Dispose()
        {

        }

        public void ToggleWho()
        {
            collapse = true;
            Toggle();
        }
    }
}
