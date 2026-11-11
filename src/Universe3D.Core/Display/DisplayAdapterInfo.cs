using System.Text.RegularExpressions;

namespace Universe3D.Core.Display;

public static class DisplayAdapterInfo
{
    private static readonly Regex UsbRx = new(@"DisplayLink|USB", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>True for a USB display adapter (e.g. DisplayLink), whose frames are copied over USB.</summary>
    public static bool IsUsbDisplayAdapter(string adapterDeviceString) => UsbRx.IsMatch(adapterDeviceString ?? "");
}
