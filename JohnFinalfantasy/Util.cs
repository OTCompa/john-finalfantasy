using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace JohnFinalfantasy;

internal static class Util {
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
