using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JohnFinalfantasy
{
    internal class CrossRealmPartyHandler : PartyHandler
    {
        public CrossRealmPartyHandler(Plugin plugin, ref bool stateChanged, ref PlayerList playerList)
            : base(plugin, ref stateChanged, ref playerList) {}

        public override unsafe bool ChangePartyNames(SeString text)
        {
            bool changed = false;
            var playerParty = InfoProxyCrossRealm.Instance()->LocalPlayerGroupIndex;
            var crParty = InfoProxyCrossRealm.Instance()->CrossRealmGroups[playerParty];
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

        public override unsafe bool UpdatePartyList(int expected = 0)
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

        public override unsafe void ResetPartyNames()
        {
            var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
            var crParty = InfoProxyCrossRealm.Instance()->CrossRealmGroups[0];
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

                playerList.UpdateEntryTextNode(contentId, hudParty->PartyMembers[i].Name);
                resetPartyHelper(contentId);
            }
        }
    }
}
