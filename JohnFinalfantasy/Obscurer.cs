using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace JohnFinalfantasy;

internal unsafe class Obscurer : IDisposable {
    private Plugin Plugin { get; }

    private Stopwatch UpdateTimer { get; } = new();
    private IReadOnlySet<string> Friends { get; set; }
    private Dictionary<string, string> replacements { get; set; }
    private Dictionary<string, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String> currentlySwapped { get; set; }
    internal bool stateChanged { get; set; } = false;
    private int partySize { get; set; } = 0;
    private FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm* InfoProxyCrossRealm { get; set; }
    internal unsafe Obscurer(Plugin plugin) {
        this.Plugin = plugin;
        replacements = new Dictionary<string, string>();
        currentlySwapped = new Dictionary<string, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String>();
        InfoProxyCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance();
        unsafe
        {
            var isCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.IsCrossRealmParty();
            if (isCrossRealm)
            {
                var crossRealmGroup = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
                var numMembers = (int)crossRealmGroup->GroupMemberCount;
                var pMember = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmMember*)crossRealmGroup->GroupMembers;
                //pMember++;
                for (int i = 0; i < numMembers; i++)
                {
                    string name = Marshal.PtrToStringUTF8((nint)pMember->Name);
                    var worldShort = pMember->HomeWorld;
                    var world = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>()!.GetRow((uint)worldShort)?.Name.ToString();

                    replacements[name + world] = this.Plugin.Configuration.PartyNames[i];
                    pMember++;
                }
            }
            else
            {
                var pListAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
                var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
                if (pListHud == null)
                {
                    Service.PluginLog.Debug(":(");
                    return;
                }
                else
                {
                    var pMemberHud = &pListHud->PartyMember.PartyMember0;
                    var pMemberAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
                    for (int i = 0; i < pListHud->MemberCount; i++)
                    {
                        var name = Marshal.PtrToStringUTF8((nint)pMemberAgentHud->Name);
                        string world = "Unassigned";
                        var objectId = pMemberAgentHud->ObjectId;
                        foreach (var member in Service.PartyList)
                        {
                            if (member.ObjectId == objectId)
                            {
                                world = member.World.GameData.Name.ToString();
                                break;
                            }
                        }
                        replacements[name + world] = this.Plugin.Configuration.PartyNames[i];
                        pMemberHud++;
                        pMemberAgentHud++;
                    }
                }
            }
        }
        this.UpdateTimer.Start();

        //this.Plugin.Framework.Update += this.OnFrameworkUpdate;
        this.Plugin.Functions.AtkTextNodeSetText += this.OnAtkTextNodeSetText;
        //this.Plugin.Functions.CharacterInitialise += this.OnCharacterInitialise;
        //this.Plugin.Functions.FlagSlotUpdate += this.OnFlagSlotUpdate;
        //this.Plugin.Common.Functions.NamePlates.OnUpdate += this.OnNamePlateUpdate;
        //this.Plugin.ChatGui.ChatMessage += this.OnChatMessage;
    }

    private void OnLogin()
    {
        if (!Plugin.Configuration.EnableForParty)
        {
            return;
        }
        UpdatePartyList();
    }

    public unsafe void Dispose() {
        //this.Plugin.ChatGui.ChatMessage -= this.OnChatMessage;
        //this.Plugin.Common.Functions.NamePlates.OnUpdate -= this.OnNamePlateUpdate;
        this.Plugin.Functions.AtkTextNodeSetText -= this.OnAtkTextNodeSetText;
        //this.Plugin.Functions.CharacterInitialise -= this.OnCharacterInitialise;
        //this.Plugin.Functions.FlagSlotUpdate -= this.OnFlagSlotUpdate;
        //this.Plugin.Framework.Update -= this.OnFrameworkUpdate;
    }

    /*
    private void OnFrameworkUpdate(Framework framework) {
        if (this.UpdateTimer.Elapsed < TimeSpan.FromSeconds(5) || this.IsInDuty()) {
            return;
        }

        this.Friends = this.Plugin.Common.Functions.FriendList.List
            .Select(friend => friend.Name.TextValue)
            .ToHashSet();
        this.UpdateTimer.Restart();
    }
    */

    private static readonly Regex Coords = new(@"^X: \d+. Y: \d+.(?: Z: \d+.)?$", RegexOptions.Compiled);

    private void OnAtkTextNodeSetText(IntPtr node, IntPtr textPtr, ref SeString? overwrite) {
        // A catch-all for UI text. This is slow, so specialised methods should be preferred.

        var text = Util.ReadRawSeString(textPtr);

        if (text.Payloads.All(payload => payload.Type != PayloadType.RawText)) {
            return;
        }

        var tval = text.TextValue;
        if (string.IsNullOrWhiteSpace(tval) || tval.All(c => !char.IsLetter(c)) || Coords.IsMatch(tval)) {
            return;
        }

        var changed = this.ChangeNames(text);
        if (changed) {
            overwrite = text;
        }
    }

    // PERFORMANCE NOTE: This potentially loops over the party list twice and the object
    //                   table once entirely. Should be avoided if being used in a
    //                   position where the player to replace is known.
    private bool ChangeNames(SeString text) {
        if (!this.Plugin.Configuration.Enabled) {
            return false;
        }

        var changed = false;

        var player = Service.ClientState.LocalPlayer;

        if (player != null && this.Plugin.Configuration.EnableForSelf) {
            var playerName = player.Name.ToString();
            var world = player.HomeWorld.GameData.Name.ToString();
            if (this.GetReplacement(playerName, world) is { } replacement) {
                text.ReplacePlayerName(playerName, replacement);
                changed = true;
            }
        }
        
        if (this.Plugin.Configuration.Enabled) {
            unsafe
            {
                var isCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.IsCrossRealmParty();
                if (isCrossRealm)
                {
                    var crossRealmGroup = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
                    var numMembers = (int)crossRealmGroup->GroupMemberCount;
                    var pMember = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmMember*)crossRealmGroup->GroupMembers;
                    pMember++;
                    for (int i = 0; i < numMembers - 1; i++)
                    {
                        string name = Marshal.PtrToStringUTF8((nint)pMember->Name);
                        var worldShort = pMember->HomeWorld;
                        var world = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>()!.GetRow((uint)worldShort)?.Name.ToString();
                        if (this.GetReplacement(name, world) is not { } replacement)
                        {
                            continue;
                        }
                        text.ReplacePlayerName(name, replacement);
                        pMember++;
                        changed = true;
                    }
                } else
                {
                    foreach (var pMember in Service.PartyList)
                    {
                        string name = pMember.Name.ToString();
                        string world = pMember.World.GameData.Name.ToString();
                        if (this.GetReplacement(name, world) is not { } replacement)
                        {
                            continue;
                        }

                        text.ReplacePlayerName(name, replacement);
                        changed = true;
                    }
                }
            }
        }
        
        return changed;
    }
    internal unsafe void UpdatePartyList()
    {
        var isCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.IsCrossRealmParty();
        if (isCrossRealm)
        {
            var pListInfoProxy = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
            var numMembers = (int)pListInfoProxy->GroupMemberCount;
            var pMemberIP = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmMember*)pListInfoProxy->GroupMembers;
            var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
            var pMemberHud = &pListHud->PartyMember.PartyMember0;
            for (int i = 0; i < numMembers; i++)
            {
                string name = Marshal.PtrToStringUTF8((nint)pMemberIP->Name);
                var worldShort = pMemberIP->HomeWorld;
                string world = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>()!.GetRow((uint)worldShort)?.Name.ToString();
                var partyMemberGuiString = pMemberHud->Name->NodeText.ToString().Split(' ', 2);
                replacements[name + world] = this.Plugin.Configuration.PartyNames[i];
                var textNode = pMemberHud->Name->NodeText;
                currentlySwapped[name + world] = textNode;
                textNode.SetString(partyMemberGuiString[0] + ' ' + replacements[name + world]);
                pMemberHud++;
                pMemberIP++;
            }
        }
        else
        {
            var pListAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
            var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
            if (pListHud == null)
            {
                Service.PluginLog.Debug(":(");
                return;
            }
            else
            {
                var pMemberHud = &pListHud->PartyMember.PartyMember0;
                var pMemberAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
                for (int i = 0; i < pListHud->MemberCount; i++)
                {
                    var name = Marshal.PtrToStringUTF8((nint)pMemberAgentHud->Name);
                    var partyMemberGuiString = pMemberHud->Name->NodeText.ToString().Split(' ', 2);
                    string world = "Unassigned";
                    var objectId = pMemberAgentHud->ObjectId;
                    foreach (var member in Service.PartyList)
                    {
                        if (member.ObjectId == objectId)
                        {
                            world = member.World.GameData.Name.ToString();
                            break;
                        }
                    }
                    replacements[name+world] = this.Plugin.Configuration.PartyNames[i];
                    var textNode = pMemberHud->Name->NodeText;
                    Service.PluginLog.Debug(name + " " + world);
                    currentlySwapped[name + world] = textNode;
                    textNode.SetString(partyMemberGuiString[0] + ' ' + replacements[name + world]);
                    pMemberHud++;
                    pMemberAgentHud++;
                }
                stateChanged = true;
            }
        }
    }

    internal unsafe void ResetPartyList()
    {
        // update textnodes
        var isCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.IsCrossRealmParty();
        if (isCrossRealm)
        {
            var pListInfoProxy = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
            var numMembers = (int)pListInfoProxy->GroupMemberCount;
            var pMemberIP = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmMember*)pListInfoProxy->GroupMembers;
            var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
            var pMemberHud = &pListHud->PartyMember.PartyMember0;
            for (int i = 0; i < numMembers; i++)
            {
                string name = Marshal.PtrToStringUTF8((nint)pMemberIP->Name);
                var worldShort = pMemberIP->HomeWorld;
                string world = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>()!.GetRow((uint)worldShort)?.Name.ToString();
                var textNode = pMemberHud->Name->NodeText;
                currentlySwapped[name + world] = textNode;
                pMemberHud++;
                pMemberIP++;
            }
        }
        else
        {
            var pListAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
            var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
            if (pListHud == null)
            {
                Service.PluginLog.Debug(":(");
                return;
            }
            else
            {
                var pMemberHud = &pListHud->PartyMember.PartyMember0;
                var pMemberAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
                for (int i = 0; i < pListHud->MemberCount; i++)
                {
                    var name = Marshal.PtrToStringUTF8((nint)pMemberAgentHud->Name);
                    string world = "Unassigned";
                    var objectId = pMemberAgentHud->ObjectId;
                    foreach (var member in Service.PartyList)
                    {
                        if (member.ObjectId == objectId)
                        {
                            world = member.World.GameData.Name.ToString();
                            break;
                        }
                    }
                    var textNode = pMemberHud->Name->NodeText;
                    currentlySwapped[name + world] = textNode;
                    pMemberHud++;
                    pMemberAgentHud++;
                }
                stateChanged = true;
            }
        }

        // reset party list names
        if (isCrossRealm)
        {
            var crossRealmGroup = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
            var numMembers = (int)crossRealmGroup->GroupMemberCount;
            var pMember = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmMember*)crossRealmGroup->GroupMembers;
            for (int i = 0; i < numMembers; i++)
            {
                string name = Marshal.PtrToStringUTF8((nint)pMember->Name);
                var worldShort = pMember->HomeWorld;
                string world = Service.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>()!.GetRow((uint)worldShort)?.Name.ToString();
                if (currentlySwapped.TryGetValue(name + world, out var textNode))
                {
                    Service.PluginLog.Debug("\"" + name + "\"");
                    string level = textNode.ToString().Split(' ', 2)[0];
                    textNode.SetString(level + ' ' + name);
                }
                pMember++;
            }
        }
        else
        {
            foreach (var pMember in Service.PartyList)
            {
                string name = pMember.Name.ToString();
                string world = pMember.World.GameData.Name.ToString();
                if (currentlySwapped.TryGetValue(name + world, out var textNode))
                {
                    string level = textNode.ToString().Split(' ', 2)[0];
                    textNode.SetString(level + ' ' + name);

                }
            }
        }
        stateChanged = false;
    }


    internal string? GetReplacement(string name, string world)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (this.replacements.TryGetValue(name + world, out var replacement))
        {
            return replacement;
        }
        return null;
    }


}
