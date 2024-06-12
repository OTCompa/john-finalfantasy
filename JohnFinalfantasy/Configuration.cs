using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace JohnFinalfantasy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool Enabled { get; set; } = false;
    public bool EnableForSelf {  get; set; } = false;
    public bool EnableForParty { get; set; } = false;
    public List<string> PartyNames { get; set; } = new List<string>(new string[8]);
    // the below exist just to make saving less cumbersome
    [NonSerialized]
    private DalamudPluginInterface? PluginInterface;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;
        PartyNames[0] = "John'one Final'fantasy";
        PartyNames[1] = "John'two Final'fantasy";
        PartyNames[2] = "John'three Final'fantasy";
        PartyNames[3] = "John'four Final'fantasy";
        PartyNames[4] = "John'five Final'fantasy";
        PartyNames[5] = "John'six Final'fantasy";
        PartyNames[6] = "John'seven Final'fantasy";
        PartyNames[7] = "John'eight Final'fantasy";
    }

    public void Save()
    {
        PluginInterface!.SavePluginConfig(this);
    }
}
