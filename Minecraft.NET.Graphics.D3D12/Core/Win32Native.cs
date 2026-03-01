using System.Runtime.InteropServices;

namespace Minecraft.NET.Graphics.D3D12.Core;

public unsafe partial class Win32Native
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial void* CreateEventW(
        void* lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        void* lpName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial uint WaitForSingleObject(void* hHandle, uint dwMilliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(void* hObject);
}