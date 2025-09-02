using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;

namespace JohnFinalfantasy;
public class PlayerList
{
    private unsafe class Names(string original, string replacement)
    {
        public string PlayerName { get; set; } = original;
        public string? RawPartyListString { get; set; } = default;
        public string ReplacementName { get; set; } = replacement;
        public AtkTextNode* TextNode { get; set; }
    }
    private Dictionary<ulong, Names> PlayerMap { get; set; } = [];

    public bool TryGetOriginal(ulong contentId, out string? ret)
    {
        ret = default;
        if (this.PlayerMap.TryGetValue(contentId, out var val)) {
            if (string.IsNullOrEmpty(val.RawPartyListString))
            {
                ret = val.PlayerName;
            }
            else
            {
                ret = val.RawPartyListString;
            }
            return true;
        }
        return false;
    }

    public bool TryGetReplacement(ulong contentId, out string? ret)
    {
        ret = null;
        if (this.PlayerMap.TryGetValue(contentId, out var val)) {
            ret = val.ReplacementName;
            return true;
        }
        return false;
    }

    public unsafe bool TryGetTextNode(ulong contentId, out AtkTextNode* ret)
    {
        ret = null;
        if (this.PlayerMap.TryGetValue(contentId, out var val))
        {
            ret = val.TextNode;
            return true;
        }
        return false;
    }

    public void AddEntry(ulong contentId, string original, string replacement)
    {
        var entry = new Names(original, replacement);
        this.PlayerMap[contentId] = entry;
    }

    public bool UpdateEntryRawString(ulong contentId, string RawPartyListString)
    {
        if (this.PlayerMap.TryGetValue(contentId, out var val))
        {
            val.RawPartyListString = RawPartyListString;
            return true;
        }

        return false;
    }

    public unsafe bool UpdateEntryReplacement(ulong contentId, string newReplacement)
    {
        if (this.PlayerMap.TryGetValue(contentId, out var val)) 
        {
            val.ReplacementName = newReplacement;
            return true;
        }
        return false;
    }
    
    public unsafe bool UpdateEntryTextNode(ulong contentId, AtkTextNode* textNode)
    {
        if (this.PlayerMap.TryGetValue(contentId, out var val))
        {
            val.TextNode = textNode;
            return true;
        }
        return false;
    }

    public List<(string, string)> GetAllNames()
    {
        var ret = new List<(string, string)>();

        foreach (var entry in this.PlayerMap)
        {
            var entryInfo = entry.Value;
            (string, string) entryTuple = (entryInfo.PlayerName, entryInfo.ReplacementName);
            ret.Add(entryTuple);
        }

        return ret;
    }

    public void Clear() => PlayerMap.Clear();
}

