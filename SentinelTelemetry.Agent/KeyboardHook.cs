using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace SentinelTelemetry.Telemetry
{
    public class KeyboardHook: IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const uint WM_QUIT = 0x0012;

        // OJO: guardamos el delegate como campo de la clase.
        // Si no lo hacemos, el Garbage Collector lo puede liberar
        // mientras Windows todavía lo está llamando -> crash.

        private readonly NativeMethods.LowLevelKeyboardProc _proc;
        private IntPtr _hookHandle = IntPtr.Zero;
        private Thread? _hookThread;
        private uint _hookThreadId;

        public event Action? KeyPressed;

        public KeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Start()
        {
            _hookThread = new Thread(RunMessageLoop) { IsBackground = true };
            _hookThread.Start();
        }

        private void RunMessageLoop()
        {
            _hookThreadId = NativeMethods.GetCurrentThreadId();

            using var currentProcess = Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule!;
            var moduleHandle = NativeMethods.GetModuleHandle(currentModule.ModuleName);

            _hookHandle = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);

            // Un hook de bajo nivel SOLO funciona si el hilo que lo instaló
            // tiene un bucle de mensajes activo. Sin esto, Windows nunca
            // llama a HookCallback.
            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) != 0) ;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                KeyPressed?.Invoke();
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            if (_hookThreadId != 0)
            {
                NativeMethods.PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
