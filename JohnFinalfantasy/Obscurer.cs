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
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Dalamud.Utility;
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

    internal unsafe Obscurer(Plugin plugin)
    {
        this.Plugin = plugin;
        playerList = new PlayerList();

        localPartyHandler = new(plugin, ref stateChanged, ref playerList);
        crossRealmPartyHandler = new(plugin, ref stateChanged, ref playerList);

        Service.ClientState.Login += this.OnLogin;
        Service.Framework.Update += this.OnFrameworkUpdate;
        this.Plugin.Functions.OnAtkTextNodeSetText += this.OnAtkTextNodeSetText;
    }


    public unsafe void Dispose()
    {
        this.Plugin.Functions.OnAtkTextNodeSetText -= this.OnAtkTextNodeSetText;
        Service.ClientState.Login -= this.OnLogin;
        Service.Framework.Update -= this.OnFrameworkUpdate;
        if (this.stateChanged) ResetPartyList();
    }
    private void OnLogin()
    {
        if (this.Plugin.Configuration.EnableForSelf) localPartyHandler.UpdateSelf();
        if (this.Plugin.Configuration.EnableForParty) UpdatePartyList();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (isFirst)
        {
            if (this.Plugin.Configuration.EnableForSelf) localPartyHandler.UpdateSelf();
            if (this.Plugin.Configuration.EnableForParty) UpdatePartyList();
            isFirst = false;
        }
        var isCrossRealm = InfoProxyCrossRealm.IsCrossRealmParty();
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
            if (!localPartyHandler.UpdateSelf()) this.partySize = -1;
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
            bool temp;
            if (InfoProxyCrossRealm.IsCrossRealmParty())
                temp = crossRealmPartyHandler.ChangePartyNames(text);
            else
                temp = localPartyHandler.ChangePartyNames(text);

            if (temp) changed = temp;
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

    /*
     *  Update
     *  Updates current names in party list
     */

    internal bool UpdatePartyList(int expected = 0)
    {
        bool ret;

        if (InfoProxyCrossRealm.IsCrossRealmParty())
            ret = crossRealmPartyHandler.UpdatePartyList();
        else
            ret = localPartyHandler.UpdatePartyList(expected);

        stateChanged = true;
        return ret;
    }

    /*
     *  Reset
     */

    internal unsafe void ResetPartyList()
    {
        var numMembers = InfoProxyCrossRealm.GetPartyMemberCount();
        if (Service.PartyList.Length == 0 && numMembers != 0)
            crossRealmPartyHandler.ResetPartyNames();
        else
            localPartyHandler.ResetPartyNames();

        stateChanged = false;
    }
}
