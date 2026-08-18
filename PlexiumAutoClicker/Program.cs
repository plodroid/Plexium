using System.Diagnostics;
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
    private readonly System.Windows.Forms.Timer clickTimer = new();
    private readonly System.Windows.Forms.Timer statusTimer = new();

    private NumericUpDown hours = null!, mins = null!, secs = null!, millis = null!;
    private ComboBox actionType = null!, clickType = null!;
    private Button actionKey = null!, hotkeyButton = null!, startButton = null!, themeButton = null!;
    private Button recordButton = null!, playButton = null!, clearButton = null!, pickPositionButton = null!;
    private Label stateLabel = null!, macroInfo = null!, speedLabel = null!;
    private TrackBar speedBar = null!;
    private CheckBox randomizeCheck = null!, fixedPositionCheck = null!, startDelayCheck = null!, macroLoopCheck = null!;
    private NumericUpDown randomMs = null!, posX = null!, posY = null!, startDelaySeconds = null!, repeatCount = null!, macroLoopCount = null!;
    private RadioButton repeatUntilStopped = null!, repeatTimes = null!;
    private TabControl tabs = null!;

    private bool darkMode = true;
    private bool running;
    private bool recording;
    private bool playingMacro;
    private bool capturingToggleKey;
    private bool capturingSpamKey;
    private bool pickingPosition;
    private Keys toggleKey = Keys.H;
    private Keys spamKey = Keys.P;
    private InputAction selectedAction = InputAction.LeftClick;
    private long completedActions;
    private readonly Random rng = new();

    private readonly List<MacroEvent> macro = new();
    private Stopwatch? recordClock;
    private CancellationTokenSource? playbackCts;
    private GlobalHooks? hooks;

    public MainForm()
    {
        Text = "Plexium Auto Clicker";
        ClientSize = new Size(610, 570);
        MinimumSize = new Size(626, 609);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        BuildUi();
        ApplyTheme();

        clickTimer.Tick += (_, _) => ClickTick();
        statusTimer.Interval = 100;
        statusTimer.Tick += (_, _) => UpdateStatus();
        statusTimer.Start();

        hooks = new GlobalHooks(OnGlobalKey, OnGlobalMouse);
        hooks.Start();
        FormClosed += (_, _) => { playbackCts?.Cancel(); hooks?.Dispose(); };
        UpdateLabels();
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 46 };
        var title = new Label { Text = "Plexium Auto Clicker", AutoSize = true, Font = new Font("Segoe UI Semibold", 13f), Location = new Point(12, 11) };
        stateLabel = new Label { Text = "Stopped", AutoSize = true, Location = new Point(185, 15) };
        themeButton = new Button { Text = "☾ Dark", Size = new Size(86, 28), Location = new Point(510, 8) };
        themeButton.Click += (_, _) => { darkMode = !darkMode; ApplyTheme(); };
        header.Controls.AddRange(new Control[] { title, stateLabel, themeButton });
        Controls.Add(header);

        tabs = new TabControl { Location = new Point(10, 50), Size = new Size(590, 468) };
        var clickerTab = new TabPage("Auto Clicker");
        var macroTab = new TabPage("Recorder");
        var settingsTab = new TabPage("More");
        tabs.TabPages.AddRange(new[] { clickerTab, macroTab, settingsTab });
        Controls.Add(tabs);

        BuildClickerTab(clickerTab);
        BuildMacroTab(macroTab);
        BuildMoreTab(settingsTab);

        var footer = new Label { Text = "Tip: the toggle hotkey works globally while this app is open.", AutoSize = true, Location = new Point(13, 532) };
        Controls.Add(footer);
    }

    private void BuildClickerTab(TabPage page)
    {
        var intervalGroup = new GroupBox { Text = "Click interval", Location = new Point(10, 10), Size = new Size(555, 86) };
        hours = NumberBox(intervalGroup, 14, 31, 0, 999, 0); AddSuffix(intervalGroup, "hours", 78, 35);
        mins = NumberBox(intervalGroup, 120, 31, 0, 59, 0); AddSuffix(intervalGroup, "mins", 184, 35);
        secs = NumberBox(intervalGroup, 218, 31, 0, 59, 0); AddSuffix(intervalGroup, "secs", 282, 35);
        millis = NumberBox(intervalGroup, 320, 31, 1, 9999, 50); AddSuffix(intervalGroup, "ms", 393, 35);
        randomizeCheck = new CheckBox { Text = "Random ±", AutoSize = true, Location = new Point(425, 34) };
        randomMs = NumberBox(intervalGroup, 493, 31, 0, 5000, 0, 55); randomMs.Enabled = false;
        randomizeCheck.CheckedChanged += (_, _) => randomMs.Enabled = randomizeCheck.Checked;
        intervalGroup.Controls.Add(randomizeCheck);
        page.Controls.Add(intervalGroup);

        var inputGroup = new GroupBox { Text = "Input", Location = new Point(10, 105), Size = new Size(270, 130) };
        inputGroup.Controls.Add(new Label { Text = "Action:", AutoSize = true, Location = new Point(14, 30) });
        actionType = new ComboBox { Location = new Point(75, 26), Size = new Size(175, 25), DropDownStyle = ComboBoxStyle.DropDownList };
        actionType.Items.AddRange(new object[] { "Left click", "Right click", "Middle click", "Keyboard key" }); actionType.SelectedIndex = 0;
        actionType.SelectedIndexChanged += (_, _) => { selectedAction = (InputAction)actionType.SelectedIndex; actionKey.Enabled = selectedAction == InputAction.KeyPress; clickType.Enabled = selectedAction != InputAction.KeyPress; };
        inputGroup.Controls.Add(actionType);
        inputGroup.Controls.Add(new Label { Text = "Click type:", AutoSize = true, Location = new Point(14, 64) });
        clickType = new ComboBox { Location = new Point(75, 60), Size = new Size(175, 25), DropDownStyle = ComboBoxStyle.DropDownList };
        clickType.Items.AddRange(new object[] { "Single", "Double" }); clickType.SelectedIndex = 0; inputGroup.Controls.Add(clickType);
        inputGroup.Controls.Add(new Label { Text = "Spam key:", AutoSize = true, Location = new Point(14, 98) });
        actionKey = new Button { Text = "P", Location = new Point(75, 92), Size = new Size(175, 27), Enabled = false };
        actionKey.Click += (_, _) => { capturingSpamKey = true; actionKey.Text = "Press any key..."; };
        inputGroup.Controls.Add(actionKey); page.Controls.Add(inputGroup);

        var repeatGroup = new GroupBox { Text = "Repeat", Location = new Point(295, 105), Size = new Size(270, 130) };
        repeatUntilStopped = new RadioButton { Text = "Repeat until stopped", AutoSize = true, Location = new Point(15, 28), Checked = true };
        repeatTimes = new RadioButton { Text = "Repeat", AutoSize = true, Location = new Point(15, 62) };
        repeatCount = NumberBox(repeatGroup, 82, 58, 1, 999999999, 100, 95); repeatCount.Enabled = false;
        repeatGroup.Controls.Add(new Label { Text = "times", AutoSize = true, Location = new Point(184, 63) });
        repeatTimes.CheckedChanged += (_, _) => repeatCount.Enabled = repeatTimes.Checked;
        repeatGroup.Controls.AddRange(new Control[] { repeatUntilStopped, repeatTimes }); page.Controls.Add(repeatGroup);

        var hotkeyGroup = new GroupBox { Text = "Hotkey", Location = new Point(10, 244), Size = new Size(555, 70) };
        hotkeyGroup.Controls.Add(new Label { Text = "Toggle auto clicker:", AutoSize = true, Location = new Point(15, 31) });
        hotkeyButton = new Button { Text = "H", Location = new Point(145, 25), Size = new Size(140, 29) };
        hotkeyButton.Click += (_, _) => { capturingToggleKey = true; hotkeyButton.Text = "Press any key..."; };
        hotkeyGroup.Controls.Add(hotkeyButton);
        startButton = new Button { Text = "Start", Location = new Point(300, 24), Size = new Size(235, 31) };
        startButton.Click += async (_, _) => await ToggleClickerAsync(); hotkeyGroup.Controls.Add(startButton); page.Controls.Add(hotkeyGroup);

        var note = new Label { Text = "Input is sent to whichever window is currently active.", AutoSize = true, Location = new Point(15, 326) };
        page.Controls.Add(note);
    }

    private void BuildMacroTab(TabPage page)
    {
        var macroGroup = new GroupBox { Text = "Record mouse + keyboard", Location = new Point(10, 10), Size = new Size(555, 150) };
        recordButton = new Button { Text = "Record", Location = new Point(15, 28), Size = new Size(120, 32) };
        playButton = new Button { Text = "Play", Location = new Point(145, 28), Size = new Size(120, 32) };
        clearButton = new Button { Text = "Clear", Location = new Point(275, 28), Size = new Size(100, 32) };
        recordButton.Click += (_, _) => ToggleRecording(); playButton.Click += async (_, _) => await TogglePlaybackAsync(); clearButton.Click += (_, _) => { if (!recording && !playingMacro) macro.Clear(); };
        macroGroup.Controls.AddRange(new Control[] { recordButton, playButton, clearButton });
        speedLabel = new Label { Text = "Playback speed: 1.00×", AutoSize = true, Location = new Point(15, 76) };
        speedBar = new TrackBar { Location = new Point(145, 67), Size = new Size(225, 36), Minimum = 25, Maximum = 400, TickFrequency = 25, Value = 100 };
        speedBar.ValueChanged += (_, _) => speedLabel.Text = $"Playback speed: {speedBar.Value / 100.0:0.00}×";
        macroLoopCheck = new CheckBox { Text = "Loop", AutoSize = true, Location = new Point(390, 76) };
        macroLoopCount = NumberBox(macroGroup, 445, 71, 1, 99999, 2, 72); macroLoopCount.Enabled = false;
        macroLoopCheck.CheckedChanged += (_, _) => macroLoopCount.Enabled = macroLoopCheck.Checked;
        macroInfo = new Label { Text = "No recording yet.", AutoSize = true, Location = new Point(15, 118) };
        macroGroup.Controls.AddRange(new Control[] { speedLabel, speedBar, macroLoopCheck, macroInfo }); page.Controls.Add(macroGroup);

        var help = new GroupBox { Text = "Recorder notes", Location = new Point(10, 170), Size = new Size(555, 135) };
        help.Controls.Add(new Label { Text = "• Records keyboard presses, mouse clicks, and mouse movement.\n• Press Record again to stop recording.\n• Playback can be stopped at any time.\n• The toggle hotkey itself is not saved into the macro.", AutoSize = true, Location = new Point(15, 28) });
        page.Controls.Add(help);
    }

    private void BuildMoreTab(TabPage page)
    {
        var cursorGroup = new GroupBox { Text = "Cursor position", Location = new Point(10, 10), Size = new Size(555, 125) };
        fixedPositionCheck = new CheckBox { Text = "Always click a fixed position", AutoSize = true, Location = new Point(15, 27) };
        cursorGroup.Controls.Add(fixedPositionCheck);
        cursorGroup.Controls.Add(new Label { Text = "X:", AutoSize = true, Location = new Point(15, 66) });
        posX = NumberBox(cursorGroup, 38, 61, -100000, 100000, 0, 90);
        cursorGroup.Controls.Add(new Label { Text = "Y:", AutoSize = true, Location = new Point(140, 66) });
        posY = NumberBox(cursorGroup, 163, 61, -100000, 100000, 0, 90);
        pickPositionButton = new Button { Text = "Pick position", Location = new Point(270, 59), Size = new Size(125, 29) };
        pickPositionButton.Click += (_, _) => { pickingPosition = true; pickPositionButton.Text = "Click anywhere..."; WindowState = FormWindowState.Minimized; };
        var currentButton = new Button { Text = "Use current", Location = new Point(405, 59), Size = new Size(125, 29) };
        currentButton.Click += (_, _) => { posX.Value = Math.Clamp(Cursor.Position.X, (int)posX.Minimum, (int)posX.Maximum); posY.Value = Math.Clamp(Cursor.Position.Y, (int)posY.Minimum, (int)posY.Maximum); fixedPositionCheck.Checked = true; };
        cursorGroup.Controls.AddRange(new Control[] { pickPositionButton, currentButton }); page.Controls.Add(cursorGroup);

        var delayGroup = new GroupBox { Text = "Start delay", Location = new Point(10, 145), Size = new Size(555, 82) };
        startDelayCheck = new CheckBox { Text = "Wait before starting", AutoSize = true, Location = new Point(15, 33) };
        startDelaySeconds = NumberBox(delayGroup, 150, 28, 0, 3600, 3, 90); startDelaySeconds.Enabled = false;
        delayGroup.Controls.Add(new Label { Text = "seconds", AutoSize = true, Location = new Point(247, 34) });
        startDelayCheck.CheckedChanged += (_, _) => startDelaySeconds.Enabled = startDelayCheck.Checked;
        delayGroup.Controls.Add(startDelayCheck); page.Controls.Add(delayGroup);

        var shortcuts = new GroupBox { Text = "Quick info", Location = new Point(10, 237), Size = new Size(555, 105) };
        shortcuts.Controls.Add(new Label { Text = "Hotkey: H by default\nSpam key: P by default\nTheme: switch anytime with the top-right button", AutoSize = true, Location = new Point(15, 26) });
        page.Controls.Add(shortcuts);
    }

    private NumericUpDown NumberBox(Control parent, int x, int y, decimal min, decimal max, decimal value, int width = 58)
    {
        var n = new NumericUpDown { Location = new Point(x, y), Size = new Size(width, 25), Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), TextAlign = HorizontalAlignment.Center };
        parent.Controls.Add(n); return n;
    }

    private static void AddSuffix(Control parent, string text, int x, int y) => parent.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y) });

    private void ApplyTheme()
    {
        Color bg = darkMode ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
        Color panel = darkMode ? Color.FromArgb(42, 42, 42) : Color.White;
        Color input = darkMode ? Color.FromArgb(55, 55, 55) : Color.White;
        Color text = darkMode ? Color.Gainsboro : Color.Black;
        Color sub = darkMode ? Color.Silver : Color.DimGray;

        BackColor = bg; ForeColor = text;
        themeButton.Text = darkMode ? "☾ Dark" : "☀ Light";
        foreach (Control c in Controls) ThemeControl(c, bg, panel, input, text, sub);
    }

    private void ThemeControl(Control c, Color bg, Color panel, Color input, Color text, Color sub)
    {
        if (c is TabControl) { c.BackColor = bg; c.ForeColor = text; }
        else if (c is TabPage or GroupBox or Panel) { c.BackColor = panel; c.ForeColor = text; }
        else if (c is Button) { c.BackColor = darkMode ? Color.FromArgb(62, 62, 62) : SystemColors.Control; c.ForeColor = text; c.FlatStyle = FlatStyle.Standard; }
        else if (c is ComboBox or NumericUpDown or TrackBar) { c.BackColor = input; c.ForeColor = text; }
        else if (c is Label) { c.BackColor = Color.Transparent; c.ForeColor = c == stateLabel ? (running ? Color.LimeGreen : recording ? Color.OrangeRed : playingMacro ? Color.DeepSkyBlue : sub) : sub; }
        else { c.BackColor = panel; c.ForeColor = text; }
        foreach (Control child in c.Controls) ThemeControl(child, bg, panel, input, text, sub);
        c.Invalidate();
    }

    private int BaseIntervalMs()
    {
        long ms = (long)hours.Value * 3600000L + (long)mins.Value * 60000L + (long)secs.Value * 1000L + (long)millis.Value;
        return (int)Math.Clamp(ms, 1, int.MaxValue);
    }

    private int NextIntervalMs()
    {
        int baseMs = BaseIntervalMs();
        if (!randomizeCheck.Checked || randomMs.Value <= 0) return baseMs;
        int range = (int)Math.Min(randomMs.Value, int.MaxValue / 2);
        long v = baseMs + rng.Next(-range, range + 1);
        return (int)Math.Clamp(v, 1, int.MaxValue);
    }

    private async Task ToggleClickerAsync()
    {
        if (recording || playingMacro) return;
        if (running) { StopClicker(); return; }
        if (startDelayCheck.Checked && startDelaySeconds.Value > 0)
        {
            startButton.Enabled = false;
            int total = (int)startDelaySeconds.Value;
            for (int i = total; i > 0; i--) { startButton.Text = $"Starting in {i}..."; await Task.Delay(1000); }
            startButton.Enabled = true;
        }
        completedActions = 0; running = true; clickTimer.Interval = NextIntervalMs(); clickTimer.Start(); UpdateLabels();
    }

    private void StopClicker()
    {
        running = false; clickTimer.Stop(); UpdateLabels();
    }

    private void ClickTick()
    {
        if (!running) return;
        FireSelectedAction();
        completedActions++;
        if (repeatTimes.Checked && completedActions >= (long)repeatCount.Value) { StopClicker(); return; }
        clickTimer.Interval = NextIntervalMs();
    }

    private void FireSelectedAction()
    {
        Point? restore = null;
        if (fixedPositionCheck.Checked && selectedAction != InputAction.KeyPress)
        {
            restore = Cursor.Position;
            Native.SetCursorPos((int)posX.Value, (int)posY.Value);
        }
        int repeats = clickType.SelectedIndex == 1 && selectedAction != InputAction.KeyPress ? 2 : 1;
        for (int i = 0; i < repeats; i++)
        {
            switch (selectedAction)
            {
                case InputAction.LeftClick: Native.Click(MouseButtons.Left); break;
                case InputAction.RightClick: Native.Click(MouseButtons.Right); break;
                case InputAction.MiddleClick: Native.Click(MouseButtons.Middle); break;
                case InputAction.KeyPress: Native.SendKey(spamKey, true); Native.SendKey(spamKey, false); break;
            }
        }
        if (restore.HasValue) Native.SetCursorPos(restore.Value.X, restore.Value.Y);
    }

    private void ToggleRecording()
    {
        if (playingMacro) return;
        if (!recording)
        {
            StopClicker(); macro.Clear(); recordClock = Stopwatch.StartNew(); recording = true;
        }
        else { recording = false; recordClock?.Stop(); }
        UpdateLabels();
    }

    private async Task TogglePlaybackAsync()
    {
        if (recording || macro.Count == 0) return;
        if (playingMacro) { playbackCts?.Cancel(); return; }
        StopClicker(); playingMacro = true; playbackCts = new CancellationTokenSource(); UpdateLabels();
        try
        {
            int loops = macroLoopCheck.Checked ? (int)macroLoopCount.Value : 1;
            for (int loop = 0; loop < loops; loop++)
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
        }
        catch (OperationCanceledException) { }
        finally { playingMacro = false; playbackCts?.Dispose(); playbackCts = null; UpdateLabels(); }
    }

    private void OnGlobalKey(Keys key, bool down)
    {
        if (InvokeRequired) { BeginInvoke(() => OnGlobalKey(key, down)); return; }
        if (capturingToggleKey && down) { toggleKey = key; capturingToggleKey = false; UpdateLabels(); return; }
        if (capturingSpamKey && down) { spamKey = key; capturingSpamKey = false; UpdateLabels(); return; }
        if (down && key == toggleKey && !recording && !capturingSpamKey && !capturingToggleKey) { _ = ToggleClickerAsync(); return; }
        if (recording && recordClock is not null && key != toggleKey)
            macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Key, (int)key, down, Cursor.Position));
    }

    private void OnGlobalMouse(MouseButtons button, bool down, Point pos, bool move)
    {
        if (InvokeRequired) { BeginInvoke(() => OnGlobalMouse(button, down, pos, move)); return; }
        if (pickingPosition && !move && button == MouseButtons.Left && down)
        {
            posX.Value = Math.Clamp(pos.X, (int)posX.Minimum, (int)posX.Maximum);
            posY.Value = Math.Clamp(pos.Y, (int)posY.Minimum, (int)posY.Maximum);
            fixedPositionCheck.Checked = true; pickingPosition = false; pickPositionButton.Text = "Pick position"; WindowState = FormWindowState.Normal; Activate(); return;
        }
        if (!recording || recordClock is null || playingMacro) return;
        if (move)
        {
            if (macro.Count == 0 || recordClock.ElapsedMilliseconds - macro[^1].TimeMs >= 12)
                macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Move, 0, false, pos));
        }
        else macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Mouse, (int)button, down, pos));
    }

    private void UpdateLabels()
    {
        hotkeyButton.Text = capturingToggleKey ? "Press any key..." : toggleKey.ToString();
        actionKey.Text = capturingSpamKey ? "Press any key..." : spamKey.ToString();
        startButton.Text = running ? "Stop" : "Start";
        recordButton.Text = recording ? "Stop recording" : "Record";
        playButton.Text = playingMacro ? "Stop" : "Play";
        stateLabel.Text = recording ? "Recording" : playingMacro ? "Playing" : running ? $"Running ({completedActions})" : "Stopped";
        playButton.Enabled = !recording && macro.Count > 0;
        clearButton.Enabled = !recording && !playingMacro;
        ApplyTheme();
    }

    private void UpdateStatus()
    {
        if (recording && recordClock is not null) macroInfo.Text = $"Recording: {macro.Count} events • {recordClock.Elapsed.TotalSeconds:0.0}s";
        else if (macro.Count > 0) macroInfo.Text = $"Recorded: {macro.Count} events • {macro[^1].TimeMs / 1000.0:0.00}s";
        else macroInfo.Text = "No recording yet.";
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
            if (down || up)
            {
                var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(l);
                if ((data.flags & Native.LLKHF_INJECTED) == 0) keyCallback((Keys)data.vkCode, down);
            }
        }
        return Native.CallNextHookEx(kbHook, n, w, l);
    }
    private IntPtr MouseHook(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            var d = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(l);
            if ((d.flags & Native.LLMHF_INJECTED) != 0) return Native.CallNextHookEx(mouseHook, n, w, l);
            var p = new Point(d.pt.x, d.pt.y);
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
    public const uint LLKHF_INJECTED = 0x10, LLMHF_INJECTED = 0x00000001;

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
    public static void Click(MouseButtons button)
    {
        uint down = button switch { MouseButtons.Left => MOUSEEVENTF_LEFTDOWN, MouseButtons.Right => MOUSEEVENTF_RIGHTDOWN, MouseButtons.Middle => MOUSEEVENTF_MIDDLEDOWN, _ => 0 };
        uint up = button switch { MouseButtons.Left => MOUSEEVENTF_LEFTUP, MouseButtons.Right => MOUSEEVENTF_RIGHTUP, MouseButtons.Middle => MOUSEEVENTF_MIDDLEUP, _ => 0 };
        if (down != 0) { SendMouse(down); SendMouse(up); }
    }

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo; }
}
