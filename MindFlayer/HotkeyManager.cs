using System;
using System.Runtime.InteropServices;
using System.Drawing;

namespace QuietPrompt
{
    internal static class HotkeyManager
    {
        // Constants & Win32 Interop
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int VK_F12 = 0x7B;
        private const int VK_F11 = 0x7A;
        private const int VK_F10 = 0x79;
        private const int VK_F9 = 0x78;
        private const int VK_F8 = 0x77;
        private const int VK_F7 = 0x76;
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(nint hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(nint hWnd, int id);
        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public nint hwnd;
            public uint message;
            public nint wParam;
            public nint lParam;
            public uint time;
            public Point pt;
        }

        private const int HOTKEY_ID_F12 = 9000;
        private const int HOTKEY_ID_F11 = 9001;
        private const int HOTKEY_ID_F10 = 9002;
        private const int HOTKEY_ID_F9  = 9003;
        private const int HOTKEY_ID_F8  = 9004;
        private const int HOTKEY_ID_F7  = 9005;
        private const int HOTKEY_ID_CTRL_ALT_F12 = 9006;

        public static void RegisterHotkeysOrExit()
        {
            if (!RegisterHotKey(nint.Zero, HOTKEY_ID_F12, MOD_CONTROL, VK_F12) ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F11, MOD_CONTROL, VK_F11) ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F10, MOD_CONTROL, VK_F10) ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F9,  MOD_CONTROL, VK_F9)  ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F8,  MOD_CONTROL, VK_F8)  ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_F7,  MOD_CONTROL, VK_F7)  ||
                !RegisterHotKey(nint.Zero, HOTKEY_ID_CTRL_ALT_F12, MOD_CONTROL | MOD_ALT, VK_F12))
            {
                Program.SafeWriteLine("Failed to register hotkey(s).");
                Environment.Exit(1);
            }
        }

        public static void UnregisterAllHotkeys()
        {
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F12);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F11);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F10);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F9);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F8);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_F7);
            UnregisterHotKey(nint.Zero, HOTKEY_ID_CTRL_ALT_F12);
        }

        public static void HotkeyMessageLoop()
        {
            RegisterHotkeysOrExit();
            while (true)
            {
                GetMessage(out MSG msg, nint.Zero, 0, 0);
                if (msg.message == WM_HOTKEY)
                {
                    switch (msg.wParam.ToInt32())
                    {
                        case HOTKEY_ID_F12:
                            Program.SafeWriteLine("Ctrl+F12 pressed!");
                            Program.SendAllTranscriptsToLlm();
                            break;
                        case HOTKEY_ID_F11:
                            Program.SafeWriteLine("Ctrl+F11 pressed!");
                            Program.CaptureSecondMonitorAndAppendOcr();
                            break;
                        case HOTKEY_ID_F10:
                            Program.SafeWriteLine("Ctrl+F10 pressed!");
                            Program.CaptureSnipAndAppendOcr();
                            break;
                        case HOTKEY_ID_F9:
                            Program.SafeWriteLine("Ctrl+F9 pressed!");
                            Program.ToggleMicTranscription();
                            break;
                        case HOTKEY_ID_F8:
                            Program.SafeWriteLine("Ctrl+F8 pressed!");
                            Program.PromptAndStoreUserInput();
                            break;
                        case HOTKEY_ID_F7:
                            Program.SafeWriteLine("Ctrl+F7 pressed!");
                            Program.ClearAllTranscripts();
                            break;
                        case HOTKEY_ID_CTRL_ALT_F12:
                            Program.SafeToggleOverlay();
                            break;
                    }
                }
            }
        }
    }
}