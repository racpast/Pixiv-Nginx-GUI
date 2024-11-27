using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Pixiv_Nginx_GUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 定义用于更新日志和 Nginx 状态的计时器
        private readonly DispatcherTimer _logUpdateTimer;
        private readonly DispatcherTimer _NginxStUpdateTimer;
        // 先前的证书安装状态，用于回退修改
        private string NewVersion;
        // 定义基本路径
        public static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static string dataDirectory = Path.Combine(currentDirectory, "data");
        public static string TempDirectory = Path.Combine(dataDirectory, "temp");
        public static string NginxDirectory = Path.Combine(dataDirectory, "pixiv-nginx");
        public static string OldNginxDirectory = Path.Combine(dataDirectory, "pixiv-nginx.old");
        readonly string nginxPath = Path.Combine(NginxDirectory, "nginx.exe");
        readonly string nginxConfigFile = Path.Combine(NginxDirectory, "conf", "nginx.conf");
        readonly string CERFile = Path.Combine(NginxDirectory, "ca.cer");
        readonly string hostsFile = Path.Combine(NginxDirectory, "hosts");
        public static string nginxLogPath = Path.Combine(NginxDirectory, "logs");
        readonly static string nginxLog1Path = Path.Combine(nginxLogPath, "access.log");
        readonly static string nginxLog2Path = Path.Combine(nginxLogPath, "E-hentai-access.log");
        readonly static string nginxLog3Path = Path.Combine(nginxLogPath, "E-hentai-error.log");
        readonly static string nginxLog4Path = Path.Combine(nginxLogPath, "error.log");
        // 创建一个包含所有日志文件路径的列表
        List<string> filePaths = new List<string> { nginxLog1Path, nginxLog2Path, nginxLog3Path, nginxLog4Path };


        // 窗口构造函数
        public MainWindow()
        {
            InitializeComponent();
            // 创建一个新的 DispatcherTimer 实例，用于定期更新日志信息
            _logUpdateTimer = new DispatcherTimer
            {
                // 设置 timer 的时间间隔为每5秒触发一次
                Interval = TimeSpan.FromSeconds(5)
            };
            // 当 timer 到达指定的时间间隔时，将调用 LogUpdateTimer_Tick 方法
            _logUpdateTimer.Tick += LogUpdateTimer_Tick;
            // 创建另一个新的 DispatcherTimer 实例，用于定期更新 Nginx 状态信息
            _NginxStUpdateTimer = new DispatcherTimer
            {
                // 设置 timer 的时间间隔为每5秒触发一次
                Interval = TimeSpan.FromSeconds(5)
            };
            // 当 timer 到达指定的时间间隔时，将调用 NginxStUpdateTimer_Tick 方法
            _NginxStUpdateTimer.Tick += NginxStUpdateTimer_Tick;
        }

        // 日志更新计时器触发事件
        private void LogUpdateTimer_Tick(object sender, EventArgs e)
        {
            // 更新日志
            UpdateLog();
        }

        // Nginx 状态更新计时器触发事件
        private void NginxStUpdateTimer_Tick(object sender, EventArgs e)
        {
            // 更新 Nginx 状态
            UpdateNginxST();
        }

        // 更新 Nginx 状态的方法
        public void UpdateNginxST()
        {
            // 使用 Process.GetProcessesByName 方法获取所有名为 "nginx" 的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps = Process.GetProcessesByName("nginx");
            // 检查获取到的进程数组长度是否大于 0
            // 如果大于 0，说明 Nginx 正在运行
            if (ps.Length > 0)
            {
                // 更新 Nginx 状态文本为 "当前 Nginx 状态：运行中"
                NginxST.Text = "当前 Nginx 状态：运行中";
                // 将 Nginx 状态文本的前景色设置为森林绿色
                NginxST.Foreground = new SolidColorBrush(Colors.ForestGreen);
                // 禁用启动按钮，因为 Nginx 已经在运行
                StartBtn.IsEnabled = false;
                // 启用停止按钮，因为可以停止正在运行的 Nginx
                StopBtn.IsEnabled = true;
            }
            else
            {
                // 如果进程数组长度为 0，说明 Nginx 没有运行
                // 更新 Nginx 状态文本为 "当前 Nginx 状态：已停止"
                NginxST.Text = "当前 Nginx 状态：已停止";
                // 将 Nginx 状态文本的前景色设置为红色
                NginxST.Foreground = new SolidColorBrush(Colors.Red);
                // 启用启动按钮，因为可以启动 Nginx
                StartBtn.IsEnabled = true;
                // 禁用停止按钮，因为没有正在运行的 Nginx 可以停止
                StopBtn.IsEnabled = false;
            }
        }

        // 用于检查重要目录是否存在以及日志文件是否创建的方法
        public void CheckFiles()
        {
            // 定义包含重要文件路径的数组
            string[] ImportantfilePaths = { nginxPath, nginxConfigFile, CERFile, hostsFile };
            // 定义包含日志文件路径的数组
            string[] LogfilePaths = { nginxLog1Path, nginxLog2Path, nginxLog3Path, nginxLog4Path };
            // 确保重要目录存在
            EnsureDirectoryExists(dataDirectory);
            EnsureDirectoryExists(NginxDirectory);
            EnsureDirectoryExists(TempDirectory);
            EnsureDirectoryExists(nginxLogPath);
            // 遍历重要文件路径数组
            foreach (var filePath in ImportantfilePaths)
            {
                // 如果文件不存在
                if (!File.Exists(filePath))
                {
                    // 显示消息框，提示用户检测到重要文件缺失，并建议重新下载或手动安装
                    HandyControl.Controls.MessageBox.Show("检测到重要文件缺失，请重新下载或通过手动安装压缩包来修复文件缺失！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    // 跳出循环，不再继续检查其他文件
                    break;
                }
            }
            // 遍历日志文件路径数组
            foreach (var filePath in LogfilePaths)
            {
                // 如果日志文件不存在
                if (!File.Exists(filePath))
                {
                    // 创建日志文件（如果文件已存在，File.Create 会覆盖原文件，但这里我们更关心的是创建不存在的文件）
                    // 注意：File.Create 打开文件后需要关闭，但这里仅用它来创建文件，因此使用 using 语句或 File.CreateText 会更好
                    // 更好的做法是使用 File.CreateText(filePath).Close(); 或 using (File.Create(filePath)) {}
                    File.CreateText(filePath).Close(); 
                }
                // 如果文件已存在，则不执行任何操作
            }
        }

        // 该方法用于确保指定路径的目录存在，如果目录不存在，则创建它
        public static void EnsureDirectoryExists(string path)
        {
            // 如果目录不存在
            if (!Directory.Exists(path))
            {
                // 创建目录
                Directory.CreateDirectory(path);
            }
            // 如果目录已存在，则不执行任何操作
        }

        // 用于更新日志显示的方法
        public void UpdateLog()
        {
            // 检查下拉框中是否有选中的项，并将其转换为 ComboBoxItem 类型
            if (LogCombo.SelectedItem is ComboBoxItem selectedItem)
            {
                try
                {
                    // 获取选中项的文本内容
                    string selectedText = selectedItem.Content.ToString();
                    // 根据选中项的文本内容，决定读取哪个日志文件
                    if (selectedText == "access.log")
                    {
                        // 使用 FileStream 以只读和共享读写的方式打开nginxLog1Path指定的文件
                        using (FileStream fileStream = new FileStream(nginxLog1Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            // 使用 StreamReader 读取文件内容
                            using (StreamReader reader = new StreamReader(fileStream))
                            {
                                // 将读取的内容设置为日志文本框的文本
                                LogTb.Text = reader.ReadToEnd();
                            }
                        }

                    }
                    else if (selectedText == "E-hentai-access.log")
                    {
                        // 同上，但读取的是 nginxLog2Path 指定的文件
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
                        // 同上，但读取的是 nginxLog3Path 指定的文件
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
                        // 同上，但读取的是 nginxLog4Path 指定的文件
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
                    // 如果在文件操作过程中遇到IO异常，则显示错误消息框
                    HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // 更新清理日志按钮的内容，显示所有日志文件的总大小（以MB为单位）
            DelLogBtn.Content = $"清理所有日志({GetTotalFileSizeInMB(filePaths)}MB)";
        }

        // 用于杀死所有名为 "nginx" 的进程的异步方法
        public async System.Threading.Tasks.Task KillNginx()
        {
            // 获取所有名为 "nginx" 的进程
            Process[] processes = Process.GetProcessesByName("nginx");
            // 如果没有找到名为 "nginx" 的进程，则直接返回
            if (processes.Length == 0)
            {
                return;
            }
            // 创建一个任务列表，用于存储每个杀死进程任务的任务对象
            List<System.Threading.Tasks.Task> tasks = new List<System.Threading.Tasks.Task>();
            // 遍历所有找到的 "nginx" 进程
            foreach (Process process in processes)
            {
                // 为每个进程创建一个异步任务，该任务尝试杀死进程并处理可能的异常
                System.Threading.Tasks.Task task = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // 尝试杀死当前遍历到的进程
                        process.Kill();
                        // 等待进程退出，最多等待5000毫秒（5秒）
                        bool exited = process.WaitForExit(5000);
                        // 如果进程在超时时间内没有退出，则显示警告消息框
                        if (!exited)
                        {
                            HandyControl.Controls.MessageBox.Show($"进程 {process.ProcessName} 在超时时间内没有退出。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果在杀死进程的过程中发生异常，则显示错误消息框
                        HandyControl.Controls.MessageBox.Show($"无法杀死进程 {process.ProcessName}: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
                // 将创建的任务添加到任务列表中
                tasks.Add(task);
            }
            // 等待所有杀死进程的任务完成
            await System.Threading.Tasks.Task.WhenAll(tasks);
        }

        // 用于计算给定文件路径列表中的文件总大小（以MB为单位）的静态方法
        static double GetTotalFileSizeInMB(List<string> filePaths)
        {
            // 定义一个变量来存储文件总大小（以字节为单位）
            long totalSizeInBytes = 0;
            // 遍历给定的文件路径列表
            foreach (string filePath in filePaths)
            {
                // 使用文件路径创建一个FileInfo对象，该对象提供有关文件的详细信息
                FileInfo fileInfo = new FileInfo(filePath);
                // 将当前文件的长度（以字节为单位）添加到总大小中
                totalSizeInBytes += fileInfo.Length;
            }
            // 将总大小（以字节为单位）转换为MB，并保留两位小数
            double totalSizeInMB = Math.Round((double)totalSizeInBytes / (1024 * 1024), 2);
            // 返回文件总大小（以MB为单位）
            return totalSizeInMB;
        }

        // 自动配置按钮的点击事件
        private async void AutoConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            // 异步调用该方法，杀死所有名为"nginx"的进程
            await KillNginx();
            try
            {
                // 尝试将 data\pixiv-nginx 重命名为 data\pixiv-nginx.old
                PublicHelper.RenameDirectory(NginxDirectory, OldNginxDirectory);
            }catch (Exception ex)
            {
                // 如果在重命名目录时发生异常，则显示一个错误消息框
                HandyControl.Controls.MessageBox.Show($"{ex.Message}\r\n请重试！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 创建一个 FirstUse 窗口的实例
            FirstUse firstUse = new FirstUse();
            // 显示 FirstUse 窗口
            firstUse.Show();
            // 隐藏当前窗口
            this.Hide();
        }

        // 窗口加载完成事件
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 检查应用程序的设置，判断是否为首次运行
            if (Properties.Settings.Default.IsFirst)
            {
                // 如果是首次运行，则模拟点击“自动配置”按钮，这将触发AutoConfigBtn_Click方法中的逻辑
                // 注意：这里直接调用事件处理方法可能不是最佳实践，因为它绕过了事件系统
                // 更好的做法是将AutoConfigBtn_Click中的逻辑封装到一个单独的方法中，并在这里调用该方法
                AutoConfigBtn_Click(this, e);
                // 将设置中的IsFirst标记为false，表示不再是首次运行
                Properties.Settings.Default.IsFirst = false;
                // 保存设置，使更改生效
                Properties.Settings.Default.Save();
            }
            else
            {
                // 如果不是首次运行，则检查文件
                CheckFiles();
            }
            // 更新 Nginx 的状态信息
            UpdateNginxST();
            // 为 TabControl 的 SelectionChanged 事件添加事件处理程序，用户切换选项卡时，将调用 TabControl_SelectionChanged 方法
            tabcontrol.SelectionChanged += TabControl_SelectionChanged;
            // 启动用于定期更新 Nginx 的状态信息的定时器，
            _NginxStUpdateTimer.Start();
            // 显示当前 Pixiv-Nginx 版本的提交日期
            VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + Properties.Settings.Default.CurrentVersionCommitDate;
        }

        // 刷新状态按钮的点击事件
        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            // 更新 Nginx 的状态信息
            UpdateNginxST();
        }

        // 启动按钮的点击事件
        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            // 创建一个新的Process对象，用于启动外部进程
            Process process = new Process();
            // 设置要启动的进程的文件名
            process.StartInfo.FileName = nginxPath;
            // 设置进程的工作目录
            process.StartInfo.WorkingDirectory = NginxDirectory;
            // 设置是否使用操作系统外壳启动进程
            process.StartInfo.UseShellExecute = false;
            try
            {
                // 尝试启动进程
                process.Start();
            }
            catch (Exception ex)
            {
                // 如果启动进程时发生异常，则显示一个错误消息框
                HandyControl.Controls.MessageBox.Show($"无法启动进程: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 更新 Nginx 的状态信息
            UpdateNginxST();
        }

        // 停止按钮的点击事件
        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            // 异步调用该方法，用于杀死所有名为"nginx"的进程
            await KillNginx();
            // 更新Nginx的状态信息
            UpdateNginxST();
        }

        // 检查配置按钮的点击事件
        private void CheckConfBtn_Click(object sender, RoutedEventArgs e)
        {
            // 构建要执行的命令字符串，该命令用于检查配置文件，并在执行后暂停，然后退出
            string command = $"nginx -t -c \"{nginxConfigFile}\" & pause & exit";
            // 创建一个ProcessStartInfo对象，用于配置如何启动一个进程
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                // 指定要启动的进程的文件名
                FileName = "cmd.exe",
                // 指定传递给cmd.exe的参数，/k表示执行完命令后保持窗口打开，\"{command}\"是要执行的命令
                Arguments = $"/k \"{command}\"",
                // 设置进程的工作目录
                WorkingDirectory = NginxDirectory,
                // 设置为true，表示使用操作系统shell来启动进程（默认行为）
                UseShellExecute = true,
                // 设置为false，表示不将进程的标准输出重定向到调用进程的输出流中
                RedirectStandardOutput = false,
                // 设置为false，表示不将进程的标准错误输出重定向到调用进程的错误输出流中
                RedirectStandardError = false,
                // 设置为false，表示启动进程时创建一个新窗口
                CreateNoWindow = false
            };
            try
            {
                // 尝试启动进程
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                // 如果启动进程时发生异常，显示错误消息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 重载配置按钮的点击事件
        private void ReloadConfBtn_Click(object sender, RoutedEventArgs e)
        {
            // 构建要执行的命令字符串，该命令用于重载配置文件，并在执行后暂停，然后退出
            string command = "nginx -s reload & pause & exit";
            // 创建一个ProcessStartInfo对象，用于配置如何启动一个进程
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                // 指定要启动的进程的文件名
                FileName = "cmd.exe",
                // 指定传递给cmd.exe的参数，/k表示执行完命令后保持窗口打开，\"{command}\"是要执行的命令
                Arguments = $"/k \"{command}\"",
                // 设置进程的工作目录
                WorkingDirectory = NginxDirectory,
                // 设置为true，表示使用操作系统shell来启动进程（默认行为）
                UseShellExecute = true,
                // 设置为false，表示不将进程的标准输出重定向到调用进程的输出流中
                RedirectStandardOutput = false,
                // 设置为false，表示不将进程的标准错误输出重定向到调用进程的错误输出流中
                RedirectStandardError = false,
                // 设置为false，表示启动进程时创建一个新窗口
                CreateNoWindow = false
            };
            try
            {
                // 尝试启动进程
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                // 如果启动进程时发生异常，显示错误消息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 查看版本按钮的点击事件
        private void VersionBtn_Click(object sender, RoutedEventArgs e)
        {
            // 构建要执行的命令字符串，该命令用于显示 Nginx 版本，并在执行后暂停，然后退出
            string command = "nginx -V & pause & exit";
            // 创建一个ProcessStartInfo对象，用于配置如何启动一个进程
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                // 指定要启动的进程的文件名
                FileName = "cmd.exe",
                // 指定传递给cmd.exe的参数，/k表示执行完命令后保持窗口打开，\"{command}\"是要执行的命令
                Arguments = $"/k \"{command}\"",
                // 设置进程的工作目录
                WorkingDirectory = NginxDirectory,
                // 设置为true，表示使用操作系统shell来启动进程（默认行为）
                UseShellExecute = true,
                // 设置为false，表示不将进程的标准输出重定向到调用进程的输出流中
                RedirectStandardOutput = false,
                // 设置为false，表示不将进程的标准错误输出重定向到调用进程的错误输出流中
                RedirectStandardError = false,
                // 设置为false，表示启动进程时创建一个新窗口
                CreateNoWindow = false
            };
            try
            {
                // 尝试启动进程
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                // 如果启动进程时发生异常，显示错误消息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 设置开机启动按钮的点击事件
        private void SetStartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用 TaskService 来访问和操作任务计划程序
                using (TaskService ts = new TaskService())
                {
                    // 定义要创建或更新的任务名称
                    string taskName = "StartNginx";
                    // 尝试获取已存在的同名任务
                    Task existingTask = ts.GetTask(taskName);
                    // 如果任务已存在，则删除它，以便创建新的任务
                    if (existingTask != null)
                    {
                        ts.RootFolder.DeleteTask(taskName);
                    }
                    // 创建一个新的任务定义
                    TaskDefinition td = ts.NewTask();
                    // 设置任务的描述信息和作者
                    td.RegistrationInfo.Description = "开机启动 Nginx。";
                    td.RegistrationInfo.Author = "Pixiv-Nginx-GUI";
                    // 创建一个登录触发器，当用户登录时触发任务
                    LogonTrigger logonTrigger = new LogonTrigger();
                    // 将登录触发器添加到任务定义中
                    td.Triggers.Add(logonTrigger);
                    // 创建一个执行操作，指定要执行的 Nginx 路径、参数和工作目录
                    ExecAction execAction = new ExecAction(nginxPath, null, NginxDirectory);
                    // 将执行操作添加到任务定义中
                    td.Actions.Add(execAction);
                    // 在根文件夹中注册新的任务定义
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);
                }
                // 显示提示信息，表示 Nginx 已成功设置为开机启动
                HandyControl.Controls.MessageBox.Show("成功设置 Nginx 为开机启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // 捕获异常并显示错误信息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 退出工具按钮的点击事件
        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            // 退出程序
            Environment.Exit(0);
        }

        // 停止开机启动按钮的点击事件
        private void DelStartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用 TaskService 来访问和操作任务计划程序
                using (TaskService ts = new TaskService())
                {
                    // 定义要创建或更新的任务名称
                    string taskName = "StartNginx";
                    // 尝试获取已存在的同名任务
                    Task existingTask = ts.GetTask(taskName);
                    // 如果任务已存在，则删除它
                    if (existingTask != null)
                    {
                        ts.RootFolder.DeleteTask(taskName);
                    }
                }
                // 显示提示信息，表示 Nginx 已成功停止开机启动
                HandyControl.Controls.MessageBox.Show("成功停止 Nginx 的开机启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // 捕获异常并显示错误信息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 检查更新按钮的点击事件，用于检查Pixiv-Nginx是否有新版本可用
        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            // 修改按钮内容为“检查更新中...”以提示用户正在进行检查
            CheckUpdateBtn.Content = "检查更新中...";
            try
            {
                // 异步获取 GitHub 仓库的最新提交信息
                string RepoInfo = await PublicHelper.GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");
                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(RepoInfo);
                // 从解析后的JSON中获取最后一次提交的URL
                string CommitInfoURL = repodata["object"]["url"].ToString();
                // 在更新日志文本框中添加获取到的Commit信息URL
                UpdateLogTb.Text += $"获取到最后一次Commit信息URL：{CommitInfoURL}\r\n";
                // 异步获取最后一次提交的详细信息
                string CommitInfo = await PublicHelper.GetAsync(CommitInfoURL);
                // 将返回的JSON字符串解析为JObject
                JObject commitdata = JObject.Parse(CommitInfo);
                // 从解析后的JSON中获取提交者的日期、SHA值以及提交信息
                string LCommitDT = commitdata["committer"]["date"].ToString();
                string LCommitSHA = commitdata["sha"].ToString();
                string LCommit = commitdata["message"].ToString();
                // 在更新日志文本框中添加获取到的Commit详细信息
                UpdateLogTb.Text += $"获取到最后一次Commit信息：\r\nCommit SHA：{LCommitSHA}\r\n时间：{LCommitDT}\r\n内容：{LCommit}\r\n";
                // 比较当前安装的版本与 GitHub 上的最新版本
                if (DateTime.Parse(LCommitDT) != DateTime.Parse(Properties.Settings.Default.CurrentVersionCommitDate))
                {
                    // 如果有新版本，则弹出提示框并启用更新按钮
                    HandyControl.Controls.MessageBox.Show($"Pixiv-Nginx 有新版本可用！\r\nCommit SHA：{LCommitSHA}\r\n时间：{LCommitDT}\r\n内容：{LCommit}", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateBtn.IsEnabled = true;
                    // 更新新版本信息
                    NewVersion = DateTime.Parse(LCommitDT).ToString();
                    // 在更新日志文本框中添加当前版本与新版本的信息
                    UpdateLogTb.Text += $"当前版本Commit时间：{Properties.Settings.Default.CurrentVersionCommitDate}，Pixiv-Nginx 有新版本可用。\r\n";
                }
                else
                {
                    // 如果没有新版本，则弹出提示框告知用户已是最新版本
                    HandyControl.Controls.MessageBox.Show("Pixiv-Nginx 目前已是最新版本！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    // 在更新日志文本框中添加已是最新版本的信息
                    UpdateLogTb.Text += "当前已是最新版本！\r\n";
                }
            }
            catch (Exception ex)
            {
                // 捕获异常并弹出错误提示框
                HandyControl.Controls.MessageBox.Show($"检查更新时遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 检查完成后，将按钮内容改回“检查更新”
            CheckUpdateBtn.Content = "检查更新";
        }

        // 更新至最新版本按钮的点击事件
        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            // 定义main.zip的下载地址和保存路径
            string fileUrl = "https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip";
            string destinationPath = Path.Combine(TempDirectory, "Pixiv-Nginx-main.zip");
            // 禁用有关按钮
            UpdateBtn.IsEnabled = false;
            CheckUpdateBtn.IsEnabled = false;
            ChooseUpdateBtn.IsEnabled = false;
            // 创建一个取消令牌源，用于控制下载过程的中断
            CancellationTokenSource cts = new CancellationTokenSource();
            // 设置进度更新的超时时间
            TimeSpan progressTimeout = TimeSpan.FromSeconds(10);
            // 更新按钮文本
            UpdateBtn.Content = "下载中...";
            try
            {
                // 在更新日志文本框中添加信息
                UpdateLogTb.Text += $"从{fileUrl}下载文件到{destinationPath}\r\n";
                // 存储下载前更新日志文本框的内容以便持续更新进度
                string TextBfDownload = UpdateLogTb.Text;
                // 异步下载文件，并更新下载进度
                await PublicHelper.DownloadFileAsync(fileUrl,
                                       destinationPath,
                                       new Progress<double>(progress =>
                                       {
                                           // 使用 Dispatcher 更新UI线程上的控件
                                           Dispatcher.Invoke(() =>
                                           {
                                               UpdateBtn.Content = $"下载中({progress:F2}%)";
                                               UpdateLogTb.Text = TextBfDownload + $"文件下载进度：{progress:F2}%\r\n";
                                           });
                                       }),
                                       progressTimeout,
                                       cts.Token);
                // 更新UI，表示文件下载完成并开始解压文件
                UpdateLogTb.Text += $"文件下载完成！\r\n";
                UpdateLogTb.Text += $"结束 Nginx 进程...\r\n";
                // 异步调用该方法，用于杀死所有名为"nginx"的进程
                await KillNginx();
                UpdateLogTb.Text += $"清理旧版本目录...\r\n";
                Directory.Delete(NginxDirectory, true);
                UpdateBtn.Content = $"解压中...";
                UpdateLogTb.Text += $"解压新版本压缩包...\r\n";
                // 异步解压文件
                await System.Threading.Tasks.Task.Run(() => PublicHelper.UnZip(destinationPath, dataDirectory, false));
                // 更新UI，表示文件解压完成
                UpdateBtn.Content = $"解压完成";
                UpdateLogTb.Text += $"解压新版本压缩包完成！\r\n";
                // 将新的版本号写入应用程序设置并保存
                Properties.Settings.Default.CurrentVersionCommitDate = NewVersion;
                Properties.Settings.Default.Save();
                // 显示当前 Pixiv-Nginx 版本的提交日期
                VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + Properties.Settings.Default.CurrentVersionCommitDate;
                // 弹出窗口提示更新完成
                HandyControl.Controls.MessageBox.Show("更新完成！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                // 在更新日志文本框中添加下载超时信息
                UpdateLogTb.Text += $"文件下载超时，请重试！\r\n";
            }
            catch (Exception ex)
            {
                // 在更新日志文本框中添加更新失败信息
                UpdateLogTb.Text += $"更新失败：{ex.Message}\r\n";
            }
            finally
            {
                // 清理操作：删除下载的ZIP文件，释放取消令牌源，重置按钮状态
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

        // 从本地文件安装按钮的点击事件
        private async void ChooseUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            // 创建一个OpenFileDialog实例，用于打开文件对话框
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                // 设置对话框标题
                Title = "选择ZIP文件",
                // 设置文件过滤器，只显示ZIP文件
                Filter = "ZIP文件|*.zip",
                // 禁止多选
                Multiselect = false,
                // 检查文件是否存在
                CheckFileExists = true,
                // 检查路径是否存在
                CheckPathExists = true
            };
            // 显示文件对话框，如果用户点击了确定按钮
            if (openFileDialog.ShowDialog() == true)
            {
                // 异步获取GitHub仓库的最新提交信息
                string RepoInfo = await PublicHelper.GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");
                JObject repodata = JObject.Parse(RepoInfo);
                string CommitInfoURL = repodata["object"]["url"].ToString();
                // 异步获取提交详细信息
                string CommitInfo = await PublicHelper.GetAsync(CommitInfoURL);
                JObject commitdata = JObject.Parse(CommitInfo);
                // 获取提交的日期时间（GMT）
                string LCommitDT = commitdata["committer"]["date"].ToString();
                // 获取用户选择的ZIP文件路径
                string filePath = openFileDialog.FileName;
                // 创建一个输入框对话框，提示用户输入提交日期
                InputBox inputBox = new InputBox
                {
                    // 设置提示文本，提示用户输入并显示最新版本的提交日期
                    InitialText = $"从本地文件安装时，您需要为该版本指定 Commit 日期（GMT）。\r\n最新版本 Commit 日期：{DateTime.Parse(LCommitDT)}\r\n当前版本 Commit 日期：{DateTime.Parse(Properties.Settings.Default.CurrentVersionCommitDate)}",
                    // 设置对话框标题
                    InitialTitle = "输入"
                };
                // 显示输入框对话框，并获取用户操作结果
                bool? result = inputBox.ShowDialog();
                // 如果用户点击了确定按钮
                if (result == true)
                {
                    // 循环检查用户输入的日期是否有效
                    while (!DateTime.TryParse(inputBox.InputText, out DateTime InputdateTime))
                    {
                        // 如果无效，显示错误提示
                        HandyControl.Controls.MessageBox.Show("您输入了无效的日期时间！", "输入", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                    // 禁用相关按钮
                    CheckUpdateBtn.IsEnabled = false;
                    UpdateBtn.IsEnabled = false;
                    ChooseUpdateBtn.IsEnabled = false;
                    try
                    {
                        // 更新UI，表示开始解压文件
                        UpdateLogTb.Text += $"结束 Nginx 进程...\r\n";
                        // 异步调用该方法，用于杀死所有名为"nginx"的进程
                        await KillNginx();
                        UpdateLogTb.Text += $"清理旧版本目录...\r\n";
                        // 删除现有 pixiv-nginx 目录
                        Directory.Delete(NginxDirectory, true);
                        UpdateLogTb.Text += $"解压新版本压缩包...\r\n";
                        // 在后台线程中解压文件
                        await System.Threading.Tasks.Task.Run(() => PublicHelper.UnZip(filePath, dataDirectory, false));
                        // 更新解压状态
                        UpdateLogTb.Text += $"解压新版本压缩包完成！\r\n";
                        // 将记录的新版本写入 Properties.Settings.Default.CurrentVersionCommitDate 并保存设置
                        Properties.Settings.Default.CurrentVersionCommitDate = DateTime.Parse(inputBox.InputText).ToString();
                        Properties.Settings.Default.Save();
                        // 显示当前 Pixiv-Nginx 版本的提交日期
                        VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + Properties.Settings.Default.CurrentVersionCommitDate;
                        // 弹出窗口提示更新完成
                        HandyControl.Controls.MessageBox.Show("从本地更新完成！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        // 捕捉异常并弹窗提示
                        UpdateLogTb.Text += $"从本地更新失败：{ex.Message}\r\n";
                        HandyControl.Controls.MessageBox.Show($"从本地更新失败！\r\n{ex}", "更新", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        // 无论更新是否成功，都重新启用有关按钮
                        CheckUpdateBtn.IsEnabled = true;
                        ChooseUpdateBtn.IsEnabled = true;
                    }
                }
            }
        }

        //清理日志按钮的点击事件
        private async void DelLogBtn_Click(object sender, RoutedEventArgs e)
        {
            // 异步调用该方法，用于杀死所有名为"nginx"的进程
            await KillNginx();
            // 更新 Nginx 的状态信息
            UpdateNginxST();
            // 停止定期更新日志的计时器
            _logUpdateTimer.Stop();
            // 删除所有日志文件
            File.Delete(nginxLog1Path);
            File.Delete(nginxLog2Path);
            File.Delete(nginxLog3Path);
            File.Delete(nginxLog4Path);
            // 重新建立日志文件
            CheckFiles();
            // 弹出窗口提示日志清理完成
            HandyControl.Controls.MessageBox.Show("日志清理完成！", "清理日志", MessageBoxButton.OK, MessageBoxImage.Information);
            // 更新清理日志按钮的内容，显示所有日志文件的总大小（以MB为单位）
            DelLogBtn.Content = $"清理所有日志({GetTotalFileSizeInMB(filePaths)}MB)";
            // 清空日志文本框
            LogTb.Text = "";
            // 重新启用定期更新日志的计时器
            _logUpdateTimer.Start();
        }

        // 日志文件选择下拉框选项发生改变时的事件
        private void LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 更新日志
            UpdateLog();
        }

        // 日志文本框内容发生改变时的事件
        private void UpdateLogTb_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 将文本框滚动到最底部
            UpdateLogTb.ScrollToEnd();
        }

        // 选项卡选项发生改变时的事件
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 使用模式匹配和null条件运算符来检查 sender 是否为 TabControl 的实例，如果不是，则直接返回
            if (sender is not TabControl tabControl) return;
            // 尝试将 TabControl 的选中项转换为 TabItem
            var selectedItem = tabControl.SelectedItem as TabItem;
            // 根据选中项的标题来决定执行哪个操作
            switch (selectedItem?.Header.ToString())
            {
                // 如果选中项的标题是"日志"
                case "日志":
                    // 检查文件
                    CheckFiles();
                    // 更新日志显示
                    UpdateLog();
                    // 启动日志更新定时器并停止 Nginx 状态信息更新定时器
                    _logUpdateTimer.Start();
                    _NginxStUpdateTimer.Stop();
                    break;
                // 如果选中项的标题是"主页"
                case "主页":
                    // 更新 Nginx 的状态信息
                    UpdateNginxST();
                    // 停止日志更新定时器并启动 Nginx 状态信息更新定时器
                    _NginxStUpdateTimer.Start();
                    _logUpdateTimer.Stop();
                    break;
                // 如果选中项的标题不是上述两者之一
                default:
                    // 停止所有定时器
                    _logUpdateTimer.Stop();
                    _NginxStUpdateTimer.Stop();
                    // 显示当前 Pixiv-Nginx 版本的提交日期
                    VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + Properties.Settings.Default.CurrentVersionCommitDate;
                    break;
            }
        }
    }
}
