using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
namespace JohnFinalfantasy;

internal unsafe class Obscurer : IDisposable
{
    private Plugin Plugin { get; }
    internal PlayerList playerList;

    internal bool stateChanged = false;
    internal int partySize { get; set; } = 0;
    private bool crossRealm { get; set; } = false;
    internal bool isFirst { get; set; } = true;

    private LocalPartyHandler localPartyHandler { get; set; }
    private CrossRealmPartyHandler crossRealmPartyHandler { get; set; }
    private PartyHandler currentPartyHandler { get; set; }

    internal Obscurer(Plugin plugin)
    {
        this.Plugin = plugin;
        playerList = new PlayerList();

        localPartyHandler = new(plugin, ref stateChanged, ref playerList);
        crossRealmPartyHandler = new(plugin, ref stateChanged, ref playerList);
        currentPartyHandler = localPartyHandler;

        Service.ClientState.Login += this.OnLogin;
        Service.Framework.Update += this.OnFrameworkUpdate;
        Service.NamePlateGui.OnDataUpdate += NamePlateGuiOnOnDataUpdate;
        Service.ChatGui.ChatMessage += OnChatMessage;
        this.Plugin.Functions.OnAtkTextNodeSetText += this.OnAtkTextNodeSetText;
    }

    public void Dispose()
    {
        ResetPartyList();
        this.Plugin.Functions.OnAtkTextNodeSetText -= this.OnAtkTextNodeSetText;
        Service.ClientState.Login -= this.OnLogin;
        Service.Framework.Update -= this.OnFrameworkUpdate;
        Service.NamePlateGui.OnDataUpdate -= NamePlateGuiOnOnDataUpdate;
        Service.ChatGui.ChatMessage -= OnChatMessage;
    }

    private void OnLogin()
    {
        if (this.Plugin.Configuration.EnableForSelf) currentPartyHandler.UpdatePartyListForSelf();
        if (this.Plugin.Configuration.EnableForParty) UpdatePartyList();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (isFirst)
        {
            if (this.Plugin.Configuration.EnableForSelf) currentPartyHandler.UpdatePartyListForSelf();
            if (this.Plugin.Configuration.EnableForParty) UpdatePartyList();
            isFirst = false;
        }
        var isCrossRealm = InfoProxyCrossRealm.IsCrossRealmParty();
        var partySize = 0;

        // get party list length
        if (isCrossRealm)
        {
            var crossRealmGroup = Util.GetLocalPlayerCrossRealmGroup();
            partySize = crossRealmGroup.GroupMemberCount;
            currentPartyHandler = crossRealmPartyHandler;
        }
        else
        {
            partySize = Service.PartyList.Length;
            currentPartyHandler = localPartyHandler;
        }

        // check if state changed, update if not
        if (isCrossRealm == this.crossRealm && this.partySize == partySize) return;
        this.crossRealm = isCrossRealm;
        this.partySize = partySize;
        this.playerList.Clear();

        // ensure functions keep executing until every player is loaded in 
        if (this.Plugin.Configuration.EnableForSelf)
        {
            if (!currentPartyHandler.UpdatePartyListForSelf()) this.partySize = -1;
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

        if (ReplaceNamesInSeString(text)) overwrite = text;
    }

    private void NamePlateGuiOnOnDataUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!this.Plugin.Configuration.EnableForSelf && !this.Plugin.Configuration.EnableForParty) return;
        foreach (var handler in handlers)
        {
            if (handler.NamePlateKind == NamePlateKind.PlayerCharacter)
            {
                var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)handler.PlayerCharacter?.Address;
                if (chara == null) return;
                var contentId = chara->ContentId;
                if (playerList.GetReplacement(contentId, out var replacement))
                {
                    if (replacement.IsNullOrEmpty()) return;
                    handler.Name = replacement;
                }
            }
        }
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!this.Plugin.Configuration.EnableForChat) return;

        var player = Service.ObjectTable.LocalPlayer;
        var playerContentId = Service.PlayerState.ContentId;

        if (player != null && this.Plugin.Configuration.EnableForSelf)
        {
             ReplacePlayerName(player, playerContentId, sender);
             ReplacePlayerName(player, playerContentId, message);
        }

        if (this.Plugin.Configuration.EnableForParty)
        {
            currentPartyHandler.ReplacePartyMemberNames(sender);
            currentPartyHandler.ReplacePartyMemberNames(message);
        }
    }

    // PERFORMANCE NOTE: This potentially loops over the party list twice and the object
    //                   table once entirely. Should be avoided if being used in a
    //                   position where the player to replace is known.
    private bool ReplaceNamesInSeString(SeString text)
    {
        if (!this.Plugin.Configuration.EnableForAllText) return false;

        var changed = false;
        var player = Service.ObjectTable.LocalPlayer;
        var playerContentId = Service.PlayerState.ContentId;

        if (player != null && this.Plugin.Configuration.EnableForSelf)
        {
            changed |= ReplacePlayerName(player, playerContentId, text);
        }

        if (this.Plugin.Configuration.EnableForParty)
        {
            // TODO: update this for rearranged localplayer on party list
            changed |= currentPartyHandler.ReplacePartyMemberNames(text);
        }

        return changed;
    }

    private bool ReplacePlayerName(IPlayerCharacter player, ulong contentId, SeString text)
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

    internal bool UpdatePartyList(int expected = 0)
    {
        var ret = currentPartyHandler.UpdatePartyList(expected);
        stateChanged = true;
        return ret;
    }

    internal void ResetPartyList()
    {
        var numMembers = InfoProxyCrossRealm.GetPartyMemberCount();
        if (Service.PartyList.Length == 0 && numMembers != 0)
            crossRealmPartyHandler.ResetPartyList();
        else
            localPartyHandler.ResetPartyList();

        stateChanged = false;
    }

    // TODO: update this for rearranged localplayer on party list
    public void UpdatePartyListForSelf() => currentPartyHandler.UpdatePartyListForSelf();
}
