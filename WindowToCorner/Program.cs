using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowToCorner {
    static class Program {
        static NotifyIcon nicon;
        static KeyboardHook hook;
        static bool run = true;
        static bool ctrl = false;
        static List<int> diffs;//調整用
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            //二重起動防止
            var mutex = new Mutex(false, "HeyCH.WindowToCorner");
            bool handle = false;
            try {
                handle = mutex.WaitOne(0, false);
            } catch (AbandonedMutexException) {
                handle = true;
            }
            if (!handle) return;


            //調整用ファイル読み込み
            try {
                var fn = "adj.txt";
                diffs = File.ReadAllText(fn).Split(',').Select(d => int.Parse(d)).ToList();
            } catch {
                diffs = Enumerable.Range(0, 4).Select(i => 0).ToList();
            }

            nicon = new NotifyIcon();
            nicon.Icon = Properties.Resources.WindowToCorner;
            nicon.Text = "WindowToCorner";
            nicon.Visible = true;


            ContextMenuStrip cms = new ContextMenuStrip();
            ToolStripMenuItem tsmi0 = new ToolStripMenuItem("Exit");//終了
            tsmi0.Click += (s, e) => {
                Application.Exit();
            };
            cms.Items.Add(tsmi0);
            nicon.ContextMenuStrip = cms;


            hook = new KeyboardHook();
            hook.KeyDownEvent += (s, e) => {
                if (e.KeyCode == 162) ctrl = true;
            };
            hook.KeyUpEvent += (s, e) => {
                //ctrl:162,left:37,up:38,right:39,down:40
                if (e.KeyCode == 162) ctrl = false;
                if (ctrl && e.KeyCode >= 37 && e.KeyCode <= 40) MoveWindowX(40 - e.KeyCode);
            };
            hook.Hook();


            Task.Run(() => {
                while (run) {
                }
            });

            try {
                Application.Run();
            } catch { } finally {
                if (handle) mutex.ReleaseMutex();
                mutex.Close();
            }
        }



        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private static void MoveWindowX(int type) {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            RECT rect;
            GetWindowRect(hwnd, out rect);

            int w = rect.right - rect.left;
            int h = rect.bottom - rect.top;

            if (type == 0) {
                //down
                MoveWindow(hwnd, rect.left, Screen.PrimaryScreen.WorkingArea.Height - h + diffs[3], w, h, false);
            } else if (type == 1) {
                //right
                MoveWindow(hwnd, Screen.PrimaryScreen.WorkingArea.Width - w + diffs[2], rect.top, w, h, false);
            } else if (type == 2) {
                //up
                MoveWindow(hwnd, rect.left, 0 + diffs[1], w, h, false);
            } else if (type == 3) {
                //left
                MoveWindow(hwnd, 0 + diffs[0], rect.top, w, h, false);
            }
        }
    }

    public class KeyboardHook {
        protected const int WH_KEYBOARD_LL = 0x000D;
        protected const int WM_KEYDOWN = 0x0100;
        protected const int WM_KEYUP = 0x0101;
        protected const int WM_SYSKEYDOWN = 0x0104;
        protected const int WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        public class KBDLLHOOKSTRUCT {
            public uint vkCode;
            public uint scanCode;
            public KBDLLHOOKSTRUCTFlags flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [Flags]
        public enum KBDLLHOOKSTRUCTFlags : uint {
            KEYEVENTF_EXTENDEDKEY = 0x0001,
            KEYEVENTF_KEYUP = 0x0002,
            KEYEVENTF_SCANCODE = 0x0008,
            KEYEVENTF_UNICODE = 0x0004,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private KeyboardProc proc;
        private IntPtr hookId = IntPtr.Zero;

        public void Hook() {
            if (hookId == IntPtr.Zero) {
                proc = HookProcedure;
                using (var curProcess = Process.GetCurrentProcess()) {
                    using (ProcessModule curModule = curProcess.MainModule) {
                        hookId = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                    }
                }
            }
        }

        public void UnHook() {
            UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }

        public IntPtr HookProcedure(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)) {
                var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                var vkCode = (int)kb.vkCode;
                OnKeyDownEvent(vkCode);
            } else if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)) {
                var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                var vkCode = (int)kb.vkCode;
                OnKeyUpEvent(vkCode);
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public delegate void KeyEventHandler(object sender, KeyEventArg e);
        public event KeyEventHandler KeyDownEvent;
        public event KeyEventHandler KeyUpEvent;

        protected void OnKeyDownEvent(int keyCode) {
            KeyDownEvent?.Invoke(this, new KeyEventArg(keyCode));
        }
        protected void OnKeyUpEvent(int keyCode) {
            KeyUpEvent?.Invoke(this, new KeyEventArg(keyCode));
        }

    }
    public class KeyEventArg : EventArgs {
        public int KeyCode { get; }

        public KeyEventArg(int keyCode) {
            KeyCode = keyCode;
        }
    }
}
