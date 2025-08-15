using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using System;
using System.Runtime.InteropServices;

namespace JohnFinalfantasy;

internal class GameFunctions : IDisposable
{
    private static class Signatures
    {
        internal const string AtkTextNodeSetText = "E8 ?? ?? ?? ?? 8D 4D 0F";
    }

    #region Delegates

    private delegate void AtkTextNodeSetTextDelegate(IntPtr node, IntPtr text);
    private delegate void GenerateNameDelegate(int race, int clan, int gender, Utf8String first, Utf8String last);
    private delegate IntPtr GetExcelModuleDelegate(IntPtr uiModule);
    private delegate byte LoadExdDelegate(IntPtr a1, string sheetName, byte a3, byte a4);

    #endregion

    #region Functions

    [Signature("E8 ?? ?? ?? ?? 48 8D 8E ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 1E 48 8B 9E ?? ?? ?? ?? 48 8D 8E ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D0 48 8B CB E8 ?? ?? ?? ?? 48 8B CF")]
    private readonly GenerateNameDelegate generateName = null!;
    [Signature("40 53 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 41 0F B6 D9")]
    private readonly LoadExdDelegate loadExd = null!;

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

        Service.GameInteropProvider.InitializeFromAttributes(this);
        this.AtkTextNodeSetTextHook.Enable();
    }

    public void Dispose()
    {
        this.AtkTextNodeSetTextHook.Dispose();
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

    private unsafe void LoadSheet(string name)
    {
        var ui = (IntPtr)Framework.Instance()->GetUIModule();
        var getExcelModulePtr = *(*(IntPtr**)ui + 5);
        var getExcelModule = Marshal.GetDelegateForFunctionPointer<GetExcelModuleDelegate>(getExcelModulePtr);
        var excelModule = getExcelModule(ui);
        var exdModule = *(IntPtr*)(excelModule + 8);
        var excel = *(IntPtr*)(exdModule + 0x20);

        this.loadExd(excel, name, 0, 1);
    }

    public void GenerateName(int race, int clan, int gender, Utf8String first, Utf8String last)
    {
        LoadSheet("CharaMakeName");
        this.generateName(race, clan, gender, first, last);
    }
}
