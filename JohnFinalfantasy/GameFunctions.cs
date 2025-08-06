using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace JohnFinalfantasy;

internal class GameFunctions : IDisposable
{
    private static class Signatures
    {
        internal const string AtkTextNodeSetText = "E8 ?? ?? ?? ?? 8D 4D 0F";
    }

    #region Delegates

    private delegate void AtkTextNodeSetTextDelegate(IntPtr node, IntPtr text);

    #endregion


    #region Hooks

    [Signature(Signatures.AtkTextNodeSetText, DetourName = nameof(AtkTextNodeSetTextDetour))]
    private Hook<AtkTextNodeSetTextDelegate> AtkTextNodeSetTextHook { get; init; } = null!;

    #endregion


    #region Events

    internal delegate void AtkTextNodeSetTextEventDelegate(IntPtr node, IntPtr text, ref SeString? overwrite);

    internal event AtkTextNodeSetTextEventDelegate? OnAtkTextNodeSetText;
    #endregion

    private Plugin Plugin { get; }

    internal GameFunctions(Plugin plugin)
    {
        this.Plugin = plugin;

        Service.gameInteropProvider.InitializeFromAttributes(this);

        this.AtkTextNodeSetTextHook.Enable();


    }

    public void Dispose()
    {
        this.AtkTextNodeSetTextHook.Dispose();
    }

    private void OnTerritoryChange(object? sender, ushort e)
    {

    }

    private unsafe void AtkTextNodeSetTextDetour(IntPtr node, IntPtr text)
    {
        SeString? overwrite = null;
        this.OnAtkTextNodeSetText?.Invoke(node, text, ref overwrite);

        if (overwrite != null)
        {
            fixed (byte* newText = overwrite.Encode().Terminate())
            {
                this.AtkTextNodeSetTextHook.Original(node, (IntPtr)newText);
            }

            return;
        }

        this.AtkTextNodeSetTextHook.Original(node, text);
    }
}
