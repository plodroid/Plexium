using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PlexiumAutoClicker;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly Color Bg = Color.FromArgb(27, 27, 24);
    private readonly Color Surface = Color.FromArgb(36, 36, 32);
    private readonly Color SurfaceHover = Color.FromArgb(45, 45, 40);
    private readonly Color TextPrimary = Color.FromArgb(230, 226, 218);
    private readonly Color TextSecondary = Color.FromArgb(181, 176, 165);
    private readonly Color Muted = Color.FromArgb(125, 122, 113);
    private readonly Color Accent = Color.FromArgb(200, 125, 92);
    private readonly Color AccentGlow = Color.FromArgb(232, 150, 110);

    private readonly System.Windows.Forms.Timer clickTimer = new();
    private readonly System.Windows.Forms.Timer statusTimer = new();
    private NumericUpDown hours = null!, mins = null!, secs = null!, millis = null!;
    private ComboBox actionType = null!;
    private Button actionKey = null!, hotkeyButton = null!, startButton = null!, recordButton = null!, playButton = null!, clearButton = null!;
    private Label stateLabel = null!, actionKeyLabel = null!, hotkeyLabel = null!, macroInfo = null!;
    private TrackBar speedBar = null!;
    private Label speedLabel = null!;

    private bool running;
    private bool recording;
    private bool playingMacro;
    private Keys toggleKey = Keys.H;
    private InputAction selectedAction = InputAction.LeftClick;
    private Keys spamKey = Keys.P;
    private readonly List<MacroEvent> macro = new();
    private Stopwatch? recordClock;
    private CancellationTokenSource? playbackCts;
    private GlobalHooks? hooks;
    private bool capturingToggleKey;
    private bool capturingSpamKey;

    public MainForm()
    {
        Text = "Plexium Auto Clicker";
        ClientSize = new Size(760, 600);
        MinimumSize = new Size(760, 600);
        MaximumSize = new Size(760, 600);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        DoubleBuffered = true;

        BuildUi();
        clickTimer.Tick += (_, _) => FireSelectedAction();
        statusTimer.Interval = 100;
        statusTimer.Tick += (_, _) => UpdateMacroInfo();
        statusTimer.Start();

        hooks = new GlobalHooks(OnGlobalKey, OnGlobalMouse);
        hooks.Start();
        FormClosed += (_, _) => { playbackCts?.Cancel(); hooks?.Dispose(); };
        UpdateLabels();
    }

    private void BuildUi()
    {
        var title = new Label { Text = "Plexium Auto Clicker", AutoSize = true, Font = new Font("Segoe UI Semibold", 22f), ForeColor = TextPrimary, Location = new Point(28, 22) };
        var subtitle = new Label { Text = "Fast input automation, wrapped in Plexium style.", AutoSize = true, ForeColor = TextSecondary, Location = new Point(31, 62) };
        Controls.Add(title); Controls.Add(subtitle);

        stateLabel = new Label { Text = "STOPPED", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 8.5f), ForeColor = Accent, BackColor = Color.FromArgb(46, 34, 29), Location = new Point(642, 28), Size = new Size(88, 28) };
        Controls.Add(stateLabel);

        var interval = Card(new Rectangle(28, 102, 704, 112), "CLICK INTERVAL");
        hours = TimeBox(interval, "hours", 22, 45, 0, 999);
        mins = TimeBox(interval, "mins", 178, 45, 0, 59);
        secs = TimeBox(interval, "secs", 334, 45, 0, 59);
        millis = TimeBox(interval, "ms", 490, 45, 1, 999);
        millis.Value = 50;

        var options = Card(new Rectangle(28, 228, 340, 178), "INPUT");
        var actionText = LabelSmall("Action", 20, 42); options.Controls.Add(actionText);
        actionType = new ComboBox { Location = new Point(20, 66), Size = new Size(300, 34), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = SurfaceHover, ForeColor = TextPrimary };
        actionType.Items.AddRange(new object[] { "Left click", "Right click", "Middle click", "Keyboard key" });
        actionType.SelectedIndex = 0;
        actionType.SelectedIndexChanged += (_, _) => { selectedAction = (InputAction)actionType.SelectedIndex; actionKey.Enabled = selectedAction == InputAction.KeyPress; UpdateLabels(); };
        options.Controls.Add(actionType);

        actionKeyLabel = LabelSmall("Spam key", 20, 111); options.Controls.Add(actionKeyLabel);
        actionKey = PlexButton("P", new Rectangle(156, 106, 164, 42), outline: true);
        actionKey.Click += (_, _) => { capturingSpamKey = true; actionKey.Text = "Press a key…"; };
        actionKey.Enabled = false;
        options.Controls.Add(actionKey);

        var controls = Card(new Rectangle(382, 228, 350, 178), "CONTROLS");
        hotkeyLabel = LabelSmall("Toggle hotkey", 20, 42); controls.Controls.Add(hotkeyLabel);
        hotkeyButton = PlexButton("H", new Rectangle(166, 36, 164, 42), outline: true);
        hotkeyButton.Click += (_, _) => { capturingToggleKey = true; hotkeyButton.Text = "Press a key…"; };
        controls.Controls.Add(hotkeyButton);
        startButton = PlexButton("Start", new Rectangle(20, 94, 310, 54));
        startButton.Click += (_, _) => ToggleClicker();
        controls.Controls.Add(startButton);

        var macroCard = Card(new Rectangle(28, 420, 704, 148), "RECORD & PLAYBACK");
        recordButton = PlexButton("●  Record", new Rectangle(20, 44, 148, 44), outline: true);
        playButton = PlexButton("▶  Play", new Rectangle(180, 44, 128, 44));
        clearButton = PlexButton("Clear", new Rectangle(320, 44, 92, 44), outline: true);
        recordButton.Click += (_, _) => ToggleRecording();
        playButton.Click += async (_, _) => await TogglePlaybackAsync();
        clearButton.Click += (_, _) => { if (!recording && !playingMacro) macro.Clear(); };
        macroCard.Controls.Add(recordButton); macroCard.Controls.Add(playButton); macroCard.Controls.Add(clearButton);

        speedLabel = LabelSmall("Speed 1.0×", 438, 38); macroCard.Controls.Add(speedLabel);
        speedBar = new TrackBar { Location = new Point(430, 58), Size = new Size(245, 36), Minimum = 25, Maximum = 300, TickFrequency = 25, Value = 100, BackColor = Surface };
        speedBar.ValueChanged += (_, _) => speedLabel.Text = $"Speed {speedBar.Value / 100.0:0.00}×";
        macroCard.Controls.Add(speedBar);
        macroInfo = new Label { AutoSize = false, Location = new Point(20, 102), Size = new Size(655, 26), ForeColor = Muted, Text = "No macro recorded yet." };
        macroCard.Controls.Add(macroInfo);

        var foot = new Label { Text = "H toggles by default  •  input is sent to the active window", AutoSize = true, ForeColor = Muted, Location = new Point(30, 577) };
        Controls.Add(foot);
    }

    private RoundedPanel Card(Rectangle bounds, string title)
    {
        var p = new RoundedPanel { Bounds = bounds, BackColor = Surface, Radius = 16, BorderColor = Color.FromArgb(48, 230, 226, 218) };
        var lbl = new Label { Text = title, AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI Semibold", 8.3f), Location = new Point(20, 15) };
        p.Controls.Add(lbl); Controls.Add(p); return p;
    }

    private NumericUpDown TimeBox(Control parent, string suffix, int x, int y, int min, int max)
    {
        var n = new NumericUpDown { Location = new Point(x, y), Size = new Size(88, 34), Minimum = min, Maximum = max, BackColor = SurfaceHover, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
        parent.Controls.Add(n);
        parent.Controls.Add(new Label { Text = suffix, AutoSize = true, ForeColor = TextSecondary, Location = new Point(x + 96, y + 8) });
        return n;
    }

    private Label LabelSmall(string text, int x, int y) => new() { Text = text, AutoSize = true, ForeColor = TextSecondary, Location = new Point(x, y + 8) };

    private Button PlexButton(string text, Rectangle bounds, bool outline = false)
    {
        var b = new RoundedButton { Text = text, Bounds = bounds, Radius = 12, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, BackColor = outline ? SurfaceHover : Accent, ForeColor = TextPrimary, BorderColor = outline ? Color.FromArgb(70, 230, 226, 218) : Accent, HoverColor = outline ? Color.FromArgb(55, 55, 49) : AccentGlow, Font = new Font("Segoe UI Semibold", 9.5f) };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private int IntervalMs()
    {
        long ms = (long)hours.Value * 3600000L + (long)mins.Value * 60000L + (long)secs.Value * 1000L + (long)millis.Value;
        return (int)Math.Clamp(ms, 1, int.MaxValue);
    }

    private void ToggleClicker()
    {
        if (playingMacro) return;
        running = !running;
        if (running)
        {
            clickTimer.Interval = IntervalMs();
            clickTimer.Start();
        }
        else clickTimer.Stop();
        UpdateLabels();
    }

    private void FireSelectedAction()
    {
        switch (selectedAction)
        {
            case InputAction.LeftClick: Native.SendMouse(Native.MOUSEEVENTF_LEFTDOWN); Native.SendMouse(Native.MOUSEEVENTF_LEFTUP); break;
            case InputAction.RightClick: Native.SendMouse(Native.MOUSEEVENTF_RIGHTDOWN); Native.SendMouse(Native.MOUSEEVENTF_RIGHTUP); break;
            case InputAction.MiddleClick: Native.SendMouse(Native.MOUSEEVENTF_MIDDLEDOWN); Native.SendMouse(Native.MOUSEEVENTF_MIDDLEUP); break;
            case InputAction.KeyPress: Native.SendKey(spamKey, true); Native.SendKey(spamKey, false); break;
        }
    }

    private void ToggleRecording()
    {
        if (playingMacro) return;
        if (!recording)
        {
            running = false; clickTimer.Stop(); macro.Clear();
            recordClock = Stopwatch.StartNew(); recording = true;
        }
        else
        {
            recording = false; recordClock?.Stop();
        }
        UpdateLabels();
    }

    private async Task TogglePlaybackAsync()
    {
        if (recording || macro.Count == 0) return;
        if (playingMacro) { playbackCts?.Cancel(); return; }
        running = false; clickTimer.Stop(); playingMacro = true; playbackCts = new CancellationTokenSource(); UpdateLabels();
        try
        {
            double speed = speedBar.Value / 100.0;
            long last = 0;
            foreach (var e in macro.ToArray())
            {
                long wait = (long)((e.TimeMs - last) / speed);
                if (wait > 0) await Task.Delay((int)Math.Min(wait, int.MaxValue), playbackCts.Token);
                e.Play(); last = e.TimeMs;
            }
        }
        catch (OperationCanceledException) { }
        finally { playingMacro = false; playbackCts?.Dispose(); playbackCts = null; UpdateLabels(); }
    }

    private void OnGlobalKey(Keys key, bool down)
    {
        if (InvokeRequired) { BeginInvoke(() => OnGlobalKey(key, down)); return; }
        if (capturingToggleKey && down)
        {
            toggleKey = key; capturingToggleKey = false; UpdateLabels(); return;
        }
        if (capturingSpamKey && down)
        {
            spamKey = key; capturingSpamKey = false; UpdateLabels(); return;
        }
        if (down && key == toggleKey && !recording && !capturingSpamKey && !capturingToggleKey)
        {
            ToggleClicker(); return;
        }
        if (recording && recordClock is not null)
        {
            if (key != toggleKey)
                macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Key, (int)key, down, Cursor.Position));
        }
    }

    private void OnGlobalMouse(MouseButtons button, bool down, Point pos, bool move)
    {
        if (!recording || recordClock is null || playingMacro) return;
        if (move)
        {
            if (macro.Count == 0 || recordClock.ElapsedMilliseconds - macro[^1].TimeMs >= 12)
                macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Move, 0, false, pos));
        }
        else
            macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Mouse, (int)button, down, pos));
    }

    private void UpdateLabels()
    {
        hotkeyButton.Text = capturingToggleKey ? "Press a key…" : toggleKey.ToString();
        actionKey.Text = capturingSpamKey ? "Press a key…" : spamKey.ToString();
        startButton.Text = running ? "Stop" : "Start";
        recordButton.Text = recording ? "■  Stop recording" : "●  Record";
        playButton.Text = playingMacro ? "■  Stop" : "▶  Play";
        stateLabel.Text = recording ? "RECORDING" : playingMacro ? "PLAYING" : running ? "RUNNING" : "STOPPED";
        stateLabel.ForeColor = recording ? Color.FromArgb(255, 160, 150) : Accent;
        playButton.Enabled = !recording && macro.Count > 0;
        clearButton.Enabled = !recording && !playingMacro;
    }

    private void UpdateMacroInfo()
    {
        if (recording && recordClock is not null) macroInfo.Text = $"Recording… {macro.Count} events  •  {recordClock.Elapsed.TotalSeconds:0.0}s";
        else if (macro.Count > 0) macroInfo.Text = $"Recorded {macro.Count} events  •  length {macro[^1].TimeMs / 1000.0:0.00}s";
        else macroInfo.Text = "No macro recorded yet.";
        playButton.Enabled = !recording && macro.Count > 0;
    }
}

internal enum InputAction { LeftClick, RightClick, MiddleClick, KeyPress }
internal enum MacroKind { Key, Mouse, Move }

internal readonly record struct MacroEvent(long TimeMs, MacroKind Kind, int Code, bool Down, Point Position)
{
    public void Play()
    {
        if (Kind == MacroKind.Move) { Native.SetCursorPos(Position.X, Position.Y); return; }
        if (Kind == MacroKind.Key) { Native.SendKey((Keys)Code, Down); return; }
        Native.SetCursorPos(Position.X, Position.Y);
        var b = (MouseButtons)Code;
        uint flag = b switch
        {
            MouseButtons.Left => Down ? Native.MOUSEEVENTF_LEFTDOWN : Native.MOUSEEVENTF_LEFTUP,
            MouseButtons.Right => Down ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_RIGHTUP,
            MouseButtons.Middle => Down ? Native.MOUSEEVENTF_MIDDLEDOWN : Native.MOUSEEVENTF_MIDDLEUP,
            _ => 0
        };
        if (flag != 0) Native.SendMouse(flag);
    }
}

internal sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 14;
    public Color BorderColor { get; set; } = Color.Transparent;
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(ClientRectangle.X, ClientRectangle.Y, ClientRectangle.Width - 1, ClientRectangle.Height - 1);
        using var path = RoundRect(r, Radius);
        using var pen = new Pen(BorderColor); e.Graphics.DrawPath(pen, path);
        Region = new Region(path);
    }
    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
}

internal sealed class RoundedButton : Button
{
    public int Radius { get; set; } = 12;
    public Color BorderColor { get; set; } = Color.Transparent;
    public Color HoverColor { get; set; }
    private Color normal;
    protected override void OnCreateControl() { base.OnCreateControl(); normal = BackColor; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); normal = BackColor; BackColor = HoverColor; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); BackColor = normal; Invalidate(); }
    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundRect(rect, Radius); using var brush = new SolidBrush(BackColor); pevent.Graphics.FillPath(brush, path);
        using var pen = new Pen(BorderColor); pevent.Graphics.DrawPath(pen, path);
        TextRenderer.DrawText(pevent.Graphics, Text, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        Region = new Region(path);
    }
    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
}

internal sealed class GlobalHooks : IDisposable
{
    private readonly Action<Keys, bool> keyCallback;
    private readonly Action<MouseButtons, bool, Point, bool> mouseCallback;
    private Native.LowLevelKeyboardProc? kbProc;
    private Native.LowLevelMouseProc? mouseProc;
    private IntPtr kbHook, mouseHook;
    public GlobalHooks(Action<Keys, bool> key, Action<MouseButtons, bool, Point, bool> mouse) { keyCallback = key; mouseCallback = mouse; }
    public void Start()
    {
        kbProc = KeyboardHook; mouseProc = MouseHook;
        kbHook = Native.SetWindowsHookExKeyboard(13, kbProc, IntPtr.Zero, 0);
        mouseHook = Native.SetWindowsHookExMouse(14, mouseProc, IntPtr.Zero, 0);
    }
    private IntPtr KeyboardHook(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            int msg = w.ToInt32(); bool down = msg is 0x100 or 0x104; bool up = msg is 0x101 or 0x105;
            if (down || up) { var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(l); keyCallback((Keys)data.vkCode, down); }
        }
        return Native.CallNextHookEx(kbHook, n, w, l);
    }
    private IntPtr MouseHook(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            var d = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(l); var p = new Point(d.pt.x, d.pt.y);
            switch (w.ToInt32())
            {
                case 0x200: mouseCallback(MouseButtons.None, false, p, true); break;
                case 0x201: mouseCallback(MouseButtons.Left, true, p, false); break;
                case 0x202: mouseCallback(MouseButtons.Left, false, p, false); break;
                case 0x204: mouseCallback(MouseButtons.Right, true, p, false); break;
                case 0x205: mouseCallback(MouseButtons.Right, false, p, false); break;
                case 0x207: mouseCallback(MouseButtons.Middle, true, p, false); break;
                case 0x208: mouseCallback(MouseButtons.Middle, false, p, false); break;
            }
        }
        return Native.CallNextHookEx(mouseHook, n, w, l);
    }
    public void Dispose() { if (kbHook != IntPtr.Zero) Native.UnhookWindowsHookEx(kbHook); if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook); }
}

internal static class Native
{
    public const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004, MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010, MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW")] public static extern IntPtr SetWindowsHookExKeyboard(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW")] public static extern IntPtr SetWindowsHookExMouse(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);

    public static void SendKey(Keys key, bool down)
    {
        var input = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key, dwFlags = down ? 0u : KEYEVENTF_KEYUP } } };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
    public static void SendMouse(uint flag)
    {
        var input = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flag } } };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo; }
}
