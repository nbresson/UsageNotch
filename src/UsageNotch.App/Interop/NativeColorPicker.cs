using System.Runtime.InteropServices;
using UsageNotch.Presentation.Formatting;
using UsageNotch.Presentation.Services;

namespace UsageNotch.App.Interop;

/// <summary>Boîte de dialogue « Couleurs » de Windows. Ses 16 couleurs personnalisées sont gardées pendant la session.</summary>
public sealed class NativeColorPicker : IColorPicker, IDisposable
{
    private const int CustomColorCount = 16;

    private nint _customColors;

    public NativeColorPicker()
    {
        _customColors = Marshal.AllocHGlobal(CustomColorCount * sizeof(uint));
        for (var i = 0; i < CustomColorCount; i++)
        {
            Marshal.WriteInt32(_customColors, i * sizeof(uint), 0x00FFFFFF);
        }
    }

    /// <summary>Fenêtre propriétaire de la boîte (la fenêtre de réglages) ; 0 si aucune.</summary>
    public nint Owner { get; set; }

    public string? Pick(string initialHex)
    {
        ObjectDisposedException.ThrowIf(_customColors == 0, this);

        var dialog = new NativeMethods.CHOOSECOLOR
        {
            lStructSize = Marshal.SizeOf<NativeMethods.CHOOSECOLOR>(),
            hwndOwner = Owner,
            rgbResult = HexColor.TryNormalize(initialHex, out var hex) ? HexColor.ToColorRef(hex) : 0,
            lpCustColors = _customColors,
            Flags = NativeMethods.CC_RGBINIT | NativeMethods.CC_FULLOPEN,
        };
        return NativeMethods.ChooseColor(ref dialog) ? HexColor.FromColorRef(dialog.rgbResult) : null;
    }

    public void Dispose()
    {
        if (_customColors == 0) return;
        Marshal.FreeHGlobal(_customColors);
        _customColors = 0;
    }
}
