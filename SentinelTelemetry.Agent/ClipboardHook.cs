using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static SentinelTelemetry.Telemetry.NativeMethods;

namespace SentinelTelemetry.Telemetry
{
    public class ClipboardHook
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int HWND_MESSAGE = -3;
        private const string ClassName = "SentinelClipboardListener";

        private IntPtr _windowHandle = IntPtr.Zero;
        private Thread? _messageLoopThread;
        private NativeMethods.WndProc? _wndProcDelegate;

        public event Action<string, int?>? ClipboardChanged;

        public void Start()
        {
            _messageLoopThread = new Thread(RunMessageLoop);
            _messageLoopThread.IsBackground = true;
            _messageLoopThread.SetApartmentState(ApartmentState.STA);
            _messageLoopThread.Start();
        }

        private void RunMessageLoop()
        {
            _wndProcDelegate = WndProcHandler;

            var wndClass = new NativeMethods.WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                lpszClassName = ClassName,
                lpszMenuName = string.Empty,
                hInstance = Marshal.GetHINSTANCE(typeof(ClipboardHook).Module)
            };

            NativeMethods.RegisterClassEx(ref wndClass);

            _windowHandle = NativeMethods.CreateWindowEx(
                0, ClassName, "SentinelClipboardWindow", 0,
                0, 0, 0, 0,
                new IntPtr(HWND_MESSAGE), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            NativeMethods.AddClipboardFormatListener(_windowHandle);

            MSG msg = new MSG();
            while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0) != 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        private IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                ReadClipboardAndNotify();
            }
            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void ReadClipboardAndNotify()
        {
            string contentType = "Other";
            int? contentLength = null;

            try
            {
                var dataObject = System.Windows.Forms.Clipboard.GetDataObject();
                if (dataObject is null) return;

                if (dataObject.GetDataPresent(System.Windows.Forms.DataFormats.Text))
                {
                    contentType = "Text";
                    var text = (string)dataObject.GetData(System.Windows.Forms.DataFormats.Text)!;
                    contentLength = text.Length;
                }
                else if (dataObject.GetDataPresent(System.Windows.Forms.DataFormats.Bitmap))
                {
                    contentType = "Image";
                }
                else if (dataObject.GetDataPresent(System.Windows.Forms.DataFormats.FileDrop))
                {
                    contentType = "FileDrop";
                }

            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return; // portapapeles bloqueado por otro proceso en este instante, se ignora
            }

            ClipboardChanged?.Invoke(contentType, contentLength);   
        }
        public void Stop()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                NativeMethods.RemoveClipboardFormatListener(_windowHandle);
                NativeMethods.DestroyWindow(_windowHandle);
            }
        }
    }
}
