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
                using (var table = ImRaii.Table("Who'sWho##whotable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY))
                {
                    if (table)
                    {
                        ImGui.TableSetupColumn($"###whocol1", ImGuiTableColumnFlags.WidthFixed, 10);
                        ImGui.TableSetupColumn($"###whocol2", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn($"###whocol3", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableNextRow();

                        foreach (var entry in plugin.Obscurer.playerList.GetAllNames())
                        {
                            ImGui.TableSetColumnIndex(0);
                            ImGui.TextUnformatted(i.ToString());
                            ImGui.TableSetColumnIndex(1);
                            ImGui.TextUnformatted(entry.Item1);
                            ImGui.TableSetColumnIndex(2);
                            ImGui.TextUnformatted(entry.Item2);
                            ImGui.TableNextRow();
                            i++;
                        }
                    }
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
