using System;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace WinUtils
{
    // NOTE: we could remove this class's dependency on winforms by p/invoking
    // to create the native window and spin the msg pump.
    //
    public static class WindowHelper
    {
        public delegate void CreatedWindowDelegate(IntPtr handle);
        public delegate void WndProcDelegate(ref Message msg);

        static object _lock = new object();
        static IntPtr _handle = IntPtr.Zero;
        static HiddenWindow _hiddenwindow;

        // this property lets the mainline app, communicate to other libraries
        // whether it will create a window or not.
        //
        public static bool ApplicationWillCreateWindow;

        // the handle to the hooked window. if ApplicationWillCreateWindow is
        // true and window has been created, it will be stored here. if
        // ApplicationWillCreateWindow is false and CreateHiddenWindow() has
        // been called, the hidden window handle will be here.
        //
        public static IntPtr Handle
        {
            get
            {
                return _handle;
            }
            set
            {
                lock (_lock)
                {
                    if (value == IntPtr.Zero)
                        throw new ArgumentNullException("WindowHandle");
                    if (_handle != IntPtr.Zero)
                        throw new InvalidOperationException("WindowHandle is already set");

                    _handle = value;

                    if (ApplicationWillCreateWindow)
                        _HookWindow(_handle);

                    if (CreatedWindow != null)
                        CreatedWindow(_handle);
                }
            }
        }

        // fired when Handle is set.
        //
        public static event CreatedWindowDelegate CreatedWindow;

        // if you want to process windows messages, hook up to this event.
        //
        public static event WndProcDelegate WndProc;

        // call this if ApplicationWillCreateWindow is false and you want to
        // use WndProc, call this to create a hidden window and a bg thread to
        // spin a msg pump.
        //
        public static void CreateHiddenWindow()
        {
            if (ApplicationWillCreateWindow)
                throw new InvalidOperationException("silly to create a hidden window if application is going to create a window");

            new Thread((ThreadStart)delegate
            {
                lock (_lock)
                {
                    if (_hiddenwindow != null)
                        return;
//                    Console.WriteLine("WindowHelper: Creating hidden window");
                    _hiddenwindow = new HiddenWindow();
                    Handle = _hiddenwindow.Handle;
                }
                Application.Run();
            }) { IsBackground = true }.Start();
        }

        class HiddenWindow : NativeWindow
        {
            public HiddenWindow()
            {
                var cp = new CreateParams();
                cp.Caption = "WindowHelper Hidden Window";
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST, so we are the first to get WM_DEVICECHANGE messages
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message msg)
            {
                base.WndProc(ref msg);
//                Console.WriteLine("WindowHelper: hidden window got message");
                if (WinUtils.WindowHelper.WndProc != null)
                    WinUtils.WindowHelper.WndProc(ref msg);
            }
        }

        static IntPtr _oldwndproc;
        static void _HookWindow(IntPtr hwnd)
        {
//            Console.WriteLine("WindowHelper: Hooking GUI window");
            Win32WndProc newwndproc = delegate(IntPtr h, int msg, int wparam, int lparam)
            {
//                Console.WriteLine("WindowHelper: gui window got message");
                if (WndProc != null)
                {
                    var m = new Message() {
                        HWnd = h,
                        Msg = msg,
                        WParam = new IntPtr(wparam),
                        LParam = new IntPtr(lparam)
                    };
                    WndProc(ref m);
                }
                return CallWindowProc(_oldwndproc, h, msg, wparam, lparam);
            };
            _oldwndproc = SetWindowLong(hwnd, GWL_WNDPROC, newwndproc);
        }

        const int GWL_WNDPROC = -4;
        delegate int Win32WndProc(IntPtr hwnd, int msg, int wparam, int lparam);
        [DllImport("user32")] static extern IntPtr SetWindowLong(IntPtr hwnd, int nindex, Win32WndProc newproc);
        [DllImport("user32")] static extern int CallWindowProc(IntPtr lpprevwndfunc, IntPtr hwnd, int msg, int wparam, int lparam);
    }
}
