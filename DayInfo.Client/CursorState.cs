using System.Runtime.InteropServices;

namespace DayInfo.Client;

// Whether the game is showing a mouse cursor, which in Oblivion means a menu is open: in play the
// cursor is hidden and captured, and every menu brings it back. The SDK does not say when a menu
// opens, so this is the signal the HUD goes by.
internal static class CursorState
{
    private const int CursorShowing = 0x1;

    public static bool IsVisible()
    {
        var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        // Hidden can mean "not shown" or "shown with no image", depending on how the game hid it.
        return GetCursorInfo(ref info) && (info.Flags & CursorShowing) != 0 && info.Cursor != IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int Size;
        public int Flags;
        public IntPtr Cursor;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(ref CursorInfo info);
}
