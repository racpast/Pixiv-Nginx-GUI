using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json.Linq;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Pixiv_Nginx_GUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            _logUpdateTimer = new DispatcherTimer();
            _logUpdateTimer.Interval = TimeSpan.FromSeconds(5);
            _logUpdateTimer.Tick += LogUpdateTimer_Tick;
            _NginxStUpdateTimer = new DispatcherTimer();
            _NginxStUpdateTimer.Interval = TimeSpan.FromSeconds(5);
            _NginxStUpdateTimer.Tick += NginxStUpdateTimer_Tick;
        }

        private void LogUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateLog();
        }

        private void NginxStUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateNginxST();
        }

        public void UpdateNginxST()
        {
            Process[] ps = Process.GetProcessesByName("nginx");
            if (ps.Length > 0)
            {
                NginxST.Text = "当前 Nginx 状态：运行中";
                NginxST.Foreground = new SolidColorBrush(Colors.ForestGreen);
            }
            else
            {
                NginxST.Text = "当前 Nginx 状态：已停止";
                NginxST.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private DispatcherTimer _logUpdateTimer;

        private DispatcherTimer _NginxStUpdateTimer;

        private string NewVersion;

        public static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

        public static string dataDirectory = Path.Combine(currentDirectory, "data");

        public static string NginxDirectory = Path.Combine(dataDirectory,"pixiv-nginx");

        string nginxPath = Path.Combine(NginxDirectory, "nginx.exe");

        string nginxConfigFile = Path.Combine(NginxDirectory, "conf", "nginx.conf");

        string CERFile = Path.Combine(NginxDirectory, "ca.cer");

        string hostsFile = Path.Combine(NginxDirectory, "hosts");

        public static string nginxLogPath = Path.Combine(NginxDirectory, "logs");

        string nginxLog1Path = Path.Combine(nginxLogPath, "access.log");

        string nginxLog2Path = Path.Combine(nginxLogPath, "E-hentai-access.log");

        string nginxLog3Path = Path.Combine(nginxLogPath, "E-hentai-error.log");

        string nginxLog4Path = Path.Combine(nginxLogPath, "error.log");

        public void CheckFiles()
        {
            string[] ImportantfilePaths = { nginxPath, nginxConfigFile, CERFile, hostsFile };
            string[] LogfilePaths = { nginxLog1Path, nginxLog2Path, nginxLog3Path, nginxLog4Path };
            EnsureDirectoryExists(dataDirectory);
            EnsureDirectoryExists(NginxDirectory);
            EnsureDirectoryExists(Path.Combine(dataDirectory, "temp"));
            EnsureDirectoryExists(nginxLogPath);
            foreach (var filePath in ImportantfilePaths)
            {
                if (!File.Exists(filePath))
                {
                    HandyControl.Controls.MessageBox.Show($"检测到重要文件缺失：{System.IO.Path.GetFileName(filePath)}，请重新下载或通过手动安装压缩包来修复文件缺失！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            foreach (var filePath in LogfilePaths)
            {
                if (!File.Exists(filePath))
                {
                    File.Create(filePath);
                }
            }
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void UpdateLog()
        {
            ComboBoxItem selectedItem = LogCombo.SelectedItem as ComboBoxItem;
            if (selectedItem!=null)
            {
                try
                {
                    string selectedText = selectedItem.Content.ToString();
                if (selectedText == "access.log")
                {

                        using (FileStream fileStream = new FileStream(nginxLog1Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader reader = new StreamReader(fileStream))
                            {
                                LogTb.Text = reader.ReadToEnd();
                            }
                        }

                }
                else if (selectedText == "E-hentai-access.log")
                {
                        using (FileStream fileStream = new FileStream(nginxLog2Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader reader = new StreamReader(fileStream))
                            {
                                LogTb.Text = reader.ReadToEnd();
                            }
                        }
                }
                else if (selectedText == "E-hentai-error.log")
                {

                        using (FileStream fileStream = new FileStream(nginxLog3Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader reader = new StreamReader(fileStream))
                            {
                                LogTb.Text = reader.ReadToEnd();
                            }
                        }
                }
                else if (selectedText == "error.log")
                {
                        using (FileStream fileStream = new FileStream(nginxLog4Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader reader = new StreamReader(fileStream))
                            {
                                LogTb.Text = reader.ReadToEnd();
                            }
                        }
                }
                }
                catch (IOException ex)
                {
                    HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            List<string> filePaths = new List<string> { nginxLog1Path, nginxLog2Path, nginxLog3Path, nginxLog4Path };
            DelLogBtn.Content = $"清理所有日志({GetTotalFileSizeInMB(filePaths)}MB)";
        }

        public async System.Threading.Tasks.Task KillNginx()
        {
            Process[] processes = Process.GetProcessesByName("nginx");
            if (processes.Length == 0)
            {
                return;
            }
            List<System.Threading.Tasks.Task> tasks = new List<System.Threading.Tasks.Task>();
            foreach (Process process in processes)
            {
                System.Threading.Tasks.Task task = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        process.Kill();
                        bool exited = process.WaitForExit(5000);
                        if (!exited)
                        {
                            HandyControl.Controls.MessageBox.Show($"进程 {process.ProcessName} 在超时时间内没有退出。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        HandyControl.Controls.MessageBox.Show($"无法杀死进程 {process.ProcessName}: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
                tasks.Add(task);
            }
            await System.Threading.Tasks.Task.WhenAll(tasks);
        }

        static double GetTotalFileSizeInMB(List<string> filePaths)
        {
            long totalSizeInBytes = 0;
            foreach (string filePath in filePaths)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                totalSizeInBytes += fileInfo.Length;
            }
            double totalSizeInMB = Math.Round((double)totalSizeInBytes / (1024 * 1024),2); // 将字节转换为MB
            return totalSizeInMB;
        }

        private void AutoConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            FirstUse firstUse = new FirstUse();
            firstUse.Show();
            this.Hide();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CheckFiles();
            UpdateNginxST();
            tabcontrol.SelectionChanged += TabControl_SelectionChanged;
            _NginxStUpdateTimer.Start();
            VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + Properties.Settings.Default.CurrentVersionCommitDate;
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateNginxST();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            Process process = new Process();
            process.StartInfo.FileName = nginxPath;
            process.StartInfo.WorkingDirectory = NginxDirectory;
            process.StartInfo.UseShellExecute = false;
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"无法启动进程: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            UpdateNginxST();
        }

        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            await KillNginx();
            UpdateNginxST();
        }

        private void CheckConfBtn_Click(object sender, RoutedEventArgs e)
        {
            string command = $"nginx -t -c \"{nginxConfigFile}\" & pause & exit";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{command}\"",
                WorkingDirectory = NginxDirectory,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            };
            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}","错误",MessageBoxButton.OK,MessageBoxImage.Error);
            }
        }

        private void ReloadConfBtn_Click(object sender, RoutedEventArgs e)
        {
            string command = "nginx -s reload & pause & exit";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{command}\"",
                WorkingDirectory = NginxDirectory,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            };
            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VersionBtn_Click(object sender, RoutedEventArgs e)
        {
            string command = "nginx -V & pause & exit";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{command}\"",
                WorkingDirectory = NginxDirectory,
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            };
            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetStartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (TaskService ts = new TaskService())
                {
                    string taskName = "StartNginx";
                    Microsoft.Win32.TaskScheduler.Task existingTask = ts.GetTask(taskName);
                    if (existingTask != null)
                    {
                        ts.RootFolder.DeleteTask(taskName);
                    }
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = "开机启动 Nginx。";
                    td.RegistrationInfo.Author = "Pixiv-Nginx-GUI";
                    LogonTrigger logonTrigger = new LogonTrigger();
                    td.Triggers.Add(logonTrigger);
                    ExecAction execAction = new ExecAction(nginxPath, null, NginxDirectory);
                    td.Actions.Add(execAction);
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);
                }
                HandyControl.Controls.MessageBox.Show("成功设置 Nginx 为开机启动。","提示",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch(Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void DelStartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (TaskService ts = new TaskService())
                {
                    string taskName = "StartNginx";
                    Microsoft.Win32.TaskScheduler.Task existingTask = ts.GetTask(taskName);
                    if (existingTask != null)
                    {
                        ts.RootFolder.DeleteTask(taskName);
                    }
                }
                HandyControl.Controls.MessageBox.Show("成功停止 Nginx 的开机启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateBtn.Content = "检查更新中...";
            try
            {
                string RepoInfo = await PublicHelper.GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");
                JObject repodata = JObject.Parse(RepoInfo);
                string CommitInfoURL = repodata["object"]["url"].ToString();
                UpdateLogTb.Text += $"获取到最后一次Commit信息URL：{CommitInfoURL}\r\n";
                string CommitInfo = await PublicHelper.GetAsync(CommitInfoURL);
                JObject commitdata = JObject.Parse(CommitInfo);
                string LCommitDT = commitdata["committer"]["date"].ToString();
                string LCommitSHA = commitdata["sha"].ToString();
                string LCommit = commitdata["message"].ToString();
                UpdateLogTb.Text += $"获取到最后一次Commit信息：\r\nCommit SHA：{LCommitSHA}\r\n时间：{LCommitDT}\r\n内容：{LCommit}\r\n";
                if (DateTime.Parse(LCommitDT) != DateTime.Parse(Properties.Settings.Default.CurrentVersionCommitDate))
                {
                    HandyControl.Controls.MessageBox.Show($"Pixiv-Nginx 有新版本可用！\r\nCommit SHA：{LCommitSHA}\r\n时间：{LCommitDT}\r\n内容：{LCommit}", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateBtn.IsEnabled = true;
                    NewVersion = DateTime.Parse(LCommitDT).ToString();
                    UpdateLogTb.Text += $"当前版本Commit时间：{Properties.Settings.Default.CurrentVersionCommitDate}，Pixiv-Nginx 有新版本可用。\r\n";
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show("Pixiv-Nginx 目前已是最新版本！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateLogTb.Text += "当前已是最新版本！\r\n";
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"检查更新时遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            CheckUpdateBtn.Content = "检查更新";
        }

        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            string fileUrl = "https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip";
            string destinationPath = Path.Combine(currentDirectory, "data", "temp", "Pixiv-Nginx-main.zip");
            UpdateBtn.IsEnabled = false;
            CheckUpdateBtn.IsEnabled = false;
            ChooseUpdateBtn.IsEnabled = false;
            CancellationTokenSource cts = new CancellationTokenSource();
            TimeSpan progressTimeout = TimeSpan.FromSeconds(30);
            UpdateBtn.Content = "下载中...";
            try
            {
                UpdateLogTb.Text += $"从{fileUrl}下载文件到{destinationPath}\r\n";
                string TextBfDownload = UpdateLogTb.Text;
                await PublicHelper.DownloadFileAsync(fileUrl,
                                       destinationPath,
                                       new Progress<double>(progress =>
                                       {
                                           Dispatcher.Invoke(() =>
                                           {
                                               UpdateBtn.Content = $"下载中({progress:F2}%)";
                                               UpdateLogTb.Text = TextBfDownload + $"文件下载进度：{progress:F2}%\r\n";
                                           });
                                       }),
                                       progressTimeout,
                                       cts.Token);
                UpdateLogTb.Text += $"文件下载完成！\r\n";
                UpdateLogTb.Text += $"结束 Nginx 进程...\r\n";
                await KillNginx();
                UpdateLogTb.Text += $"清理旧版本目录...\r\n";
                Directory.Delete(NginxDirectory, true);
                UpdateBtn.Content = $"解压中...";
                UpdateLogTb.Text += $"解压新版本压缩包...\r\n";
                await System.Threading.Tasks.Task.Run(() => PublicHelper.UnZip(destinationPath, dataDirectory, false));
                UpdateBtn.Content = $"解压完成";
                UpdateLogTb.Text += $"解压新版本压缩包完成！\r\n";
                Properties.Settings.Default.CurrentVersionCommitDate = NewVersion;
                Properties.Settings.Default.Save();
                VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + Properties.Settings.Default.CurrentVersionCommitDate;
                HandyControl.Controls.MessageBox.Show("更新完成！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                UpdateLogTb.Text += $"文件下载超时，请重试！\r\n";
            }
            catch (Exception ex)
            {
                UpdateLogTb.Text += $"更新失败：{ex.Message}\r\n";
            }
            finally
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                CheckUpdateBtn.IsEnabled = true;
                ChooseUpdateBtn.IsEnabled = true;
                cts.Dispose();
                UpdateBtn.Content = "更新至最新版本";
            }
        }

        private async void ChooseUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "选择ZIP文件",
                Filter = "ZIP文件|*.zip",
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true
            };
            if (openFileDialog.ShowDialog() == true)
            {
                string RepoInfo = await PublicHelper.GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");
                JObject repodata = JObject.Parse(RepoInfo);
                string CommitInfoURL = repodata["object"]["url"].ToString();
                string CommitInfo = await PublicHelper.GetAsync(CommitInfoURL);
                JObject commitdata = JObject.Parse(CommitInfo);
                string LCommitDT = commitdata["committer"]["date"].ToString();
                string filePath = openFileDialog.FileName;
                InputBox inputBox = new InputBox
                {
                    InitialText = $"从本地文件安装时，您需要为该版本指定 Commit 日期（GMT）。\r\n最新版本 Commit 日期：{DateTime.Parse(LCommitDT)}\r\n当前版本 Commit 日期：{DateTime.Parse(Properties.Settings.Default.CurrentVersionCommitDate)}",
                    InitialTitle = "输入"
                };

                bool? result = inputBox.ShowDialog();

                if (result == true)
                {
                    while (!DateTime.TryParse(inputBox.InputText, out DateTime InputdateTime))
                    {
                        HandyControl.Controls.MessageBox.Show("您输入了无效的日期时间！", "输入", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                    CheckUpdateBtn.IsEnabled = false;
                    UpdateBtn.IsEnabled = false;
                    ChooseUpdateBtn.IsEnabled = false;
                    try
                    {
                        UpdateLogTb.Text += $"结束 Nginx 进程...\r\n";
                        await KillNginx();
                        UpdateLogTb.Text += $"清理旧版本目录...\r\n";
                        Directory.Delete(NginxDirectory, true);
                        UpdateLogTb.Text += $"解压新版本压缩包...\r\n";
                        await System.Threading.Tasks.Task.Run(() => PublicHelper.UnZip(filePath, dataDirectory, false));
                        UpdateLogTb.Text += $"解压新版本压缩包完成！\r\n";
                        Properties.Settings.Default.CurrentVersionCommitDate = DateTime.Parse(inputBox.InputText).ToString();
                        Properties.Settings.Default.Save();
                        VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + Properties.Settings.Default.CurrentVersionCommitDate;
                        HandyControl.Controls.MessageBox.Show("从本地更新完成！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        UpdateLogTb.Text += $"从本地更新失败：{ex.Message}\r\n";
                        HandyControl.Controls.MessageBox.Show($"从本地更新失败！\r\n{ex}", "更新", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        CheckUpdateBtn.IsEnabled = true;
                        ChooseUpdateBtn.IsEnabled = true;
                    }
                }
            }
        }

        private async void DelLogBtn_Click(object sender, RoutedEventArgs e)
        {
            await KillNginx();
            UpdateNginxST();
            _logUpdateTimer.Stop();
            File.Delete(nginxLog1Path);
            File.Delete(nginxLog2Path);
            File.Delete(nginxLog3Path);
            File.Delete(nginxLog4Path);
            CheckFiles();
            HandyControl.Controls.MessageBox.Show("日志清理完成！","清理日志",MessageBoxButton.OK,MessageBoxImage.Information);
            List<string> filePaths = new List<string> { nginxLog1Path, nginxLog2Path, nginxLog3Path, nginxLog4Path };
            DelLogBtn.Content = $"清理所有日志({GetTotalFileSizeInMB(filePaths)}MB)";
            LogTb.Text = "";
            _logUpdateTimer.Start();
        }

        private void LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateLog();
        }

        private void UpdateLogTb_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLogTb.ScrollToEnd();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl == null) return;
            var selectedItem = tabControl.SelectedItem as TabItem;
            switch (selectedItem?.Header.ToString())
            {
                case "日志":
                    CheckFiles();
                    UpdateLog();
                    _logUpdateTimer.Start();
                    _NginxStUpdateTimer.Stop(); 
                    break;
                case "主页":
                    UpdateNginxST();
                    _NginxStUpdateTimer.Start();
                    _logUpdateTimer.Stop();
                    break;
                default:
                    _logUpdateTimer.Stop();
                    _NginxStUpdateTimer.Stop();
                    VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + Properties.Settings.Default.CurrentVersionCommitDate;
                    break;
            }
        }
    }
}
