using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Vitals {
  internal static class Program {
    [STAThread]
    private static void Main(string[] args) {
      if (args != null && args.Length >= 2 && args[0] == "--shot") {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var window = new SettingsWindow(Settings.Load())) {
          window.Show();
          Application.DoEvents();
          using (var bmp = new Bitmap(window.Width, window.Height)) {
            window.DrawToBitmap(bmp, new Rectangle(0, 0, window.Width, window.Height));
            bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
          }
          window.Close();
        }
        return;
      }

      if (args != null && args.Length >= 2 && args[0] == "--bench") {
        var sb = new StringBuilder();
        var watch = new System.Diagnostics.Stopwatch();
        const int rounds = 10;

        Sensors.Read(true, true, true);   // 预热
        Thread.Sleep(200);

        watch.Restart();
        for (int i = 0; i < rounds; i++) Sensors.Read(true, true, true);
        sb.AppendLine("完整采样(含风扇/磁盘网络/电池): " + (watch.Elapsed.TotalMilliseconds / rounds).ToString("F2") + " ms");

        watch.Restart();
        for (int i = 0; i < rounds; i++) Sensors.Read(false, false, false);
        sb.AppendLine("仅 CPU+GPU+内存:            " + (watch.Elapsed.TotalMilliseconds / rounds).ToString("F2") + " ms");

        int f1, f2, f3;
        watch.Restart();
        for (int i = 0; i < rounds; i++) HpBios.TryGetFans(out f1, out f2, out f3);
        sb.AppendLine("风扇(WMI 查询):             " + (watch.Elapsed.TotalMilliseconds / rounds).ToString("F2") + " ms");

        watch.Restart();
        for (int i = 0; i < rounds; i++) RateCounters.Sample();
        sb.AppendLine("磁盘/网络(PDH):             " + (watch.Elapsed.TotalMilliseconds / rounds).ToString("F2") + " ms");

        watch.Restart();
        for (int i = 0; i < rounds; i++) {
          var bmp = new Bitmap(400, 200, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
          using (var g = Graphics.FromImage(bmp)) {
            Font font = SystemFonts.DefaultFont;
            for (int line = 0; line < 7; line++) {
              g.DrawString("CPU 55°C 20W 3.5G " + line, font, Brushes.White, 4, 4 + line * 22);
              g.DrawString("CPU 55°C 20W 3.5G " + line, font, Brushes.Black, 5.2f, 5.2f + line * 22);
            }
          }
          bmp.Dispose();
        }
        sb.AppendLine("悬浮条重绘(7 行含阴影):      " + (watch.Elapsed.TotalMilliseconds / rounds).ToString("F2") + " ms");

        sb.AppendLine("工作集: " + (Environment.WorkingSet / 1048576) + " MB");
        File.WriteAllText(args[1], sb.ToString(), Encoding.UTF8);
        return;
      }

      if (args != null && args.Length >= 4 && args[0] == "--fps-test") {
        string name = args[1];
        int duration = int.Parse(args[2]);
        using (var monitor = new FrameMonitor()) {
          bool started = monitor.Start();
          Thread.Sleep(duration);
          double fps = monitor.GetFps(name);
          var report = new StringBuilder();
          report.AppendLine("started=" + started);
          report.AppendLine("target=" + name);
          report.AppendLine("fps=" + fps.ToString("F1"));
          report.AppendLine("error=" + monitor.LastError);
          File.WriteAllText(args[3], report.ToString(), Encoding.UTF8);
        }
        return;
      }

      if (args != null && args.Length >= 2 && args[0] == "--fps-dump") {
        using (var monitor = new FrameMonitor()) {
          monitor.Start();
          Thread.Sleep(5000);
          var report = new StringBuilder();
          report.AppendLine("error=" + monitor.LastError);
          foreach (string name in monitor.Names())
            report.AppendLine(name + " = " + monitor.GetFps(name).ToString("F1") + " fps");
          File.WriteAllText(args[1], report.ToString(), Encoding.UTF8);
        }
        return;
      }

      if (args != null && args.Length > 0 && args[0] == "--once") {
        string path = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "vitals_sample.txt");
        var sample = Sensors.Read(true, true, true);
        Thread.Sleep(700);
        sample = Sensors.Read(true, true, true);
        var sb = new StringBuilder();
        sb.AppendLine("CPU 可用: " + sample.CpuOk);
        sb.AppendLine("CPU 温度: " + sample.CpuTemp.ToString("F1") + " °C");
        sb.AppendLine("CPU 功耗: " + sample.CpuPower.ToString("F1") + " W");
        sb.AppendLine("CPU 频率: " + sample.CpuClockMhz.ToString("F0") + " MHz");
        sb.AppendLine("CPU 占用: " + sample.CpuUsage.ToString("F1") + " %");
        sb.AppendLine("GPU 可用: " + sample.GpuOk);
        sb.AppendLine("GPU 温度: " + sample.GpuTemp.ToString("F0") + " °C");
        sb.AppendLine("GPU 功耗: " + sample.GpuPower.ToString("F1") + " W");
        sb.AppendLine("GPU 频率: " + sample.GpuClockMhz.ToString("F0") + " MHz");
        sb.AppendLine("GPU 显存: " + sample.GpuMemUsedMb.ToString("F0") + " / " + sample.GpuMemTotalMb.ToString("F0") + " MB");
        sb.AppendLine("GPU 占用: " + sample.GpuUtil + " %");
        sb.AppendLine("风扇: " + sample.FanRpm1 + " / " + sample.FanRpm2 + " / " + sample.FanRpm3);
        sb.AppendLine("磁盘: R " + sample.DiskReadBps.ToString("F0") + " B/s  W " + sample.DiskWriteBps.ToString("F0") + " B/s");
        sb.AppendLine("网络: " + sample.NetBps.ToString("F0") + " B/s");
        sb.AppendLine("电池: " + sample.BatteryPercent + " %  在线=" + sample.BatteryOnline + "  功率=" + sample.BatteryWatts);
        sb.AppendLine("内存占用: " + sample.MemPercent + " %");
        sb.AppendLine("PawnIO 诊断: " + Sensors.PawnIoDiagnostic);
        sb.AppendLine("是否管理员: " + (new System.Security.Principal.WindowsPrincipal(
          System.Security.Principal.WindowsIdentity.GetCurrent())
          .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return;
      }

      bool created;
      using (var mutex = new Mutex(true, "Vitals.SingleInstance.7F3A", out created)) {
        if (!created) return;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write(e.ExceptionObject as Exception);
        Application.ThreadException += (s, e) => Log.Write(e.Exception);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try {
          Application.Run(new TrayApp());
        } catch (Exception ex) {
          Log.Write(ex);
          MessageBox.Show(ex.Message, "Vitals", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
      }
    }
  }

  internal sealed class Sample {
    public bool CpuOk;
    public double CpuTemp;
    public double CpuPower;
    public double CpuClockMhz;
    public double CpuUsage;
    public bool GpuOk;
    public double GpuTemp;
    public double GpuPower;
    public double GpuClockMhz;
    public double GpuMemClockMhz;
    public int GpuUtil = -1;
    public double GpuMemUsedMb;
    public double GpuMemTotalMb;
    public int FanRpm1 = -1;
    public int FanRpm2 = -1;
    public int FanRpm3 = -1;
    public double DiskReadBps;
    public double DiskWriteBps;
    public double NetBps;
    public int BatteryPercent = -1;
    public bool BatteryOnline;
    public double BatteryWatts = -1;
    public int BatteryMinutes = -1;
    public int MemPercent = -1;
    public string CpuThrottle = "";
    public string GpuThrottle = "";
  }

  internal sealed class DisplayLine {
    public string Title;
    public string Value;
    public Color TitleColor;
    public Color ValueColor;
    public double[] Chart;
  }

  // 所有可配置项，保存在 exe 同目录的 Vitals.ini
  internal sealed class Settings {
    public string CpuName = "CPU";
    public string GpuName = "GPU";
    public string VramName = "显存";
    public string FanName = "风扇";
    public string DiskName = "磁盘";
    public string NetName = "网络";
    public string BatName = "电池";
    public string SysName = "SYS";
    public string FpsName = "FPS";

    public bool CpuTemp = true;
    public bool CpuPower = true;
    public bool CpuFreq = true;
    public bool CpuUsage = false;

    public bool GpuTemp = true;
    public bool GpuPower = true;
    public bool GpuFreq = true;
    public bool GpuUsage = false;
    public bool VramUsed = true;

    public bool Fan = true;
    public bool FanPercent = false;
    public int FanMaxRpm = 7000;
    public bool Disk = false;
    public bool Net = false;
    public bool Battery = false;
    public bool SysMem = true;

    public bool ShowFps = false;
    public bool ShowLowFps = false;
    public bool ShowFpsChart = false;
    public bool CpuTempChart = false;
    public bool CpuPowerChart = false;
    public bool GpuTempChart = false;
    public bool GpuPowerChart = false;
    public bool ThrottleHint = true;
    public bool HotkeyEnabled = true;
    public bool ShowPeak = false;
    public bool CsvLog = false;
    public bool AutoProfile = false;
    public bool SnapEdge = true;

    public int Interval = 1000;
    public int FontSize = 12;
    public int Background = 0;      // 0 = 纯文字, 1 = 半透明底板
    public int Opacity = 150;       // 底板不透明度 40~235
    public int PosX = 12;
    public int PosY = 12;
    public int Screen = -1;         // -1 = 主屏
    public bool Overlay = true;
    public bool DragRequested = false;   // 仅本次运行有效，不写入配置文件

    public static string FilePath {
      get {
        try {
          string dir = Path.GetDirectoryName(Application.ExecutablePath);
          if (!string.IsNullOrEmpty(dir)) return Path.Combine(dir, "Vitals.ini");
        } catch { }
        return Path.Combine(Path.GetTempPath(), "Vitals.ini");
      }
    }

    public static Settings Load() {
      var settings = new Settings();
      try {
        if (!File.Exists(FilePath)) return settings;
        foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8)) {
          string line = raw.Trim();
          if (line.Length == 0 || line.StartsWith("#")) continue;
          int split = line.IndexOf('=');
          if (split <= 0) continue;
          settings.Apply(line.Substring(0, split).Trim(), line.Substring(split + 1).Trim());
        }
      } catch { }
      return settings;
    }

    private void Apply(string key, string value) {
      switch (key) {
        case "CpuName": CpuName = value; break;
        case "GpuName": GpuName = value; break;
        case "VramName": VramName = value; break;
        case "FanName": FanName = value; break;
        case "DiskName": DiskName = value; break;
        case "NetName": NetName = value; break;
        case "BatName": BatName = value; break;
        case "SysName": SysName = value; break;
        case "FpsName": FpsName = value; break;
        case "CpuTemp": CpuTemp = ParseBool(value, CpuTemp); break;
        case "CpuPower": CpuPower = ParseBool(value, CpuPower); break;
        case "CpuFreq": CpuFreq = ParseBool(value, CpuFreq); break;
        case "CpuUsage": CpuUsage = ParseBool(value, CpuUsage); break;
        case "GpuTemp": GpuTemp = ParseBool(value, GpuTemp); break;
        case "GpuPower": GpuPower = ParseBool(value, GpuPower); break;
        case "GpuFreq": GpuFreq = ParseBool(value, GpuFreq); break;
        case "GpuUsage": GpuUsage = ParseBool(value, GpuUsage); break;
        case "VramUsed": VramUsed = ParseBool(value, VramUsed); break;
        case "Fan": Fan = ParseBool(value, Fan); break;
        case "Disk": Disk = ParseBool(value, Disk); break;
        case "Net": Net = ParseBool(value, Net); break;
        case "Battery": Battery = ParseBool(value, Battery); break;
        case "SysMem": SysMem = ParseBool(value, SysMem); break;
        case "ShowFps": ShowFps = ParseBool(value, ShowFps); break;
        case "ShowLowFps": ShowLowFps = ParseBool(value, ShowLowFps); break;
        case "ShowFpsChart": ShowFpsChart = ParseBool(value, ShowFpsChart); break;
        case "CpuTempChart": CpuTempChart = ParseBool(value, CpuTempChart); break;
        case "CpuPowerChart": CpuPowerChart = ParseBool(value, CpuPowerChart); break;
        case "GpuTempChart": GpuTempChart = ParseBool(value, GpuTempChart); break;
        case "GpuPowerChart": GpuPowerChart = ParseBool(value, GpuPowerChart); break;
        case "FanPercent": FanPercent = ParseBool(value, FanPercent); break;
        case "ThrottleHint": ThrottleHint = ParseBool(value, ThrottleHint); break;
        case "HotkeyEnabled": HotkeyEnabled = ParseBool(value, HotkeyEnabled); break;
        case "FanMaxRpm": { int v; if (int.TryParse(value, out v) && v >= 1000 && v <= 12000) FanMaxRpm = v; break; }
        case "ShowPeak": ShowPeak = ParseBool(value, ShowPeak); break;
        case "CsvLog": CsvLog = ParseBool(value, CsvLog); break;
        case "AutoProfile": AutoProfile = ParseBool(value, AutoProfile); break;
        case "SnapEdge": SnapEdge = ParseBool(value, SnapEdge); break;
        case "Overlay": Overlay = ParseBool(value, Overlay); break;
        case "Interval": { int v; if (int.TryParse(value, out v) && (v == 1000 || v == 2000 || v == 5000)) Interval = v; break; }
        case "FontSize": { int v; if (int.TryParse(value, out v) && v >= 8 && v <= 32) FontSize = v; break; }
        case "Background": { int v; if (int.TryParse(value, out v) && v >= 0 && v <= 1) Background = v; break; }
        case "Opacity": { int v; if (int.TryParse(value, out v) && v >= 40 && v <= 235) Opacity = v; break; }
        case "PosX": { int v; if (int.TryParse(value, out v)) PosX = v; break; }
        case "PosY": { int v; if (int.TryParse(value, out v)) PosY = v; break; }
        case "Screen": { int v; if (int.TryParse(value, out v)) Screen = v; break; }
      }
    }

    private static bool ParseBool(string value, bool fallback) {
      if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1") return true;
      if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0") return false;
      return fallback;
    }

    public void Save() {
      try {
        var sb = new StringBuilder();
        sb.AppendLine("CpuName=" + CpuName);
        sb.AppendLine("GpuName=" + GpuName);
        sb.AppendLine("VramName=" + VramName);
        sb.AppendLine("FanName=" + FanName);
        sb.AppendLine("DiskName=" + DiskName);
        sb.AppendLine("NetName=" + NetName);
        sb.AppendLine("BatName=" + BatName);
        sb.AppendLine("SysName=" + SysName);
        sb.AppendLine("FpsName=" + FpsName);
        sb.AppendLine("CpuTemp=" + CpuTemp);
        sb.AppendLine("CpuPower=" + CpuPower);
        sb.AppendLine("CpuFreq=" + CpuFreq);
        sb.AppendLine("CpuUsage=" + CpuUsage);
        sb.AppendLine("GpuTemp=" + GpuTemp);
        sb.AppendLine("GpuPower=" + GpuPower);
        sb.AppendLine("GpuFreq=" + GpuFreq);
        sb.AppendLine("GpuUsage=" + GpuUsage);
        sb.AppendLine("VramUsed=" + VramUsed);
        sb.AppendLine("Fan=" + Fan);
        sb.AppendLine("Disk=" + Disk);
        sb.AppendLine("Net=" + Net);
        sb.AppendLine("Battery=" + Battery);
        sb.AppendLine("SysMem=" + SysMem);
        sb.AppendLine("ShowFps=" + ShowFps);
        sb.AppendLine("ShowLowFps=" + ShowLowFps);
        sb.AppendLine("ShowFpsChart=" + ShowFpsChart);
        sb.AppendLine("CpuTempChart=" + CpuTempChart);
        sb.AppendLine("CpuPowerChart=" + CpuPowerChart);
        sb.AppendLine("GpuTempChart=" + GpuTempChart);
        sb.AppendLine("GpuPowerChart=" + GpuPowerChart);
        sb.AppendLine("FanPercent=" + FanPercent);
        sb.AppendLine("FanMaxRpm=" + FanMaxRpm);
        sb.AppendLine("ThrottleHint=" + ThrottleHint);
        sb.AppendLine("HotkeyEnabled=" + HotkeyEnabled);
        sb.AppendLine("ShowPeak=" + ShowPeak);
        sb.AppendLine("CsvLog=" + CsvLog);
        sb.AppendLine("AutoProfile=" + AutoProfile);
        sb.AppendLine("SnapEdge=" + SnapEdge);
        sb.AppendLine("Overlay=" + Overlay);
        sb.AppendLine("Interval=" + Interval);
        sb.AppendLine("FontSize=" + FontSize);
        sb.AppendLine("Background=" + Background);
        sb.AppendLine("Opacity=" + Opacity);
        sb.AppendLine("PosX=" + PosX);
        sb.AppendLine("PosY=" + PosY);
        sb.AppendLine("Screen=" + Screen);
        File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
      } catch { }
    }
  }

  internal sealed class InputDialog : Form {
    private readonly TextBox _box;

    public string Value { get { return _box.Text.Trim(); } }

    public InputDialog(string title, string current) {
      Text = title;
      FormBorderStyle = FormBorderStyle.FixedDialog;
      StartPosition = FormStartPosition.CenterScreen;
      MinimizeBox = false;
      MaximizeBox = false;
      ShowInTaskbar = false;
      TopMost = true;
      ClientSize = new Size(320, 96);

      var label = new Label { Text = "显示名称：", Left = 12, Top = 12, AutoSize = true };
      _box = new TextBox { Left = 12, Top = 34, Width = 296, Text = current };
      var ok = new Button { Text = "确定", Left = 232, Top = 62, Width = 76, DialogResult = DialogResult.OK };
      var cancel = new Button { Text = "取消", Left = 148, Top = 62, Width = 76, DialogResult = DialogResult.Cancel };

      Controls.Add(label);
      Controls.Add(_box);
      Controls.Add(ok);
      Controls.Add(cancel);
      AcceptButton = ok;
      CancelButton = cancel;
    }
  }

  internal sealed class TrayApp : ApplicationContext {
    private readonly NotifyIcon _tray;
    private readonly OverlayForm _overlay;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly FrameMonitor _fps = new FrameMonitor();
    private readonly Settings _settings = Settings.Load();
    private readonly Dictionary<string, double> _peaks = new Dictionary<string, double>();
    private readonly Dictionary<string, Queue<double>> _history = new Dictionary<string, Queue<double>>();
    private string _fpsTarget = "";
    private int _currentProfile;
    private int _sampling;

    public TrayApp() {
      _overlay = new OverlayForm();
      _overlay.Moved += (s, e) => {
        if (_settings.SnapEdge) _overlay.SnapToEdges();
        _settings.PosX = _overlay.Left;
        _settings.PosY = _overlay.Top;
        _settings.Save();
      };
      _overlay.Hotkey += (s, e) => {
        _settings.Overlay = !_settings.Overlay;
        _settings.Save();
        _overlay.Visible = _settings.Overlay;
      };

      var menu = new ContextMenuStrip();
      var overlayItem = new ToolStripMenuItem("显示悬浮条") { Checked = _settings.Overlay, CheckOnClick = true };
      overlayItem.CheckedChanged += (s, e) => {
        _settings.Overlay = overlayItem.Checked;
        _settings.Save();
        _overlay.Visible = overlayItem.Checked;
      };
      menu.Items.Add(overlayItem);
      menu.Items.Add(new ToolStripSeparator());

      var profileMenu = new ToolStripMenuItem("方案");
      profileMenu.DropDownItems.Add(ProfileItem("游戏", 1));
      profileMenu.DropDownItems.Add(ProfileItem("办公", 2));
      profileMenu.DropDownItems.Add(ProfileItem("极简", 3));
      menu.Items.Add(profileMenu);

      var settingsItem = new ToolStripMenuItem("设置…");
      settingsItem.Click += (s, e) => ShowSettings();
      menu.Items.Add(settingsItem);

      var intervalMenu = new ToolStripMenuItem("刷新间隔");
      foreach (int ms in new[] { 1000, 2000, 5000 }) {
        int value = ms;
        var item = new ToolStripMenuItem((ms / 1000) + " 秒") { Checked = ms == _settings.Interval };
        item.Click += (s, e) => {
          _settings.Interval = value;
          _settings.Save();
          _timer.Interval = value;
          foreach (ToolStripItem other in intervalMenu.DropDownItems) ((ToolStripMenuItem)other).Checked = false;
          item.Checked = true;
        };
        intervalMenu.DropDownItems.Add(item);
      }
      menu.Items.Add(intervalMenu);

      var startMenu = new ToolStripMenuItem("开机自启");
      var installItem = new ToolStripMenuItem("安装（计划任务，免 UAC）");
      installItem.Click += (s, e) => ShowResult(AutoStart.Install());
      var removeItem = new ToolStripMenuItem("移除");
      removeItem.Click += (s, e) => ShowResult(AutoStart.Remove());
      startMenu.DropDownItems.Add(installItem);
      startMenu.DropDownItems.Add(removeItem);
      menu.Items.Add(startMenu);

      menu.Items.Add(new ToolStripSeparator());
      var exit = new ToolStripMenuItem("退出");
      exit.Click += (s, e) => ExitThread();
      menu.Items.Add(exit);

      _tray = new NotifyIcon {
        Icon = LoadIcon(),
        Visible = true,
        Text = "Vitals 启动中",
        ContextMenuStrip = menu
      };
      _tray.DoubleClick += (s, e) => {
        _settings.Overlay = !_settings.Overlay;
        _settings.Save();
        _overlay.Visible = _settings.Overlay;
        overlayItem.Checked = _settings.Overlay;
      };

      _overlay.ShowInTaskbar = false;
      ApplyAppearance();
      _overlay.MoveTo(_settings.PosX, _settings.PosY);
      _overlay.Visible = _settings.Overlay;
      _overlay.SetHotkey(_settings.HotkeyEnabled);

      _timer = new System.Windows.Forms.Timer { Interval = _settings.Interval };
      _timer.Tick += (s, e) => Refresh();
      _timer.Start();

      _settings.Save();
      Refresh();
    }

    private void ShowSettings() {
      using (var window = new SettingsWindow(_settings)) {
        window.ShowDialog();
        _settings.Save();
        _timer.Interval = _settings.Interval;
        ApplyAppearance();
        _overlay.Visible = _settings.Overlay;
        _overlay.MoveTo(_settings.PosX, _settings.PosY);
        _overlay.SetHotkey(_settings.HotkeyEnabled);
        _overlay.SetDragMode(_settings.DragRequested);
        _peaks.Clear();
        Refresh();
      }
    }

    private void ApplyAppearance() {
      _overlay.SetFontSize(_settings.FontSize);
      _overlay.SetBackground(_settings.Background, _settings.Opacity);
    }

    private static void ShowResult(bool ok) {
      MessageBox.Show(ok ? "已设置。" : "操作失败，请检查计划任务权限。", "Vitals",
        MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private ToolStripMenuItem ProfileItem(string text, int id) {
      var item = new ToolStripMenuItem(text);
      item.Click += (s, e) => ApplyProfile(id);
      return item;
    }

    private void ApplyProfile(int id) {
      _currentProfile = id;
      switch (id) {
        case 1:
          _settings.CpuTemp = _settings.CpuPower = _settings.CpuFreq = true;
          _settings.CpuUsage = false;
          _settings.GpuTemp = _settings.GpuPower = _settings.GpuFreq = true;
          _settings.GpuUsage = true;
          _settings.VramUsed = true;
          _settings.Fan = true;
          _settings.SysMem = false;
          _settings.ShowFps = true;
          _settings.ShowLowFps = true;
          _settings.ShowFpsChart = true;
          break;
        case 2:
          _settings.CpuTemp = true;
          _settings.CpuPower = _settings.CpuFreq = _settings.CpuUsage = false;
          _settings.GpuTemp = true;
          _settings.GpuPower = _settings.GpuFreq = _settings.GpuUsage = false;
          _settings.VramUsed = false;
          _settings.Fan = false;
          _settings.SysMem = true;
          _settings.ShowFps = _settings.ShowLowFps = _settings.ShowFpsChart = false;
          break;
        default:
          _settings.CpuTemp = true;
          _settings.CpuPower = _settings.CpuFreq = _settings.CpuUsage = false;
          _settings.GpuTemp = true;
          _settings.GpuPower = _settings.GpuFreq = _settings.GpuUsage = false;
          _settings.VramUsed = _settings.Fan = _settings.SysMem = false;
          _settings.ShowFps = _settings.ShowLowFps = _settings.ShowFpsChart = false;
          break;
      }
      if (!_settings.ShowFps) _fps.Stop();
      _settings.Save();
      Refresh();
    }

    // 采样放到后台线程，UI 线程只负责渲染
    private void Refresh() {
      if (Interlocked.CompareExchange(ref _sampling, 1, 0) != 0) return;

      bool needFan = _settings.Fan;
      bool needIo = _settings.Disk || _settings.Net;
      bool needBattery = _settings.Battery;
      SynchronizationContext ui = SynchronizationContext.Current;

      ThreadPool.QueueUserWorkItem(_ => {
        Sample sample = null;
        double fps = 0, low = 0;
        try {
          sample = Sensors.Read(needFan, needIo, needBattery);
          if (_settings.ShowFps) {
            fps = _fps.GetFps(_fpsTarget);
            if (_settings.ShowLowFps) low = _fps.GetLowFps(_fpsTarget);
          }
          if (_settings.CsvLog) CsvLog.Append(sample, fps, low);
        } catch {
        } finally {
          Interlocked.Exchange(ref _sampling, 0);
        }

        if (sample == null) return;
        Sample result = sample;
        if (ui != null) {
          ui.Post(state => Apply(result, fps, low), null);
        } else {
          try { _overlay.BeginInvoke((Action)(() => Apply(result, fps, low))); } catch { }
        }
      });
    }

    private void Apply(Sample sample, double fps, double low) {
      if (_settings.AutoProfile) {
        int desired = ForegroundName() == "dwm.exe" ? 2 : 1;
        if (_currentProfile != 0 && _currentProfile != desired) ApplyProfile(desired);
      }

      if (_settings.ShowFps) UpdateFpsMonitor();
      else _fps.Stop();

      if (!_settings.ShowFps) {
        fps = 0;
        low = 0;
      }

      Push("cput", _settings.CpuTempChart ? sample.CpuTemp : 0);
      Push("cpup", _settings.CpuPowerChart ? sample.CpuPower : 0);
      Push("gput", _settings.GpuTempChart ? sample.GpuTemp : 0);
      Push("gpup", _settings.GpuPowerChart ? sample.GpuPower : 0);
      if (_settings.ShowFpsChart) Push("fps", fps);

      List<DisplayLine> lines = BuildLines(sample, fps, low);
      var text = new StringBuilder();
      foreach (DisplayLine line in lines) {
        if (line.Title == null || line.Title.Length == 0) continue;   // 图表行不进托盘提示
        if (text.Length > 0) text.Append('\n');
        text.Append(line.Title).Append(' ').Append(line.Value);
      }
      if (text.Length == 0) text.Append("未选择监测项");

      _tray.Text = Trim(text.ToString(), 63);
      _overlay.SetLines(lines);
    }

    private void Push(string key, double value) {
      Queue<double> queue;
      if (!_history.TryGetValue(key, out queue)) {
        queue = new Queue<double>();
        _history[key] = queue;
      }
      queue.Enqueue(value);
      while (queue.Count > 60) queue.Dequeue();
    }

    private double[] ChartFor(string key, bool enabled) {
      if (!enabled) return null;
      Queue<double> queue;
      if (!_history.TryGetValue(key, out queue) || queue.Count < 2) return null;
      bool any = false;
      foreach (double value in queue) {
        if (value > 0) { any = true; break; }
      }
      return any ? queue.ToArray() : null;
    }

    private string ForegroundName() {
      try {
        int pid = FrameMonitor.ForegroundPid();
        if (pid <= 0) return "";
        if (pid == Process.GetCurrentProcess().Id) return _fpsTarget;
        string name = Process.GetProcessById(pid).ProcessName + ".exe";
        if (FrameMonitor.IsShellProcess(pid)) return "dwm.exe";
        return name;
      } catch {
        return "";
      }
    }

    private void UpdateFpsMonitor() {
      try {
        if (!_fps.Running) _fps.Start();
        string name = ForegroundName();
        if (name.Length > 0) _fpsTarget = name;
      } catch {
      }
    }

    private double Peak(string key, double value) {
      double old;
      if (_peaks.TryGetValue(key, out old)) {
        if (value > old) { _peaks[key] = value; return value; }
        return old;
      }
      _peaks[key] = value;
      return value;
    }

    private List<DisplayLine> BuildLines(Sample s, double fps, double low) {
      var lines = new List<DisplayLine>();
      Color cpuColor = Color.FromArgb(255, 90, 170, 230);
      Color gpuColor = Color.FromArgb(255, 120, 210, 130);
      Color vramColor = Color.FromArgb(255, 190, 150, 240);
      Color fanColor = Color.FromArgb(255, 120, 200, 200);
      Color ioColor = Color.FromArgb(255, 170, 180, 200);
      Color batColor = Color.FromArgb(255, 220, 200, 120);
      Color sysColor = Color.FromArgb(255, 240, 190, 90);
      Color fpsColor = Color.FromArgb(255, 235, 235, 235);
      Color hot = Color.FromArgb(255, 240, 90, 90);

      var cpu = new List<string>();
      var cpuFlags = new List<bool>();
      if (s.CpuOk) {
        if (_settings.CpuTemp) {
          cpu.Add(TempWithPeak("cput", s.CpuTemp));
          cpuFlags.Add(s.CpuTemp >= 85);
        }
        if (_settings.CpuPower) { cpu.Add(s.CpuPower.ToString("F0") + "W"); cpuFlags.Add(false); }
        if (_settings.CpuFreq) { cpu.Add((s.CpuClockMhz / 1000.0).ToString("F1") + "G"); cpuFlags.Add(false); }
        if (_settings.CpuUsage) { cpu.Add(s.CpuUsage.ToString("F0") + "%"); cpuFlags.Add(s.CpuUsage >= 90); }
        if (_settings.ThrottleHint && s.CpuThrottle.Length > 0) cpu.Add("↓" + s.CpuThrottle);
      }
      if (cpu.Count > 0) lines.Add(Line(_settings.CpuName, cpu, cpuFlags, cpuColor, hot));
      var cpuChart = ChartFor("cput", _settings.CpuTempChart);
      if (cpuChart != null) lines.Add(ChartLine(cpuChart, cpuColor));

      var gpu = new List<string>();
      var gpuFlags = new List<bool>();
      if (s.GpuOk) {
        if (_settings.GpuTemp) {
          gpu.Add(TempWithPeak("gput", s.GpuTemp));
          gpuFlags.Add(s.GpuTemp >= 85);
        }
        if (_settings.GpuPower) { gpu.Add(s.GpuPower.ToString("F0") + "W"); gpuFlags.Add(false); }
        if (_settings.GpuFreq) { gpu.Add(s.GpuClockMhz.ToString("F0") + "M"); gpuFlags.Add(false); }
        if (_settings.GpuUsage && s.GpuUtil >= 0) { gpu.Add(s.GpuUtil + "%"); gpuFlags.Add(false); }
        if (_settings.ThrottleHint && s.GpuThrottle.Length > 0) gpu.Add("↓" + s.GpuThrottle);
      }
      if (gpu.Count > 0) lines.Add(Line(_settings.GpuName, gpu, gpuFlags, gpuColor, hot));
      var gpuChart = ChartFor("gput", _settings.GpuTempChart);
      if (gpuChart != null) lines.Add(ChartLine(gpuChart, gpuColor));

      var vram = new List<string>();
      if (_settings.VramUsed && s.GpuMemTotalMb > 0)
        vram.Add((s.GpuMemUsedMb / 1024.0).ToString("F1") + "/" + (s.GpuMemTotalMb / 1024.0).ToString("F1") + "G");
      if (vram.Count > 0) lines.Add(Line(_settings.VramName, vram, new List<bool> { false }, vramColor, hot));

      var fans = new List<string>();
      if (_settings.Fan && s.FanRpm1 >= 0) {
        fans.Add(FanText(s.FanRpm1));
        if (s.FanRpm2 > 0) fans.Add(FanText(s.FanRpm2));
        if (s.FanRpm3 > 0) fans.Add(FanText(s.FanRpm3));
      }
      if (fans.Count > 0) lines.Add(Line(_settings.FanName, fans, new List<bool> { false }, fanColor, hot));

      if (_settings.Disk && (s.DiskReadBps > 0 || s.DiskWriteBps > 0)) {
        lines.Add(Line(_settings.DiskName,
          new List<string> { "R" + FormatRate(s.DiskReadBps), "W" + FormatRate(s.DiskWriteBps) },
          new List<bool> { false, false }, ioColor, hot));
      }

      if (_settings.Net)
        lines.Add(Line(_settings.NetName, new List<string> { FormatRate(s.NetBps) }, new List<bool> { false }, ioColor, hot));

      if (_settings.Battery && s.BatteryPercent >= 0) {
        var parts = new List<string> { s.BatteryPercent + "%" };
        var flags = new List<bool> { s.BatteryPercent <= 15 };
        if (s.BatteryWatts >= 0) { parts.Add(s.BatteryWatts.ToString("F1") + "W"); flags.Add(false); }
        if (!s.BatteryOnline && s.BatteryMinutes > 0) {
          parts.Add((s.BatteryMinutes / 60) + "h" + (s.BatteryMinutes % 60).ToString("00") + "m");
          flags.Add(false);
        }
        lines.Add(Line(_settings.BatName, parts, flags, batColor, hot));
      }

      if (_settings.SysMem && s.MemPercent >= 0)
        lines.Add(Line(_settings.SysName, new List<string> { s.MemPercent + "%" }, new List<bool> { s.MemPercent >= 90 }, sysColor, hot));

      if (_settings.ShowFps) {
        var parts = new List<string> { fps > 0 ? fps.ToString("F0") : "--" };
        if (_settings.ShowLowFps) parts.Add(low > 0 ? "1%:" + low.ToString("F0") : "1%:--");
        lines.Add(Line(_settings.FpsName, parts, new List<bool> { false }, fpsColor, hot));
        var fpsChart = ChartFor("fps", _settings.ShowFpsChart);
        if (fpsChart != null) lines.Add(ChartLine(fpsChart, fpsColor));
      }

      return lines;
    }

    private string TempWithPeak(string key, double value) {
      string text = value.ToString("F0") + "°C";
      if (!_settings.ShowPeak) return text;
      double peak = Peak(key, value);
      if (peak > value + 0.5) return text + "↑" + peak.ToString("F0");
      return text;
    }

    private string FanText(int rpm) {
      if (!_settings.FanPercent || _settings.FanMaxRpm <= 0) return rpm.ToString();
      int percent = (int)Math.Round(rpm * 100.0 / _settings.FanMaxRpm);
      if (percent > 100) percent = 100;
      return rpm + "(" + percent + "%)";
    }

    private static DisplayLine ChartLine(double[] values, Color color) {
      return new DisplayLine {
        Title = "",
        Value = "",
        TitleColor = color,
        ValueColor = color,
        Chart = values
      };
    }

    private static DisplayLine Line(string title, List<string> parts, List<bool> flags, Color titleColor, Color hot) {
      bool alert = false;
      foreach (bool flag in flags) if (flag) alert = true;
      return new DisplayLine {
        Title = title,
        Value = string.Join("  ", parts),
        TitleColor = titleColor,
        ValueColor = alert ? hot : Color.FromArgb(255, 245, 245, 245)
      };
    }

    private static string FormatRate(double bytesPerSecond) {
      if (bytesPerSecond < 1024) return bytesPerSecond.ToString("F0") + "B/s";
      if (bytesPerSecond < 1048576) return (bytesPerSecond / 1024.0).ToString("F0") + "K/s";
      return (bytesPerSecond / 1048576.0).ToString("F1") + "M/s";
    }

    private static string Trim(string value, int max) {
      if (value.Length <= max) return value;
      return value.Substring(0, max - 1) + "…";
    }

    // 托盘图标直接取 exe 内嵌的 app.ico，16×16 那一帧
    private static Icon LoadIcon() {
      try {
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Vitals.app.ico")) {
          if (stream != null) return new Icon(stream, new Size(16, 16));
        }
      } catch { }
      try {
        return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
      } catch {
        return SystemIcons.Application;
      }
    }

    protected override void ExitThreadCore() {
      _timer.Stop();
      try { _fps.Stop(); } catch { }
      _tray.Visible = false;
      _tray.Dispose();
      _overlay.Close();
      base.ExitThreadCore();
    }
  }


  // 设置窗口：把所有开关从三级菜单挪到这里
  internal sealed class SettingsWindow : Form {
    private readonly Settings _s;

    public SettingsWindow(Settings settings) {
      _s = settings;
      Text = "Vitals 设置";
      StartPosition = FormStartPosition.CenterScreen;
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false;
      MinimizeBox = false;
      ShowInTaskbar = false;
      TopMost = true;
      ClientSize = new Size(480, 520);

      var tabs = new TabControl { Dock = DockStyle.Fill };

      tabs.TabPages.Add(BuildMonitorTab());
      tabs.TabPages.Add(BuildCurveTab());
      tabs.TabPages.Add(BuildLookTab());
      tabs.TabPages.Add(BuildAboutTab());

      Controls.Add(tabs);
    }

    private TabPage BuildMonitorTab() {
      var page = new TabPage("监测项");

      Label(page, "CPU", 16, 12);
      Check(page, "温度", 16, 38, () => _s.CpuTemp, v => _s.CpuTemp = v);
      Check(page, "功耗", 16, 66, () => _s.CpuPower, v => _s.CpuPower = v);
      Check(page, "频率", 16, 94, () => _s.CpuFreq, v => _s.CpuFreq = v);
      Check(page, "占用", 16, 122, () => _s.CpuUsage, v => _s.CpuUsage = v);

      Label(page, "GPU", 240, 12);
      Check(page, "温度", 240, 38, () => _s.GpuTemp, v => _s.GpuTemp = v);
      Check(page, "功耗", 240, 66, () => _s.GpuPower, v => _s.GpuPower = v);
      Check(page, "频率", 240, 94, () => _s.GpuFreq, v => _s.GpuFreq = v);
      Check(page, "占用", 240, 122, () => _s.GpuUsage, v => _s.GpuUsage = v);

      Label(page, "其它", 16, 162);
      Check(page, "显存", 16, 188, () => _s.VramUsed, v => _s.VramUsed = v);
      Check(page, "风扇", 16, 216, () => _s.Fan, v => _s.Fan = v);
      Check(page, "风扇显示百分比", 16, 244, () => _s.FanPercent, v => _s.FanPercent = v);
      Check(page, "磁盘读写", 16, 272, () => _s.Disk, v => _s.Disk = v);
      Check(page, "网络速率", 16, 300, () => _s.Net, v => _s.Net = v);

      Check(page, "电池", 240, 188, () => _s.Battery, v => _s.Battery = v);
      Check(page, "内存占用", 240, 216, () => _s.SysMem, v => _s.SysMem = v);
      Check(page, "温度/功耗显示峰值", 240, 244, () => _s.ShowPeak, v => _s.ShowPeak = v);
      Check(page, "记录到 CSV", 240, 272, () => _s.CsvLog, v => _s.CsvLog = v);
      Check(page, "瓶颈提示（功耗墙/温度墙）", 240, 300, () => _s.ThrottleHint, v => _s.ThrottleHint = v);

      Label(page, "帧率", 16, 340);
      Check(page, "显示帧率", 16, 366, () => _s.ShowFps, v => _s.ShowFps = v);
      Check(page, "显示 1% low", 16, 394, () => _s.ShowLowFps, v => _s.ShowLowFps = v);
      Check(page, "自动按窗口切换方案", 240, 366, () => _s.AutoProfile, v => _s.AutoProfile = v);

      Numeric(page, "风扇最大转速 (RPM)", 240, 420, 1000, 12000, () => _s.FanMaxRpm, v => _s.FanMaxRpm = v);

      return page;
    }

    private TabPage BuildCurveTab() {
      var page = new TabPage("曲线");

      Label(page, "在对应读数下方画最近 60 个采样的折线", 16, 12);
      Check(page, "CPU 温度曲线", 16, 48, () => _s.CpuTempChart, v => _s.CpuTempChart = v);
      Check(page, "CPU 功耗曲线", 16, 76, () => _s.CpuPowerChart, v => _s.CpuPowerChart = v);
      Check(page, "GPU 温度曲线", 16, 104, () => _s.GpuTempChart, v => _s.GpuTempChart = v);
      Check(page, "GPU 功耗曲线", 16, 132, () => _s.GpuPowerChart, v => _s.GpuPowerChart = v);
      Check(page, "帧率曲线", 16, 160, () => _s.ShowFpsChart, v => _s.ShowFpsChart = v);

      return page;
    }

    private TabPage BuildLookTab() {
      var page = new TabPage("外观");

      Label(page, "字号", 16, 16);
      var size = new ComboBox { Left = 16, Top = 42, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
      size.Items.AddRange(new object[] { "小 10", "标准 12", "大 14", "特大 17" });
      size.SelectedIndex = _s.FontSize <= 10 ? 0 : (_s.FontSize <= 12 ? 1 : (_s.FontSize <= 14 ? 2 : 3));
      size.SelectedIndexChanged += (s, e) => {
        int[] values = { 10, 12, 14, 17 };
        _s.FontSize = values[size.SelectedIndex];
      };
      page.Controls.Add(size);

      Label(page, "背景", 200, 16);
      var background = new ComboBox { Left = 200, Top = 42, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
      background.Items.AddRange(new object[] { "纯文字（全透明）", "半透明底板" });
      background.SelectedIndex = _s.Background == 1 ? 1 : 0;
      background.SelectedIndexChanged += (s, e) => _s.Background = background.SelectedIndex;
      page.Controls.Add(background);

      Label(page, "底板不透明度", 16, 84);
      var opacity = new ComboBox { Left = 16, Top = 110, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
      opacity.Items.AddRange(new object[] { "80", "120", "160", "210" });
      opacity.SelectedIndex = _s.Opacity <= 80 ? 0 : (_s.Opacity <= 120 ? 1 : (_s.Opacity <= 160 ? 2 : 3));
      opacity.SelectedIndexChanged += (s, e) => {
        int[] values = { 80, 120, 160, 210 };
        _s.Opacity = values[opacity.SelectedIndex];
      };
      page.Controls.Add(opacity);

      Check(page, "贴边吸附", 200, 110, () => _s.SnapEdge, v => _s.SnapEdge = v);
      Check(page, "全局热键 Ctrl+Alt+M 显示/隐藏", 16, 160, () => _s.HotkeyEnabled, v => _s.HotkeyEnabled = v);
      Check(page, "拖动定位（勾上后关闭本窗口即可拖动，拖完再取消）", 16, 190, () => _s.DragRequested, v => _s.DragRequested = v);

      Label(page, "悬浮条位置：托盘 → 设置窗口关闭后，右键菜单 → 显示悬浮条 旁拖动", 16, 210);

      return page;
    }

    private TabPage BuildAboutTab() {
      var page = new TabPage("关于");
      var text = new Label {
        Left = 16,
        Top = 16,
        Width = 430,
        Height = 400,
        Text =
          "Vitals —— 硬件监测\r\n\r\n" +
          "配置文件：" + Settings.FilePath + "\r\n\r\n" +
          "CPU 温度/功耗/频率需要 PawnIO 驱动，并以管理员身份运行。\r\n" +
          "风扇转速走惠普 BIOS 接口，仅支持 Omen / Victus 系列。\r\n" +
          "帧率由内嵌的 Intel PresentMon 采集，不注入游戏进程。\r\n\r\n" +
          "与 HP / OMEN 官方无关，仅供个人使用。"
      };
      page.Controls.Add(text);
      return page;
    }

    private static void Label(Control parent, string text, int x, int y) {
      parent.Controls.Add(new Label { Text = text, Left = x, Top = y, AutoSize = true });
    }

    private static void Check(Control parent, string text, int x, int y, Func<bool> get, Action<bool> set) {
      var box = new CheckBox { Text = text, Left = x, Top = y, AutoSize = true, Checked = get() };
      box.CheckedChanged += (s, e) => set(box.Checked);
      parent.Controls.Add(box);
    }

    private static void Numeric(Control parent, string text, int x, int y, int min, int max,
        Func<int> get, Action<int> set) {
      parent.Controls.Add(new Label { Text = text, Left = x, Top = y - 22, AutoSize = true });
      var box = new NumericUpDown {
        Left = x,
        Top = y,
        Width = 110,
        Minimum = min,
        Maximum = max,
        Value = Math.Max(min, Math.Min(max, get()))
      };
      box.ValueChanged += (s, e) => set((int)box.Value);
      parent.Controls.Add(box);
    }
  }


  internal sealed class FrameMonitor : IDisposable {
    private struct Frame { public double Tick; public double Ms; }

    private Process _process;
    private Thread _reader;
    private static string _exePath;
    private readonly Dictionary<string, Queue<Frame>> _byName =
      new Dictionary<string, Queue<Frame>>(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new object();
    public string LastError = "";

    public bool Running {
      get { Process process = _process; return process != null && !process.HasExited; }
    }

    public bool Start() {
      if (Running) return true;
      try {
        // 上一次被强杀后残留的 PresentMon 会占着全局对象，导致新实例收不到任何帧
        foreach (Process stale in Process.GetProcessesByName("PresentMon")) {
          try { stale.Kill(); stale.WaitForExit(1000); } catch { }
        }

        string exe = ExtractPresentMon();
        if (exe == null) { LastError = "无法释放 PresentMon"; return false; }

        var info = new ProcessStartInfo {
          FileName = exe,
          // 不加 --process_name，采集全部进程；窗口切换时不需要重启
          Arguments = "--output_stdout --no_console_stats --v1_metrics" +
                      " --no_track_gpu --no_track_display --no_track_input" +
                      " --stop_existing_session --session_name VitalsFps" + Process.GetCurrentProcess().Id,
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };

        _process = Process.Start(info);
        if (_process == null) { LastError = "PresentMon 启动失败"; return false; }

        lock (_lock) { _byName.Clear(); }

        _reader = new Thread(ReadLoop) { IsBackground = true };
        _reader.Start();
        return true;
      } catch (Exception ex) {
        LastError = ex.Message;
        Stop();
        return false;
      }
    }

    public void Stop() {
      Process process = _process;
      _process = null;
      if (process != null) {
        try { if (!process.HasExited) process.Kill(); } catch { }
        try { process.Dispose(); } catch { }
      }
      lock (_lock) { _byName.Clear(); }
    }

    // 取某个进程名最近 2 秒的平均帧率，0 表示没有帧
    public double GetFps(string processName) {
      if (string.IsNullOrEmpty(processName)) return 0;
      lock (_lock) {
        Queue<Frame> queue;
        if (!_byName.TryGetValue(processName, out queue) || queue.Count < 3) return 0;

        double now = Environment.TickCount;
        while (queue.Count > 0 && now - queue.Peek().Tick > 2000) queue.Dequeue();
        if (queue.Count < 3) return 0;

        double sum = 0;
        foreach (Frame frame in queue) sum += frame.Ms;
        return sum > 0 ? 1000.0 * queue.Count / sum : 0;
      }
    }

    // 1% low：取最近 2 秒内最慢的 1% 帧，换算成等效帧率
    public double GetLowFps(string processName) {
      if (string.IsNullOrEmpty(processName)) return 0;
      lock (_lock) {
        Queue<Frame> queue;
        if (!_byName.TryGetValue(processName, out queue) || queue.Count < 20) return 0;

        double now = Environment.TickCount;
        var list = new List<double>();
        foreach (Frame frame in queue) {
          if (now - frame.Tick <= 2000) list.Add(frame.Ms);
        }
        if (list.Count < 20) return 0;

        list.Sort();
        int take = Math.Max(1, list.Count / 100);
        double sum = 0;
        for (int i = list.Count - take; i < list.Count; i++) sum += list[i];
        double average = sum / take;
        return average > 0 ? 1000.0 / average : 0;
      }
    }

    // 该进程名最近是否有帧（用于判断数据是否新鲜）
    public bool HasRecent(string processName) {
      return GetFps(processName) > 0;
    }

    public IEnumerable<string> Names() {
      lock (_lock) {
        var list = new List<string>();
        list.AddRange(_byName.Keys);
        return list;
      }
    }

    private void ReadLoop() {
      Process process = _process;
      if (process == null) return;
      try {
        int nameColumn = -1;
        int msColumn = -1;
        string line;
        while ((line = process.StandardOutput.ReadLine()) != null) {
          if (msColumn < 0) {
            if (line.StartsWith("Application", StringComparison.Ordinal)) {
              string[] header = line.Split(',');
              for (int i = 0; i < header.Length; i++) {
                string column = header[i].Trim();
                if (column == "Application") nameColumn = i;
                else if (column == "msBetweenPresents") msColumn = i;
              }
            }
            continue;
          }

          string[] parts = line.Split(',');
          if (msColumn >= parts.Length || nameColumn >= parts.Length) continue;

          string name = parts[nameColumn].Trim();
          if (name.Length == 0) continue;

          double ms;
          if (!double.TryParse(parts[msColumn].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out ms)) continue;
          if (ms <= 0 || ms > 2000) continue;

          double now = Environment.TickCount;
          lock (_lock) {
            Queue<Frame> queue;
            if (!_byName.TryGetValue(name, out queue)) {
              queue = new Queue<Frame>();
              _byName[name] = queue;
            }
            queue.Enqueue(new Frame { Tick = now, Ms = ms });
            // 只保留最近 3 秒，避免长时间运行累积
            while (queue.Count > 0 && now - queue.Peek().Tick > 3000) queue.Dequeue();
          }
        }
      } catch {
        // 子进程退出时读流会抛异常，忽略
      }
    }

    private static string ExtractPresentMon() {
      string dir = Path.Combine(Path.GetTempPath(), "Vitals");
      try {
        Directory.CreateDirectory(dir);
        if (_exePath != null && File.Exists(_exePath)) return _exePath;

        string path = Path.Combine(dir, "PresentMon.exe");
        try {
          if (File.Exists(path)) {
            _exePath = path;
            return path;
          }
          CopyResource(path);
          _exePath = path;
          return path;
        } catch {
          // 共享副本被占用（例如清理不掉的旧进程）就退回带进程号的独立副本
          string alt = Path.Combine(dir, "PresentMon-" + Process.GetCurrentProcess().Id + ".exe");
          CopyResource(alt);
          _exePath = alt;
          return alt;
        }
      } catch {
        return null;
      }
    }

    private static void CopyResource(string path) {
      using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Vitals.PresentMon.exe")) {
        if (stream == null) throw new InvalidOperationException("missing resource");
        using (FileStream file = File.Create(path)) stream.CopyTo(file);
      }
    }

    public static int ForegroundPid() {
      try {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero) return 0;
        int pid;
        GetWindowThreadProcessId(window, out pid);
        return pid;
      } catch {
        return 0;
      }
    }

    // 桌面、开始菜单、任务栏这些都是 explorer 的窗口，它们本身不出帧，
    // 真正合成画面的是 dwm，所以按桌面时改看 dwm 的帧率。
    public static bool IsShellProcess(int pid) {
      try {
        string name = Process.GetProcessById(pid).ProcessName.ToLowerInvariant();
        switch (name) {
          case "explorer":
          case "startmenuexperiencehost":
          case "shellexperiencehost":
          case "searchhost":
          case "textinputhost":
          case "lockapp":
          case "applicationframehost":
            return true;
        }
      } catch { }
      return false;
    }

    public void Dispose() { Stop(); }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);
  }

  // 纯透明背景的悬浮层：只有文字，不遮挡画面；可拖动定位
  // 纯透明悬浮层：默认无底板、鼠标穿透；可拖动、可选半透明底板
  // 纯透明悬浮层：可拖动、可选半透明底板、支持逐行曲线、支持全局热键
  internal sealed class OverlayForm : Form {
    private const int Padding = 6;
    private const float ShadowOffset = 1.2f;
    private const int ChartRowHeight = 30;
    private const int HotkeyId = 0x4C4D;

    private Font _font = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
    private List<DisplayLine> _lines = new List<DisplayLine>();
    private Bitmap _bitmap;
    private bool _dragMode;
    private bool _dragging;
    private bool _hotkey;
    private Point _dragOrigin;
    private Point _windowOrigin;
    private int _fontPoints = 12;
    private int _background;
    private int _opacity = 150;

    public event EventHandler Moved;
    public event EventHandler Hotkey;

    public OverlayForm() {
      FormBorderStyle = FormBorderStyle.None;
      TopMost = true;
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.Manual;
      Size = new Size(1, 1);
    }

    protected override CreateParams CreateParams {
      get {
        var cp = base.CreateParams;
        cp.ExStyle |= WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        if (!_dragMode) cp.ExStyle |= WS_EX_TRANSPARENT;
        return cp;
      }
    }

    public void SetHotkey(bool enabled) {
      if (_hotkey == enabled) return;
      if (!IsHandleCreated) { _hotkey = enabled; return; }
      if (enabled) {
        if (RegisterHotKey(Handle, HotkeyId, MOD_CONTROL | MOD_ALT, VK_M)) _hotkey = true;
      } else {
        if (_hotkey) UnregisterHotKey(Handle, HotkeyId);
        _hotkey = false;
      }
    }

    protected override void OnHandleCreated(EventArgs e) {
      base.OnHandleCreated(e);
      RebuildFont();
      if (_hotkey) {
        if (RegisterHotKey(Handle, HotkeyId, MOD_CONTROL | MOD_ALT, VK_M)) _hotkey = true;
      }
      Render();
    }

    protected override void WndProc(ref Message m) {
      if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId) {
        if (Hotkey != null) Hotkey(this, EventArgs.Empty);
      }
      base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
      if (_hotkey && IsHandleCreated) UnregisterHotKey(Handle, HotkeyId);
      base.OnFormClosed(e);
    }

    public void SetFontSize(int points) {
      if (points < 8) points = 8;
      if (points > 32) points = 32;
      _fontPoints = points;
      RebuildFont();
      Render();
    }

    public void SetBackground(int style, int opacity) {
      _background = style;
      _opacity = opacity < 40 ? 40 : (opacity > 235 ? 235 : opacity);
      Render();
    }

    public void SetDragMode(bool enabled) {
      if (_dragMode == enabled) return;
      _dragMode = enabled;
      UpdateStyles();
      Cursor = enabled ? Cursors.SizeAll : Cursors.Default;
      Render();
    }

    public void MoveTo(int x, int y) {
      Rectangle bounds = SystemInformation.VirtualScreen;
      int maxX = bounds.Right - Math.Max(Width, 1);
      int maxY = bounds.Bottom - Math.Max(Height, 1);
      int px = Math.Max(bounds.Left, Math.Min(x, maxX));
      int py = Math.Max(bounds.Top, Math.Min(y, maxY));
      Location = new Point(px, py);
      RenderLayered();
    }

    public void SnapToEdges() {
      try {
        Screen screen = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2));
        Rectangle area = screen.WorkingArea;
        const int margin = 8;
        const int threshold = 24;
        int x = Left, y = Top;
        if (Math.Abs(Left - area.Left) < threshold) x = area.Left + margin;
        if (Math.Abs(area.Right - (Left + Width)) < threshold) x = area.Right - Width - margin;
        if (Math.Abs(Top - area.Top) < threshold) y = area.Top + margin;
        if (Math.Abs(area.Bottom - (Top + Height)) < threshold) y = area.Bottom - Height - margin;
        if (x != Left || y != Top) {
          Location = new Point(x, y);
          RenderLayered();
        }
      } catch { }
    }

    public void SetLines(List<DisplayLine> lines) {
      _lines = lines ?? new List<DisplayLine>();
      Render();
    }

    private float DpiScale() {
      try {
        uint dpi = GetDpiForWindow(Handle);
        if (dpi >= 72) return dpi / 96f;
      } catch { }
      return 1f;
    }

    private void RebuildFont() {
      Font old = _font;
      float scale = DpiScale();
      if (scale < 0.5f || scale > 4f) scale = 1f;
      _font = new Font("Segoe UI", _fontPoints * scale, FontStyle.Bold, GraphicsUnit.Point);
      if (old != null) old.Dispose();
    }

    private static bool IsChartRow(DisplayLine line) {
      return line.Chart != null && line.Chart.Length > 1 && string.IsNullOrEmpty(line.Title);
    }

    private void Render() {
      using (var measure = new Bitmap(1, 1))
      using (var mg = Graphics.FromImage(measure)) {
        float lineHeight = _font.GetHeight(mg);
        float maxTitle = 1, maxValue = 1;
        int rowCount = 0;
        foreach (DisplayLine line in _lines) {
          if (IsChartRow(line)) { rowCount++; continue; }
          rowCount++;
          float t = mg.MeasureString(line.Title ?? "", _font).Width;
          float v = mg.MeasureString(line.Value ?? "", _font).Width;
          if (t > maxTitle) maxTitle = t;
          if (v > maxValue) maxValue = v;
        }

        float gap = mg.MeasureString(" ", _font).Width * 1.5f;
        float chartCount = 0;
        foreach (DisplayLine line in _lines) if (IsChartRow(line)) chartCount++;

        int width = (int)Math.Ceiling(maxTitle + gap + maxValue) + Padding * 2 + 4;
        int height = (int)Math.Ceiling(lineHeight * (rowCount - chartCount) + ChartRowHeight * chartCount) + Padding * 2;
        if (width < 2) width = 2;
        if (height < 2) height = 2;

        if (_bitmap == null || _bitmap.Width != width || _bitmap.Height != height) {
          var old = _bitmap;
          _bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
          if (old != null) old.Dispose();
        }

        using (var gfx = Graphics.FromImage(_bitmap)) {
          gfx.SmoothingMode = SmoothingMode.AntiAlias;
          gfx.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
          gfx.Clear(Color.Transparent);

          if (_background == 1) {
            using (var path = RoundedRect(new Rectangle(0, 0, width, height), 8))
            using (var brush = new SolidBrush(Color.FromArgb(_opacity, 12, 14, 18)))
              gfx.FillPath(brush, path);
          }

          float y = Padding;
          foreach (DisplayLine line in _lines) {
            if (IsChartRow(line)) {
              DrawChart(gfx, line.Chart, Padding, y + 2, width - Padding * 2 - 4, ChartRowHeight - 8, line.TitleColor);
              y += ChartRowHeight;
              continue;
            }

            float valueX = Padding + maxTitle + gap;
            using (var shadow = new SolidBrush(Color.FromArgb(_background == 1 ? 90 : 190, 0, 0, 0))) {
              gfx.DrawString(line.Title ?? "", _font, shadow, Padding + ShadowOffset, y + ShadowOffset);
              gfx.DrawString(line.Value ?? "", _font, shadow, valueX + ShadowOffset, y + ShadowOffset);
            }

            using (var titleBrush = new SolidBrush(line.TitleColor))
              gfx.DrawString(line.Title ?? "", _font, titleBrush, Padding, y);

            using (var valueBrush = new SolidBrush(line.ValueColor))
              gfx.DrawString(line.Value ?? "", _font, valueBrush, valueX, y);

            y += _font.GetHeight(gfx);
          }

          if (_dragMode) {
            using (var pen = new Pen(Color.FromArgb(180, 255, 255, 255), 1f))
              gfx.DrawRectangle(pen, 0, 0, width - 1, height - 1);
          }
        }

        if (Size != _bitmap.Size) Size = _bitmap.Size;
      }

      RenderLayered();
    }

    private static void DrawChart(Graphics g, double[] values, float x, float y, float width, float height, Color color) {
      double max = 30;
      foreach (double value in values) {
        if (value > max) max = value;
      }

      using (var baseline = new Pen(Color.FromArgb(60, color), 1f))
        g.DrawLine(baseline, x, y + height, x + width, y + height);

      using (var pen = new Pen(Color.FromArgb(220, color), 1.6f)) {
        PointF previous = PointF.Empty;
        for (int i = 0; i < values.Length; i++) {
          float px = x + width * i / Math.Max(1, values.Length - 1);
          double value = values[i] < 0 ? 0 : values[i];
          float py = y + height - (float)(value / max * height);
          var point = new PointF(px, py);
          if (i > 0) g.DrawLine(pen, previous, point);
          previous = point;
        }
      }
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius) {
      var path = new System.Drawing.Drawing2D.GraphicsPath();
      int d = radius * 2;
      path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
      path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
      path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
      path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
      path.CloseFigure();
      return path;
    }

    private void RenderLayered() {
      if (_bitmap == null || !IsHandleCreated) return;

      IntPtr screenDc = GetDC(IntPtr.Zero);
      IntPtr memDc = CreateCompatibleDC(screenDc);
      IntPtr hBitmap = _bitmap.GetHbitmap(Color.FromArgb(0));
      IntPtr oldBitmap = SelectObject(memDc, hBitmap);

      var size = new NativeSize(_bitmap.Width, _bitmap.Height);
      var src = new NativePoint(0, 0);
      var dst = new NativePoint(Left, Top);
      var blend = new BlendFunction {
        BlendOp = AC_SRC_OVER,
        BlendFlags = 0,
        SourceConstantAlpha = 255,
        AlphaFormat = AC_SRC_ALPHA
      };

      UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);

      SelectObject(memDc, oldBitmap);
      DeleteObject(hBitmap);
      DeleteDC(memDc);
      ReleaseDC(IntPtr.Zero, screenDc);
    }

    protected override void OnMove(EventArgs e) {
      base.OnMove(e);
      RenderLayered();
    }

    protected override void OnMouseDown(MouseEventArgs e) {
      if (_dragMode && e.Button == MouseButtons.Left) {
        _dragging = true;
        _dragOrigin = Cursor.Position;
        _windowOrigin = Location;
        Capture = true;
      }
      base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e) {
      if (_dragging) {
        Point now = Cursor.Position;
        Location = new Point(_windowOrigin.X + (now.X - _dragOrigin.X), _windowOrigin.Y + (now.Y - _dragOrigin.Y));
      }
      base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
      if (_dragging) {
        _dragging = false;
        Capture = false;
        RenderLayered();
        if (Moved != null) Moved(this, EventArgs.Empty);
      }
      base.OnMouseUp(e);
    }

    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int ULW_ALPHA = 0x02;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_M = 0x4D;
    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize {
      public int Cx, Cy;
      public NativeSize(int cx, int cy) { Cx = cx; Cy = cy; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint {
      public int X, Y;
      public NativePoint(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction {
      public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize, IntPtr hdcSrc, ref NativePoint pptSrc, uint crKey, ref BlendFunction pblend, uint dwFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
  }


  internal static class CsvLog {
    private static string _path;
    private static string _day;

    public static void Append(Sample s, double fps, double low) {
      try {
        string today = DateTime.Now.ToString("yyyyMMdd");
        if (_path == null || _day != today) {
          _day = today;
          string dir = Path.GetDirectoryName(Application.ExecutablePath);
          if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
          _path = Path.Combine(dir, "Vitals-" + today + ".csv");
          if (!File.Exists(_path)) {
            File.WriteAllText(_path,
              "time,cpu_temp,cpu_power,cpu_freq,cpu_usage,gpu_temp,gpu_power,gpu_freq,gpu_util," +
              "vram_used_mb,vram_total_mb,fan1,fan2,disk_read_bps,disk_write_bps,net_bps," +
              "battery_percent,battery_online,battery_watts,mem_percent,fps,fps_1pct_low\r\n",
              new UTF8Encoding(false));
          }
        }

        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(',');
        sb.Append(s.CpuTemp.ToString("F1")).Append(',');
        sb.Append(s.CpuPower.ToString("F1")).Append(',');
        sb.Append(s.CpuClockMhz.ToString("F0")).Append(',');
        sb.Append(s.CpuUsage.ToString("F1")).Append(',');
        sb.Append(s.GpuTemp.ToString("F0")).Append(',');
        sb.Append(s.GpuPower.ToString("F1")).Append(',');
        sb.Append(s.GpuClockMhz.ToString("F0")).Append(',');
        sb.Append(s.GpuUtil).Append(',');
        sb.Append(s.GpuMemUsedMb.ToString("F0")).Append(',');
        sb.Append(s.GpuMemTotalMb.ToString("F0")).Append(',');
        sb.Append(s.FanRpm1).Append(',');
        sb.Append(s.FanRpm2).Append(',');
        sb.Append(s.DiskReadBps.ToString("F0")).Append(',');
        sb.Append(s.DiskWriteBps.ToString("F0")).Append(',');
        sb.Append(s.NetBps.ToString("F0")).Append(',');
        sb.Append(s.BatteryPercent).Append(',');
        sb.Append(s.BatteryOnline ? '1' : '0').Append(',');
        sb.Append(s.BatteryWatts.ToString("F1")).Append(',');
        sb.Append(s.MemPercent).Append(',');
        sb.Append(fps.ToString("F0")).Append(',');
        sb.Append(low.ToString("F0")).Append("\r\n");

        File.AppendAllText(_path, sb.ToString(), new UTF8Encoding(false));
      } catch { }
    }
  }

  // 崩溃日志
  internal static class Log {
    public static void Write(Exception ex) {
      try {
        string dir = Path.GetDirectoryName(Application.ExecutablePath);
        string path = Path.Combine(string.IsNullOrEmpty(dir) ? Path.GetTempPath() : dir, "Vitals.log");
        File.AppendAllText(path,
          DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + (ex == null ? "unknown" : ex.ToString()) + Environment.NewLine,
          new UTF8Encoding(false));
      } catch { }
    }
  }

  // 通过 HP BIOS 的 WMI 接口读风扇档位（hpqBIntM，命令 0x2D）
  internal static class HpBios {
    private static ManagementObject _methods;
    private static ManagementClass _inClass;
    private static bool _tried;

    public static byte[] Send(uint commandType, byte[] data, int outputSize) {
      if (!_tried) {
        _tried = true;
        try {
          using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM hpqBIntM")) {
            foreach (ManagementObject mo in searcher.Get()) { _methods = mo; break; }
          }
        } catch {
          _methods = null;
        }
      }
      if (_methods == null) return null;

      try {
        if (_inClass == null) _inClass = new ManagementClass(@"root\wmi", "hpqBDataIn", null);
        using (ManagementObject input = _inClass.CreateInstance()) {
          input["Command"] = 0x20008u;
          input["CommandType"] = commandType;
          input["Sign"] = new byte[] { 0x53, 0x45, 0x43, 0x55 };
          if (data != null) {
            input["hpqBData"] = data;
            input["Size"] = (uint)data.Length;
          } else {
            input["Size"] = 0u;
          }

          string method = "hpqBIOSInt" + outputSize;
          using (ManagementBaseObject inParams = _methods.GetMethodParameters(method)) {
            inParams["InData"] = input;
            using (ManagementBaseObject result = _methods.InvokeMethod(method, inParams, null)) {
              if (result == null) return null;
              using (var outData = result["OutData"] as ManagementBaseObject) {
                if (outData == null) return null;
                if ((uint)outData["rwReturnCode"] != 0) return null;
                if (outputSize == 0) return new byte[0];
                return (byte[])outData["Data"];
              }
            }
          }
        }
      } catch {
        return null;
      }
    }

    // 0x2D：返回 3 个风扇档位，单位 100 RPM
    public static bool TryGetFans(out int fan1, out int fan2, out int fan3) {
      fan1 = fan2 = fan3 = -1;
      byte[] data = Send(0x2D, new byte[] { 0, 0, 0, 0 }, 128);
      if (data == null || data.Length < 3) return false;
      fan1 = data[0] * 100;
      fan2 = data[1] * 100;
      fan3 = data[2] * 100;
      return true;
    }
  }

  // 磁盘 / 网络速率，走 PDH 英文计数器（与系统语言无关）
  internal static class RateCounters {
    private static IntPtr _query = IntPtr.Zero;
    private static IntPtr _diskRead = IntPtr.Zero;
    private static IntPtr _diskWrite = IntPtr.Zero;
    private static IntPtr _network = IntPtr.Zero;
    private static bool _tried, _ready;
    public static double ReadBps, WriteBps, NetBps;

    public static void Sample() {
      if (!_tried) Init();
      if (!_ready) return;
      try {
        if (PdhCollectQueryData(_query) != 0) return;
        ReadBps = ReadOne(_diskRead);
        WriteBps = ReadOne(_diskWrite);
        NetBps = ReadNetwork();
      } catch { }
    }

    private static void Init() {
      _tried = true;
      try {
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) return;
        bool ok = PdhAddEnglishCounter(_query, @"\PhysicalDisk(_Total)\Disk Read Bytes/sec", IntPtr.Zero, out _diskRead) == 0;
        ok &= PdhAddEnglishCounter(_query, @"\PhysicalDisk(_Total)\Disk Write Bytes/sec", IntPtr.Zero, out _diskWrite) == 0;
        PdhAddEnglishCounter(_query, @"\Network Interface(*)\Bytes Total/sec", IntPtr.Zero, out _network);
        if (!ok) return;
        PdhCollectQueryData(_query);
        _ready = true;
      } catch {
        _ready = false;
      }
    }

    private static double ReadOne(IntPtr counter) {
      if (counter == IntPtr.Zero) return 0;
      uint type;
      var value = new PdhFmtCounterValue();
      if (PdhGetFormattedCounterValue(counter, PDH_FMT_DOUBLE, out type, ref value) != 0) return 0;
      if (value.Status != 0) return 0;
      return value.DoubleValue < 0 ? 0 : value.DoubleValue;
    }

    private static double ReadNetwork() {
      if (_network == IntPtr.Zero) return 0;
      uint size = 0, count = 0;
      uint status = PdhGetFormattedCounterArray(_network, PDH_FMT_DOUBLE, ref size, out count, IntPtr.Zero);
      if (status != PDH_MORE_DATA || size == 0) return 0;

      double total = 0;
      IntPtr buffer = Marshal.AllocHGlobal((int)size);
      try {
        status = PdhGetFormattedCounterArray(_network, PDH_FMT_DOUBLE, ref size, out count, buffer);
        if (status != 0) return 0;

        int itemSize = Marshal.SizeOf(typeof(PdhFmtCounterValueItem));
        for (int i = 0; i < count; i++) {
          IntPtr itemPtr = new IntPtr(buffer.ToInt64() + i * itemSize);
          var item = (PdhFmtCounterValueItem)Marshal.PtrToStructure(itemPtr, typeof(PdhFmtCounterValueItem));
          string name = Marshal.PtrToStringUni(item.Name);
          if (name == null) continue;
          if (name.IndexOf("Loopback", StringComparison.OrdinalIgnoreCase) >= 0) continue;
          if (name.IndexOf("isatap", StringComparison.OrdinalIgnoreCase) >= 0) continue;
          if (item.Value.Status != 0) continue;
          if (item.Value.DoubleValue > 0) total += item.Value.DoubleValue;
        }
      } finally {
        Marshal.FreeHGlobal(buffer);
      }
      return total;
    }

    private const uint PDH_FMT_DOUBLE = 0x00000200;
    private const uint PDH_MORE_DATA = 0x800007D2;

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValue {
      public uint Status;
      public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValueItem {
      public IntPtr Name;
      public PdhFmtCounterValue Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, ref PdhFmtCounterValue value);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint size, out uint count, IntPtr buffer);
  }

  // 开机自启：计划任务，登录时以最高权限启动，避免每次 UAC
  internal static class AutoStart {
    private const string TaskName = "Vitals";

    public static bool Install() {
      string exe = Application.ExecutablePath;
      string args = "/Create /TN \"" + TaskName + "\" /TR \"\\\"" + exe + "\\\"\" /SC ONLOGON /RL HIGHEST /F";
      return Run(args);
    }

    public static bool Remove() {
      return Run("/Delete /TN \"" + TaskName + "\" /F");
    }

    private static bool Run(string arguments) {
      try {
        var info = new ProcessStartInfo("schtasks.exe", arguments) {
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };
        using (Process process = Process.Start(info)) {
          if (process == null) return false;
          process.WaitForExit(15000);
          return process.HasExited && process.ExitCode == 0;
        }
      } catch {
        return false;
      }
    }
  }


  internal static class Sensors {
    internal static string PawnIoDiagnostic = "未初始化";
    private static bool _initialized;
    private static bool _cpuReady;
    private static bool _gpuReady;
    private static IntPtr _pawnHandle = new IntPtr(-1);
    private static IntPtr _nvmlDevice = IntPtr.Zero;
    private static uint _energyUnitExp = 16;
    private static ulong _lastEnergy;
    private static long _lastEnergyTicks;
    private static ulong _lastIdle, _lastKernel, _lastUser;
    private static long _lastBatteryTicks;
    private static double _batteryWatts = -1;
    private static long _lastFanTicks;
    private static int _fan1 = -1, _fan2 = -1, _fan3 = -1;
    private static bool _fanValid;

    public static Sample Read(bool needFan, bool needDiskNet, bool needBattery) {
      if (!_initialized) Init();

      var s = new Sample();

      if (_cpuReady) {
        ReadCpu(s);
        ReadCpuUsage(s);
      }
      if (_gpuReady) ReadGpu(s);

      if (needFan) ReadFans(s);
      if (needDiskNet) {
        RateCounters.Sample();
        s.DiskReadBps = RateCounters.ReadBps;
        s.DiskWriteBps = RateCounters.WriteBps;
        s.NetBps = RateCounters.NetBps;
      }
      if (needBattery) ReadBattery(s);

      s.MemPercent = ReadMemoryPercent();
      return s;
    }

    // 风扇转速变化慢，3 秒查一次就够，其余时间复用上次结果（WMI 一次约 36ms）
    private static void ReadFans(Sample s) {
      long now = Environment.TickCount;
      if (!_fanValid || unchecked(now - _lastFanTicks) >= 3000) {
        _lastFanTicks = now;
        int f1, f2, f3;
        if (HpBios.TryGetFans(out f1, out f2, out f3)) {
          _fan1 = f1;
          _fan2 = f2;
          _fan3 = f3;
          _fanValid = true;
        }
      }
      if (_fanValid) {
        s.FanRpm1 = _fan1;
        s.FanRpm2 = _fan2;
        s.FanRpm3 = _fan3;
      }
    }

    private static void ReadBattery(Sample s) {
      try {
        var status = new SystemPowerStatus();
        if (Native.GetSystemPowerStatus(ref status)) {
          s.BatteryPercent = status.BatteryLifePercent == 255 ? -1 : status.BatteryLifePercent;
          s.BatteryOnline = status.ACLineStatus == 1;
          if (status.BatteryLifeTime > 0 && (status.BatteryFlag & 128) == 0)
            s.BatteryMinutes = (int)(status.BatteryLifeTime / 60);
        }

        // 功率查询走 WMI 较重，5 秒一次且只在放电时读
        long now = Environment.TickCount;
        if (!s.BatteryOnline && unchecked(now - _lastBatteryTicks) > 5000) {
          _lastBatteryTicks = now;
          _batteryWatts = ReadBatteryWatts();
        }
        if (s.BatteryOnline) _batteryWatts = -1;
        s.BatteryWatts = _batteryWatts;
      } catch { }
    }

    private static double ReadBatteryWatts() {
      try {
        using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT DischargeRate FROM BatteryStatus")) {
          foreach (ManagementBaseObject item in searcher.Get()) {
            object value = item["DischargeRate"];
            if (value == null) continue;
            double mw = Convert.ToDouble(value);
            if (mw > 0 && mw < 200000) return mw / 1000.0;
          }
        }
      } catch { }
      return -1;
    }

    private static void Init() {
      _initialized = true;

      try {
        IntPtr handle = Native.CreateFileW(@"\\?\GLOBALROOT\Device\PawnIO",
          0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle == new IntPtr(-1)) {
          PawnIoDiagnostic = "打开设备失败，错误码 " + Marshal.GetLastWin32Error();
        } else {
          byte[] module = LoadModuleBytes();
          uint returned;
          if (Native.DeviceIoControl(handle, IOCTL_LOAD_BINARY, module, (uint)module.Length, null, 0, out returned, IntPtr.Zero)) {
            _pawnHandle = handle;
            ulong unit = 0;
            if (ReadMsr(0x606, out unit)) {
              uint exp = (uint)((unit >> 8) & 0x1F);
              if (exp <= 20) _energyUnitExp = exp;
            }
            _cpuReady = true;
            PawnIoDiagnostic = "正常，模块 " + module.Length + " 字节";
          } else {
            PawnIoDiagnostic = "加载模块失败，错误码 " + Marshal.GetLastWin32Error();
            Native.CloseHandle(handle);
          }
        }
      } catch (Exception ex) {
        _cpuReady = false;
        PawnIoDiagnostic = "异常: " + ex.GetType().Name + " " + ex.Message;
      }

      try {
        if (Nvml.Init() == 0 && Nvml.GetHandle(0, out _nvmlDevice) == 0)
          _gpuReady = true;
      } catch {
        _gpuReady = false;
      }

      ulong idle, kernel, user;
      Native.GetSystemTimes(out idle, out kernel, out user);
      _lastIdle = idle; _lastKernel = kernel; _lastUser = user;
    }

    private static byte[] LoadModuleBytes() {
      var assembly = Assembly.GetExecutingAssembly();
      string name = null;
      foreach (string item in assembly.GetManifestResourceNames()) {
        if (item.EndsWith("IntelMSR.bin", StringComparison.OrdinalIgnoreCase)) { name = item; break; }
      }
      if (name == null) return new byte[0];
      using (Stream stream = assembly.GetManifestResourceStream(name))
      using (var memory = new MemoryStream()) {
        stream.CopyTo(memory);
        return memory.ToArray();
      }
    }

    private static void ReadCpu(Sample s) {
      try {
        double tjMax = 100;
        ulong misc;
        if (ReadMsr(0x1A2, out misc)) {
          uint value = (uint)((misc >> 16) & 0xFF);
          if (value > 0) tjMax = value;
        }

        double temp = -1;
        ulong therm;
        if (ReadMsr(0x1B1, out therm))
          temp = tjMax - ((therm >> 16) & 0x7F);
        if (temp < 0 || temp > 130) {
          if (ReadMsr(0x19C, out therm))
            temp = tjMax - ((therm >> 16) & 0x7F);
        }

        ulong perf;
        double mhz = 0;
        if (ReadMsr(0x198, out perf))
          mhz = ((perf >> 8) & 0xFF) * 100.0;

        ulong energy;
        double power = s.CpuPower;
        if (ReadMsr(0x611, out energy)) {
          long now = Environment.TickCount;
          if (_lastEnergy != 0 && now > _lastEnergyTicks) {
            ulong delta = energy & 0xFFFFFFFFUL;
            ulong prev = _lastEnergy & 0xFFFFFFFFUL;
            if (delta >= prev) {
              double joules = (delta - prev) / Math.Pow(2, _energyUnitExp);
              power = joules / ((now - _lastEnergyTicks) / 1000.0);
            }
          }
          _lastEnergy = energy;
          _lastEnergyTicks = now;
        }

        s.CpuOk = temp > 0;
        s.CpuTemp = temp > 0 ? temp : 0;
        s.CpuPower = power;
        s.CpuClockMhz = mhz;
        if (temp > 0 && temp >= tjMax - 3) s.CpuThrottle = "温度墙";
      } catch {
        s.CpuOk = false;
      }
    }

    private static void ReadCpuUsage(Sample s) {      try {
        ulong idle, kernel, user;
        if (!Native.GetSystemTimes(out idle, out kernel, out user)) return;

        ulong dIdle = idle - _lastIdle;
        ulong dKernel = kernel - _lastKernel;
        ulong dUser = user - _lastUser;
        _lastIdle = idle; _lastKernel = kernel; _lastUser = user;

        ulong total = dKernel + dUser;
        if (total > 0) {
          double busy = total - dIdle;
          s.CpuUsage = Math.Max(0, Math.Min(100, busy * 100.0 / total));
        }
      } catch { }
    }

    private static void ReadGpu(Sample s) {
      try {
        uint value;
        if (Nvml.GetTemperature(_nvmlDevice, 0, out value) == 0) s.GpuTemp = value;
        if (Nvml.GetPowerUsage(_nvmlDevice, out value) == 0) s.GpuPower = value / 1000.0;
        if (Nvml.GetClockInfo(_nvmlDevice, 0, out value) == 0) s.GpuClockMhz = value;
        if (Nvml.GetClockInfo(_nvmlDevice, 2, out value) == 0) s.GpuMemClockMhz = value;

        var utilization = new NvmlUtilization();
        if (Nvml.GetUtilizationRates(_nvmlDevice, ref utilization) == 0) s.GpuUtil = (int)utilization.Gpu;

        var memory = new NvmlMemory();
        if (Nvml.GetMemoryInfo(_nvmlDevice, ref memory) == 0 && memory.Total > 0) {
          s.GpuMemUsedMb = memory.Used / 1048576.0;
          s.GpuMemTotalMb = memory.Total / 1048576.0;
        }

        ulong reasons;
        if (Nvml.GetClocksThrottleReasons(_nvmlDevice, out reasons) == 0)
          s.GpuThrottle = DescribeThrottle(reasons);

        s.GpuOk = true;
      } catch {
        s.GpuOk = false;
      }
    }

    // NVML nvmlClocksThrottleReason 位标志
    private static string DescribeThrottle(ulong reasons) {
      if ((reasons & 0x40UL) != 0 || (reasons & 0x20UL) != 0) return "温度墙";
      if ((reasons & 0x80UL) != 0 || (reasons & 0x08UL) != 0) return "硬件降频";
      if ((reasons & 0x04UL) != 0) return "功耗墙";
      if ((reasons & 0x02UL) != 0) return "应用锁频";
      return "";
    }

    private static int ReadMemoryPercent() {
      try {
        var status = new MemoryStatusEx();
        status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        if (Native.GlobalMemoryStatusEx(ref status)) return (int)status.MemoryLoad;
      } catch { }
      return -1;
    }

    private static bool ReadMsr(uint index, out ulong value) {
      value = 0;
      if (_pawnHandle == new IntPtr(-1)) return false;

      var input = new byte[32 + 8];
      byte[] name = Encoding.ASCII.GetBytes("ioctl_read_msr");
      Buffer.BlockCopy(name, 0, input, 0, Math.Min(31, name.Length));
      Buffer.BlockCopy(BitConverter.GetBytes((long)index), 0, input, 32, 8);

      var output = new byte[8];
      uint returned;
      if (!Native.DeviceIoControl(_pawnHandle, IOCTL_EXECUTE_FN, input, (uint)input.Length, output, (uint)output.Length, out returned, IntPtr.Zero))
        return false;
      if (returned < 8) return false;

      value = BitConverter.ToUInt64(output, 0);
      return true;
    }

    private const uint DEVICE_TYPE = 41394u << 16;
    private const uint IOCTL_LOAD_BINARY = DEVICE_TYPE | (0x821u << 2);
    private const uint IOCTL_EXECUTE_FN = DEVICE_TYPE | (0x841u << 2);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx {
      public uint Length;
      public uint MemoryLoad;
      public ulong TotalPhys;
      public ulong AvailPhys;
      public ulong TotalPageFile;
      public ulong AvailPageFile;
      public ulong TotalVirtual;
      public ulong AvailVirtual;
      public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NvmlMemory {
      public ulong Total;
      public ulong Free;
      public ulong Used;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NvmlUtilization {
      public uint Gpu;
      public uint Memory;
    }

    private static class Native {
      [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
      internal static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

      [DllImport("kernel32.dll", SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      internal static extern bool DeviceIoControl(IntPtr device, uint code, byte[] inBuffer, uint inSize, byte[] outBuffer, uint outSize, out uint returned, IntPtr overlapped);

      [DllImport("kernel32.dll", SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      internal static extern bool CloseHandle(IntPtr handle);

      [DllImport("kernel32.dll", SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

      [DllImport("kernel32.dll", SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      internal static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

      [DllImport("kernel32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      internal static extern bool GetSystemPowerStatus(ref SystemPowerStatus status);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemPowerStatus {
      public byte ACLineStatus;
      public byte BatteryFlag;
      public byte BatteryLifePercent;
      public byte Reserved;
      public int BatteryLifeTime;
      public int BatteryFullLifeTime;
    }

    private static class Nvml {
      [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
      internal static extern int nvmlInit_v2();

      [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
      internal static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

      [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
      internal static extern int nvmlDeviceGetTemperature(IntPtr device, int sensor, out uint value);

      [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
      internal static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint value);

      [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
      internal static extern int nvmlDeviceGetClockInfo(IntPtr device, int type, out uint value);

      [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
      internal static extern int nvmlDeviceGetMemoryInfo(IntPtr device, ref NvmlMemory memory);

      [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
      internal static extern int nvmlDeviceGetUtilizationRates(IntPtr device, ref NvmlUtilization utilization);

      [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
      internal static extern int nvmlDeviceGetCurrentClocksThrottleReasons(IntPtr device, out ulong reasons);

      internal static int GetClocksThrottleReasons(IntPtr device, out ulong reasons) {
        return nvmlDeviceGetCurrentClocksThrottleReasons(device, out reasons);
      }

      internal static int GetUtilizationRates(IntPtr device, ref NvmlUtilization utilization) { return nvmlDeviceGetUtilizationRates(device, ref utilization); }

      internal static int Init() { return nvmlInit_v2(); }
      internal static int GetHandle(uint index, out IntPtr device) { return nvmlDeviceGetHandleByIndex_v2(index, out device); }
      internal static int GetTemperature(IntPtr device, int sensor, out uint value) { return nvmlDeviceGetTemperature(device, sensor, out value); }
      internal static int GetPowerUsage(IntPtr device, out uint value) { return nvmlDeviceGetPowerUsage(device, out value); }
      internal static int GetClockInfo(IntPtr device, int type, out uint value) { return nvmlDeviceGetClockInfo(device, type, out value); }
      internal static int GetMemoryInfo(IntPtr device, ref NvmlMemory memory) { return nvmlDeviceGetMemoryInfo(device, ref memory); }
    }
  }
}
