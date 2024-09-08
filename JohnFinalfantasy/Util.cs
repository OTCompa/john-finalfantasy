using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace JohnFinalfantasy;

internal static class Util {
    internal static readonly Regex Coords = new(@"^X: \d+. Y: \d+.(?: Z: \d+.)?$", RegexOptions.Compiled);
    internal static readonly Regex LevelPrefix = new("^((?:\u0002\u001a\u0002\u0002\u0003\u0002\u0012\u0002\\?\u0003)?[][-?]+\\s(?:\u0002\u0012\u0002Y\u0003)?\\s?)(.*)$", RegexOptions.Compiled);

    internal static void ReplacePlayerName(this SeString text, string name, string replacement) {
        if (string.IsNullOrEmpty(name)) {
            return;
        }

        foreach (var payload in text.Payloads) {
            switch (payload) {
                case TextPayload txt:
                    txt.Text = txt.Text.Replace(name, replacement);

                    break;
            }
        }
    }

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

    internal static byte[] Terminate(this byte[] bs) {
            var terminated = new byte[bs.Length + 1];
            Array.Copy(bs, terminated, bs.Length);
            terminated[^1] = 0;
            return terminated;
        }

    internal static SeString ReadRawSeString(IntPtr ptr) {
        var bytes = ReadRawBytes(ptr);
        return SeString.Parse(bytes);
    }

    private static unsafe byte[] ReadRawBytes(IntPtr ptr) {
        if (ptr == IntPtr.Zero) {
            return Array.Empty<byte>();
        }

        var bytes = new List<byte>();

        var bytePtr = (byte*) ptr;
        while (*bytePtr != 0) {
            bytes.Add(*bytePtr);
            bytePtr += 1;
        }

        return bytes.ToArray();
    }
}
