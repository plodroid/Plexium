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
    private readonly System.Windows.Forms.Timer clickTimer = new();
    private readonly System.Windows.Forms.Timer statusTimer = new();

    private PlexNumberBox hours = null!, mins = null!, secs = null!, millis = null!, randomMs = null!, repeatCount = null!;
    private PlexNumberBox posX = null!, posY = null!, startDelaySeconds = null!, macroLoopCount = null!;
    private PlexComboBox actionType = null!, clickType = null!;
    private Button actionKey = null!, hotkeyButton = null!, startButton = null!, themeButton = null!, recordButton = null!, playButton = null!, clearButton = null!, pickPositionButton = null!;
    private Label stateLabel = null!, macroInfo = null!, speedLabel = null!, hotkeyHint = null!;
    private PlexSlider speedBar = null!;
    private PlexCheckBox randomizeCheck = null!, fixedPositionCheck = null!, startDelayCheck = null!, macroLoopCheck = null!;
    private PlexRadioButton repeatUntilStopped = null!, repeatTimes = null!;

    private bool darkMode = true, running, recording, playingMacro, capturingToggleKey, capturingSpamKey, pickingPosition;
    private Keys toggleKey = Keys.H, spamKey = Keys.P;
    private InputAction selectedAction = InputAction.LeftClick;
    private long completedActions;
    private readonly Random rng = new();
    private readonly List<MacroEvent> macro = new();
    private Stopwatch? recordClock;
    private CancellationTokenSource? playbackCts;
    private GlobalHooks? hooks;

    private Theme T => darkMode ? Theme.Dark : Theme.Light;

    public MainForm()
    {
        Text = "Plexium Auto Clicker";
        ClientSize = new Size(820, 650);
        MinimumSize = MaximumSize = new Size(836, 689);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;

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
        Controls.Add(new Label { Name = "Title", Text = "Plexium Auto Clicker", AutoSize = true, Font = new Font("Segoe UI Semibold", 20f), Location = new Point(28, 20) });
        Controls.Add(new Label { Name = "Subtitle", Text = "Fast, simple input automation.", AutoSize = true, Location = new Point(31, 57) });

        stateLabel = new Label { Text = "STOPPED", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 8f), Location = new Point(610, 26), Size = new Size(90, 30) };
        Controls.Add(stateLabel);
        themeButton = MakeButton("☾  Dark", new Rectangle(708, 24, 84, 34), true);
        themeButton.Click += (_, _) => { darkMode = !darkMode; ApplyTheme(); };
        Controls.Add(themeButton);

        var intervalCard = Card("CLICK INTERVAL", new Rectangle(28, 94, 370, 176));
        AddMiniLabel(intervalCard, "Hours", 18, 42); hours = NumberBox(intervalCard, 18, 64, 0, 999, 0, 76);
        AddMiniLabel(intervalCard, "Minutes", 108, 42); mins = NumberBox(intervalCard, 108, 64, 0, 59, 0, 76);
        AddMiniLabel(intervalCard, "Seconds", 198, 42); secs = NumberBox(intervalCard, 198, 64, 0, 59, 0, 76);
        AddMiniLabel(intervalCard, "Millis", 288, 42); millis = NumberBox(intervalCard, 288, 64, 1, 9999, 50, 64);
        randomizeCheck = new PlexCheckBox { Text = "Randomize interval", AutoSize = true, Location = new Point(18, 118) };
        randomMs = NumberBox(intervalCard, 180, 113, 0, 5000, 0, 80); randomMs.Enabled = false;
        intervalCard.Controls.Add(new Label { Text = "± ms", AutoSize = true, Location = new Point(268, 119) });
        randomizeCheck.CheckedChanged += (_, _) => randomMs.Enabled = randomizeCheck.Checked;
        intervalCard.Controls.Add(randomizeCheck);

        var inputCard = Card("INPUT & REPEAT", new Rectangle(422, 94, 370, 176));
        AddMiniLabel(inputCard, "Action", 18, 42);
        actionType = MakeCombo(inputCard, 18, 64, 155, new[] { "Left click", "Right click", "Middle click", "Keyboard key" });
        AddMiniLabel(inputCard, "Click type", 190, 42);
        clickType = MakeCombo(inputCard, 190, 64, 160, new[] { "Single", "Double" });
        actionType.SelectedIndexChanged += (_, _) => { selectedAction = (InputAction)actionType.SelectedIndex; actionKey.Enabled = selectedAction == InputAction.KeyPress; clickType.Enabled = selectedAction != InputAction.KeyPress; };
        repeatUntilStopped = new PlexRadioButton { Text = "Until stopped", AutoSize = true, Location = new Point(18, 116), Checked = true };
        repeatTimes = new PlexRadioButton { Text = "Repeat", AutoSize = true, Location = new Point(122, 116) };
        repeatCount = NumberBox(inputCard, 188, 111, 1, 999999999, 100, 86); repeatCount.Enabled = false;
        inputCard.Controls.Add(new Label { Text = "times", AutoSize = true, Location = new Point(282, 117) });
        repeatTimes.CheckedChanged += (_, _) => repeatCount.Enabled = repeatTimes.Checked;
        inputCard.Controls.AddRange(new Control[] { repeatUntilStopped, repeatTimes });

        var cursorCard = Card("CURSOR & START", new Rectangle(28, 286, 370, 150));
        fixedPositionCheck = new PlexCheckBox { Text = "Fixed click position", AutoSize = true, Location = new Point(18, 42) };
        cursorCard.Controls.Add(fixedPositionCheck);
        AddMiniLabel(cursorCard, "X", 18, 76); posX = NumberBox(cursorCard, 36, 70, -100000, 100000, 0, 78);
        AddMiniLabel(cursorCard, "Y", 124, 76); posY = NumberBox(cursorCard, 142, 70, -100000, 100000, 0, 78);
        pickPositionButton = MakeButton("Pick", new Rectangle(234, 68, 56, 28), true);
        pickPositionButton.Click += (_, _) => { pickingPosition = true; pickPositionButton.Text = "..."; WindowState = FormWindowState.Minimized; };
        var useCurrent = MakeButton("Current", new Rectangle(296, 68, 58, 28), true);
        useCurrent.Click += (_, _) => { posX.Value = Math.Clamp(Cursor.Position.X, (int)posX.Minimum, (int)posX.Maximum); posY.Value = Math.Clamp(Cursor.Position.Y, (int)posY.Minimum, (int)posY.Maximum); fixedPositionCheck.Checked = true; };
        cursorCard.Controls.AddRange(new Control[] { pickPositionButton, useCurrent });
        startDelayCheck = new PlexCheckBox { Text = "Start delay", AutoSize = true, Location = new Point(18, 111) };
        startDelaySeconds = NumberBox(cursorCard, 112, 106, 0, 3600, 3, 72); startDelaySeconds.Enabled = false;
        cursorCard.Controls.Add(new Label { Text = "seconds", AutoSize = true, Location = new Point(192, 112) });
        startDelayCheck.CheckedChanged += (_, _) => startDelaySeconds.Enabled = startDelayCheck.Checked;
        cursorCard.Controls.Add(startDelayCheck);

        var controlCard = Card("CONTROLS", new Rectangle(422, 286, 370, 150));
        AddMiniLabel(controlCard, "Toggle hotkey", 18, 42);
        hotkeyButton = MakeButton("H", new Rectangle(18, 63, 104, 34), true);
        hotkeyButton.Click += (_, _) => { capturingToggleKey = true; hotkeyButton.Text = "Press key..."; };
        AddMiniLabel(controlCard, "Spam key", 140, 42);
        actionKey = MakeButton("P", new Rectangle(140, 63, 104, 34), true); actionKey.Enabled = false;
        actionKey.Click += (_, _) => { capturingSpamKey = true; actionKey.Text = "Press key..."; };
        startButton = MakeButton("Start", new Rectangle(258, 61, 94, 38), false);
        startButton.Click += async (_, _) => await ToggleClickerAsync();
        hotkeyHint = new Label { Text = "H toggles globally while Plexium is open.", AutoSize = true, Location = new Point(18, 113) };
        controlCard.Controls.AddRange(new Control[] { hotkeyButton, actionKey, startButton, hotkeyHint });

        var macroCard = Card("RECORD & PLAYBACK", new Rectangle(28, 452, 764, 160));
        recordButton = MakeButton("●  Record", new Rectangle(18, 44, 114, 36), true);
        playButton = MakeButton("▶  Play", new Rectangle(142, 44, 104, 36), false);
        clearButton = MakeButton("Clear", new Rectangle(256, 44, 82, 36), true);
        recordButton.Click += (_, _) => ToggleRecording();
        playButton.Click += async (_, _) => await TogglePlaybackAsync();
        clearButton.Click += (_, _) => { if (!recording && !playingMacro) macro.Clear(); };
        macroCard.Controls.AddRange(new Control[] { recordButton, playButton, clearButton });

        speedLabel = new Label { Text = "Playback 1.00×", AutoSize = true, Location = new Point(365, 52) };
        speedBar = new PlexSlider { Location = new Point(468, 47), Size = new Size(170, 26), Minimum = 25, Maximum = 400, Value = 100 };
        speedBar.ValueChanged += (_, _) => speedLabel.Text = $"Playback {speedBar.Value / 100.0:0.00}×";
        macroLoopCheck = new PlexCheckBox { Text = "Loop", AutoSize = true, Location = new Point(648, 52) };
        macroLoopCount = NumberBox(macroCard, 696, 46, 1, 99999, 2, 50); macroLoopCount.Enabled = false;
        macroLoopCheck.CheckedChanged += (_, _) => macroLoopCount.Enabled = macroLoopCheck.Checked;
        macroInfo = new Label { Text = "No recording yet.", AutoSize = true, Location = new Point(18, 100) };
        var recorderHint = new Label { Text = "Records mouse movement, clicks and keyboard input.", AutoSize = true, Location = new Point(18, 124) };
        macroCard.Controls.AddRange(new Control[] { speedLabel, speedBar, macroLoopCheck, macroInfo, recorderHint });

        Controls.Add(new Label { Name = "Footer", Text = "Input is sent to the active window  •  100% C#", AutoSize = true, Location = new Point(30, 625) });
    }

    private RoundedPanel Card(string title, Rectangle bounds)
    {
        var p = new RoundedPanel { Bounds = bounds, Radius = 14 };
        p.Controls.Add(new Label { Name = "CardTitle", Text = title, AutoSize = true, Font = new Font("Segoe UI Semibold", 8.2f), Location = new Point(18, 16) });
        Controls.Add(p);
        return p;
    }

    private void AddMiniLabel(Control parent, string text, int x, int y) => parent.Controls.Add(new Label { Name = "Mini", Text = text, AutoSize = true, Font = new Font("Segoe UI", 8.5f), Location = new Point(x, y) });

    private PlexNumberBox NumberBox(Control parent, int x, int y, decimal min, decimal max, decimal value, int width)
    {
        var n = new PlexNumberBox { Location = new Point(x, y), Size = new Size(width, 28), Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max) };
        parent.Controls.Add(n); return n;
    }

    private PlexComboBox MakeCombo(Control parent, int x, int y, int width, string[] values)
    {
        var c = new PlexComboBox { Location = new Point(x, y), Size = new Size(width, 28) };
        c.SetItems(values); c.SelectedIndex = 0; parent.Controls.Add(c); return c;
    }

    private Button MakeButton(string text, Rectangle bounds, bool outline)
    {
        var b = new RoundedButton { Text = text, Bounds = bounds, Radius = 9, Outline = outline, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI Semibold", 9f) };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    private void ApplyTheme()
    {
        var t = T;
        BackColor = t.Bg; ForeColor = t.Text;
        themeButton.Text = darkMode ? "☾  Dark" : "☀  Light";
        foreach (Control c in Controls) ThemeControl(c, t);
        Invalidate(true);
    }

    private void ThemeControl(Control c, Theme t)
    {
        c.ForeColor = t.Text;
        if (c is RoundedPanel rp) { rp.BackColor = t.Surface; rp.BorderColor = t.Border; }
        else if (c is RoundedButton rb) { rb.BackColor = rb.Outline ? t.SurfaceHover : t.Accent; rb.ForeColor = rb.Outline ? t.Text : Color.White; rb.BorderColor = rb.Outline ? t.BorderStrong : t.Accent; rb.HoverColor = rb.Outline ? t.Input : t.AccentHover; }
        else if (c is IPlexThemed themed) themed.ApplyTheme(t);
        else if (c is Label l)
        {
            c.BackColor = Color.Transparent;
            if (l == stateLabel) { c.ForeColor = running ? t.Success : recording ? t.Danger : playingMacro ? t.Info : t.Muted; c.BackColor = t.SurfaceHover; }
            else if (l.Name == "Title") c.ForeColor = t.Text;
            else if (l.Name == "CardTitle") c.ForeColor = t.Accent;
            else if (l.Name is "Mini" or "Subtitle" or "Footer") c.ForeColor = t.Muted;
            else c.ForeColor = t.Secondary;
        }
        foreach (Control child in c.Controls) ThemeControl(child, t);
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
        return (int)Math.Clamp((long)baseMs + rng.Next(-range, range + 1), 1, int.MaxValue);
    }

    private async Task ToggleClickerAsync()
    {
        if (recording || playingMacro) return;
        if (running) { StopClicker(); return; }
        if (startDelayCheck.Checked && startDelaySeconds.Value > 0)
        {
            startButton.Enabled = false;
            for (int i = (int)startDelaySeconds.Value; i > 0; i--) { startButton.Text = $"Starting in {i}…"; await Task.Delay(1000); }
            startButton.Enabled = true;
        }
        completedActions = 0; running = true; clickTimer.Interval = NextIntervalMs(); clickTimer.Start(); UpdateLabels();
    }

    private void StopClicker() { running = false; clickTimer.Stop(); UpdateLabels(); }

    private void ClickTick()
    {
        if (!running) return;
        FireSelectedAction(); completedActions++;
        if (repeatTimes.Checked && completedActions >= (long)repeatCount.Value) { StopClicker(); return; }
        clickTimer.Interval = NextIntervalMs();
    }

    private void FireSelectedAction()
    {
        Point? restore = null;
        if (fixedPositionCheck.Checked && selectedAction != InputAction.KeyPress)
        {
            restore = Cursor.Position; Native.SetCursorPos((int)posX.Value, (int)posY.Value);
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
        if (!recording) { StopClicker(); macro.Clear(); recordClock = Stopwatch.StartNew(); recording = true; }
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
                double speed = speedBar.Value / 100.0; long last = 0;
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
        if (recording && recordClock is not null && key != toggleKey) macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Key, (int)key, down, Cursor.Position));
    }

    private void OnGlobalMouse(MouseButtons button, bool down, Point pos, bool move)
    {
        if (InvokeRequired) { BeginInvoke(() => OnGlobalMouse(button, down, pos, move)); return; }
        if (pickingPosition && !move && button == MouseButtons.Left && down)
        {
            posX.Value = Math.Clamp(pos.X, (int)posX.Minimum, (int)posX.Maximum); posY.Value = Math.Clamp(pos.Y, (int)posY.Minimum, (int)posY.Maximum);
            fixedPositionCheck.Checked = true; pickingPosition = false; pickPositionButton.Text = "Pick"; WindowState = FormWindowState.Normal; Activate(); return;
        }
        if (!recording || recordClock is null || playingMacro) return;
        if (move)
        {
            if (macro.Count == 0 || recordClock.ElapsedMilliseconds - macro[^1].TimeMs >= 12) macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Move, 0, false, pos));
        }
        else macro.Add(new MacroEvent(recordClock.ElapsedMilliseconds, MacroKind.Mouse, (int)button, down, pos));
    }

    private void UpdateLabels()
    {
        hotkeyButton.Text = capturingToggleKey ? "Press key..." : toggleKey.ToString();
        actionKey.Text = capturingSpamKey ? "Press key..." : spamKey.ToString();
        startButton.Text = running ? "Stop" : "Start";
        recordButton.Text = recording ? "■  Stop" : "●  Record";
        playButton.Text = playingMacro ? "■  Stop" : "▶  Play";
        stateLabel.Text = recording ? "RECORDING" : playingMacro ? "PLAYING" : running ? $"RUNNING {completedActions}" : "STOPPED";
        hotkeyHint.Text = $"{toggleKey} toggles globally while Plexium is open.";
        playButton.Enabled = !recording && macro.Count > 0; clearButton.Enabled = !recording && !playingMacro;
        ApplyTheme();
    }

    private void UpdateStatus()
    {
        if (recording && recordClock is not null) macroInfo.Text = $"Recording…  {macro.Count} events  •  {recordClock.Elapsed.TotalSeconds:0.0}s";
        else if (macro.Count > 0) macroInfo.Text = $"Recorded {macro.Count} events  •  {macro[^1].TimeMs / 1000.0:0.00}s";
        else macroInfo.Text = "No recording yet.";
        playButton.Enabled = !recording && macro.Count > 0;
    }
}

internal interface IPlexThemed { void ApplyTheme(Theme t); }

internal sealed class PlexNumberBox : UserControl, IPlexThemed
{
    private readonly TextBox box = new() { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Center };
    private readonly Button up = new() { Text = "▲", FlatStyle = FlatStyle.Flat, TabStop = false };
    private readonly Button down = new() { Text = "▼", FlatStyle = FlatStyle.Flat, TabStop = false };
    private decimal value;
    private Theme theme = Theme.Dark;
    public decimal Minimum { get; set; }
    public decimal Maximum { get; set; } = 100;
    public decimal Value { get => value; set { this.value = Math.Clamp(value, Minimum, Maximum); box.Text = this.value.ToString(); } }
    public PlexNumberBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 28; box.Font = new Font("Segoe UI", 9f); box.Location = new Point(6, 6); box.Height = 18;
        up.FlatAppearance.BorderSize = down.FlatAppearance.BorderSize = 0;
        Controls.AddRange(new Control[]{box,up,down});
        box.TextChanged += (_,_) => { if(decimal.TryParse(box.Text, out var v)) value = Math.Clamp(v, Minimum, Maximum); };
        box.Leave += (_,_) => Value = value;
        up.Click += (_,_) => Value = Math.Min(Maximum, Value + 1); down.Click += (_,_) => Value = Math.Max(Minimum, Value - 1);
        Resize += (_,_) => LayoutChildren();
    }
    private void LayoutChildren(){ box.Width=Math.Max(20,Width-30); up.SetBounds(Width-22,2,20,12); down.SetBounds(Width-22,14,20,12); }
    public void ApplyTheme(Theme t){ theme=t; BackColor=t.Input; ForeColor=t.Text; box.BackColor=t.Input; box.ForeColor=t.Text; foreach(var b in new[]{up,down}){b.BackColor=t.SurfaceHover;b.ForeColor=t.Muted;} Invalidate(); }
    protected override void OnPaint(PaintEventArgs e){ base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using var p=RoundedPanel.RoundRect(new Rectangle(0,0,Width-1,Height-1),7); using var pen=new Pen(Enabled?theme.BorderStrong:theme.Border); e.Graphics.DrawPath(pen,p); }
}

internal sealed class PlexComboBox : Control, IPlexThemed
{
    private string[] items = Array.Empty<string>(); private int selectedIndex=-1; private Theme theme=Theme.Dark;
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex { get=>selectedIndex; set { selectedIndex=Math.Clamp(value,-1,items.Length-1); Invalidate(); SelectedIndexChanged?.Invoke(this,EventArgs.Empty);} }
    public void SetItems(string[] values){items=values; Invalidate();}
    public PlexComboBox(){ SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true); Cursor=Cursors.Hand; }
    public void ApplyTheme(Theme t){theme=t; BackColor=t.Input; ForeColor=t.Text; Invalidate();}
    protected override void OnClick(EventArgs e){ base.OnClick(e); if(!Enabled||items.Length==0)return; var menu=new ContextMenuStrip{ShowImageMargin=false,BackColor=theme.Surface,ForeColor=theme.Text}; for(int i=0;i<items.Length;i++){int j=i; var it=new ToolStripMenuItem(items[i]){BackColor=theme.Surface,ForeColor=theme.Text}; it.Click+=(_,_)=>SelectedIndex=j; menu.Items.Add(it);} menu.Show(this,new Point(0,Height)); }
    protected override void OnPaint(PaintEventArgs e){ e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using var p=RoundedPanel.RoundRect(new Rectangle(0,0,Width-1,Height-1),7); using var b=new SolidBrush(theme.Input); using var pen=new Pen(theme.BorderStrong); e.Graphics.FillPath(b,p); e.Graphics.DrawPath(pen,p); var txt=selectedIndex>=0?items[selectedIndex]:"Select"; TextRenderer.DrawText(e.Graphics,txt,Font,new Rectangle(10,0,Width-34,Height),Enabled?theme.Text:theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); TextRenderer.DrawText(e.Graphics,"⌄",Font,new Rectangle(Width-28,0,20,Height),theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter); }
}

internal sealed class PlexCheckBox : CheckBox, IPlexThemed
{
    private Theme theme=Theme.Dark;
    public PlexCheckBox(){ SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true); Padding=new Padding(22,0,0,0); Height=22; }
    public void ApplyTheme(Theme t){theme=t; BackColor=Color.Transparent; ForeColor=t.Secondary; Invalidate();}
    protected override void OnPaint(PaintEventArgs e){ e.Graphics.Clear(Parent?.BackColor??theme.Surface); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; var r=new Rectangle(1,3,15,15); using var p=RoundedPanel.RoundRect(r,4); using var b=new SolidBrush(Checked?theme.Accent:theme.Input); using var pen=new Pen(Checked?theme.Accent:theme.BorderStrong); e.Graphics.FillPath(b,p); e.Graphics.DrawPath(pen,p); if(Checked) TextRenderer.DrawText(e.Graphics,"✓",new Font(Font.FontFamily,8,FontStyle.Bold),r,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter); TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(22,0,Width-22,Height),theme.Secondary,TextFormatFlags.VerticalCenter); }
}

internal sealed class PlexRadioButton : RadioButton, IPlexThemed
{
    private Theme theme=Theme.Dark;
    public PlexRadioButton(){ SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true); Padding=new Padding(22,0,0,0); Height=22; }
    public void ApplyTheme(Theme t){theme=t; BackColor=Color.Transparent; ForeColor=t.Secondary; Invalidate();}
    protected override void OnPaint(PaintEventArgs e){ e.Graphics.Clear(Parent?.BackColor??theme.Surface); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; var r=new Rectangle(1,3,15,15); using var pen=new Pen(Checked?theme.Accent:theme.BorderStrong,1.5f); e.Graphics.DrawEllipse(pen,r); if(Checked){using var b=new SolidBrush(theme.Accent); e.Graphics.FillEllipse(b,new Rectangle(5,7,7,7));} TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(22,0,Width-22,Height),theme.Secondary,TextFormatFlags.VerticalCenter); }
}

internal sealed class PlexSlider : Control, IPlexThemed
{
    private Theme theme=Theme.Dark; private int value=100;
    public int Minimum{get;set;}=0; public int Maximum{get;set;}=100; public int Value{get=>value;set{this.value=Math.Clamp(value,Minimum,Maximum);Invalidate();ValueChanged?.Invoke(this,EventArgs.Empty);}}
    public event EventHandler? ValueChanged;
    public PlexSlider(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;}
    public void ApplyTheme(Theme t){theme=t;Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){Set(e.X);} protected override void OnMouseMove(MouseEventArgs e){if(e.Button==MouseButtons.Left)Set(e.X);} private void Set(int x){Value=Minimum+(int)((Maximum-Minimum)*Math.Clamp(x/(double)Math.Max(1,Width),0,1));}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; int y=Height/2; using var track=new Pen(theme.BorderStrong,3){StartCap=LineCap.Round,EndCap=LineCap.Round}; e.Graphics.DrawLine(track,4,y,Width-4,y); double f=(Value-Minimum)/(double)Math.Max(1,Maximum-Minimum); int x=4+(int)((Width-8)*f); using var fill=new Pen(theme.Accent,3){StartCap=LineCap.Round,EndCap=LineCap.Round}; e.Graphics.DrawLine(fill,4,y,x,y); using var b=new SolidBrush(theme.Accent); e.Graphics.FillEllipse(b,x-6,y-6,12,12);}
}

internal readonly record struct Theme(Color Bg, Color Surface, Color SurfaceHover, Color Input, Color Text, Color Secondary, Color Muted, Color Accent, Color AccentHover, Color Border, Color BorderStrong, Color Success, Color Danger, Color Info)
{
    public static readonly Theme Dark = new(Color.FromArgb(27,27,24), Color.FromArgb(36,36,32), Color.FromArgb(45,45,40), Color.FromArgb(49,49,44), Color.FromArgb(230,226,218), Color.FromArgb(181,176,165), Color.FromArgb(125,122,113), Color.FromArgb(200,125,92), Color.FromArgb(232,150,110), Color.FromArgb(48,48,43), Color.FromArgb(73,72,66), Color.FromArgb(113,198,146), Color.FromArgb(232,124,117), Color.FromArgb(117,181,232));
    public static readonly Theme Light = new(Color.FromArgb(245,241,235), Color.White, Color.FromArgb(249,246,241), Color.FromArgb(252,250,247), Color.FromArgb(42,39,34), Color.FromArgb(92,87,80), Color.FromArgb(140,135,126), Color.FromArgb(184,106,72), Color.FromArgb(200,125,92), Color.FromArgb(230,224,217), Color.FromArgb(210,202,193), Color.FromArgb(48,145,89), Color.FromArgb(190,73,66), Color.FromArgb(54,126,188));
}

internal sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 14;
    public Color BorderColor { get; set; } = Color.Gray;
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundRect(ClientRectangle, Radius); using var pen = new Pen(BorderColor);
        e.Graphics.DrawPath(pen, path);
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); using var p = RoundRect(ClientRectangle, Radius); Region = new Region(p); }
    internal static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        int d = radius * 2; var p = new GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d - 1, r.Top, d, d, 270, 90); p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d - 1, d, d, 90, 90); p.CloseFigure(); return p;
    }
}

internal sealed class RoundedButton : Button
{
    public int Radius { get; set; } = 9;
    public bool Outline { get; set; }
    public Color BorderColor { get; set; } = Color.Gray;
    public Color HoverColor { get; set; } = Color.Gray;
    private Color normal;
    protected override void OnMouseEnter(EventArgs e) { normal = BackColor; BackColor = HoverColor; base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { BackColor = normal; base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedPanel.RoundRect(ClientRectangle, Radius);
        using var brush = new SolidBrush(BackColor); pevent.Graphics.FillPath(brush, path);
        using var pen = new Pen(BorderColor); pevent.Graphics.DrawPath(pen, path);
        TextRenderer.DrawText(pevent.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
        uint flag = b switch { MouseButtons.Left => Down ? Native.MOUSEEVENTF_LEFTDOWN : Native.MOUSEEVENTF_LEFTUP, MouseButtons.Right => Down ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_RIGHTUP, MouseButtons.Middle => Down ? Native.MOUSEEVENTF_MIDDLEDOWN : Native.MOUSEEVENTF_MIDDLEUP, _ => 0 };
        if (flag != 0) Native.SendMouse(flag);
    }
}

internal sealed class GlobalHooks : IDisposable
{
    private readonly Action<Keys, bool> keyCallback; private readonly Action<MouseButtons, bool, Point, bool> mouseCallback;
    private Native.LowLevelKeyboardProc? kbProc; private Native.LowLevelMouseProc? mouseProc; private IntPtr kbHook, mouseHook;
    public GlobalHooks(Action<Keys, bool> key, Action<MouseButtons, bool, Point, bool> mouse) { keyCallback = key; mouseCallback = mouse; }
    public void Start() { kbProc = KeyboardHook; mouseProc = MouseHook; kbHook = Native.SetWindowsHookExKeyboard(13, kbProc, IntPtr.Zero, 0); mouseHook = Native.SetWindowsHookExMouse(14, mouseProc, IntPtr.Zero, 0); }
    private IntPtr KeyboardHook(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0) { int msg = w.ToInt32(); bool down = msg is 0x100 or 0x104, up = msg is 0x101 or 0x105; if (down || up) { var d = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(l); if ((d.flags & Native.LLKHF_INJECTED) == 0) keyCallback((Keys)d.vkCode, down); } }
        return Native.CallNextHookEx(kbHook, n, w, l);
    }
    private IntPtr MouseHook(int n, IntPtr w, IntPtr l)
    {
        if (n >= 0)
        {
            var d = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(l); if ((d.flags & Native.LLMHF_INJECTED) != 0) return Native.CallNextHookEx(mouseHook, n, w, l);
            var p = new Point(d.pt.x, d.pt.y);
            switch (w.ToInt32()) { case 0x200: mouseCallback(MouseButtons.None, false, p, true); break; case 0x201: mouseCallback(MouseButtons.Left, true, p, false); break; case 0x202: mouseCallback(MouseButtons.Left, false, p, false); break; case 0x204: mouseCallback(MouseButtons.Right, true, p, false); break; case 0x205: mouseCallback(MouseButtons.Right, false, p, false); break; case 0x207: mouseCallback(MouseButtons.Middle, true, p, false); break; case 0x208: mouseCallback(MouseButtons.Middle, false, p, false); break; }
        }
        return Native.CallNextHookEx(mouseHook, n, w, l);
    }
    public void Dispose() { if (kbHook != IntPtr.Zero) Native.UnhookWindowsHookEx(kbHook); if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook); }
}

internal static class Native
{
    public const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1, MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004, MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010, MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040, KEYEVENTF_KEYUP = 0x0002, LLKHF_INJECTED = 0x10, LLMHF_INJECTED = 1;
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam); public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW")] public static extern IntPtr SetWindowsHookExKeyboard(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW")] public static extern IntPtr SetWindowsHookExMouse(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk); [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam); [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize); [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    public static void SendKey(Keys key, bool down) { var input = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)key, dwFlags = down ? 0u : KEYEVENTF_KEYUP } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); }
    public static void SendMouse(uint flag) { var input = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flag } } }; SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()); }
    public static void Click(MouseButtons button) { uint down = button switch { MouseButtons.Left => MOUSEEVENTF_LEFTDOWN, MouseButtons.Right => MOUSEEVENTF_RIGHTDOWN, MouseButtons.Middle => MOUSEEVENTF_MIDDLEDOWN, _ => 0 }; uint up = button switch { MouseButtons.Left => MOUSEEVENTF_LEFTUP, MouseButtons.Right => MOUSEEVENTF_RIGHTUP, MouseButtons.Middle => MOUSEEVENTF_MIDDLEUP, _ => 0 }; if (down != 0) { SendMouse(down); SendMouse(up); } }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo; }
}