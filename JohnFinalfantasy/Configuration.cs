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
    public bool EnableForAllText { get; set; } = false;
    public bool EnableForChat { get; set; } = false;
    public bool EnableForSelf {  get; set; } = false;
    public bool EnableForParty { get; set; } = false;
    public List<string> PartyNames { get; set; } = new List<string>();
    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
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
        pluginInterface!.SavePluginConfig(this);
    }
}
