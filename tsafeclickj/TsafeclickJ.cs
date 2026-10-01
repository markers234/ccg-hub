// TsafeclickJ - keyboard + mouse auto clicker with sequences, recording and global hotkeys.
// Build (Windows):  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:TsafeclickJ.exe TsafeclickJ.cs
// Build (Linux/mono): mcs -target:winexe -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:TsafeclickJ.exe TsafeclickJ.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("TsafeclickJ")]
[assembly: System.Reflection.AssemblyDescription("Keyboard and mouse auto clicker")]
[assembly: System.Reflection.AssemblyProduct("TsafeclickJ")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace TsafeclickJ
{
    // ------------------------------------------------------------------ Win32
    static class Native
    {
        public const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_UNICODE = 0x4, KEYEVENTF_SCANCODE = 0x8;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x2, MOUSEEVENTF_LEFTUP = 0x4, MOUSEEVENTF_RIGHTDOWN = 0x8, MOUSEEVENTF_RIGHTUP = 0x10,
            MOUSEEVENTF_MIDDLEDOWN = 0x20, MOUSEEVENTF_MIDDLEUP = 0x40, MOUSEEVENTF_XDOWN = 0x80, MOUSEEVENTF_XUP = 0x100,
            MOUSEEVENTF_WHEEL = 0x800;
        public const int WM_HOTKEY = 0x312;
        public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        public const uint MOD_NOREPEAT = 0x4000;

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint uPeriod);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint uPeriod);
    }

    public enum MouseBtn { Left, Right, Middle, X1, X2 }

    // ------------------------------------------------------------------ input injection
    static class Inj
    {
        public static volatile bool GameMode;
        static readonly int Size = Marshal.SizeOf(typeof(Native.INPUT));
        static readonly IntPtr Tag = new IntPtr(0x75AFE);

        static void Send(params Native.INPUT[] inputs) { Native.SendInput((uint)inputs.Length, inputs, Size); }

        static Native.INPUT Mouse(uint flags, uint data)
        {
            var i = new Native.INPUT();
            i.type = Native.INPUT_MOUSE;
            i.u.mi.dwFlags = flags;
            i.u.mi.mouseData = data;
            i.u.mi.dwExtraInfo = Tag;
            return i;
        }

        static uint DownFlag(MouseBtn b)
        {
            switch (b)
            {
                case MouseBtn.Right: return Native.MOUSEEVENTF_RIGHTDOWN;
                case MouseBtn.Middle: return Native.MOUSEEVENTF_MIDDLEDOWN;
                case MouseBtn.X1: case MouseBtn.X2: return Native.MOUSEEVENTF_XDOWN;
                default: return Native.MOUSEEVENTF_LEFTDOWN;
            }
        }
        static uint UpFlag(MouseBtn b) { return DownFlag(b) << 1; }
        static uint XData(MouseBtn b) { return b == MouseBtn.X1 ? 1u : b == MouseBtn.X2 ? 2u : 0u; }

        public static void MouseDown(MouseBtn b) { Send(Mouse(DownFlag(b), XData(b))); }
        public static void MouseUp(MouseBtn b) { Send(Mouse(UpFlag(b), XData(b))); }
        public static void Wheel(int notches) { Send(Mouse(Native.MOUSEEVENTF_WHEEL, unchecked((uint)(notches * 120)))); }
        public static void MoveTo(int x, int y) { Native.SetCursorPos(x, y); }

        static bool IsExtended(Keys k)
        {
            switch (k)
            {
                case Keys.Insert: case Keys.Delete: case Keys.Home: case Keys.End: case Keys.PageUp: case Keys.PageDown:
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: case Keys.NumLock: case Keys.Divide:
                case Keys.RControlKey: case Keys.RMenu: case Keys.LWin: case Keys.RWin: case Keys.Apps: case Keys.PrintScreen:
                    return true;
            }
            return false;
        }

        static Native.INPUT Key(Keys key, bool up)
        {
            ushort vk = (ushort)(key & Keys.KeyCode);
            ushort scan = (ushort)Native.MapVirtualKey(vk, 0);
            var i = new Native.INPUT();
            i.type = Native.INPUT_KEYBOARD;
            uint flags = up ? Native.KEYEVENTF_KEYUP : 0;
            if (IsExtended(key)) flags |= Native.KEYEVENTF_EXTENDEDKEY;
            if (GameMode && scan != 0) { flags |= Native.KEYEVENTF_SCANCODE; i.u.ki.wVk = 0; }
            else i.u.ki.wVk = vk;
            i.u.ki.wScan = scan;
            i.u.ki.dwFlags = flags;
            i.u.ki.dwExtraInfo = Tag;
            return i;
        }

        public static void KeyDown(Keys k) { Send(Key(k, false)); }
        public static void KeyUp(Keys k) { Send(Key(k, true)); }

        public static readonly Keys[] ModKeys = { Keys.ControlKey, Keys.ShiftKey, Keys.Menu, Keys.LWin };
        public static void ModsDown(int mods) { for (int b = 0; b < 4; b++) if ((mods & (1 << b)) != 0) KeyDown(ModKeys[b]); }
        public static void ModsUp(int mods) { for (int b = 3; b >= 0; b--) if ((mods & (1 << b)) != 0) KeyUp(ModKeys[b]); }

        public static void TypeText(string s)
        {
            foreach (char c in s)
            {
                if (c == '\r') continue;
                if (c == '\n') { KeyDown(Keys.Enter); KeyUp(Keys.Enter); continue; }
                var d = new Native.INPUT(); d.type = Native.INPUT_KEYBOARD;
                d.u.ki.wScan = c; d.u.ki.dwFlags = Native.KEYEVENTF_UNICODE; d.u.ki.dwExtraInfo = Tag;
                var u = d; u.u.ki.dwFlags = Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP;
                Send(d, u);
            }
        }

        public static string KeyName(Keys k)
        {
            if (k == Keys.None) return "(none)";
            if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num " + (k - Keys.NumPad0);
            switch (k)
            {
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemQuestion: return "/";
                case Keys.Oemtilde: return "`";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemPipe: return "\\";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.Return: return "Enter";
                case Keys.Next: return "PageDown";
                case Keys.Prior: return "PageUp";
                case Keys.Capital: return "CapsLock";
                case Keys.Menu: case Keys.LMenu: return "Alt";
                case Keys.RMenu: return "Right Alt";
                case Keys.ControlKey: case Keys.LControlKey: return "Ctrl";
                case Keys.RControlKey: return "Right Ctrl";
                case Keys.ShiftKey: case Keys.LShiftKey: return "Shift";
                case Keys.RShiftKey: return "Right Shift";
                case Keys.Back: return "Backspace";
                case Keys.Escape: return "Esc";
            }
            return k.ToString();
        }

        public static string ModsText(int mods)
        {
            var sb = new StringBuilder();
            if ((mods & 1) != 0) sb.Append("Ctrl+");
            if ((mods & 2) != 0) sb.Append("Shift+");
            if ((mods & 4) != 0) sb.Append("Alt+");
            if ((mods & 8) != 0) sb.Append("Win+");
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------------ high precision worker thread
    class Worker
    {
        Thread thread;
        volatile bool stopFlag;
        Stopwatch sw = new Stopwatch();

        public bool Running { get { var t = thread; return t != null && t.IsAlive; } }
        public bool StopRequested { get { return stopFlag; } }
        public double NowMs { get { return sw.Elapsed.TotalMilliseconds; } }

        public void Start(Action<Worker> body, Action onExit)
        {
            if (Running) return;
            stopFlag = false;
            sw.Restart();
            thread = new Thread(() =>
            {
                try { body(this); }
                catch (ThreadInterruptedException) { }
                catch (Exception ex) { Debug.WriteLine(ex); }
                finally { if (onExit != null) onExit(); }
            });
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.AboveNormal;
            thread.Start();
        }

        public void Stop() { stopFlag = true; }

        // Sleeps coarsely while far away, then yields/spins for the last ~1.5 ms so 1-2 ms intervals stay accurate.
        public bool WaitUntil(double targetMs)
        {
            while (true)
            {
                if (stopFlag) return false;
                double rem = targetMs - NowMs;
                if (rem <= 0) return true;
                if (rem > 1.6) Thread.Sleep(rem > 20 ? 10 : 1);
                else Thread.Yield();
            }
        }

        public bool Wait(double ms) { return ms <= 0 ? !stopFlag : WaitUntil(NowMs + ms); }
    }

    // ------------------------------------------------------------------ sequence steps
    public enum StepType { KeyPress, KeyDown, KeyUp, MouseClick, MouseDown, MouseUp, MoveMouse, Scroll, TypeText, Wait }

    public class Step
    {
        public static readonly string[] TypeNames = { "Key press", "Key down", "Key up", "Mouse click", "Mouse down", "Mouse up", "Move mouse", "Scroll wheel", "Type text", "Wait" };
        public static readonly string[] ButtonNames = { "Left", "Right", "Middle", "Side 1 (X1)", "Side 2 (X2)" };

        public StepType Type;
        public Keys Key;
        public int Mods;
        public MouseBtn Button;
        public bool AtCursor = true;
        public int X, Y;
        public int HoldMs;
        public int DelayMs = 2;
        public int Repeat = 1;
        public int Amount = 1;
        public string Text = "";

        public bool IsKey { get { return Type == StepType.KeyPress || Type == StepType.KeyDown || Type == StepType.KeyUp; } }
        public bool IsMouseButton { get { return Type == StepType.MouseClick || Type == StepType.MouseDown || Type == StepType.MouseUp; } }

        public Step Clone() { return (Step)MemberwiseClone(); }

        public string Details()
        {
            if (IsKey) return Inj.ModsText(Mods) + Inj.KeyName(Key);
            if (IsMouseButton) return ButtonNames[(int)Button] + (AtCursor ? " at cursor" : " @ " + X + ", " + Y);
            switch (Type)
            {
                case StepType.MoveMouse: return X + ", " + Y;
                case StepType.Scroll: return (Amount > 0 ? "+" : "") + Amount + " notch" + (Math.Abs(Amount) == 1 ? "" : "es") + (Amount > 0 ? " (up)" : " (down)");
                case StepType.TypeText: return "\"" + Text.Replace("\n", "\\n") + "\"";
            }
            return "";
        }

        public string Serialize()
        {
            return string.Join("|", new string[] {
                ((int)Type).ToString(), ((int)Key).ToString(), Mods.ToString(), ((int)Button).ToString(), AtCursor ? "1" : "0",
                X.ToString(), Y.ToString(), HoldMs.ToString(), DelayMs.ToString(), Repeat.ToString(), Amount.ToString(),
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Text ?? "")) });
        }

        public static Step Parse(string line)
        {
            var p = line.Split('|');
            if (p.Length < 12) return null;
            var s = new Step();
            s.Type = (StepType)int.Parse(p[0]);
            s.Key = (Keys)int.Parse(p[1]);
            s.Mods = int.Parse(p[2]);
            s.Button = (MouseBtn)int.Parse(p[3]);
            s.AtCursor = p[4] == "1";
            s.X = int.Parse(p[5]); s.Y = int.Parse(p[6]);
            s.HoldMs = int.Parse(p[7]); s.DelayMs = int.Parse(p[8]);
            s.Repeat = int.Parse(p[9]); s.Amount = int.Parse(p[10]);
            s.Text = Encoding.UTF8.GetString(Convert.FromBase64String(p[11]));
            return s;
        }
    }

    // ------------------------------------------------------------------ key capture box
    class KeyBox : TextBox
    {
        Keys key = Keys.None;
        public static volatile bool Locked; // ignore our own injected keys while a clicker is running
        public event EventHandler KeyChanged;
        public KeyBox() { ReadOnly = true; ShortcutsEnabled = false; Cursor = Cursors.Hand; Text = "click, then press a key"; }
        public Keys Key
        {
            get { return key; }
            set { key = value; Text = Inj.KeyName(value); if (KeyChanged != null) KeyChanged(this, EventArgs.Empty); }
        }
        protected override bool IsInputKey(Keys keyData) { return true; }
        protected override bool ProcessDialogKey(Keys keyData) { return false; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!Locked) Key = e.KeyCode;
            e.Handled = true; e.SuppressKeyPress = true;
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            // PrintScreen only produces a key-up.
            if (e.KeyCode == Keys.PrintScreen && !Locked) Key = Keys.PrintScreen;
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ main window
    class MainForm : Form
    {
        const string AppName = "TsafeclickJ";
        static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
        static readonly string SettingsFile = Path.Combine(DataDir, "settings.ini");
        static readonly string AutoSeqFile = Path.Combine(DataDir, "last_sequence.tsq");

        static readonly Keys[] HotkeyChoices = {
            Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12,
            Keys.Insert, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown, Keys.Pause, Keys.Scroll };
        static readonly string[] HotkeyNames = { "hkMouse", "hkKey", "hkSeq", "hkRec", "hkStop" };
        static readonly string[] HotkeyLabels = { "Mouse clicker", "Keyboard clicker", "Sequence", "Record", "Stop everything" };
        static readonly Keys[] HotkeyDefaults = { Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10 };

        readonly Dictionary<string, Control> ctl = new Dictionary<string, Control>();
        readonly Worker mouseW = new Worker(), keyW = new Worker(), seqW = new Worker();
        long mouseCount, keyCount, seqCount, lastTotal;
        DateTime lastCpsTime = DateTime.Now;
        volatile int seqIndex = -1;
        int shownSeqIndex = -2;
        string overrideStatus;

        List<Step> steps = new List<Step>();
        Panel sidebar, content, statusBar;
        Panel[] pages;
        Button[] navs;
        Button btnMouseStart, btnKeyStart, btnSeqStart, btnRecord, btnStopAll;
        Label lblStatus, lblCount, lblCps, lblCursor, lblTitle;
        KeyBox kbKey, stepKey;
        ListView seqList;
        TextBox stepText;
        System.Windows.Forms.Timer uiTimer;
        bool dark = true;
        bool skipSave;

        // recording
        bool recording;
        IntPtr kbHook = IntPtr.Zero, msHook = IntPtr.Zero;
        Native.HookProc kbProc, msProc;
        readonly Stopwatch recSw = new Stopwatch();
        readonly List<RecEvent> rec = new List<RecEvent>();
        readonly HashSet<uint> downKeys = new HashSet<uint>();
        class RecEvent { public bool IsMouse; public bool Down; public Keys Key; public MouseBtn Btn; public int X, Y; public double T; }

        // theme colours
        Color cBg, cSide, cInput, cFore, cDim, cAccent;
        static readonly Color cGreen = Color.FromArgb(34, 160, 90), cRed = Color.FromArgb(205, 60, 60), cOrange = Color.FromArgb(215, 130, 30);

        public MainForm()
        {
            Text = AppName + " - Auto Clicker";
            ClientSize = new Size(720, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildLayout();
            BuildMousePage(pages[0]);
            BuildKeyboardPage(pages[1]);
            BuildSequencePage(pages[2]);
            BuildSettingsPage(pages[3]);
            ShowPage(0);

            LoadSettings();
            if (!LoadSequence(AutoSeqFile, false)) AddExampleSequence();
            RefreshSeqList();
            UpdateStepEditorEnabled();

            dark = Chk("optDark");
            ApplyTheme();
            TopMost = Chk("optTop");

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 100;
            uiTimer.Tick += delegate { UiTick(); };
            uiTimer.Start();
        }

        // ============================================================== layout helpers
        Label L(Control parent, string text, int x, int y, bool header = false)
        {
            var l = new Label { Text = text, Location = new Point(x, y + (header ? 0 : 3)), AutoSize = true };
            if (header) { l.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold); l.Tag = "header"; }
            parent.Controls.Add(l);
            return l;
        }

        NumericUpDown N(Control parent, string name, int x, int y, int w, int min, int max, int val)
        {
            var n = new NumericUpDown { Name = name, Location = new Point(x, y), Width = w, Minimum = min, Maximum = max, Value = val, ThousandsSeparator = false };
            parent.Controls.Add(n); ctl[name] = n;
            return n;
        }

        CheckBox C(Control parent, string name, string text, int x, int y, bool val)
        {
            var c = new CheckBox { Name = name, Text = text, Location = new Point(x, y + 2), AutoSize = true, Checked = val };
            parent.Controls.Add(c); ctl[name] = c;
            return c;
        }

        RadioButton R(Control parent, string name, string text, int x, int y, int w, bool val)
        {
            var r = new RadioButton { Name = name, Text = text, Location = new Point(x, y + 1), Size = new Size(w, 22), Checked = val };
            parent.Controls.Add(r); ctl[name] = r;
            return r;
        }

        ComboBox CB(Control parent, string name, int x, int y, int w, string[] items, int sel)
        {
            var c = new ComboBox { Name = name, Location = new Point(x, y), Width = w, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            c.Items.AddRange(items); c.SelectedIndex = sel;
            parent.Controls.Add(c); ctl[name] = c;
            return c;
        }

        Button B(Control parent, string text, int x, int y, int w, int h, EventHandler click)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            b.Click += click;
            parent.Controls.Add(b);
            return b;
        }

        Panel Group(Control parent, int x, int y, int w, int h)
        {
            var p = new Panel { Location = new Point(x, y), Size = new Size(w, h) };
            parent.Controls.Add(p);
            return p;
        }

        void IntervalRow(Control parent, string prefix, int y)
        {
            N(parent, prefix + "H", 20, y, 60, 0, 999, 0); L(parent, "hours", 82, y);
            N(parent, prefix + "M", 150, y, 60, 0, 59, 0); L(parent, "mins", 212, y);
            N(parent, prefix + "S", 280, y, 60, 0, 59, 0); L(parent, "secs", 342, y);
            N(parent, prefix + "Ms", 410, y, 70, 0, 999, 2); L(parent, "ms", 482, y);
        }

        double IntervalMs(string prefix)
        {
            double ms = (double)Num(prefix + "H") * 3600000 + (double)Num(prefix + "M") * 60000 + (double)Num(prefix + "S") * 1000 + (double)Num(prefix + "Ms");
            return Math.Max(1, ms);
        }

        decimal Num(string n) { return ((NumericUpDown)ctl[n]).Value; }
        int NumI(string n) { return (int)((NumericUpDown)ctl[n]).Value; }
        bool Chk(string n) { var c = ctl[n]; return c is CheckBox ? ((CheckBox)c).Checked : ((RadioButton)c).Checked; }
        int Sel(string n) { return ((ComboBox)ctl[n]).SelectedIndex; }

        // ============================================================== layout
        void BuildLayout()
        {
            sidebar = new Panel { Location = new Point(0, 0), Size = new Size(140, 520) };
            Controls.Add(sidebar);
            lblTitle = new Label { Text = AppName, Font = new Font("Segoe UI", 14f, FontStyle.Bold), Location = new Point(10, 14), AutoSize = true, Tag = "header" };
            sidebar.Controls.Add(lblTitle);
            var sub = new Label { Text = "auto clicker  v1.0", Location = new Point(13, 44), AutoSize = true, Tag = "dim" };
            sidebar.Controls.Add(sub);

            string[] names = { "  Mouse", "  Keyboard", "  Sequence", "  Settings" };
            navs = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                navs[i] = B(sidebar, names[i], 0, 80 + i * 44, 140, 40, delegate { ShowPage(idx); });
                navs[i].TextAlign = ContentAlignment.MiddleLeft;
                navs[i].FlatAppearance.BorderSize = 0;
                navs[i].Font = new Font("Segoe UI", 10f);
            }
            btnStopAll = B(sidebar, "STOP ALL", 10, 466, 120, 40, delegate { StopAll(); });
            btnStopAll.Tag = "accent";
            btnStopAll.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            content = new Panel { Location = new Point(140, 0), Size = new Size(580, 470) };
            Controls.Add(content);
            pages = new Panel[4];
            for (int i = 0; i < 4; i++)
            {
                pages[i] = new Panel { Location = new Point(0, 0), Size = content.Size, Visible = false };
                content.Controls.Add(pages[i]);
            }

            statusBar = new Panel { Location = new Point(140, 470), Size = new Size(580, 50) };
            Controls.Add(statusBar);
            lblStatus = new Label { Location = new Point(14, 6), Size = new Size(560, 18), Text = "Ready" };
            lblCount = new Label { Location = new Point(14, 26), Size = new Size(420, 18), Tag = "dim" };
            lblCps = new Label { Location = new Point(440, 26), Size = new Size(130, 18), TextAlign = ContentAlignment.TopRight, Tag = "dim" };
            statusBar.Controls.Add(lblStatus); statusBar.Controls.Add(lblCount); statusBar.Controls.Add(lblCps);
        }

        int currentPage;
        void ShowPage(int idx)
        {
            currentPage = idx;
            for (int i = 0; i < pages.Length; i++) pages[i].Visible = i == idx;
            ApplyNavColors();
        }

        void BuildMousePage(Panel p)
        {
            L(p, "Click interval", 20, 15, true);
            IntervalRow(p, "m", 42);
            L(p, "Random extra delay  0 –", 20, 76); N(p, "mJitter", 175, 76, 70, 0, 100000, 0); L(p, "ms (humanize)", 250, 76);

            L(p, "Click options", 20, 112, true);
            L(p, "Button", 20, 139); CB(p, "mBtn", 70, 139, 120, Step.ButtonNames, 0);
            L(p, "Type", 210, 139); CB(p, "mType", 250, 139, 100, new[] { "Single", "Double", "Triple" }, 0);
            L(p, "Hold", 370, 139); N(p, "mHold", 410, 139, 70, 0, 100000, 0); L(p, "ms", 482, 139);

            L(p, "Repeat", 20, 178, true);
            var g = Group(p, 20, 203, 540, 30);
            R(g, "mInf", "Repeat until stopped", 0, 0, 170, true);
            R(g, "mFin", "Repeat", 190, 0, 66, false);
            N(g, "mCount", 260, 0, 90, 1, 100000000, 100); L(g, "times", 355, 0);

            L(p, "Cursor position", 20, 243, true);
            var g2 = Group(p, 20, 268, 540, 30);
            R(g2, "mCur", "Current location", 0, 0, 140, true);
            R(g2, "mFix", "Fixed", 150, 0, 58, false);
            L(g2, "X", 210, 0); N(g2, "mX", 225, 0, 70, -20000, 20000, 0);
            L(g2, "Y", 305, 0); N(g2, "mY", 320, 0, 70, -20000, 20000, 0);
            B(g2, "Pick (3s)", 405, -1, 90, 26, delegate { PickPosition((NumericUpDown)ctl["mX"], (NumericUpDown)ctl["mY"], delegate { ((RadioButton)ctl["mFix"]).Checked = true; }); });
            L(p, "Random offset ±", 20, 306); N(p, "mOffset", 130, 306, 60, 0, 500, 0); L(p, "px (fixed position only)", 195, 306);
            lblCursor = L(p, "Cursor: 0, 0", 400, 306); lblCursor.Tag = "dim";

            btnMouseStart = B(p, "", 20, 390, 540, 54, delegate { ToggleMouse(); });
            btnMouseStart.Tag = "accent";
            btnMouseStart.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        }

        void BuildKeyboardPage(Panel p)
        {
            L(p, "Key to press", 20, 15, true);
            kbKey = new KeyBox { Location = new Point(20, 42), Width = 190 };
            kbKey.Key = Keys.E;
            p.Controls.Add(kbKey);
            C(p, "kCtrl", "Ctrl", 230, 42, false);
            C(p, "kShift", "Shift", 295, 42, false);
            C(p, "kAlt", "Alt", 365, 42, false);
            C(p, "kWin", "Win", 425, 42, false);

            L(p, "Press interval", 20, 82, true);
            IntervalRow(p, "k", 108);
            L(p, "Random extra delay  0 –", 20, 142); N(p, "kJitter", 175, 142, 70, 0, 100000, 0); L(p, "ms", 250, 142);
            L(p, "Hold key", 330, 142); N(p, "kHold", 395, 142, 70, 0, 100000, 0); L(p, "ms", 470, 142);

            L(p, "Repeat", 20, 182, true);
            var g = Group(p, 20, 207, 540, 30);
            R(g, "kInf", "Repeat until stopped", 0, 0, 170, true);
            R(g, "kFin", "Repeat", 190, 0, 66, false);
            N(g, "kCount", 260, 0, 90, 1, 100000000, 100); L(g, "times", 355, 0);

            var tip = L(p, "Tip: for a chain of different keys / clicks use the Sequence tab.\r\nTurn on \"Game mode\" in Settings if a game ignores the key presses.", 20, 255);
            tip.Tag = "dim";

            btnKeyStart = B(p, "", 20, 390, 540, 54, delegate { ToggleKeys(); });
            btnKeyStart.Tag = "accent";
            btnKeyStart.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        }

        void BuildSequencePage(Panel p)
        {
            seqList = new ListView { Location = new Point(10, 10), Size = new Size(560, 200), View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.Nonclickable, BorderStyle = BorderStyle.FixedSingle };
            seqList.Columns.Add("#", 36); seqList.Columns.Add("Action", 105); seqList.Columns.Add("Details", 215);
            seqList.Columns.Add("Hold", 55); seqList.Columns.Add("Delay after", 80); seqList.Columns.Add("Repeat", 50);
            seqList.SelectedIndexChanged += delegate { LoadSelectedIntoEditor(); };
            seqList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveStep(); };
            p.Controls.Add(seqList);

            int y = 218;
            L(p, "Action", 10, y); var t = CB(p, "sType", 60, y, 130, Step.TypeNames, 0);
            t.SelectedIndexChanged += delegate { UpdateStepEditorEnabled(); };
            L(p, "Key", 200, y); stepKey = new KeyBox { Location = new Point(230, y), Width = 120 }; stepKey.Key = Keys.A; p.Controls.Add(stepKey);
            C(p, "sCtrl", "Ctrl", 360, y, false); C(p, "sShift", "Shift", 415, y, false); C(p, "sAlt", "Alt", 478, y, false); C(p, "sWin", "Win", 525, y, false);
            ctl["sKey"] = stepKey;

            y = 248;
            L(p, "Button", 10, y); CB(p, "sBtn", 60, y, 130, Step.ButtonNames, 0);
            C(p, "sAtCursor", "At cursor", 200, y, true).CheckedChanged += delegate { UpdateStepEditorEnabled(); };
            L(p, "X", 285, y); N(p, "sX", 298, y, 65, -20000, 20000, 0);
            L(p, "Y", 370, y); N(p, "sY", 383, y, 65, -20000, 20000, 0);
            ctl["sPick"] = B(p, "Pick (3s)", 458, y - 1, 112, 26, delegate { PickPosition((NumericUpDown)ctl["sX"], (NumericUpDown)ctl["sY"], delegate { ((CheckBox)ctl["sAtCursor"]).Checked = false; }); });

            y = 278;
            L(p, "Text", 10, y); stepText = new TextBox { Location = new Point(60, y), Width = 290 }; p.Controls.Add(stepText); ctl["sText"] = stepText;
            L(p, "Scroll", 360, y); N(p, "sAmount", 405, y, 60, -100, 100, 1); L(p, "(+ up / - down)", 470, y).Tag = "dim";

            y = 308;
            L(p, "Hold ms", 10, y); N(p, "sHold", 65, y, 70, 0, 1000000, 0);
            L(p, "Delay after ms", 150, y); N(p, "sDelay", 245, y, 80, 0, 100000000, 2);
            L(p, "Repeat", 340, y); N(p, "sRepeat", 390, y, 70, 1, 1000000, 1);

            y = 342;
            string[] bt = { "Add", "Update", "Remove", "Move ▲", "Move ▼", "Duplicate", "Clear" };
            EventHandler[] bh = {
                delegate { AddStep(); }, delegate { UpdateStep(); }, delegate { RemoveStep(); },
                delegate { MoveStep(-1); }, delegate { MoveStep(1); }, delegate { DuplicateStep(); }, delegate { ClearSteps(); } };
            for (int i = 0; i < bt.Length; i++) B(p, bt[i], 10 + i * 80, y, 76, 28, bh[i]);

            y = 378;
            var g = Group(p, 10, y, 330, 28);
            R(g, "sLoopInf", "Loop forever", 0, 0, 100, true);
            R(g, "sLoopFin", "Run", 105, 0, 48, false);
            N(g, "sLoops", 155, 0, 80, 1, 100000000, 1); L(g, "times", 240, 0);
            L(p, "Gap between loops", 340, y); N(p, "sLoopGap", 460, y, 75, 0, 100000000, 0); L(p, "ms", 538, y);

            y = 418;
            btnSeqStart = B(p, "", 10, y, 270, 44, delegate { ToggleSequence(); });
            btnSeqStart.Tag = "accent"; btnSeqStart.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnRecord = B(p, "", 288, y, 130, 44, delegate { ToggleRecording(); });
            btnRecord.Tag = "accent";
            B(p, "Save…", 426, y, 70, 44, delegate { SaveSequenceDialog(); });
            B(p, "Load…", 500, y, 70, 44, delegate { LoadSequenceDialog(); });
        }

        void BuildSettingsPage(Panel p)
        {
            L(p, "Global hotkeys (work even when the window isn't focused)", 20, 15, true);
            string[] hkItems = new string[HotkeyChoices.Length];
            for (int i = 0; i < hkItems.Length; i++) hkItems[i] = Inj.KeyName(HotkeyChoices[i]);
            for (int i = 0; i < HotkeyNames.Length; i++)
            {
                int col = i % 2, row = i / 2;
                int x = 20 + col * 275, y = 45 + row * 32;
                L(p, HotkeyLabels[i], x, y);
                CB(p, HotkeyNames[i], x + 120, y, 110, hkItems, Array.IndexOf(HotkeyChoices, HotkeyDefaults[i]));
            }
            B(p, "Apply hotkeys", 295, 108, 230, 26, delegate { RegisterHotkeys(true); });

            L(p, "Behaviour", 20, 150, true);
            C(p, "optTop", "Always on top", 20, 176, false).CheckedChanged += delegate { TopMost = Chk("optTop"); };
            C(p, "optDark", "Dark theme", 200, 176, true).CheckedChanged += delegate { dark = Chk("optDark"); ApplyTheme(); };
            C(p, "optBeep", "Beep on start / stop", 380, 176, true);
            C(p, "optGame", "Game mode (scan codes)", 20, 204, false);
            C(p, "optMin", "Minimize when started", 200, 204, false);
            C(p, "optRecTiming", "Record real timing", 380, 204, true);
            C(p, "optRemember", "Remember settings", 20, 232, true);
            C(p, "optRecMouse", "Record mouse clicks", 200, 232, true);

            L(p, "Start delay", 20, 268); N(p, "optDelay", 100, 268, 60, 0, 3600, 0); L(p, "sec", 165, 268);
            L(p, "Auto-stop after", 230, 268); N(p, "optLimit", 335, 268, 80, 0, 10000000, 0); L(p, "sec (0 = never)", 420, 268);

            L(p, "About", 20, 310, true);
            var about = L(p,
                "TsafeclickJ v1.0 — default speed is 2 ms per click (~500 clicks per second).\r\n" +
                "Sequence: build a list of key presses, clicks, text and waits, or hit Record.\r\n" +
                "Panic button: the \"Stop everything\" hotkey (default F10) stops all clickers.\r\n" +
                "Settings save to %APPDATA%\\TsafeclickJ.", 20, 335);
            about.Tag = "dim";

            B(p, "Reset counters", 20, 420, 140, 30, delegate { Interlocked.Exchange(ref mouseCount, 0); Interlocked.Exchange(ref keyCount, 0); Interlocked.Exchange(ref seqCount, 0); lastTotal = 0; });
            B(p, "Reset all settings", 170, 420, 150, 30, delegate { ResetSettings(); });
        }

        // ============================================================== theme
        void ApplyTheme()
        {
            if (dark)
            {
                cBg = Color.FromArgb(26, 27, 33); cSide = Color.FromArgb(17, 18, 22); cInput = Color.FromArgb(42, 44, 54);
                cFore = Color.FromArgb(232, 233, 238); cDim = Color.FromArgb(150, 153, 165); cAccent = Color.FromArgb(70, 175, 255);
            }
            else
            {
                cBg = Color.FromArgb(246, 247, 250); cSide = Color.FromArgb(226, 230, 238); cInput = Color.White;
                cFore = Color.FromArgb(25, 26, 30); cDim = Color.FromArgb(95, 98, 110); cAccent = Color.FromArgb(0, 110, 210);
            }
            BackColor = cBg;
            ThemeTree(this);
            sidebar.BackColor = cSide;
            statusBar.BackColor = cSide;
            ApplyNavColors();
            shownSeqIndex = -2;
            UiTick();
        }

        void ThemeTree(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                string tag = c.Tag as string;
                if (c is Panel) { c.BackColor = cBg; c.ForeColor = cFore; }
                else if (c is Label) { c.BackColor = Color.Transparent; c.ForeColor = tag == "header" ? cAccent : tag == "dim" ? cDim : cFore; }
                else if (c is Button)
                {
                    var b = (Button)c;
                    if (tag != "accent") { b.BackColor = cInput; b.ForeColor = cFore; }
                    b.FlatAppearance.BorderColor = dark ? Color.FromArgb(70, 72, 85) : Color.FromArgb(190, 195, 205);
                }
                else if (c is TextBox || c is NumericUpDown || c is ComboBox || c is ListView) { c.BackColor = cInput; c.ForeColor = cFore; }
                else { c.BackColor = cBg; c.ForeColor = cFore; }
                if (c.HasChildren && !(c is NumericUpDown)) ThemeTree(c);
            }
        }

        void ApplyNavColors()
        {
            if (navs == null) return;
            for (int i = 0; i < navs.Length; i++)
            {
                bool on = i == currentPage;
                navs[i].BackColor = on ? cBg : cSide;
                navs[i].ForeColor = on ? cAccent : cFore;
                navs[i].FlatAppearance.MouseOverBackColor = cBg;
            }
        }

        void StyleToggle(Button b, bool running, string idleText, string runText, Color idleColor)
        {
            string text = running ? runText : idleText;
            if (b.Text != text) b.Text = text;
            Color col = running ? cRed : idleColor;
            if (b.BackColor != col) { b.BackColor = col; b.ForeColor = Color.White; }
        }

        string HotkeyName(int i)
        {
            int s = Sel(HotkeyNames[i]);
            return s >= 0 ? Inj.KeyName(HotkeyChoices[s]) : "?";
        }

        // ============================================================== periodic UI refresh
        void UiTick()
        {
            if (btnMouseStart == null) return;
            KeyBox.Locked = mouseW.Running || keyW.Running || seqW.Running;
            StyleToggle(btnMouseStart, mouseW.Running, "▶  Start mouse clicker  (" + HotkeyName(0) + ")", "■  Stop mouse clicker  (" + HotkeyName(0) + ")", cGreen);
            StyleToggle(btnKeyStart, keyW.Running, "▶  Start keyboard clicker  (" + HotkeyName(1) + ")", "■  Stop keyboard clicker  (" + HotkeyName(1) + ")", cGreen);
            StyleToggle(btnSeqStart, seqW.Running, "▶  Run sequence  (" + HotkeyName(2) + ")", "■  Stop sequence  (" + HotkeyName(2) + ")", cGreen);
            StyleToggle(btnRecord, recording, "●  Record  (" + HotkeyName(3) + ")", "■  Stop rec  (" + HotkeyName(3) + ")", cOrange);
            StyleToggle(btnStopAll, false, "STOP ALL  (" + HotkeyName(4) + ")", "", cRed);

            var pos = Cursor.Position;
            lblCursor.Text = "Cursor: " + pos.X + ", " + pos.Y;

            if (overrideStatus != null) lblStatus.Text = overrideStatus;
            else if (recording) lblStatus.Text = "● Recording… " + rec.Count + " events captured. Press " + HotkeyName(3) + " to stop.";
            else
            {
                var parts = new List<string>();
                if (mouseW.Running) parts.Add("Mouse clicker running");
                if (keyW.Running) parts.Add("Keyboard clicker running");
                if (seqW.Running) parts.Add("Sequence running" + (seqIndex >= 0 ? " (step " + (seqIndex + 1) + ")" : ""));
                lblStatus.Text = parts.Count == 0 ? "Ready — " + HotkeyName(4) + " stops everything" : string.Join("  •  ", parts.ToArray());
            }

            long m = Interlocked.Read(ref mouseCount), k = Interlocked.Read(ref keyCount), s = Interlocked.Read(ref seqCount);
            lblCount.Text = "Clicks: " + m.ToString("N0") + "    Key presses: " + k.ToString("N0") + "    Sequence steps: " + s.ToString("N0");
            var now = DateTime.Now;
            double dt = (now - lastCpsTime).TotalSeconds;
            if (dt >= 0.5)
            {
                long total = m + k + s;
                double cps = Math.Max(0, (total - lastTotal) / dt);
                lblCps.Text = cps.ToString("N0") + " actions/sec";
                lastTotal = total; lastCpsTime = now;
            }

            int si = seqW.Running ? seqIndex : -1;
            if (si != shownSeqIndex)
            {
                shownSeqIndex = si;
                for (int i = 0; i < seqList.Items.Count; i++)
                {
                    var it = seqList.Items[i];
                    it.BackColor = i == si ? (dark ? Color.FromArgb(30, 90, 60) : Color.FromArgb(190, 240, 205)) : cInput;
                    it.ForeColor = cFore;
                }
            }
        }

        // ============================================================== run helpers
        class RunOpts { public double StartDelayMs, LimitMs; public bool Beep; }

        RunOpts Opts()
        {
            Inj.GameMode = Chk("optGame");
            return new RunOpts { StartDelayMs = (double)Num("optDelay") * 1000, LimitMs = (double)Num("optLimit") * 1000, Beep = Chk("optBeep") };
        }

        static void Beep(bool on, bool enabled)
        {
            if (!enabled) return;
            ThreadPool.QueueUserWorkItem(_ => { try { Console.Beep(on ? 1250 : 650, 70); } catch { } });
        }

        void AfterStart() { if (Chk("optMin")) WindowState = FormWindowState.Minimized; }

        // Runs body after the start delay, enforcing the auto-stop limit through the returned deadline.
        void Launch(Worker w, RunOpts o, Action<Worker, double> body)
        {
            w.Start(wk =>
            {
                if (!wk.Wait(o.StartDelayMs)) return;
                Beep(true, o.Beep);
                double deadline = o.LimitMs > 0 ? wk.NowMs + o.LimitMs : double.MaxValue;
                body(wk, deadline);
            }, () => Beep(false, o.Beep));
            AfterStart();
        }

        // ============================================================== mouse clicker
        void ToggleMouse()
        {
            if (mouseW.Running) { mouseW.Stop(); return; }
            double interval = IntervalMs("m"), jitter = (double)Num("mJitter");
            var btn = (MouseBtn)Math.Max(0, Sel("mBtn"));
            int clicks = Sel("mType") + 1, hold = NumI("mHold"), offset = NumI("mOffset");
            bool infinite = Chk("mInf"), fixedPos = Chk("mFix");
            long count = (long)Num("mCount");
            int fx = NumI("mX"), fy = NumI("mY");

            Launch(mouseW, Opts(), (w, deadline) =>
            {
                var rnd = new Random();
                double next = w.NowMs;
                long done = 0;
                while (!w.StopRequested)
                {
                    if (fixedPos) Inj.MoveTo(fx + (offset > 0 ? rnd.Next(-offset, offset + 1) : 0), fy + (offset > 0 ? rnd.Next(-offset, offset + 1) : 0));
                    for (int c = 0; c < clicks; c++)
                    {
                        Inj.MouseDown(btn);
                        if (hold > 0) w.Wait(hold);
                        Inj.MouseUp(btn);
                        Interlocked.Increment(ref mouseCount);
                    }
                    done++;
                    if (!infinite && done >= count) break;
                    if (w.NowMs >= deadline) break;
                    next += interval + (jitter > 0 ? rnd.NextDouble() * jitter : 0);
                    double now = w.NowMs;
                    if (now - next > 100) next = now; // fell far behind (e.g. system stall): don't burst to catch up
                    if (!w.WaitUntil(Math.Min(next, deadline))) break;
                }
            });
        }

        // ============================================================== keyboard clicker
        void ToggleKeys()
        {
            if (keyW.Running) { keyW.Stop(); return; }
            Keys key = kbKey.Key;
            if (key == Keys.None) { MessageBox.Show(this, "Click the key box and press the key you want spammed.", AppName); return; }
            int mods = (Chk("kCtrl") ? 1 : 0) | (Chk("kShift") ? 2 : 0) | (Chk("kAlt") ? 4 : 0) | (Chk("kWin") ? 8 : 0);
            double interval = IntervalMs("k"), jitter = (double)Num("kJitter");
            int hold = NumI("kHold");
            bool infinite = Chk("kInf");
            long count = (long)Num("kCount");

            Launch(keyW, Opts(), (w, deadline) =>
            {
                var rnd = new Random();
                double next = w.NowMs;
                long done = 0;
                while (!w.StopRequested)
                {
                    Inj.ModsDown(mods);
                    Inj.KeyDown(key);
                    if (hold > 0) w.Wait(hold);
                    Inj.KeyUp(key);
                    Inj.ModsUp(mods);
                    Interlocked.Increment(ref keyCount);
                    done++;
                    if (!infinite && done >= count) break;
                    if (w.NowMs >= deadline) break;
                    next += interval + (jitter > 0 ? rnd.NextDouble() * jitter : 0);
                    double now = w.NowMs;
                    if (now - next > 100) next = now;
                    if (!w.WaitUntil(Math.Min(next, deadline))) break;
                }
            });
        }

        // ============================================================== sequence runner
        void ToggleSequence()
        {
            if (seqW.Running) { seqW.Stop(); return; }
            if (recording) StopRecording();
            if (steps.Count == 0) { MessageBox.Show(this, "The sequence is empty. Add some steps first.", AppName); return; }
            var list = new List<Step>();
            foreach (var s in steps) list.Add(s.Clone());
            bool infinite = Chk("sLoopInf");
            long loops = (long)Num("sLoops");
            double gap = (double)Num("sLoopGap");

            Launch(seqW, Opts(), (w, deadline) =>
            {
                var heldKeys = new HashSet<Keys>();
                var heldBtns = new HashSet<MouseBtn>();
                try
                {
                    long loop = 0;
                    while (!w.StopRequested)
                    {
                        for (int i = 0; i < list.Count && !w.StopRequested; i++)
                        {
                            seqIndex = i;
                            var s = list[i];
                            for (int r = 0; r < Math.Max(1, s.Repeat); r++)
                            {
                                RunStep(w, s, heldKeys, heldBtns);
                                Interlocked.Increment(ref seqCount);
                                if (w.NowMs >= deadline || !w.Wait(s.DelayMs)) return;
                            }
                        }
                        loop++;
                        if (!infinite && loop >= loops) break;
                        if (!w.Wait(gap)) break;
                    }
                }
                finally
                {
                    // never leave keys or buttons stuck down
                    foreach (var k in heldKeys) Inj.KeyUp(k);
                    foreach (var b in heldBtns) Inj.MouseUp(b);
                    seqIndex = -1;
                }
            });
        }

        static void RunStep(Worker w, Step s, HashSet<Keys> heldKeys, HashSet<MouseBtn> heldBtns)
        {
            if (s.IsMouseButton && !s.AtCursor) Inj.MoveTo(s.X, s.Y);
            switch (s.Type)
            {
                case StepType.KeyPress:
                    Inj.ModsDown(s.Mods); Inj.KeyDown(s.Key);
                    if (s.HoldMs > 0) w.Wait(s.HoldMs);
                    Inj.KeyUp(s.Key); Inj.ModsUp(s.Mods);
                    break;
                case StepType.KeyDown:
                    Inj.ModsDown(s.Mods); Inj.KeyDown(s.Key);
                    heldKeys.Add(s.Key);
                    for (int b = 0; b < 4; b++) if ((s.Mods & (1 << b)) != 0) heldKeys.Add(Inj.ModKeys[b]);
                    break;
                case StepType.KeyUp:
                    Inj.KeyUp(s.Key); Inj.ModsUp(s.Mods);
                    heldKeys.Remove(s.Key);
                    for (int b = 0; b < 4; b++) if ((s.Mods & (1 << b)) != 0) heldKeys.Remove(Inj.ModKeys[b]);
                    break;
                case StepType.MouseClick:
                    Inj.MouseDown(s.Button);
                    if (s.HoldMs > 0) w.Wait(s.HoldMs);
                    Inj.MouseUp(s.Button);
                    break;
                case StepType.MouseDown: Inj.MouseDown(s.Button); heldBtns.Add(s.Button); break;
                case StepType.MouseUp: Inj.MouseUp(s.Button); heldBtns.Remove(s.Button); break;
                case StepType.MoveMouse: Inj.MoveTo(s.X, s.Y); break;
                case StepType.Scroll: Inj.Wheel(s.Amount); break;
                case StepType.TypeText: Inj.TypeText(s.Text ?? ""); break;
                case StepType.Wait: if (s.HoldMs > 0) w.Wait(s.HoldMs); break;
            }
        }

        void StopAll()
        {
            mouseW.Stop(); keyW.Stop(); seqW.Stop();
            if (recording) StopRecording();
        }

        // ============================================================== sequence editor
        Step StepFromEditor()
        {
            var s = new Step();
            s.Type = (StepType)Math.Max(0, Sel("sType"));
            s.Key = stepKey.Key;
            s.Mods = (Chk("sCtrl") ? 1 : 0) | (Chk("sShift") ? 2 : 0) | (Chk("sAlt") ? 4 : 0) | (Chk("sWin") ? 8 : 0);
            s.Button = (MouseBtn)Math.Max(0, Sel("sBtn"));
            s.AtCursor = Chk("sAtCursor");
            s.X = NumI("sX"); s.Y = NumI("sY");
            s.Text = stepText.Text;
            s.Amount = NumI("sAmount");
            s.HoldMs = NumI("sHold"); s.DelayMs = NumI("sDelay"); s.Repeat = NumI("sRepeat");
            if (s.IsKey && s.Key == Keys.None) { MessageBox.Show(this, "Click the Key box and press a key first.", AppName); return null; }
            if (s.Type == StepType.Scroll && s.Amount == 0) s.Amount = 1;
            return s;
        }

        void LoadSelectedIntoEditor()
        {
            int i = SelectedStep();
            if (i < 0) return;
            var s = steps[i];
            ((ComboBox)ctl["sType"]).SelectedIndex = (int)s.Type;
            if (s.Key != Keys.None) stepKey.Key = s.Key;
            ((CheckBox)ctl["sCtrl"]).Checked = (s.Mods & 1) != 0;
            ((CheckBox)ctl["sShift"]).Checked = (s.Mods & 2) != 0;
            ((CheckBox)ctl["sAlt"]).Checked = (s.Mods & 4) != 0;
            ((CheckBox)ctl["sWin"]).Checked = (s.Mods & 8) != 0;
            ((ComboBox)ctl["sBtn"]).SelectedIndex = (int)s.Button;
            ((CheckBox)ctl["sAtCursor"]).Checked = s.AtCursor;
            SetNum("sX", s.X); SetNum("sY", s.Y);
            stepText.Text = s.Text;
            SetNum("sAmount", s.Amount); SetNum("sHold", s.HoldMs); SetNum("sDelay", s.DelayMs); SetNum("sRepeat", s.Repeat);
        }

        void SetNum(string name, decimal v)
        {
            var n = (NumericUpDown)ctl[name];
            n.Value = Math.Min(n.Maximum, Math.Max(n.Minimum, v));
        }

        void UpdateStepEditorEnabled()
        {
            var t = (StepType)Math.Max(0, Sel("sType"));
            var s = new Step { Type = t };
            bool key = s.IsKey, btn = s.IsMouseButton, pos = btn || t == StepType.MoveMouse;
            bool atCur = btn && Chk("sAtCursor");
            stepKey.Enabled = key;
            foreach (var n in new[] { "sCtrl", "sShift", "sAlt", "sWin" }) ctl[n].Enabled = key;
            ctl["sBtn"].Enabled = btn;
            ctl["sAtCursor"].Enabled = btn;
            ctl["sX"].Enabled = ctl["sY"].Enabled = ctl["sPick"].Enabled = pos && !atCur;
            stepText.Enabled = t == StepType.TypeText;
            ctl["sAmount"].Enabled = t == StepType.Scroll;
            ctl["sHold"].Enabled = t == StepType.KeyPress || t == StepType.MouseClick || t == StepType.Wait;
        }

        int SelectedStep() { return seqList.SelectedIndices.Count > 0 ? seqList.SelectedIndices[0] : -1; }

        void RefreshSeqList(int select = -1)
        {
            seqList.BeginUpdate();
            seqList.Items.Clear();
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                bool showHold = s.Type == StepType.KeyPress || s.Type == StepType.MouseClick || s.Type == StepType.Wait;
                var it = new ListViewItem(new[] { (i + 1).ToString(), Step.TypeNames[(int)s.Type], s.Details(), showHold ? s.HoldMs + " ms" : "", s.DelayMs + " ms", "×" + s.Repeat });
                it.BackColor = cInput; it.ForeColor = cFore;
                seqList.Items.Add(it);
            }
            if (select >= 0 && select < steps.Count) { seqList.Items[select].Selected = true; seqList.EnsureVisible(select); }
            seqList.EndUpdate();
            shownSeqIndex = -2;
        }

        void AddStep()
        {
            var s = StepFromEditor(); if (s == null) return;
            int i = SelectedStep();
            int at = i >= 0 ? i + 1 : steps.Count;
            steps.Insert(at, s);
            RefreshSeqList(at);
        }

        void UpdateStep()
        {
            int i = SelectedStep(); if (i < 0) return;
            var s = StepFromEditor(); if (s == null) return;
            steps[i] = s; RefreshSeqList(i);
        }

        void RemoveStep()
        {
            int i = SelectedStep(); if (i < 0) return;
            steps.RemoveAt(i); RefreshSeqList(Math.Min(i, steps.Count - 1));
        }

        void MoveStep(int d)
        {
            int i = SelectedStep(), j = i + d;
            if (i < 0 || j < 0 || j >= steps.Count) return;
            var t = steps[i]; steps[i] = steps[j]; steps[j] = t;
            RefreshSeqList(j);
        }

        void DuplicateStep()
        {
            int i = SelectedStep(); if (i < 0) return;
            steps.Insert(i + 1, steps[i].Clone()); RefreshSeqList(i + 1);
        }

        void ClearSteps()
        {
            if (steps.Count == 0) return;
            if (MessageBox.Show(this, "Remove all " + steps.Count + " steps?", AppName, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            steps.Clear(); RefreshSeqList();
        }

        void AddExampleSequence()
        {
            foreach (var k in new[] { Keys.A, Keys.S, Keys.D })
                steps.Add(new Step { Type = StepType.KeyPress, Key = k, DelayMs = 2 });
            steps.Add(new Step { Type = StepType.MouseClick, Button = MouseBtn.Left, DelayMs = 2 });
        }

        // ============================================================== sequence files
        bool SaveSequence(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                var sb = new StringBuilder("TSAFECLICKJ-SEQ 1\r\n");
                foreach (var s in steps) sb.Append(s.Serialize()).Append("\r\n");
                File.WriteAllText(path, sb.ToString());
                return true;
            }
            catch { return false; }
        }

        bool LoadSequence(string path, bool showErrors)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var lines = File.ReadAllLines(path);
                if (lines.Length == 0 || !lines[0].StartsWith("TSAFECLICKJ-SEQ")) throw new InvalidDataException("Not a TsafeclickJ sequence file.");
                var list = new List<Step>();
                for (int i = 1; i < lines.Length; i++)
                {
                    if (lines[i].Trim().Length == 0) continue;
                    var s = Step.Parse(lines[i]);
                    if (s != null) list.Add(s);
                }
                steps = list;
                return true;
            }
            catch (Exception ex)
            {
                if (showErrors) MessageBox.Show(this, "Could not load sequence:\r\n" + ex.Message, AppName);
                return false;
            }
        }

        void SaveSequenceDialog()
        {
            using (var d = new SaveFileDialog { Filter = "TsafeclickJ sequence (*.tsq)|*.tsq|All files|*.*", FileName = "sequence.tsq" })
                if (d.ShowDialog(this) == DialogResult.OK && !SaveSequence(d.FileName)) MessageBox.Show(this, "Could not save the file.", AppName);
        }

        void LoadSequenceDialog()
        {
            using (var d = new OpenFileDialog { Filter = "TsafeclickJ sequence (*.tsq)|*.tsq|All files|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK && LoadSequence(d.FileName, true)) RefreshSeqList();
        }

        // ============================================================== pick position
        System.Windows.Forms.Timer pickTimer;
        void PickPosition(NumericUpDown nx, NumericUpDown ny, Action after)
        {
            if (pickTimer != null) return;
            int left = 3;
            overrideStatus = "Move your mouse to the spot… " + left;
            pickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            pickTimer.Tick += delegate
            {
                left--;
                if (left > 0) { overrideStatus = "Move your mouse to the spot… " + left; return; }
                pickTimer.Stop(); pickTimer.Dispose(); pickTimer = null;
                var p = Cursor.Position;
                nx.Value = Math.Min(nx.Maximum, Math.Max(nx.Minimum, p.X));
                ny.Value = Math.Min(ny.Maximum, Math.Max(ny.Minimum, p.Y));
                if (after != null) after();
                overrideStatus = null;
                lblStatus.Text = "Position picked: " + p.X + ", " + p.Y;
                Activate();
            };
            pickTimer.Start();
        }

        // ============================================================== recording
        void ToggleRecording() { if (recording) StopRecording(); else StartRecording(); }

        void StartRecording()
        {
            if (seqW.Running) seqW.Stop();
            rec.Clear(); downKeys.Clear();
            kbProc = KbHook; msProc = MsHook;
            IntPtr mod = Native.GetModuleHandle(null);
            kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, kbProc, mod, 0);
            if (Chk("optRecMouse")) msHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, msProc, mod, 0);
            if (kbHook == IntPtr.Zero) { MessageBox.Show(this, "Could not start recording (keyboard hook failed).", AppName); return; }
            recSw.Restart();
            recording = true;
            ShowPage(2);
        }

        void StopRecording()
        {
            if (!recording) return;
            recording = false;
            if (kbHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(kbHook); kbHook = IntPtr.Zero; }
            if (msHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(msHook); msHook = IntPtr.Zero; }
            int added = ConvertRecording();
            lblStatus.Text = "Recording added " + added + " steps.";
        }

        bool IsHotkey(Keys k)
        {
            for (int i = 0; i < HotkeyNames.Length; i++)
            {
                int s = Sel(HotkeyNames[i]);
                if (s >= 0 && HotkeyChoices[s] == k) return true;
            }
            return false;
        }

        IntPtr KbHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && recording)
            {
                var d = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                bool injected = (d.flags & 0x10) != 0;
                Keys k = (Keys)d.vkCode;
                if (!injected && !IsHotkey(k))
                {
                    bool down = msg == 0x100 || msg == 0x104, up = msg == 0x101 || msg == 0x105;
                    if (down && downKeys.Add(d.vkCode)) rec.Add(new RecEvent { Down = true, Key = k, T = recSw.Elapsed.TotalMilliseconds });
                    else if (up) { downKeys.Remove(d.vkCode); rec.Add(new RecEvent { Down = false, Key = k, T = recSw.Elapsed.TotalMilliseconds }); }
                }
            }
            return Native.CallNextHookEx(kbHook, code, wParam, lParam);
        }

        IntPtr MsHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && recording)
            {
                var d = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                bool injected = (d.flags & 0x1) != 0;
                // ignore clicks on our own window (e.g. pressing "Stop rec")
                if (!injected && !Bounds.Contains(d.pt.X, d.pt.Y))
                {
                    MouseBtn b = MouseBtn.Left; bool down = false, valid = true;
                    switch (msg)
                    {
                        case 0x201: b = MouseBtn.Left; down = true; break;
                        case 0x202: b = MouseBtn.Left; break;
                        case 0x204: b = MouseBtn.Right; down = true; break;
                        case 0x205: b = MouseBtn.Right; break;
                        case 0x207: b = MouseBtn.Middle; down = true; break;
                        case 0x208: b = MouseBtn.Middle; break;
                        case 0x20B: b = ((d.mouseData >> 16) & 0xFFFF) == 1 ? MouseBtn.X1 : MouseBtn.X2; down = true; break;
                        case 0x20C: b = ((d.mouseData >> 16) & 0xFFFF) == 1 ? MouseBtn.X1 : MouseBtn.X2; break;
                        default: valid = false; break;
                    }
                    if (valid) rec.Add(new RecEvent { IsMouse = true, Down = down, Btn = b, X = d.pt.X, Y = d.pt.Y, T = recSw.Elapsed.TotalMilliseconds });
                }
            }
            return Native.CallNextHookEx(msHook, code, wParam, lParam);
        }

        int ConvertRecording()
        {
            bool timing = Chk("optRecTiming");
            int defDelay = NumI("sDelay");
            var outSteps = new List<Step>();
            for (int i = 0; i < rec.Count; i++)
            {
                var e = rec[i];
                var s = new Step();
                int consumed = i;
                bool pair = e.Down && i + 1 < rec.Count && rec[i + 1].IsMouse == e.IsMouse && !rec[i + 1].Down &&
                            (e.IsMouse ? rec[i + 1].Btn == e.Btn : rec[i + 1].Key == e.Key);
                if (e.IsMouse)
                {
                    s.Button = e.Btn; s.AtCursor = false; s.X = e.X; s.Y = e.Y;
                    s.Type = pair ? StepType.MouseClick : e.Down ? StepType.MouseDown : StepType.MouseUp;
                }
                else
                {
                    s.Key = e.Key;
                    s.Type = pair ? StepType.KeyPress : e.Down ? StepType.KeyDown : StepType.KeyUp;
                }
                if (pair) { consumed = i + 1; s.HoldMs = timing ? (int)Math.Round(rec[i + 1].T - e.T) : 0; }
                double endT = rec[consumed].T;
                s.DelayMs = timing && consumed + 1 < rec.Count ? (int)Math.Round(rec[consumed + 1].T - endT) : defDelay;
                outSteps.Add(s);
                i = consumed;
            }
            int at = SelectedStep() >= 0 ? SelectedStep() + 1 : steps.Count;
            steps.InsertRange(at, outSteps);
            RefreshSeqList(outSteps.Count > 0 ? at + outSteps.Count - 1 : -1);
            return outSteps.Count;
        }

        // ============================================================== hotkeys
        void RegisterHotkeys(bool report)
        {
            if (!IsHandleCreated) return;
            var failed = new List<string>();
            for (int i = 0; i < HotkeyNames.Length; i++)
            {
                Native.UnregisterHotKey(Handle, i + 1);
                int s = Sel(HotkeyNames[i]);
                if (s < 0) continue;
                if (!Native.RegisterHotKey(Handle, i + 1, Native.MOD_NOREPEAT, (uint)HotkeyChoices[s])) failed.Add(Inj.KeyName(HotkeyChoices[s]));
            }
            if (failed.Count > 0) lblStatus.Text = "Hotkey already in use by another program: " + string.Join(", ", failed.ToArray());
            else if (report) lblStatus.Text = "Hotkeys applied.";
            UiTick();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { RegisterHotkeys(false); }
            catch (Exception ex) { lblStatus.Text = "Hotkeys unavailable: " + ex.Message; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case 1: ToggleMouse(); break;
                    case 2: ToggleKeys(); break;
                    case 3: ToggleSequence(); break;
                    case 4: ToggleRecording(); break;
                    case 5: StopAll(); break;
                }
                return;
            }
            base.WndProc(ref m);
        }

        // ============================================================== settings persistence
        IEnumerable<Control> AllControls(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                yield return c;
                foreach (var cc in AllControls(c)) yield return cc;
            }
        }

        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                var sb = new StringBuilder();
                foreach (var c in AllControls(this))
                {
                    if (string.IsNullOrEmpty(c.Name)) continue;
                    string v = null;
                    if (c is NumericUpDown) v = ((NumericUpDown)c).Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    else if (c is CheckBox) v = ((CheckBox)c).Checked ? "1" : "0";
                    else if (c is RadioButton) v = ((RadioButton)c).Checked ? "1" : "0";
                    else if (c is ComboBox) v = ((ComboBox)c).SelectedIndex.ToString();
                    if (v != null) sb.Append(c.Name).Append('=').Append(v).Append("\r\n");
                }
                sb.Append("kbKey=").Append((int)kbKey.Key).Append("\r\n");
                File.WriteAllText(SettingsFile, sb.ToString());
                SaveSequence(AutoSeqFile);
            }
            catch { }
        }

        void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;
                var map = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(SettingsFile))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) map[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
                string rem;
                if (map.TryGetValue("optRemember", out rem) && rem == "0") { ((CheckBox)ctl["optRemember"]).Checked = false; return; }
                foreach (var c in AllControls(this))
                {
                    string v;
                    if (string.IsNullOrEmpty(c.Name) || !map.TryGetValue(c.Name, out v)) continue;
                    try
                    {
                        if (c is NumericUpDown)
                        {
                            var n = (NumericUpDown)c;
                            n.Value = Math.Min(n.Maximum, Math.Max(n.Minimum, decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture)));
                        }
                        else if (c is CheckBox) ((CheckBox)c).Checked = v == "1";
                        else if (c is RadioButton) { if (v == "1") ((RadioButton)c).Checked = true; }
                        else if (c is ComboBox)
                        {
                            var cb = (ComboBox)c; int idx = int.Parse(v);
                            if (idx >= 0 && idx < cb.Items.Count) cb.SelectedIndex = idx;
                        }
                    }
                    catch { }
                }
                string kk;
                if (map.TryGetValue("kbKey", out kk)) { int k; if (int.TryParse(kk, out k) && k != 0) kbKey.Key = (Keys)k; }
            }
            catch { }
        }

        void ResetSettings()
        {
            if (MessageBox.Show(this, "Reset all settings to defaults? (Your sequence is kept.)", AppName, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            StopAll();
            try { File.Delete(SettingsFile); } catch { }
            SaveSequence(AutoSeqFile);
            skipSave = true;
            MessageBox.Show(this, "Settings reset. Open TsafeclickJ again to use the defaults.", AppName);
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopAll();
            try { for (int i = 0; i < HotkeyNames.Length; i++) Native.UnregisterHotKey(Handle, i + 1); } catch { }
            if (skipSave) { }
            else if (Chk("optRemember")) SaveSettings();
            else
            {
                try { if (File.Exists(SettingsFile)) File.WriteAllText(SettingsFile, "optRemember=0\r\n"); } catch { }
                SaveSequence(AutoSeqFile);
            }
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "TsafeclickJ_single_instance", out created))
            {
                if (!created) { MessageBox.Show("TsafeclickJ is already running.", "TsafeclickJ"); return; }
                try { Native.timeBeginPeriod(1); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                try { Native.timeEndPeriod(1); } catch { }
            }
        }
    }
}
