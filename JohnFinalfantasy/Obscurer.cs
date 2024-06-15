using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using System.Reflection.Metadata.Ecma335;
using System.Net.NetworkInformation;
using Dalamud.Utility;
using static Lumina.Data.Parsing.Uld.NodeData;
namespace JohnFinalfantasy;

internal unsafe class Obscurer : IDisposable {
    private Plugin Plugin { get; }
    internal Dictionary<string, string> replacements { get; set; }
    private Dictionary<string, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String> currentlySwapped { get; set; }
    internal bool stateChanged { get; set; } = false;
    internal int partySize { get; set; } = 0;
    private bool crossRealm { get; set; } = false;
    private FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm* InfoProxyCrossRealm { get; set; }
    private Regex PmemberHudRegex { get; set; }

    internal unsafe Obscurer(Plugin plugin) {
        this.Plugin = plugin;
        replacements = new Dictionary<string, string>();
        currentlySwapped = new Dictionary<string, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String>();

        PmemberHudRegex = new Regex("^([][-?]+\\s(?:\u0002\u0012\u0002Y\u0003)?\\s?)(.*)$");

        InfoProxyCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance();
        if (this.Plugin.Configuration.EnableForSelf)
        {
            UpdateSelf();
        }
        if (this.Plugin.Configuration.EnableForParty)
        {
            var isCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.IsCrossRealmParty();
            if (isCrossRealm)
            {
                var crossRealmGroup = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
                var numMembers = (int)crossRealmGroup->GroupMemberCount;
                var pMember = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmMember*)crossRealmGroup->GroupMembers;
                pMember++;
                for (int i = 1; i < numMembers; i++)
                {
                    string name = Marshal.PtrToStringUTF8((nint)pMember->Name);
                    var worldShort = pMember->HomeWorld;
                    string world = Util.GetWorld(worldShort);

                    replacements[name + " " + world] = this.Plugin.Configuration.PartyNames[i];
                    pMember++;
                }
            }
            else
            {
                var pListAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
                var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
                if (pListHud == null)
                {
                    Service.PluginLog.Error("HUD is null");
                }
                else
                {
                    var pMemberHud = &pListHud->PartyMember.PartyMember0;
                    var pMemberAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
                    pMemberHud++;
                    pMemberAgentHud++;
                    for (int i = 1; i < pListHud->MemberCount; i++)
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
                        replacements[name + " " + world] = this.Plugin.Configuration.PartyNames[i];
                        pMemberHud++;
                        pMemberAgentHud++;
                    }
                }
            }
        }

        Service.ClientState.Login += this.OnLogin;
        Service.Framework.Update += this.OnFrameworkUpdate;
        this.Plugin.Functions.AtkTextNodeSetText += this.OnAtkTextNodeSetText;

        //this.Plugin.Functions.CharacterInitialise += this.OnCharacterInitialise;
        //this.Plugin.Functions.FlagSlotUpdate += this.OnFlagSlotUpdate;
        //this.Plugin.Common.Functions.NamePlates.OnUpdate += this.OnNamePlateUpdate;
        //this.Plugin.ChatGui.ChatMessage += this.OnChatMessage;
    }


    public unsafe void Dispose() {

        Service.ClientState.Login -= this.OnLogin;
        //this.Plugin.ChatGui.ChatMessage -= this.OnChatMessage;
        //this.Plugin.Common.Functions.NamePlates.OnUpdate -= this.OnNamePlateUpdate;
        this.Plugin.Functions.AtkTextNodeSetText -= this.OnAtkTextNodeSetText;
        //this.Plugin.Functions.CharacterInitialise -= this.OnCharacterInitialise;
        //this.Plugin.Functions.FlagSlotUpdate -= this.OnFlagSlotUpdate;
        Service.Framework.Update -= this.OnFrameworkUpdate;
        if (this.stateChanged)
        {
            ResetPartyList();
        }
    }
    private void OnLogin()
    {
        if (this.Plugin.Configuration.EnableForSelf)
        {
            UpdateSelf();
        }
        if (this.Plugin.Configuration.EnableForParty)
        {
            UpdatePartyList();
        }
    }

    private void OnFrameworkUpdate(IFramework framework) {
        var isCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.IsCrossRealmParty();
        int partySize = 0;
        if (isCrossRealm)
        {
            var crossRealmGroup = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
            partySize = (int)crossRealmGroup->GroupMemberCount;
        } else
        {
            partySize = Service.PartyList.Length;
        }
        if (isCrossRealm == this.crossRealm && this.partySize == partySize)
        {
            return;
        }
        this.crossRealm = isCrossRealm;
        this.partySize = partySize;
        this.replacements.Clear();
        this.currentlySwapped.Clear();
        // ensure functions keep executing until every player is loaded in 
        if (this.Plugin.Configuration.EnableForSelf)
        {
            if (!UpdateSelf())
            {
                this.partySize = -1;
            }
        }
        if (this.Plugin.Configuration.EnableForParty)
        {
            if (!UpdatePartyList(partySize))
            {
                this.partySize = -1;
            }
        }
    }
    

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
            var world = player.HomeWorld.GameData!.Name.ToString();
            if (this.GetReplacement(playerName, world) is { } replacement) {
                text.ReplacePlayerName(playerName, replacement);
                changed = true;
            }
        }
        
        if (this.Plugin.Configuration.EnableForParty) {
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
                        string? name = Marshal.PtrToStringUTF8((nint)pMember->Name);
                        if (string.IsNullOrEmpty(name))
                        {
                            continue;
                        }
                        var worldShort = pMember->HomeWorld;
                        string world = Util.GetWorld(worldShort);
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
                        if (player == null || pMember.ObjectId == player.ObjectId)
                        {
                            continue;
                        }
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

    internal unsafe bool UpdateSelf()
    {
        var player = Service.ClientState.LocalPlayer;
        var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (pListHud == null)
        {
            return false;
        }
        var pMemberHud = &pListHud->PartyMember.PartyMember0;
        if (player == null)
        {
            return false;
        }
        string name = player.Name.ToString();
        var world = player.HomeWorld.GameData.Name.ToString();
        var partyMemberGuiString = pMemberHud->Name->NodeText.ToString().Split(' ', 2);
        replacements[name + " " + world] = this.Plugin.Configuration.PartyNames[0];
        var textNode = pMemberHud->Name->NodeText;
        Service.PluginLog.Info("Self updating: " + name + " " + world);
        currentlySwapped[name + " " + world] = textNode;
        textNode.SetString(partyMemberGuiString[0] + ' ' + replacements[name + " " + world]);
        stateChanged = true;
        return true;
    }

    private string GetPrefix(Utf8String textNode)
    {
        MatchCollection matched = MatchHudTextNode(textNode);
        if (matched.Count > 0)
        {
            var matches = matched[0].Groups;
            return matches[1].Value;
        }
        Service.PluginLog.Error("Regex failed for: " + textNode);
        return "";
    }

    private void UpdatePartyListHud(string name, string world, Utf8String textNode, int pos)
    {
        string prefix = GetPrefix(textNode);
        var indexName = Util.IndexName(name, world);
        replacements[indexName] = this.Plugin.Configuration.PartyNames[pos];
        currentlySwapped[indexName] = textNode;
        textNode.SetString(prefix + replacements[indexName]);
    }
    private void UpdateCRPartyList()
    {
        var crParty = (CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        for (int i = 0; i < crParty->GroupMemberCount; i++)
        {
            var pMember = crParty->GroupMembersSpan[i];
            string? name = Marshal.PtrToStringUTF8((nint)pMember.Name);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            var worldShort = pMember.HomeWorld;
            var world = Util.GetWorld(worldShort);

            Service.PluginLog.Info("Cross-world party updating: " + name + " " + world);
            UpdatePartyListHud(name, world, hudParty->PartyMember[i].Name->NodeText, i);
        }
    }

    // TODO: check with pets or trusts
    private bool UpdateLocalPartyList(int expected)
    {
        var ret = true;
        var localParty = Service.AgentHud;
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (hudParty == null)
        {
            Service.PluginLog.Debug("HUD is null");
            return false;
        }
        else
        {
            
            var numLoadedMembers = hudParty->MemberCount;
            if (numLoadedMembers < expected)
            {
                ret = false;
            }

            var player = Service.ClientState.LocalPlayer;
            if (player != null)
            {
                string name = player.Name.ToString();
                string world = player.HomeWorld.GameData!.Name.ToString();
                UpdatePartyListHud(name, world, hudParty->PartyMember[0].Name->NodeText, 0);
            }

            for (int i = 1; i < numLoadedMembers; i++)
            {
                var pMember = localParty->PartyMemberListSpan[i];
                string? name = Marshal.PtrToStringUTF8((nint)pMember.Name);
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }
                string world = FindObjectIdWorld(pMember.ObjectId);

                //UpdatePartyListHud(name, world, hudParty->PartyMember[i].Name->NodeText, i);
                var textNode = hudParty->PartyMember[i].Name->NodeText;
                var indexName = Util.IndexName(name, world);
                replacements[indexName] = this.Plugin.Configuration.PartyNames[i];
                if (ret)
                {
                    Service.PluginLog.Info("Local party updating: " + name + " " + world);
                    MatchCollection matched = MatchHudTextNode(textNode);
                    var matches = matched[0].Groups;
                    string prefix = matches[1].Value;
                    currentlySwapped[indexName] = textNode;
                    textNode.SetString(prefix + replacements[indexName]);
                }
            }
            
            /*
            var pListHud = hudParty;
            var pMemberHud = &pListHud->PartyMember.PartyMember0;
            var pMemberAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
            var numMembers = pListHud->MemberCount;

            if (numMembers < expected)
            {
                ret = false;
            }
            pMemberHud++;
            pMemberAgentHud++;
            for (int i = 1; i < pListHud->MemberCount; i++)
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
                replacements[name + " " + world] = this.Plugin.Configuration.PartyNames[i];
                if (ret)
                {
                    var textNode = pMemberHud->Name->NodeText;
                    Service.PluginLog.Info("Local party updating: " + name + " " + world);
                    currentlySwapped[name + " " + world] = textNode;
                    textNode.SetString(partyMemberGuiString[0] + ' ' + replacements[name + " " + world]);
                }
                //pMemberHud->Name->TextFlags2 = (byte)TextFlags2.Ellipsis;
                pMemberHud++;
                pMemberAgentHud++;
            }
            */
        }
        return ret;
    }

    
    /*
    internal unsafe bool UpdatePartyList(int expected = 0)
    {
        var ret = true;
        var isCrossRealm = InfoProxyCrossRealm->IsInCrossRealmParty != 0;
        if (isCrossRealm)
        {
            UpdateCRPartyList();
        }
        else
        {
            //ret = UpdateLocalPartyList(expected);
            
            var pListAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
            var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
            if (pListHud == null)
            {
                Service.PluginLog.Debug("HUD is null");
                return false;
            }
            else
            {
                var pMemberHud = &pListHud->PartyMember.PartyMember0;
                var pMemberAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
                var numMembers = pListHud->MemberCount;

                if (numMembers < expected)
                {
                    ret = false;
                }
                pMemberHud++;
                pMemberAgentHud++;
                for (int i = 1; i < pListHud->MemberCount; i++)
                {
                    var name = Marshal.PtrToStringUTF8((nint)pMemberAgentHud->Name);
                    var textNode = pMemberHud->Name->NodeText;
                    string world = "Unassigned";
                    var objectId = pMemberAgentHud->ObjectId;
                    foreach (var member in Service.PartyList)
                    {
                        if (member.ObjectId == objectId)
                        {
                            world = member.World.GameData!.Name.ToString();
                            break;
                        }
                    }

                    var indexName = Util.IndexName(name, world);
                    replacements[indexName] = this.Plugin.Configuration.PartyNames[i];
                    if (ret)
                    {
                        Service.PluginLog.Info("Local party updating: " + name + " " + world);
                        MatchCollection matched = MatchHudTextNode(textNode);
                        var matches = matched[0].Groups;
                        string prefix = matches[1].Value;
                        currentlySwapped[indexName] = textNode;
                        textNode.SetString(prefix + replacements[indexName]);
                    }
                    //pMemberHud->Name->TextFlags2 = (byte)TextFlags2.Ellipsis;
                    pMemberHud++;
                    pMemberAgentHud++;
                }
            
            }
            ////
        }
        stateChanged = true;
        return ret;
    }
    */

    
    internal unsafe bool UpdatePartyList(int expected = 0)
    {
        var ret = true;
        var isCrossRealm = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.IsCrossRealmParty();
        if (isCrossRealm)
        {
            UpdateCRPartyList();
        }
        else
        {
            var pListAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
            var pListHud = (FFXIVClientStructs.FFXIV.Client.UI.AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
            if (pListHud == null)
            {
                Service.PluginLog.Debug("HUD is null");
                return false;
            }
            else
            {
                var pMemberHud = &pListHud->PartyMember.PartyMember0;
                var pMemberAgentHud = (HudPartyMember*)Service.AgentHud->PartyMemberList;
                var numMembers = pListHud->MemberCount;

                if (numMembers < expected)
                {
                    ret = false;
                }
                pMemberHud++;
                pMemberAgentHud++;
                for (int i = 1; i < pListHud->MemberCount; i++)
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
                    replacements[name + " " + world] = this.Plugin.Configuration.PartyNames[i];
                    if (ret)
                    {
                        var textNode = pMemberHud->Name->NodeText;
                        Service.PluginLog.Info("Local party updating: " + name + " " + world);
                        currentlySwapped[name + " " + world] = textNode;
                        textNode.SetString(partyMemberGuiString[0] + ' ' + replacements[name + " " + world]);
                    }
                    //pMemberHud->Name->TextFlags2 = (byte)TextFlags2.Ellipsis;
                    pMemberHud++;
                    pMemberAgentHud++;
                }
            }
        }
        stateChanged = true;
        return ret;
    }
    

    internal unsafe void ResetPartyList()
    {
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (InfoProxyCrossRealm->IsInCrossRealmParty != 0)
        {
            var crParty = (CrossRealmGroup*)InfoProxyCrossRealm->CrossRealmGroupArray;
            UpdateCRTextNodePtrs(*crParty, *hudParty);
            ResetCRPartyNames(*crParty);
        }
        else
        {
            UpdateLocalTextNodePtrs(*Service.AgentHud, *hudParty);
            ResetLocalPartyNames();
        }
        stateChanged = false;
    }

    private void ResetCRPartyNames(CrossRealmGroup crParty)
    {
        for (int i = 0; i < crParty.GroupMemberCount; i++)
        {
            var pMember = crParty.GroupMembersSpan[i];
            var name = Marshal.PtrToStringUTF8((nint)pMember.Name);
            if (name == null)
            {
                continue;
            }
            var worldShort = pMember.HomeWorld;
            string indexName = Util.IndexName(name, Util.GetWorld(worldShort));
            if (currentlySwapped.TryGetValue(indexName, out var textNode))
            {
                string prefix = GetPrefix(textNode);
                textNode.SetString(prefix + name);
            }
        }
    }

    private void UpdateCRTextNodePtrs(CrossRealmGroup crParty, AddonPartyList hudParty)
    {
        for (var i = 0; i < crParty.GroupMemberCount; i++)
        {
            var pMember = crParty.GroupMembersSpan[i];
            string? name = Marshal.PtrToStringUTF8((nint)pMember.Name);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            var worldShort = pMember.HomeWorld;

            UpdateTextNodePtr(name, Util.GetWorld(worldShort), hudParty.PartyMember[i]);
        }
    }

    private void ResetLocalPartyNames()
    {
        if (Service.PartyList.Length == 0)
        {
            var player = Service.ClientState.LocalPlayer;
            if (player != null)
            {
                string name = player.Name.ToString();
                string world = player.HomeWorld.GameData!.Name.ToString();
                string indexName = Util.IndexName(name, world);
                Service.PluginLog.Info("Local party resetting self: " + name + " " + world);
                if (currentlySwapped.TryGetValue(indexName, out var textNode))
                {
                    string prefix = textNode.ToString().Split(" ")[0] + " ";
                    textNode.SetString(prefix + name);
                }
            }
        }
        foreach (var pMember in Service.PartyList)
        {
            string name = pMember.Name.ToString();
            string world = pMember.World.GameData!.Name.ToString();
            Service.PluginLog.Info("Local party resetting: " + name + " " + world);
            if (currentlySwapped.TryGetValue(name + " " + world, out var textNode))
            {
                string level = textNode.ToString().Split(' ', 2)[0];
                textNode.SetString(level + ' ' + name);

            }
        }
    }

    private void UpdateLocalTextNodePtrs(AgentHUD localParty, AddonPartyList hudParty)
    {
        // Update text ptr for self
        var player = Service.ClientState.LocalPlayer;
        if (player != null)
        {
            string name = player.Name.ToString();
            string world = player.HomeWorld.GameData!.Name.ToString();
            UpdateTextNodePtr(name, world, hudParty.PartyMember[0]);
        }

        // Update text ptr for party
        for (int i = 1; i < localParty.PartyMemberCount; i++)
        {
            var pMember = localParty.PartyMemberListSpan[i];
            string? name = Marshal.PtrToStringUTF8((nint)pMember.Name);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            string world = FindObjectIdWorld(pMember.ObjectId);
            UpdateTextNodePtr(name, world, hudParty.PartyMember[i]);
        }
    }

    private string FindObjectIdWorld(uint objectId)
    {
        // Dependent on iPartyList so does not work for solo local parties
        foreach (var member in Service.PartyList)
        {
            // TODO: REMOVE
            Service.PluginLog.Debug(member.ObjectId.ToString() + " " + objectId.ToString());
            if (member.ObjectId == objectId)
            {
                var temp = member.World.GameData!.Name.ToString();
                if (!string.IsNullOrEmpty(temp))
                {
                    return temp;
                }
            }
        }
        Service.PluginLog.Debug("erm");
        return "Unassigned";
    }

    private void UpdateTextNodePtr(string name, string world, AddonPartyList.PartyListMemberStruct hudElement)
    {
        var index = Util.IndexName(name, world);
        currentlySwapped[index] = hudElement.Name->NodeText;
    }

    internal string? GetReplacement(string name, string world)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        string indexName = Util.IndexName(name, world);
        if (this.replacements.TryGetValue(indexName, out var replacement))
        {
            return replacement;
        }
        return null;
    }

    private MatchCollection MatchHudTextNode(Utf8String textNode)
    {
        return this.PmemberHudRegex.Matches(textNode.ToString()!);
    }
}
