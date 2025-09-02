using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JohnFinalfantasy;

internal abstract class PartyHandler
{
    protected Plugin Plugin { get; set; }
    protected bool stateChanged { get; set; }

    protected PlayerList playerList { get; set; }

    protected PartyHandler(Plugin plugin, ref bool stateChanged, ref PlayerList playerList)
    {
        Plugin = plugin;
        this.stateChanged = stateChanged;
        this.playerList = playerList;
    }

    public abstract bool ReplacePartyMemberNames(SeString text);
    public abstract bool UpdatePartyList(int expected);
    public abstract void ResetPartyList();

    public bool UpdatePartyListForSelf()
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


    protected unsafe bool updatePartyListHelper(ulong contentId, int pos)
    {
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList").Address;
        if (hudParty == null)
        {
            Service.PluginLog.Error("HUD is null");
            return false;
        }

        var textNode = hudParty->PartyMembers[pos].Name;
        Service.PluginLog.Debug(textNode->NodeText.ToString());
        playerList.UpdateEntryRawString(contentId, textNode->NodeText.ToString());
        string? prefix = Util.GetPrefix(textNode->NodeText);
        if (!string.IsNullOrEmpty(prefix))
        {
            if (!playerList.UpdateEntryTextNode(contentId, textNode))
            {
                Service.PluginLog.Error("Unable to update player's text node: " + contentId.ToString());
                return false;
            }
            if (!playerList.TryGetReplacement(contentId, out var replacement))
            {
                Service.PluginLog.Error("Unable to retrieve ReplacementName name: " + contentId.ToString());
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
                if (!playerList.TryGetReplacement(contentId, out var replacement))
                {
                    Service.PluginLog.Error("Unable to retrieve ReplacementName name: " + contentId.ToString());
                    return false;
                }
                textNode->SetText(replacement!);
            }
        }
        return true;
    }

    protected unsafe void resetPartyHelper(ulong contentId)
    {
        if (!playerList.TryGetOriginal(contentId, out var original)) return;
        if (!playerList.TryGetTextNode(contentId, out var textNode)) return;

        textNode->SetText(original!);
    }
}
