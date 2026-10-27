using System.Runtime.InteropServices;

namespace GameShell.Desktop.Shortcuts;

internal static class SdlNative
{
    private const string SdlLibrary = "SDL3";

    [Flags]
    internal enum InitFlags : uint
    {
        Gamepad = 0x00002000
    }

    internal enum SdlGamepadButton
    {
        Invalid = -1,
        South = 0,
        East = 1,
        West = 2,
        North = 3,
        Back = 4,
        Guide = 5,
        Start = 6,
        LeftStick = 7,
        RightStick = 8,
        LeftShoulder = 9,
        RightShoulder = 10,
        DpadUp = 11,
        DpadDown = 12,
        DpadLeft = 13,
        DpadRight = 14,
        Misc1 = 15,
        RightPaddle1 = 16,
        LeftPaddle1 = 17,
        RightPaddle2 = 18,
        LeftPaddle2 = 19,
        Touchpad = 20,
        Misc2 = 21,
        Misc3 = 22,
        Misc4 = 23,
        Misc5 = 24,
        Misc6 = 25,
        Count = 26
    }

    internal enum SdlGamepadAxis
    {
        LeftX = 0,
        LeftY = 1
    }

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_InitSubSystem")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SDL_InitSubSystem(InitFlags flags);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Quit")]
    internal static extern void SDL_Quit();

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_WasInit")]
    internal static extern InitFlags SDL_WasInit(InitFlags flags);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_PumpEvents")]
    internal static extern void SDL_PumpEvents();

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_UpdateGamepads")]
    internal static extern byte SDL_UpdateGamepads();

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetGamepads")]
    internal static extern nint SDL_GetGamepads(out int count);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_IsGamepad")]
    internal static extern byte SDL_IsGamepad(uint joystickId);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_OpenGamepad")]
    internal static extern nint SDL_OpenGamepad(uint joystickId);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_CloseGamepad")]
    internal static extern void SDL_CloseGamepad(nint gamepad);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetGamepadName")]
    internal static extern nint SDL_GetGamepadName(nint gamepad);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetGamepadMapping")]
    internal static extern nint SDL_GetGamepadMapping(nint gamepad);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GamepadHasButton")]
    internal static extern byte SDL_GamepadHasButton(nint gamepad, SdlGamepadButton button);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetGamepadButton")]
    internal static extern byte SDL_GetGamepadButton(nint gamepad, SdlGamepadButton button);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetGamepadAxis")]
    internal static extern short SDL_GetGamepadAxis(nint gamepad, SdlGamepadAxis axis);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GamepadConnected")]
    internal static extern byte SDL_GamepadConnected(nint gamepad);

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetVersion")]
    internal static extern int SDL_GetVersion();

    [DllImport(SdlLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetError")]
    internal static extern nint SDL_GetError();

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileName(nint module, char[] fileName, uint size);

    internal static string? Utf8(nint value) => value == nint.Zero ? null : Marshal.PtrToStringUTF8(value);

    internal static string? LoadedLibraryPath()
    {
        var module = GetModuleHandle("SDL3.dll");
        if (module == nint.Zero) return null;

        var buffer = new char[512];
        var length = GetModuleFileName(module, buffer, (uint)buffer.Length);
        return length == 0 ? null : new string(buffer, 0, (int)length);
    }
}