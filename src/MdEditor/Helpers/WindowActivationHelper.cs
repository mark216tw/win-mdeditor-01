using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace VibeCode.MdEditor.Helpers;

internal static class WindowActivationHelper
{
    private const int SwRestore = 9;

    public static bool AllowSetForeground(uint processId)
    {
        return AllowSetForegroundWindow(processId) != 0;
    }

    public static bool BringToForeground(Window window)
    {
        var windowHandle = WindowNative.GetWindowHandle(window);

        if (IsIconic(windowHandle) != 0)
        {
            _ = ShowWindow(windowHandle, SwRestore);
        }

        window.Activate();
        return SetForegroundWindow(windowHandle) != 0;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int IsIconic(nint windowHandle);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int ShowWindow(nint windowHandle, int command);
}
