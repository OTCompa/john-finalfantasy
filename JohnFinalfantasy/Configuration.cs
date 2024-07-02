using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JohnFinalfantasy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool Enabled { get; set; } = false;
    public bool EnableForSelf {  get; set; } = false;
    public bool EnableForParty { get; set; } = false;
    public List<string> PartyNames { get; set; } = new List<string>();
    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private IDalamudPluginInterface? PluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
        if (PartyNames.Count == 0)
        {
            string[] nums = ["one", "two", "three", "four", "five", "six", "seven", "eight"];
            for (int i = 0; i < 8; i++)
            {
                PartyNames.Add("John'" + nums[i] + " Ffxiv");
            }
        }
    }

    public void Save()
    {
        PluginInterface!.SavePluginConfig(this);
    }
}
