using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using static System.Net.Mime.MediaTypeNames;
using System.Threading.Channels;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Numerics;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace JohnFinalfantasy;

internal unsafe class Obscurer : IDisposable {
    private Plugin Plugin { get; }
    internal Dictionary<string, string> replacements { get; set; }
    private Dictionary<string, AtkTextNode> currentlySwapped { get; set; }
    internal bool stateChanged { get; set; } = false;
    internal int partySize { get; set; } = 0;
    private bool crossRealm { get; set; } = false;
    private InfoProxyCrossRealm InfoProxyCrossRealm { get; set; }
    private InfoProxyPartyMember* infoProxyPartyMember { get; set; }
    private InfoProxyCommonList CommonListPartyList { get; set; }
    private Regex pMemberPrefixRegex { get; set; }
    private static readonly Regex Coords = new(@"^X: \d+. Y: \d+.(?: Z: \d+.)?$", RegexOptions.Compiled);

    internal unsafe Obscurer(Plugin plugin) {
        this.Plugin = plugin;
        replacements = new Dictionary<string, string>();
        currentlySwapped = new Dictionary<string, AtkTextNode>();
        pMemberPrefixRegex = new Regex("^((?:\u0002\u001a\u0002\u0002\u0003\u0002\u0012\u0002\\?\u0003)?[][-?]+\\s(?:\u0002\u0012\u0002Y\u0003)?\\s?)(.*)$");
        InfoProxyCrossRealm = *FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance();
        infoProxyPartyMember = InfoProxyPartyMember.Instance();
        CommonListPartyList = infoProxyPartyMember->InfoProxyCommonList;

        if (this.Plugin.Configuration.EnableForSelf)
        {
            UpdateSelf();
        }
        if (this.Plugin.Configuration.EnableForParty)
        {
            UpdatePartyList();
        }

        Service.ClientState.Login += this.OnLogin;
        Service.Framework.Update += this.OnFrameworkUpdate;
        this.Plugin.Functions.AtkTextNodeSetText += this.OnAtkTextNodeSetText;
    }


    public unsafe void Dispose()
    {
        this.Plugin.Functions.AtkTextNodeSetText -= this.OnAtkTextNodeSetText;
        Service.ClientState.Login -= this.OnLogin;
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
        var isCrossRealm = InfoProxyCrossRealm.IsInCrossRealmParty != 0;
        int partySize = 0;
        if (InfoProxyCrossRealm.IsInCrossRealmParty != 0)
        {
            var crossRealmGroup = (FFXIVClientStructs.FFXIV.Client.UI.Info.CrossRealmGroup)InfoProxyCrossRealm.CrossRealmGroups[0];
            partySize = (int)crossRealmGroup.GroupMemberCount;
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


    /*
     *  Change
     *  updates all non party list related text nodes
     */

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
            bool temp = ChangeSelfName(player, text);
            if (temp) { changed = temp; }
        }
        
        if (this.Plugin.Configuration.EnableForParty) {
            if (InfoProxyCrossRealm.IsInCrossRealmParty != 0)
            {
                bool temp = ChangeCRPartyNames(text);
                if (temp) { changed = temp; }

            } else
            {
                bool temp = ChangeLocalPartyNames(player, text);
                if (temp) { changed = temp; }
            }
        }
        return changed;
    }

    private bool ChangeSelfName(IPlayerCharacter player, SeString text)
    {
        if (player != null)
        {
            var playerName = player.Name.ToString();
            var world = player.HomeWorld.GameData!.Name.ToString();
            if (this.GetReplacement(playerName, world) is { } replacement)
            {
                text.ReplacePlayerName(playerName, replacement);
                return true;
            }
        }
        return false;
    }

    private bool ChangeCRPartyNames(SeString text)
    {
        var crParty = (CrossRealmGroup)InfoProxyCrossRealm.CrossRealmGroups[0];
        var numMembers = (int)crParty.GroupMemberCount;
        bool changed = false;
        for (int i = 1; i < numMembers; i++)
        {
            var pMember = crParty.GroupMembers[i];
            string? name = pMember.NameString;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            var worldShort = pMember.HomeWorld;
            string world = Util.GetWorld(worldShort);
            if (this.GetReplacement(name, world) is not { } replacement)
            {
                continue;
            }
            text.ReplacePlayerName(name, replacement);
            changed = true;
        }
        return changed;
    }

    private bool ChangeLocalPartyNames(IPlayerCharacter? player, SeString text)
    {
        bool changed = false;
        foreach (var pMember in Service.PartyList)
        {
            if (player == null || pMember.ObjectId == player.GameObjectId)
            {
                continue;
            }
            string name = pMember.Name.ToString();
            string world = pMember.World.GameData!.Name.ToString();
            if (this.GetReplacement(name, world) is not { } replacement)
            {
                continue;
            }

            text.ReplacePlayerName(name, replacement);
            changed = true;
        }
        return changed;
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


    /*
     *  Update
     */

    internal unsafe bool UpdateSelf()
    {
        var player = Service.ClientState.LocalPlayer;
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (hudParty == null)
        {
            Service.PluginLog.Error("HUD is null");
            return false;
        }
        var pMemberHud = hudParty->PartyMembers[0];
        if (player == null)
        {
            return false;
        }
        string name = player.Name.ToString();
        var world = player.HomeWorld.GameData!.Name.ToString();
        string indexName = Util.IndexName(name, world);
        replacements[indexName] = this.Plugin.Configuration.PartyNames[0];
        Service.PluginLog.Info("Self updating: " + name + " " + world);
        UpdatePartyListHelper(indexName, *hudParty, 0);
        stateChanged = true;
        return true;
    }

    internal unsafe bool UpdatePartyList(int expected = 0)
    {
        var ret = true;
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (hudParty == null)
        {
            Service.PluginLog.Error("HUD is null");
            return false;
        }
        if (InfoProxyCrossRealm.IsInCrossRealmParty != 0)
        {
            UpdateCrPartyList(*hudParty);
        }
        else
        {
            ret = UpdateLocalPartyList(*hudParty, expected);
        }
        stateChanged = true;
        return ret;
    }

    private void UpdateCrPartyList(AddonPartyList hudParty)
    {
        var crParty = (CrossRealmGroup)InfoProxyCrossRealm.CrossRealmGroups[0];
        var numMembers = (int)crParty.GroupMemberCount;
        for (int i = 1; i < numMembers; i++)
        {
            var pMember = crParty.GroupMembers[i];
            string? name = pMember.NameString;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            var worldShort = pMember.HomeWorld;
            string world = Util.GetWorld(worldShort);
            string indexName = Util.IndexName(name, world);

            Service.PluginLog.Info("Cross-world party updating: " + name + " " + world);
            replacements[indexName] = this.Plugin.Configuration.PartyNames[i];

            UpdatePartyListHelper(indexName, hudParty, i);
        }
    }

    private bool UpdateLocalPartyList(AddonPartyList hudParty, int expected)
    {
        bool ret = true;
        var player = Service.ClientState.LocalPlayer;
        var numMembers = hudParty.MemberCount;
        var localParty = Service.AgentHud;
        if (numMembers < expected)
        {
            ret = false;
        }
        for (int i = 1; i < numMembers; i++)
        {
            var pMember = localParty->PartyMembers[i];
            string? name = Marshal.PtrToStringUTF8((nint)pMember.Name);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            string world = GetWorldByContentId(pMember.ContentId);
            var indexName = Util.IndexName(name, world);
            replacements[indexName] = this.Plugin.Configuration.PartyNames[i];
            if (ret)
            {
                Service.PluginLog.Info("Local party updating: " + name + " " + world);

                UpdatePartyListHelper(indexName, hudParty, i);
            }
        }
        return ret;
    }

    private void UpdatePartyListHelper(string indexName, AddonPartyList hudParty, int pos)
    {
        var textNode = hudParty.PartyMembers[pos].Name;
        var name = (int)hudParty.PartyMembers[pos].Name->TextFlags;
        hudParty.PartyMembers[pos].Name->TextFlags |= (int)FFXIVClientStructs.FFXIV.Component.GUI.TextFlags.Edge;
        hudParty.PartyMembers[pos].Name->TextFlags2 |= (int)FFXIVClientStructs.FFXIV.Component.GUI.TextFlags2.Ellipsis;

        string? prefix = GetPrefix(textNode->NodeText);
        if (!string.IsNullOrEmpty(prefix))
        {
            currentlySwapped[indexName] = *textNode;
            
            textNode->SetText(prefix + replacements[indexName]);
        }
    }


    /*
     *  Reset
     */

    internal unsafe void ResetPartyList()
    {
        var hudParty = (AddonPartyList*)Service.GameGui.GetAddonByName("_PartyList");
        if (InfoProxyCrossRealm.IsInCrossRealmParty != 0)
        {
            var crParty = (CrossRealmGroup)InfoProxyCrossRealm.CrossRealmGroups[0];
            UpdateCRTextNodePtrs(crParty, *hudParty);
            ResetCRPartyNames(crParty);
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
            var pMember = crParty.GroupMembers[i];
            var name = pMember.NameString;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            var worldShort = pMember.HomeWorld;
            ResetPartyHelper(name, Util.GetWorld(worldShort));
        }
    }

    private void UpdateCRTextNodePtrs(CrossRealmGroup crParty, AddonPartyList hudParty)
    {
        for (var i = 0; i < crParty.GroupMemberCount; i++)
        {
            var pMember = crParty.GroupMembers[i];
            string? name = pMember.NameString;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            var worldShort = pMember.HomeWorld;

            UpdateTextNodePtr(name, Util.GetWorld(worldShort), hudParty.PartyMembers[i]);
        }
    }

    private void ResetPartyHelper(string name, string world)
    {
        string indexName = Util.IndexName(name, world);
        Service.PluginLog.Info("Resetting: " + name + " " + world);
        if (currentlySwapped.TryGetValue(indexName, out var textNode))
        {
            string? prefix = GetPrefix(textNode.NodeText);
            if (!string.IsNullOrEmpty(prefix))
            {
                textNode.SetText(prefix + name);
            }
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
                ResetPartyHelper(name, world);
            }
        }
        foreach (var pMember in Service.PartyList)
        {
            string name = pMember.Name.ToString();
            string world = pMember.World.GameData!.Name.ToString();
            ResetPartyHelper(name, world);
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
            UpdateTextNodePtr(name, world, hudParty.PartyMembers[0]);
        }

        // Update text ptr for party
        for (int i = 1; i < localParty.PartyMemberCount; i++)
        {
            var pMember = localParty.PartyMembers[i];
            string? name = Marshal.PtrToStringUTF8((nint)pMember.Name);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            string world = GetWorldByContentId(pMember.ContentId);
            UpdateTextNodePtr(name, world, hudParty.PartyMembers[i]);
        }
    }

    private void UpdateTextNodePtr(string name, string world, AddonPartyList.PartyListMemberStruct hudElement)
    {
        var index = Util.IndexName(name, world);
        currentlySwapped[index] = *hudElement.Name;
    }

    /* General Helpers */

    private string GetWorldByContentId(ulong contentId)
    {
        var player = CommonListPartyList.GetEntryByContentId(contentId);
        if (player == null)
        {
            Service.PluginLog.Debug("Character not found: ", contentId);
            return "Unassigned";
        }
        var worldShort = player->HomeWorld;
        return Util.GetWorld((short)worldShort);
    }

    private MatchCollection MatchHudTextNode(Utf8String textNode)
    {
        return this.pMemberPrefixRegex.Matches(textNode.ToString()!);
    }

    // this should fail for "Viewing Cutscene", which is intentional
    // any other case isn't tho
    private string? GetPrefix(Utf8String textNode)
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
