using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Group;

namespace JohnFinalfantasy
{
    internal class LocalPartyHandler : PartyHandler
    {
        public LocalPartyHandler(Plugin plugin, ref bool stateChanged, ref PlayerList playerList)
            : base(plugin, ref stateChanged, ref playerList) {}

        public override bool ChangePartyNames(SeString text)
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

        public override unsafe bool UpdatePartyList(int expected)
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
                string? original = pMember.Name.ToString();
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

        public override unsafe void ResetPartyNames()
        {
            var agentHud = AgentModule.Instance()->GetAgentHUD();
            var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
            if (hudParty == null)
            {
                Service.PluginLog.Error("HUD is null");
                return;
            }

            for (int i = 0; i < agentHud->PartyMemberCount; i++)
            {
                var pMember = agentHud->PartyMembers[i];
                var contentId = pMember.ContentId;
                playerList.UpdateEntryTextNode(contentId, hudParty->PartyMembers[i].Name);
                resetPartyHelper(contentId);
            }
        }
    }
}
