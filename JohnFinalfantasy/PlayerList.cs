using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;

namespace JohnFinalfantasy;
public class PlayerList
{
    private unsafe class Names
    {
        public string original { get; set; }
        public string replacement { get; set; }
        public AtkTextNode* textNode { get; set; }
        public Names(string original, string replacement)
        {
            this.original = original;
            this.replacement = replacement;
        }
    }
    private Dictionary<ulong, Names> PlayerMap { get; set; }

    public PlayerList() => PlayerMap = new Dictionary<ulong, Names>();

    public bool GetOriginal(ulong contentId, out string? ret)
    {
        ret = null;
        if (contentId != 0 && this.PlayerMap.TryGetValue(contentId, out var val)) {
            ret = val.original;
            return true;
        }
        return false;
    }

    public bool GetReplacement(ulong contentId, out string? ret)
    {
        ret = null;
        if (contentId != 0 && this.PlayerMap.TryGetValue(contentId, out var val)) {
            ret = val.replacement;
            return true;
        }
        return false;
    }

    public unsafe bool GetTextNode(ulong contentId, out AtkTextNode* ret)
    {
        ret = null;
        if (contentId != 0 && this.PlayerMap.TryGetValue(contentId, out var val))
        {
            ret = val.textNode;
            return true;
        }
        return false;
    }

    public void AddEntry(ulong contentId, string original, string replacement)
    {
        var entry = new Names(original, replacement);
        this.PlayerMap[contentId] = entry;
    }

    public unsafe bool UpdateEntryReplacement(ulong contentId, string newReplacement)
    {
        if (this.PlayerMap.ContainsKey(contentId)) 
        {
            this.PlayerMap[contentId].replacement = newReplacement;
            return true;
        }
        return false;
    }
    
    public unsafe bool UpdateEntryTextNode(ulong contentId, AtkTextNode* textNode)
    {
        if (this.PlayerMap.ContainsKey(contentId))
        {
            this.PlayerMap[contentId].textNode = textNode;
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
            (string, string) entryTuple = (entryInfo.original, entryInfo.replacement);
            ret.Add(entryTuple);
        }

        return ret;
    }

    public void Clear() => PlayerMap.Clear();
}

