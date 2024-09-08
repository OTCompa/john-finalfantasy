using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace JohnFinalfantasy;

internal unsafe class Obscurer : IDisposable
{
    private Plugin Plugin { get; }
    internal PlayerList playerList { get; set; }

    internal bool stateChanged { get; set; } = false;
    internal int partySize { get; set; } = 0;
    private bool crossRealm { get; set; } = false;

    internal unsafe Obscurer(Plugin plugin)
    {
        this.Plugin = plugin;
        playerList = new PlayerList();

        if (this.Plugin.Configuration.EnableForSelf) UpdateSelf();
        if (this.Plugin.Configuration.EnableForParty) UpdatePartyList();

        Service.ClientState.Login += this.OnLogin;
        Service.Framework.Update += this.OnFrameworkUpdate;
        this.Plugin.Functions.AtkTextNodeSetText += this.OnAtkTextNodeSetText;
    }


    public unsafe void Dispose()
    {
        this.Plugin.Functions.AtkTextNodeSetText -= this.OnAtkTextNodeSetText;
        Service.ClientState.Login -= this.OnLogin;
        Service.Framework.Update -= this.OnFrameworkUpdate;
        if (this.stateChanged) ResetPartyList();
    }
    private void OnLogin()
    {
        if (this.Plugin.Configuration.EnableForSelf) UpdateSelf();
        if (this.Plugin.Configuration.EnableForParty) UpdatePartyList();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var isCrossRealm = InfoProxyCrossRealm.Instance()->IsInCrossRealmParty != 0;
        int partySize = 0;

        // get party list length
        if (isCrossRealm)
        {
            var crossRealmGroup = InfoProxyCrossRealm.Instance()->CrossRealmGroups[0];
            partySize = (int)crossRealmGroup.GroupMemberCount;
        }
        else
        {
            partySize = Service.PartyList.Length;
        }

        // check if state changed, update if not
        if (isCrossRealm == this.crossRealm && this.partySize == partySize) return;
        this.crossRealm = isCrossRealm;
        this.partySize = partySize;
        this.playerList.Clear();

        // ensure functions keep executing until every player is loaded in 
        if (this.Plugin.Configuration.EnableForSelf)
        {
            if (!UpdateSelf()) this.partySize = -1;
        }
        if (this.Plugin.Configuration.EnableForParty)
        {
            if (!UpdatePartyList(partySize)) this.partySize = -1;
        }
    }

    private void OnAtkTextNodeSetText(IntPtr node, IntPtr textPtr, ref SeString? overwrite)
    {
        // A catch-all for UI text. This is slow, so specialised methods should be preferred.

        var text = Util.ReadRawSeString(textPtr);

        if (text.Payloads.All(payload => payload.Type != PayloadType.RawText))
        {
            return;
        }

        var tval = text.TextValue;
        if (string.IsNullOrWhiteSpace(tval) || tval.All(c => !char.IsLetter(c)) || Util.Coords.IsMatch(tval))
        {
            return;
        }

        var changed = this.ChangeNames(text);
        if (changed) overwrite = text;
    }


    /*
     *  Change
     *  updates all non party list related text nodes
     */

    // PERFORMANCE NOTE: This potentially loops over the party list twice and the object
    //                   table once entirely. Should be avoided if being used in a
    //                   position where the player to replace is known.
    private bool ChangeNames(SeString text)
    {
        if (!this.Plugin.Configuration.Enabled)
        {
            return false;
        }

        var changed = false;
        var player = Service.ClientState.LocalPlayer;
        var playerContentId = Service.ClientState.LocalContentId;
        if (player != null && this.Plugin.Configuration.EnableForSelf)
        {
            bool temp = ChangeSelfName(player, playerContentId, text);
            if (temp) changed = temp;
        }

        if (this.Plugin.Configuration.EnableForParty)
        {
            if (InfoProxyCrossRealm.Instance()->IsInCrossRealmParty != 0)
            {
                bool temp = ChangeCrPartyNames(text);
                if (temp) changed = temp;

            }
            else
            {
                bool temp = ChangeLocalPartyNames(text);
                if (temp) changed = temp;
            }
        }
        return changed;
    }

    private bool ChangeSelfName(IPlayerCharacter player, ulong contentId, SeString text)
    {
        if (player != null)
        {
            var playerName = player.Name.ToString();
            if (playerList.GetReplacement(contentId, out var replacement))
            {
                text.ReplacePlayerName(playerName, replacement!);
                return true;
            }
        }
        return false;
    }

    private bool ChangeCrPartyNames(SeString text)
    {
        bool changed = false;
        var crParty = InfoProxyCrossRealm.Instance()->CrossRealmGroups[0];
        var numMembers = (int)crParty.GroupMemberCount;

        for (int i = 1; i < numMembers; i++)
        {
            var pMember = crParty.GroupMembers[i];
            string? name = pMember.NameString;

            if (string.IsNullOrEmpty(name)) continue;
            if (!playerList.GetReplacement(pMember.ContentId, out var replacement)) continue;

            text.ReplacePlayerName(name, replacement!);
            changed = true;
        }

        return changed;
    }

    private bool ChangeLocalPartyNames(SeString text)
    {
        bool changed = false;
        var selfContentId = Service.ClientState.LocalContentId;

        foreach (var pMember in Service.PartyList)
        {
            if ((ulong)pMember.ContentId == selfContentId) continue;
            string name = pMember.Name.ToString();
            if (!playerList.GetReplacement((ulong)pMember.ContentId, out var replacement)) continue;

            text.ReplacePlayerName(name, replacement!);
            changed = true;
        }

        return changed;
    }


    /*
     *  Update
     *  Updates current names in party list
     */

    internal bool UpdateSelf()
    {
        var player = Service.ClientState.LocalPlayer;
        var playerContentId = Service.ClientState.LocalContentId;

        if (player == null) return false;
        var originalName = player.Name.ToString();
        var replacement = this.Plugin.Configuration.PartyNames[0];
        playerList.AddEntry(playerContentId, originalName, replacement);

        Service.PluginLog.Info("Self updating: " + originalName + " -> " + replacement);
        updatePartyListHelper(playerContentId, 0);
        stateChanged = true;
        return true;
    }

    internal bool UpdatePartyList(int expected = 0)
    {
        var ret = true;
        var infoProxyCrossRealm = InfoProxyCrossRealm.Instance();

        if (infoProxyCrossRealm->IsInCrossRealmParty != 0)
        {
            ret = updateCrPartyList();
        }
        else
        {
            ret = updateLocalPartyList(expected);
        }
        stateChanged = true;
        return ret;
    }

    private bool updateCrPartyList()
    {
        var infoProxyCrossRealm = InfoProxyCrossRealm.Instance();
        var crParty = infoProxyCrossRealm->CrossRealmGroups[0];
        var numMembers = (int)crParty.GroupMemberCount;
        var ret = true;
        for (int i = 1; i < numMembers; i++)
        {
            var pMember = crParty.GroupMembers[i];
            var contentId = pMember.ContentId;
            var original = pMember.NameString;
            var replacement = this.Plugin.Configuration.PartyNames[i];
            playerList.AddEntry(contentId, original, replacement);

            if (!updatePartyListHelper(contentId, i))
            {
                ret = false;
            }
        }
        return ret;
    }

    private bool updateLocalPartyList(int expected)
    {
        bool ret = true;
        var playerContentId = Service.ClientState.LocalContentId;

        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (hudParty == null)
        {
            Service.PluginLog.Error("HUD is null");
            return false;
        }

        var numMembers = hudParty->MemberCount;
        var localParty = AgentModule.Instance()->GetAgentHUD();
        if (numMembers < expected)
        {
            ret = false;
        }

        for (int i = 1; i < numMembers; i++)
        {
            var pMember = localParty->PartyMembers[i];
            string? original = Marshal.PtrToStringUTF8((nint)pMember.Name);
            if (string.IsNullOrEmpty(original)) continue;
            var replacement = this.Plugin.Configuration.PartyNames[i];
            var contentId = pMember.ContentId;
            playerList.AddEntry(contentId, original!, replacement);
            if (ret)
            {
                Service.PluginLog.Info("Local party updating: " + original + " -> " + replacement);
                updatePartyListHelper(contentId, i);
            }
        }

        return ret;
    }

    private bool updatePartyListHelper(ulong contentId, int pos)
    {
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (hudParty == null)
        {
            Service.PluginLog.Error("HUD is null");
            return false;
        }

        var textNode = hudParty->PartyMembers[pos].Name;
        string? prefix = GetPrefix(textNode->NodeText);
        if (!string.IsNullOrEmpty(prefix))
        {
            if (!playerList.UpdateEntryTextNode(contentId, textNode))
            {
                Service.PluginLog.Error("Unable to update player's text node: " + contentId.ToString());
                return false;
            }
            if (!playerList.GetReplacement(contentId, out var replacement))
            {
                Service.PluginLog.Error("Unable to retrieve replacement name: " + contentId.ToString());
                return false;
            }
            textNode->SetText(prefix + replacement!);
        }
        else
        {
            if (textNode->NodeText.ToString() != "Viewing Cutscene")
            {
                // PvP
                if (!playerList.UpdateEntryTextNode(contentId, textNode))
                {
                    Service.PluginLog.Error("Unable to update player's text node: " + contentId.ToString());
                    return false;
                }
                if (!playerList.GetReplacement(contentId, out var replacement))
                {
                    Service.PluginLog.Error("Unable to retrieve replacement name: " + contentId.ToString());
                    return false;
                }
                textNode->SetText(replacement!);
            }
        }
        return true;
    }

    /*
     *  Reset
     */

    internal unsafe void ResetPartyList()
    {
        if (InfoProxyCrossRealm.Instance()->IsInCrossRealmParty != 0)
        {
            var crParty = InfoProxyCrossRealm.Instance()->CrossRealmGroups[0];
            ResetCrPartyNames(crParty);
        }
        else
        {
            ResetLocalPartyNames();
        }
        stateChanged = false;
    }

    private void ResetCrPartyNames(CrossRealmGroup crParty)
    {
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (hudParty == null)
        {
            Service.PluginLog.Error("HUD is null");
            return;
        }

        for (int i = 0; i < crParty.GroupMemberCount; i++)
        {
            var pMember = crParty.GroupMembers[i];
            var name = pMember.NameString;
            var contentId = pMember.ContentId;
            if (string.IsNullOrEmpty(name)) continue;
            UpdateTextNodePtr(contentId, hudParty->PartyMembers[i]);
            ResetPartyHelper(contentId);
        }
    }

    private void ResetLocalPartyNames()
    {
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (hudParty == null)
        {
            Service.PluginLog.Error("HUD is null");
            return;
        }

        if (Service.PartyList.Length == 0)
        {
            var contentId = Service.ClientState.LocalContentId;
            UpdateTextNodePtr(contentId, hudParty->PartyMembers[0]);
            ResetPartyHelper(contentId);
        }

        for (int i = 0; i < Service.PartyList.Length; i++)
        {
            var pMember = Service.PartyList[i];
            var contentId = (ulong)pMember!.ContentId;
            UpdateTextNodePtr(contentId, hudParty->PartyMembers[i]);
            ResetPartyHelper(contentId);
        }
    }

    private void ResetPartyHelper(ulong contentId)
    {
        if (!playerList.GetOriginal(contentId, out var original))
        {
            return;
        }
        if (!playerList.GetTextNode(contentId, out var textNode))
        {
            return;
        }

        string? prefix = GetPrefix(textNode->NodeText);
        if (string.IsNullOrEmpty(prefix)) textNode->SetText(original!);
        else textNode->SetText(prefix + original!);
    }

    private void UpdateTextNodePtr(ulong contentId, AddonPartyList.PartyListMemberStruct partyMember)
    {
        playerList.UpdateEntryTextNode(contentId, partyMember.Name);
    }

    /* General Helpers */

    private static MatchCollection MatchHudTextNode(Utf8String textNode)
    {
        return Util.LevelPrefix.Matches(textNode.ToString()!);
    }

    // this should fail for "Viewing Cutscene", which is intentional
    // any other case isn't tho
    internal static string? GetPrefix(Utf8String textNode)
    {
        MatchCollection matched = MatchHudTextNode(textNode);
        if (matched.Count > 0)
        {
            var matches = matched[0].Groups;
            return matches[1].Value;
        }
        Service.PluginLog.Debug("Regex failed for: " + textNode);
        return null;
    }
}
