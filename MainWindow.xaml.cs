using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;

namespace RobocopyGUI;

public partial class MainWindow : Window
{
    private Process? _process;
    private CancellationTokenSource? _cts;
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private DateTime _startTime;

    public MainWindow()
    {
        InitializeComponent();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _startTime;
            TxtElapsed.Text = $"耗时: {elapsed:hh\\:mm\\:ss}";
        };

        Closed += (_, _) =>
        {
            _cts?.Cancel();
            try { _process?.Kill(true); } catch { }
            _timer.Stop();
        };

        UpdateCommandPreview();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        public string lpszTitle;
        public int ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    private string? BrowseFolder()
    {
        var bi = new BROWSEINFO
        {
            hwndOwner = new WindowInteropHelper(this).Handle,
            lpszTitle = "选择文件夹",
            ulFlags = 0x00000001 | 0x00000040
        };
        IntPtr pidl = SHBrowseForFolder(ref bi);
        if (pidl == IntPtr.Zero) return null;
        var sb = new StringBuilder(260);
        SHGetPathFromIDList(pidl, sb);
        CoTaskMemFree(pidl);
        var result = sb.ToString();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    private void BtnBrowseSource_Click(object s, RoutedEventArgs e)
    {
        var path = BrowseFolder();
        if (path != null) TxtSource.Text = path;
    }

    private void BtnBrowseDestination_Click(object s, RoutedEventArgs e)
    {
        var path = BrowseFolder();
        if (path != null) TxtDestination.Text = path;
    }

    private void BtnBrowseLog_Click(object s, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Log files (*.log)|*.log|All files (*.*)|*.*",
            FileName = "robocopy"
        };
        if (dlg.ShowDialog() == true)
            TxtLogFile.Text = dlg.FileName;
    }

    private void Option_Changed(object s, RoutedEventArgs e) => UpdateCommandPreview();

    private void UpdateCommandPreview()
    {
        if (TxtCommandPreview == null) return;
        var (exe, args) = BuildCommand(false);
        TxtCommandPreview.Text = $"{exe} {args}";
    }

    private (string exe, string args) BuildCommand(bool dryRun)
    {
        var sb = new StringBuilder();

        var src = TxtSource.Text.Trim().TrimEnd('\\');
        var dst = TxtDestination.Text.Trim().TrimEnd('\\');
        sb.Append(string.IsNullOrEmpty(src) ? "\"<源>\" " : $"\"{src}\" ");
        sb.Append(string.IsNullOrEmpty(dst) ? "\"<目标>\" " : $"\"{dst}\" ");

        var filter = TxtFileFilter.Text.Trim();
        if (!string.IsNullOrEmpty(filter)) sb.Append($"{filter} ");

        if (RbMirror.IsChecked == true) sb.Append("/MIR ");
        if (RbMove.IsChecked == true) sb.Append("/MOV ");
        if (RbMovePurge.IsChecked == true) sb.Append("/MOVE ");

        if (RbSubdirAll.IsChecked == true) sb.Append("/S ");
        if (RbSubdirEmpty.IsChecked == true) sb.Append("/E ");

        var exF = TxtExcludeFiles.Text.Trim();
        if (!string.IsNullOrEmpty(exF)) sb.Append($"/XF {exF} ");
        var exD = TxtExcludeDirs.Text.Trim();
        if (!string.IsNullOrEmpty(exD)) sb.Append($"/XD {exD} ");

        var retries = int.TryParse(TxtRetries.Text.Trim(), out var r) ? r : 3;
        if (retries != 3) sb.Append($"/R:{retries} ");
        var wait = int.TryParse(TxtWait.Text.Trim(), out var w) ? w : 5;
        if (wait != 30) sb.Append($"/W:{wait} ");
        var threads = int.TryParse(TxtThreads.Text.Trim(), out var t) ? t : 8;
        if (threads > 1) sb.Append($"/MT:{threads} ");

        if (ChkRestartable.IsChecked == true) sb.Append("/Z ");
        if (ChkBackup.IsChecked == true) sb.Append("/B ");

        var logFile = TxtLogFile.Text.Trim();
        if (!string.IsNullOrEmpty(logFile))
            sb.Append(ChkAppendLog.IsChecked == true ? $"/LOG+:{logFile} " : $"/LOG:{logFile} ");

        if (dryRun) sb.Append("/L ");

        var extra = TxtExtraArgs.Text.Trim();
        if (!string.IsNullOrEmpty(extra)) sb.Append($"{extra} ");

        return ("robocopy", sb.ToString().TrimEnd());
    }

    private async void BtnRun_Click(object s, RoutedEventArgs e) => await RunRobocopy(false);
    private async void BtnDryRun_Click(object s, RoutedEventArgs e) => await RunRobocopy(true);

    private async Task RunRobocopy(bool dryRun)
    {
        var src = TxtSource.Text.Trim();
        var dst = TxtDestination.Text.Trim();
        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst))
        {
            MessageBox.Show("请先填写源路径和目标路径。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var (exe, args) = BuildCommand(dryRun);
        TxtOutput.Text = $"$ {exe} {args}\n";
        AppendOutput(dryRun ? "--- 模拟执行 (/L) ---" : "--- 开始执行 ---\n");

        _cts = new CancellationTokenSource();

        Encoding outEnc;
        try
        {
            var oem = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
            outEnc = Encoding.GetEncoding(oem);
        }
        catch { outEnc = Encoding.Default; }

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = outEnc,
                StandardErrorEncoding = outEnc
            },
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                Dispatcher.Invoke(() => AppendOutput(e.Data));
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                Dispatcher.Invoke(() => AppendOutput(e.Data));
        };

        SetRunning(true);

        try
        {
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            await _process.WaitForExitAsync(_cts.Token);
            var code = _process.ExitCode;
            AppendOutput($"\n--- 完成 | 退出码: {code} ({DescribeExitCode(code)}) ---");
            TxtExitCode.Text = $"退出码: {code}";
            TxtStatus.Text = code <= 3 ? "成功" : "有错误";
        }
        catch (OperationCanceledException)
        {
            AppendOutput("\n--- 已取消 ---");
            TxtStatus.Text = "已取消";
        }
        catch (Exception ex)
        {
            AppendOutput($"\n异常: {ex.Message}");
            TxtStatus.Text = "错误";
        }
        finally
        {
            _process.Dispose();
            _process = null;
            _cts.Dispose();
            _cts = null;
            SetRunning(false);
        }
    }

    private void BtnCancel_Click(object s, RoutedEventArgs e)
    {
        _cts?.Cancel();
        try { _process?.Kill(true); } catch { }
    }

    private void SetRunning(bool running)
    {
        BtnRun.IsEnabled = !running;
        BtnDryRun.IsEnabled = !running;
        BtnCancel.IsEnabled = running;
        ProgressBar.IsIndeterminate = running;
        ProgressBar.Visibility = running ? Visibility.Visible : Visibility.Hidden;
        TxtStatus.Text = running ? "执行中..." : "就绪";
        if (running) { _startTime = DateTime.Now; _timer.Start(); }
        else _timer.Stop();
    }

    private void AppendOutput(string line)
    {
        var t = TxtOutput.Text;
        if (t.Length > 600_000) TxtOutput.Text = t.Substring(t.Length - 300_000);
        TxtOutput.Text += line + "\n";
        OutputScroller.ScrollToBottom();
    }

    private static string DescribeExitCode(int c) => c switch
    {
        0 => "无变化", 1 => "文件已复制", 2 => "存在额外项",
        3 => "已复制+额外项", 4 => "不匹配", 8 => "无法复制",
        16 => "严重错误", _ => $"组合状态 ({c})"
    };

    private void BtnReset_Click(object s, RoutedEventArgs e)
    {
        TxtSource.Text = ""; TxtDestination.Text = "";
        RbCopyAll.IsChecked = true; RbSubdirAll.IsChecked = true;
        TxtFileFilter.Text = ""; TxtExcludeFiles.Text = ""; TxtExcludeDirs.Text = "";
        TxtRetries.Text = "3"; TxtWait.Text = "5"; TxtThreads.Text = "8";
        ChkRestartable.IsChecked = true; ChkBackup.IsChecked = false;
        TxtLogFile.Text = ""; ChkAppendLog.IsChecked = true; TxtExtraArgs.Text = "";
        TxtOutput.Text = "等待执行...";
        TxtStatus.Text = "就绪"; TxtElapsed.Text = "耗时: 00:00:00";
        TxtExitCode.Text = "退出码: --";
        UpdateCommandPreview();
    }

    private async void BtnCopyCommand_Click(object s, RoutedEventArgs e)
    {
        var (exe, args) = BuildCommand(false);
        Clipboard.SetText($"{exe} {args}");
        var btn = (Button)s;
        var orig = btn.Content;
        btn.Content = "已复制";
        await Task.Delay(1200);
        btn.Content = orig;
    }
}