using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static Pixiv_Nginx_GUI.PublicHelper;
using static Pixiv_Nginx_GUI.LogHelper;

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

        // 新的版本号，完成安装时写入配置文件
        private string NewVersion;

        // 从配置文件读取的程序信息
        string CurrentVersionCommitDate;

        bool IsFirst;

        string GUIVersion;

        // 窗口构造函数
        public MainWindow()
        {
            // 读取日志开关
            OutputLog = StringBoolConverter.StringToBool(ConfigINI.INIRead("日志开关", "OutputLog", INIPath));

            if (OutputLog && !Directory.Exists(GUILogDirectory))
            {
                // 创建目录
                Directory.CreateDirectory(GUILogDirectory);
            }

            WriteLog("进入MainWindow()", LogLevel.Debug);

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
            // 窗口可拖动化
            this.TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            WriteLog("完成MainWindow()", LogLevel.Debug);
        }

        // 日志更新计时器触发事件
        private void LogUpdateTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("进入LogUpdateTimer_Tick(object sender, EventArgs e)", LogLevel.Debug);

            // 更新日志
            UpdateLog();

            WriteLog("完成LogUpdateTimer_Tick(object sender, EventArgs e)", LogLevel.Debug);
        }

        // Nginx 状态更新计时器触发事件
        private void NginxStUpdateTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("进入NginxStUpdateTimer_Tick(object sender, EventArgs e)", LogLevel.Debug);

            // 更新 Nginx 状态
            UpdateNginxST();

            WriteLog("完成NginxStUpdateTimer_Tick(object sender, EventArgs e)", LogLevel.Debug);
        }

        // 更新 Nginx 状态的方法
        public void UpdateNginxST()
        {
            WriteLog("进入UpdateNginxST()", LogLevel.Debug);

            // 使用 Process.GetProcessesByName 方法获取所有名为 "nginx" 的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps = Process.GetProcessesByName("nginx");
            // 检查获取到的进程数组长度是否大于 0
            // 如果大于 0，说明 Nginx 正在运行

            WriteLog($"获取到的进程数组长度为 {ps.Length}", LogLevel.Debug);

            if (ps.Length > 0)
            {
                // 更新 Nginx 状态文本为 "当前 Nginx 状态：运行中"
                NginxST.Text = "当前 Nginx 状态：运行中";
                // 将 Nginx 状态文本的前景色设置为森林绿
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

            WriteLog("完成UpdateNginxST()", LogLevel.Debug);
        }

        // 用于检查重要目录是否存在以及日志文件、配置文件是否创建的方法
        public bool CheckFiles()
        {
            WriteLog("进入CheckFiles()", LogLevel.Debug);

            // 确保重要目录存在
            EnsureDirectoryExists(dataDirectory);
            EnsureDirectoryExists(NginxDirectory);
            EnsureDirectoryExists(TempDirectory);
            EnsureDirectoryExists(nginxLogPath);
            // 遍历日志文件路径数组
            foreach (var filePath in LogfilePaths)
            {
                // 如果日志文件不存在
                if (!File.Exists(filePath))
                {
                    WriteLog($"日志文件{filePath}不存在，创建。", LogLevel.Warning);

                    // 创建日志文件（如果文件已存在，File.Create 会覆盖原文件，但这里我们更关心的是创建不存在的文件）
                    // 注意：File.Create 打开文件后需要关闭，但这里仅用它来创建文件，因此使用 using 语句或 File.CreateText 会更好
                    // 更好的做法是使用 File.CreateText(filePath).Close(); 或 using (File.Create(filePath)) {}
                    File.CreateText(filePath).Close();
                }
                // 如果文件已存在，则不执行任何操作

                WriteLog($"日志文件{filePath}存在。", LogLevel.Info);
            }
            // 如果配置文件不存在，则创建配置文件
            if (!File.Exists(INIPath))
            {
                WriteLog($"配置文件{INIPath}不存在，创建。", LogLevel.Warning);

                ConfigINI.INIWrite("程序信息", "CurrentVersionCommitDate", "", INIPath);
                ConfigINI.INIWrite("程序信息", "IsFirst", "true", INIPath);
                ConfigINI.INIWrite("程序信息", "GUIVersion", PresetGUIVersion, INIPath);
                ConfigINI.INIWrite("日志开关", "OutputLog", "false", INIPath);
            }
            // 释放证书文件待用
            if (!File.Exists(CERFile))
            {
                WriteLog($"文件{CERFile}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.ca_cer, CERFile);
            }
            if (!File.Exists(CRTFile))
            {
                WriteLog($"文件{CRTFile}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.pixiv_net_crt, CRTFile);
            }
            if (!File.Exists(KeyFile))
            {
                WriteLog($"文件{KeyFile}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.pixiv_net_key, KeyFile);
            }
            // 遍历重要文件路径数组
            foreach (var filePath in ImportantfilePaths)
            {
                // 如果文件不存在
                if (!File.Exists(filePath))
                {
                    WriteLog($"重要文件{filePath}不存在，跳出检查循环。", LogLevel.Error);

                    // 跳出循环，不再继续检查其他文件

                    WriteLog("完成CheckFiles()，返回false", LogLevel.Debug);

                    return false;
                }

                WriteLog($"重要文件{filePath}存在。", LogLevel.Info);
            }

            WriteLog("完成CheckFiles()，返回true", LogLevel.Debug);

            return true;
        }

        // 用于更新日志显示的方法
        public void UpdateLog()
        {
            WriteLog("进入UpdateLog()", LogLevel.Debug);

            // 检查下拉框中是否有选中的项，并将其转换为 ComboBoxItem 类型
            if (LogCombo.SelectedItem is ComboBoxItem selectedItem)
            {
                try
                {
                    // 获取选中项的文本内容
                    string selectedText = selectedItem.Content.ToString();

                    WriteLog($"获取到选中项文本内容{selectedText}，下面开始尝试读取日志文件。", LogLevel.Info);

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
                catch (Exception ex)
                {
                    // 如果在操作过程中遇到异常，则显示错误消息框

                    WriteLog($"遇到异常{ex}", LogLevel.Error);

                    HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // 更新清理日志按钮的内容，显示所有日志文件的总大小（以MB为单位）
            DelLogBtn.Content = $"清理所有日志({GetTotalFileSizeInMB(LogfilePaths)}MB)";

            WriteLog("完成UpdateLog()", LogLevel.Debug);
        }

        // 更新程序信息
        public void UpdateInfo()
        {
            WriteLog("进入UpdateInfo()", LogLevel.Debug);

            // 读取程序信息
            CurrentVersionCommitDate = ConfigINI.INIRead("程序信息", "CurrentVersionCommitDate", INIPath);
            IsFirst = StringBoolConverter.StringToBool(ConfigINI.INIRead("程序信息", "IsFirst", INIPath));
            GUIVersion = ConfigINI.INIRead("程序信息", "GUIVersion", INIPath);

            WriteLog($"从配置文件读取到CurrentVersionCommitDate：{CurrentVersionCommitDate}", LogLevel.Info);
            WriteLog($"从配置文件读取到IsFirst：{IsFirst}", LogLevel.Info);
            WriteLog($"从配置文件读取到GUIVersion：{GUIVersion}", LogLevel.Info);

            WriteLog("完成UpdateInfo()", LogLevel.Debug);
        }

        // 自动配置按钮的点击事件
        private async void AutoConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入AutoConfigBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 异步调用该方法，杀死所有名为"nginx"的进程
            await KillNginx();
            try
            {
                // 尝试将 data\pixiv-nginx 重命名为 data\pixiv-nginx.old
                RenameDirectory(NginxDirectory, OldNginxDirectory);
            }catch (Exception ex)
            {
                // 如果在重命名目录时发生异常，则显示一个错误消息框
                WriteLog($"尝试将pixiv-nginx 重命名为pixiv-nginx.old时遇到异常{ex}", LogLevel.Error);

                HandyControl.Controls.MessageBox.Show($"{ex.Message}\r\n请重试！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 创建一个 FirstUse 窗口的实例
            FirstUse firstUse = new FirstUse();
            // 显示 FirstUse 窗口
            firstUse.Show();
            // 隐藏当前窗口
            this.Hide();

            WriteLog("完成AutoConfigBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 窗口加载完成事件
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WriteLog("进入Window_Loaded(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 检查文件，当配置文件不存在时可以创建
            CheckFiles();
            // 读取程序信息
            UpdateInfo();
            // 检查应用程序的设置，判断是否完成部署
            if (IsFirst)
            {
                WriteLog("软件尚未完成部署，进入自动部署流程。", LogLevel.Info);

                // 确保 api.github.com 可以正常访问
                EnsureGithubAPI();
                Flushdns();
                // 如果是首次运行，则模拟点击“自动配置”按钮，这将触发 AutoConfigBtn_Click 方法中的逻辑
                // 注意：这里直接调用事件处理方法可能不是最佳实践，因为它绕过了事件系统
                // 更好的做法是将 AutoConfigBtn_Click 中的逻辑封装到一个单独的方法中，并在这里调用该方法
                AutoConfigBtn_Click(this, e);
            }
            else
            {
                // 如果不是首次运行，则检查文件时关注是否有重要文件缺失
                if (!CheckFiles())
                {
                    WriteLog("检测到重要文件缺失！", LogLevel.Error);

                    HandyControl.Controls.MessageBox.Show("检测到重要文件缺失，请通过重新自动部署解决该问题！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            try
            {
                // 异步获取 GitHub 的最新发布信息
                string LatestReleaseInfo = await GetAsync("https://api.github.com/repos/racpast/Pixiv-Nginx-GUI/releases/latest");

                WriteLog($"获取到GitHub的最新发布信息LatestReleaseInfo：{LatestReleaseInfo}", LogLevel.Info);

                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(LatestReleaseInfo);
                // 从解析后的JSON中获取最后一次发布的信息
                string LatestReleaseTag = repodata["tag_name"].ToString();
                string LatestReleasePublishedDt = repodata["published_at"].ToString();

                WriteLog($"提取到最后一次发布的信息LatestReleaseTag：{LatestReleaseTag}", LogLevel.Info);
                WriteLog($"提取到最后一次发布的信息LatestReleasePublishedDt：{LatestReleasePublishedDt}", LogLevel.Info);

                // 比较当前安装的版本与最后一次发布的版本
                if (LatestReleaseTag.ToUpper() != GUIVersion)
                {
                    WriteLog("Pixiv-Nginx-GUI有新版本可以使用。", LogLevel.Info);

                    // 如果有新版本，则弹出提示框
                    HandyControl.Controls.MessageBox.Show($"Pixiv-Nginx-GUI 有新版本可用，请及时获取最新版本！\r\n版本号：{LatestReleaseTag.ToUpper()}\r\n发布时间(GMT)：{LatestReleasePublishedDt}", "主程序更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            // 更准确的报错信息（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
            catch (OperationCanceledException)
            {
                WriteLog("获取信息请求超时，遇到OperationCanceledException。", LogLevel.Error);

                // 如果获取信息请求超时，显示提示信息
                HandyControl.Controls.MessageBox.Show("从 api.github.com 获取信息超时！\r\n请检查是否可以访问到 api.github.com 或反馈！", "错误", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}", LogLevel.Error);

                // 捕获异常并弹出错误提示框
                HandyControl.Controls.MessageBox.Show($"检查更新时遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 在主窗口标题显示版本
            WindowTitle.Text = "Pixiv-Nginx 部署工具 " + GUIVersion;
            // 更新 Nginx 的状态信息
            UpdateNginxST();
            // 为 TabControl 的 SelectionChanged 事件添加事件处理程序，用户切换选项卡时，将调用 TabControl_SelectionChanged 方法
            // 在这里才添加的原因是如果在xaml中添加，窗口加载完成时就会触发 TabControl_SelectionChanged ，而所选页面为主页时会 UpdateNginxST() ，此时 NginxST 为 Null
            tabcontrol.SelectionChanged += TabControl_SelectionChanged;
            // 启动用于定期更新 Nginx 的状态信息的定时器，
            _NginxStUpdateTimer.Start();
            // 显示当前 Pixiv-Nginx 版本的提交日期
            if (CurrentVersionCommitDate == "")
            {
                WriteLog($"检测到CurrentVersionCommitDate为空。", LogLevel.Warning);

                VersionInfo.Text = "请先完成自动部署流程！";
                CheckUpdateBtn.IsEnabled = false;
                ChooseUpdateBtn.IsEnabled = false;
                ChooseUpdateBtn.IsEnabled = false;
            }
            else
            {
                WriteLog($"检测到CurrentVersionCommitDate为{CurrentVersionCommitDate}", LogLevel.Info);

                CheckUpdateBtn.IsEnabled = true;
                ChooseUpdateBtn.IsEnabled = true;
                ChooseUpdateBtn.IsEnabled = true;
                VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + CurrentVersionCommitDate;
            }

            WriteLog("完成Window_Loaded(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 刷新状态按钮的点击事件
        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入RefreshBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 更新 Nginx 的状态信息
            UpdateNginxST();

            WriteLog("完成RefreshBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 启动按钮的点击事件
        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入StartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

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
                WriteLog($"尝试启动进程时遇到异常：{ex}", LogLevel.Error);

                // 如果启动进程时发生异常，则显示一个错误消息框
                HandyControl.Controls.MessageBox.Show($"无法启动进程: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 更新 Nginx 的状态信息
            UpdateNginxST();

            WriteLog("完成StartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 停止按钮的点击事件
        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入StopBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 异步调用该方法，用于杀死所有名为"nginx"的进程
            await KillNginx();
            // 更新Nginx的状态信息
            UpdateNginxST();

            WriteLog("完成StopBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 检查配置按钮的点击事件
        private void CheckConfBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CheckConfBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 构建要执行的命令字符串，该命令用于检查配置文件，并在执行后暂停，然后退出
            string command = $"nginx -t -c \"{nginxConfigFile}\" & pause & exit";
            RunCMD(command,NginxDirectory);

            WriteLog("完成CheckConfBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 重载配置按钮的点击事件
        private void ReloadConfBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ReloadConfBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 构建要执行的命令字符串，该命令用于重载配置文件，并在执行后暂停，然后退出
            string command = "nginx -s reload & pause & exit";
            RunCMD(command,NginxDirectory);

            WriteLog("完成ReloadConfBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 查看版本按钮的点击事件
        private void VersionBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入VersionBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 构建要执行的命令字符串，该命令用于显示 Nginx 版本，并在执行后暂停，然后退出
            string command = "nginx -V & pause & exit";
            RunCMD(command ,NginxDirectory);

            WriteLog("完成VersionBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 设置开机启动按钮的点击事件
        private void SetStartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入SetStartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

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
                        WriteLog("计划任务StartNginx已经存在，尝试移除。", LogLevel.Warning);

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
                WriteLog($"尝试设置开机启动时遇到异常：{ex}", LogLevel.Error);

                // 捕获异常并显示错误信息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成SetStartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 退出工具按钮的点击事件
        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ExitBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 退出程序
            Environment.Exit(0);

            // 不必要的日志记录
            WriteLog("完成ExitBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 停止开机启动按钮的点击事件
        private void DelStartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入DelStartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

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
                        WriteLog("计划任务StartNginx已经存在，尝试移除。", LogLevel.Info);

                        ts.RootFolder.DeleteTask(taskName);
                    }
                }
                // 显示提示信息，表示 Nginx 已成功停止开机启动
                HandyControl.Controls.MessageBox.Show("成功停止 Nginx 的开机启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试停止开机启动时遇到异常：{ex}", LogLevel.Error);

                // 捕获异常并显示错误信息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成DelStartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 检查更新按钮的点击事件，用于检查Pixiv-Nginx是否有新版本可用
        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CheckUpdateBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 修改按钮内容为“检查更新中...”以提示用户正在进行检查
            CheckUpdateBtn.Content = "检查更新中...";
            try
            {
                // 异步获取 GitHub 仓库的最新提交信息
                string RepoInfo = await GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");

                WriteLog($"获取到GitHub仓库的最新提交信息RepoInfo：{RepoInfo}", LogLevel.Info);

                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(RepoInfo);
                // 从解析后的JSON中获取最后一次提交的URL
                string CommitInfoURL = repodata["object"]["url"].ToString();

                WriteLog($"获取到最后一次提交信息的URL CommitInfoURL：{CommitInfoURL}", LogLevel.Info);

                // 在更新日志文本框中添加获取到的Commit信息URL
                UpdateLogTb.Text += $"获取到最后一次Commit信息URL：{CommitInfoURL}\r\n";
                // 异步获取最后一次提交的详细信息
                string CommitInfo = await GetAsync(CommitInfoURL);

                WriteLog($"获取到最后一次提交的详细信息CommitInfo：{CommitInfo}", LogLevel.Info);

                // 将返回的JSON字符串解析为JObject
                JObject commitdata = JObject.Parse(CommitInfo);
                // 从解析后的JSON中获取提交者的日期、SHA值以及提交信息
                // 注意：dateToken 所返回的格式受系统设置影响，这里需要转换为标准格式（https://github.com/racpast/Pixiv-Nginx-GUI/issues/1）
                JToken dateToken = commitdata["committer"]["date"];
                DateTime dateTime = dateToken.ToObject<DateTime>(); // 或者使用 (DateTime)dateToken 如果确定它是 DateTime 类型
                string LCommitDT = dateTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
                string LCommitSHA = commitdata["sha"].ToString();
                string LCommit = commitdata["message"].ToString();

                WriteLog($"提取到最后一次提交的详细信息LCommitDT：{LCommitDT}", LogLevel.Info);
                WriteLog($"提取到最后一次提交的详细信息LCommitSHA：{LCommitSHA}", LogLevel.Info);
                WriteLog($"提取到最后一次提交的详细信息LCommit：{LCommit}", LogLevel.Info);

                // 在更新日志文本框中添加获取到的Commit详细信息
                UpdateLogTb.Text += $"获取到最后一次Commit信息：\r\nCommit SHA：{LCommitSHA}\r\n时间：{LCommitDT}\r\n内容：{LCommit}\r\n";
                // 比较当前安装的版本与 GitHub 上的最新版本
                if (DateTime.Parse(LCommitDT) != DateTime.Parse(CurrentVersionCommitDate))
                {
                    WriteLog($"Pixiv-Nginx有新版本可用。", LogLevel.Info);

                    // 如果有新版本，则弹出提示框并启用更新按钮
                    HandyControl.Controls.MessageBox.Show($"Pixiv-Nginx 有新版本可用！\r\nCommit SHA：{LCommitSHA}\r\n时间：{LCommitDT}\r\n内容：{LCommit}", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateBtn.IsEnabled = true;
                    
                    WriteLog($"NewVersion被设置为{LCommitDT}", LogLevel.Debug);

                    // 更新新版本信息
                    NewVersion = LCommitDT;
                    // 在更新日志文本框中添加当前版本与新版本的信息
                    UpdateLogTb.Text += $"当前版本Commit时间：{CurrentVersionCommitDate}，Pixiv-Nginx 有新版本可用。\r\n";
                }
                else
                {
                    WriteLog($"Pixiv-Nginx已经是最新版本。", LogLevel.Info);

                    // 如果没有新版本，则弹出提示框告知用户已是最新版本
                    HandyControl.Controls.MessageBox.Show("Pixiv-Nginx 目前已是最新版本！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    // 在更新日志文本框中添加已是最新版本的信息
                    UpdateLogTb.Text += "当前已是最新版本！\r\n";
                }
            }
            // 更准确的报错信息（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
            catch (OperationCanceledException)
            {
                WriteLog("获取信息请求超时，遇到OperationCanceledException。", LogLevel.Error);

                // 如果获取信息请求超时，显示提示信息
                HandyControl.Controls.MessageBox.Show("从 api.github.com 获取信息超时！\r\n若多次尝试后仍然报错请检查是否可以访问到 api.github.com 或反馈！", "错误", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}", LogLevel.Error);

                // 捕获异常并弹出错误提示框
                HandyControl.Controls.MessageBox.Show($"检查更新时遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 检查完成后，将按钮内容改回“检查更新”
                CheckUpdateBtn.Content = "检查更新";
            }

            WriteLog("完成CheckUpdateBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 更新至最新版本按钮的点击事件
        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入UpdateBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 禁用有关按钮
            UpdateBtn.IsEnabled = false;
            CheckUpdateBtn.IsEnabled = false;
            ChooseUpdateBtn.IsEnabled = false;
            string fileUrl = "https://github.com/mashirozx/Pixiv-Nginx/archive/refs/heads/main.zip";
            string ProxyUrl;
            UpdateBtn.Content = "寻找最优代理...";
            UpdateLogTb.Text += "开始寻找最优代理...";
            string fastestproxy = await FindFastestProxy(proxies, fileUrl);
            if (fastestproxy == "Mirror")
            {
                WriteLog("fastestproxy为Mirror，最优代理为镜像站。", LogLevel.Info);

                ProxyUrl = "https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip";
                UpdateLogTb.Text += "最优代理服务器是: " + ProxyUrl;
            }
            else
            {
                WriteLog($"fastestproxy（最优代理）为{fastestproxy}。", LogLevel.Info);

                ProxyUrl = "https://" + fastestproxy + "/https://github.com/mashirozx/Pixiv-Nginx/archive/refs/heads/main.zip";
                UpdateLogTb.Text += "最优代理服务器是: " + fastestproxy;
            }
            // 定义 main.zip 的保存路径
            string destinationPath = Path.Combine(TempDirectory, "Pixiv-Nginx-main.zip");
            // 创建一个取消令牌源，用于控制下载过程的中断
            CancellationTokenSource cts = new CancellationTokenSource();
            // 设置进度更新的超时时间
            TimeSpan progressTimeout = TimeSpan.FromSeconds(10);
            // 更新按钮文本
            UpdateBtn.Content = "下载中...";
            try
            {
                WriteLog($"尝试从{ProxyUrl}下载文件到{destinationPath}。", LogLevel.Info);

                // 在更新日志文本框中添加信息
                UpdateLogTb.Text += $"从{ProxyUrl}下载文件到{destinationPath}\r\n";
                // 存储下载前更新日志文本框的内容以便持续更新进度
                string TextBfDownload = UpdateLogTb.Text;
                // 异步下载文件，并更新下载进度
                await DownloadFileAsync(ProxyUrl,
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

                WriteLog($"文件下载完成。", LogLevel.Info);

                // 更新UI，表示文件下载完成并开始解压文件
                UpdateLogTb.Text += $"文件下载完成！\r\n";
                UpdateLogTb.Text += $"结束 Nginx 进程...\r\n";
                // 异步调用该方法，用于杀死所有名为"nginx"的进程
                await KillNginx();
                UpdateLogTb.Text += $"清理旧版本目录...\r\n";

                WriteLog($"清理旧版本目录{NginxDirectory}。", LogLevel.Info);

                Directory.Delete(NginxDirectory, true);
                UpdateBtn.Content = $"解压中...";
                UpdateLogTb.Text += $"解压新版本压缩包...\r\n";

                WriteLog($"开始从文件{destinationPath}解压到{dataDirectory}。", LogLevel.Info);

                // 异步解压文件
                await System.Threading.Tasks.Task.Run(() => UnZip(destinationPath, dataDirectory, false));

                WriteLog($"文件解压到完成。", LogLevel.Info);

                // 更新UI，表示文件解压完成
                UpdateBtn.Content = $"解压完成";
                UpdateLogTb.Text += $"解压新版本压缩包完成！\r\n";

                // 镜像站与加速代理所下载的压缩包差异的处理
                if (fastestproxy != "Mirror")
                {
                    WriteLog("非镜像站下载的源码压缩包，改名处理。", LogLevel.Info);

                    RenameDirectory(Path.Combine(dataDirectory, "Pixiv-Nginx-main"), NginxDirectory);
                }
                // 清理非必要文件
                CleanUnnecessary();
                // 替换所有 CA
                WriteLog($"替换{OldCERFile}为{CERFile}。", LogLevel.Info);

                File.Copy(CERFile, OldCERFile, overwrite: true);

                WriteLog($"替换{OldCRTFile}为{CRTFile}。", LogLevel.Info);

                File.Copy(CRTFile, OldCRTFile, overwrite: true);

                WriteLog($"替换{OldKeyFile}为{KeyFile}。", LogLevel.Info);

                File.Copy(KeyFile, OldKeyFile, overwrite: true);
                try
                {
                    // 检测 hosts 文件是否存在
                    if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts"))
                    {
                        WriteLog("C:\\Windows\\System32\\drivers\\etc\\hosts存在，备份文件至C:\\Windows\\System32\\drivers\\etc\\hosts.bak。", LogLevel.Info);

                        // 存在则备份 hosts 文件
                        File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts", "C:\\Windows\\System32\\drivers\\etc\\hosts.bak", true);
                        // 排除无关条目、修改有关条目并添加不存在条目
                        WriteHosts.AppendHosts("C:\\Windows\\System32\\drivers\\etc\\hosts", hostsFile);
                    }
                    else
                    {
                        WriteLog($"C:\\Windows\\System32\\drivers\\etc\\hosts不存在，直接复制{hostsFile}。", LogLevel.Warning);

                        // 不存在则直接复制 hosts 文件
                        File.Copy(hostsFile, "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                    }
                    HostsAdditionalModification();
                    // 刷新 DNS 缓存
                    string command = "ipconfig /flushdns & pause & exit";
                    RunCMD(command);
                }
                catch (Exception ex)
                {
                    WriteLog($"操作hosts时出错：{ex.Message}", LogLevel.Error);

                    // 如果在操作 hosts 过程中发生异常，则显示错误消息框
                    HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // 将新的版本号写入应用程序设置并保存

                WriteLog($"NewVersion：{NewVersion}被写入配置文件。", LogLevel.Debug);

                ConfigINI.INIWrite("程序信息", "CurrentVersionCommitDate", NewVersion, INIPath);
                UpdateInfo();
                VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + CurrentVersionCommitDate;
                // 显示当前 Pixiv-Nginx 版本的提交日期
                VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + CurrentVersionCommitDate;

                WriteLog($"更新完成。", LogLevel.Info);

                // 弹出窗口提示更新完成
                HandyControl.Controls.MessageBox.Show("更新完成！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                WriteLog("文件下载超时，遇到OperationCanceledException。", LogLevel.Error);

                // 在更新日志文本框中添加下载超时信息
                UpdateLogTb.Text += $"文件下载超时，请重试！\r\n";
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}", LogLevel.Error);

                // 在更新日志文本框中添加更新失败信息
                UpdateLogTb.Text += $"更新失败：{ex.Message}\r\n";
            }
            finally
            {
                // 清理操作：删除下载的ZIP文件，释放取消令牌源，重置按钮状态
                if (File.Exists(destinationPath))
                {
                    WriteLog($"删除下载的文件{destinationPath}。", LogLevel.Info);

                    File.Delete(destinationPath);
                }
                CheckUpdateBtn.IsEnabled = true;
                ChooseUpdateBtn.IsEnabled = true;
                cts.Dispose();
                UpdateBtn.Content = "更新至最新版本";
            }

            WriteLog("完成UpdateBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 从本地文件安装按钮的点击事件
        private async void ChooseUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ChooseUpdateBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

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
                string RepoInfo = await GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");

                WriteLog($"获取到GitHub仓库的最新提交信息RepoInfo：{RepoInfo}", LogLevel.Info);

                JObject repodata = JObject.Parse(RepoInfo);
                string CommitInfoURL = repodata["object"]["url"].ToString();

                WriteLog($"获取到最后一次提交信息的URL CommitInfoURL：{CommitInfoURL}", LogLevel.Info);

                // 异步获取提交详细信息
                string CommitInfo = await GetAsync(CommitInfoURL);

                WriteLog($"获取到最后一次提交的详细信息CommitInfo：{CommitInfo}", LogLevel.Info);

                JObject commitdata = JObject.Parse(CommitInfo);
                // 获取提交的日期时间（GMT）
                // 注意：dateToken 所返回的格式受系统设置影响，这里需要转换为标准格式（https://github.com/racpast/Pixiv-Nginx-GUI/issues/1）
                JToken dateToken = commitdata["committer"]["date"];
                DateTime dateTime = dateToken.ToObject<DateTime>(); // 或者使用 (DateTime)dateToken 如果确定它是 DateTime 类型
                string LCommitDT = dateTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

                WriteLog($"提取到最后一次提交的详细信息LCommitDT：{LCommitDT}", LogLevel.Info);

                // 获取用户选择的ZIP文件路径
                string filePath = openFileDialog.FileName;

                WriteLog($"用户在选择文件对话框中选择了{filePath}。", LogLevel.Debug);

                // 创建一个输入框对话框，提示用户输入提交日期
                InputBox inputBox = new InputBox
                {
                    // 设置提示文本，提示用户输入并显示最新版本的提交日期
                    InitialText = $"从本地文件安装时，您需要为该版本指定 Commit 日期（GMT）。\r\n最新版本 Commit 日期：{DateTime.Parse(LCommitDT)}\r\n当前版本 Commit 日期：{DateTime.Parse(CurrentVersionCommitDate)}",
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
                        WriteLog($"用户在输入日期对话框中输入了{inputBox.InputText}，无效的日期时间！", LogLevel.Warning);

                        // 如果无效，显示错误提示
                        HandyControl.Controls.MessageBox.Show("您输入了无效的日期时间！", "输入", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }

                    WriteLog($"用户在输入日期对话框中指定为{inputBox.InputText}。", LogLevel.Debug);

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

                        WriteLog($"删除{NginxDirectory}。", LogLevel.Info);

                        Directory.Delete(NginxDirectory, true);
                        UpdateLogTb.Text += $"解压新版本压缩包...\r\n";
                        // 在后台线程中解压文件

                        WriteLog($"开始从文件{filePath}解压到{dataDirectory}。", LogLevel.Info);

                        await System.Threading.Tasks.Task.Run(() => UnZip(filePath, dataDirectory, false));

                        WriteLog($"文件解压完成。", LogLevel.Info);

                        // 更新解压状态
                        UpdateLogTb.Text += $"解压新版本压缩包完成！\r\n";

                        // 镜像站与加速代理所下载的压缩包差异的处理
                        if (Directory.Exists(Path.Combine(dataDirectory, "Pixiv-Nginx-main")))
                        {
                            WriteLog("非镜像站下载的源码压缩包，改名处理。", LogLevel.Info);

                            RenameDirectory(Path.Combine(dataDirectory, "Pixiv-Nginx-main"), NginxDirectory);
                        }
                        // 清理非必要文件
                        CleanUnnecessary();
                        // 替换所有 CA
                        WriteLog($"替换{OldCERFile}为{CERFile}。", LogLevel.Info);

                        File.Copy(CERFile, OldCERFile, overwrite: true);

                        WriteLog($"替换{OldCRTFile}为{CRTFile}。", LogLevel.Info);

                        File.Copy(CRTFile, OldCRTFile, overwrite: true);

                        WriteLog($"替换{OldKeyFile}为{KeyFile}。", LogLevel.Info);

                        File.Copy(KeyFile, OldKeyFile, overwrite: true);
                        try
                        {
                            // 检测 hosts 文件是否存在
                            if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts"))
                            {
                                WriteLog("C:\\Windows\\System32\\drivers\\etc\\hosts存在，备份文件至C:\\Windows\\System32\\drivers\\etc\\hosts.bak。", LogLevel.Info);

                                // 存在则备份 hosts 文件
                                File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts", "C:\\Windows\\System32\\drivers\\etc\\hosts.bak", true);
                                // 排除无关条目、修改有关条目并添加不存在条目
                                WriteHosts.AppendHosts("C:\\Windows\\System32\\drivers\\etc\\hosts", hostsFile);
                            }
                            else
                            {
                                WriteLog($"C:\\Windows\\System32\\drivers\\etc\\hosts不存在，直接复制{hostsFile}。", LogLevel.Warning);

                                // 不存在则直接复制 hosts 文件
                                File.Copy(hostsFile, "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                            }
                            HostsAdditionalModification();
                            // 刷新 DNS 缓存
                            string command = "ipconfig /flushdns & pause & exit";
                            RunCMD(command);
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"操作hosts时出错：{ex.Message}", LogLevel.Error);

                            // 如果在操作 hosts 过程中发生异常，则显示错误消息框
                            HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        // 将记录的新版本写入配置文件

                        WriteLog($"{DateTime.Parse(inputBox.InputText)}被写入配置文件。", LogLevel.Debug);

                        ConfigINI.INIWrite("程序信息", "CurrentVersionCommitDate", DateTime.Parse(inputBox.InputText).ToString(), INIPath);
                        // 显示当前 Pixiv-Nginx 版本的提交日期
                        VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + CurrentVersionCommitDate;

                        WriteLog($"从本地更新完成。", LogLevel.Info);

                        // 弹出窗口提示更新完成
                        HandyControl.Controls.MessageBox.Show("从本地更新完成！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"遇到异常：{ex}", LogLevel.Error);

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

            WriteLog("完成ChooseUpdateBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        //清理日志按钮的点击事件
        private async void DelLogBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入DelLogBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 异步调用该方法，用于杀死所有名为"nginx"的进程
            await KillNginx();
            // 更新 Nginx 的状态信息
            UpdateNginxST();
            // 停止定期更新日志的计时器
            _logUpdateTimer.Stop();
            // 删除所有日志文件
            foreach(string logpath in LogfilePaths)
            {
                WriteLog($"删除日志文件{logpath}。", LogLevel.Info);

                File.Delete(logpath);
            }
            // 重新建立日志文件
            CheckFiles();

            WriteLog($"日志文件清理完成。", LogLevel.Info);

            // 弹出窗口提示日志清理完成
            HandyControl.Controls.MessageBox.Show("日志清理完成！", "清理日志", MessageBoxButton.OK, MessageBoxImage.Information);
            // 更新清理日志按钮的内容，显示所有日志文件的总大小（以MB为单位）
            DelLogBtn.Content = $"清理所有日志({GetTotalFileSizeInMB(LogfilePaths)}MB)";
            // 清空日志文本框
            LogTb.Text = "";
            // 重新启用定期更新日志的计时器
            _logUpdateTimer.Start();

            WriteLog("完成DelLogBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 日志文件选择下拉框选项发生改变时的事件
        private void LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WriteLog("进入LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)", LogLevel.Debug);

            // 更新日志
            UpdateLog();

            WriteLog("完成LogCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)", LogLevel.Debug);
        }

        // 日志文本框内容发生改变时的事件
        private void UpdateLogTb_TextChanged(object sender, TextChangedEventArgs e)
        {
            WriteLog("进入UpdateLogTb_TextChanged(object sender, TextChangedEventArgs e)", LogLevel.Debug);

            // 将文本框滚动到最底部
            UpdateLogTb.ScrollToEnd();

            WriteLog("完成UpdateLogTb_TextChanged(object sender, TextChangedEventArgs e)", LogLevel.Debug);
        }

        // 选项卡选项发生改变时的事件
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WriteLog("进入TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)", LogLevel.Debug);

            // 使用模式匹配和null条件运算符来检查 sender 是否为 TabControl 的实例，如果不是，则直接返回
            if (sender is not TabControl tabControl) return;
            // 尝试将 TabControl 的选中项转换为 TabItem
            var selectedItem = tabControl.SelectedItem as TabItem;

            WriteLog($"获取到选项卡标题：{selectedItem?.Header.ToString()}。", LogLevel.Debug);

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
                    UpdateInfo();
                    if (CurrentVersionCommitDate == "")
                    {
                        VersionInfo.Text = "请先完成自动部署流程！";
                        CheckUpdateBtn.IsEnabled = false;
                        ChooseUpdateBtn.IsEnabled = false;
                        ChooseUpdateBtn.IsEnabled = false;
                    }
                    else
                    {
                        CheckUpdateBtn.IsEnabled = true;
                        ChooseUpdateBtn.IsEnabled = true;
                        ChooseUpdateBtn.IsEnabled = true;
                        VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + CurrentVersionCommitDate;
                    }
                    break;
            }

            WriteLog("完成TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)", LogLevel.Debug);
        }

        // 重新加载 hosts 至系统按钮的点击事件
        private async void ReloadHostsBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ReloadHostsBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
            if (File.Exists(hostsFile))
            {
                await KillNginx();
                try
                {
                    // 检测 hosts 文件是否存在
                    if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts"))
                    {
                        WriteLog("C:\\Windows\\System32\\drivers\\etc\\hosts存在，备份文件至C:\\Windows\\System32\\drivers\\etc\\hosts.bak。", LogLevel.Info);

                        // 存在则备份 hosts 文件
                        File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts", "C:\\Windows\\System32\\drivers\\etc\\hosts.bak", true);
                        // 排除无关条目、修改有关条目并添加不存在条目
                        WriteHosts.AppendHosts("C:\\Windows\\System32\\drivers\\etc\\hosts", hostsFile);
                    }
                    else
                    {
                        WriteLog($"C:\\Windows\\System32\\drivers\\etc\\hosts不存在，直接复制{hostsFile}。", LogLevel.Warning);

                        // 不存在则直接复制 hosts 文件
                        File.Copy(hostsFile, "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                    }
                    HostsAdditionalModification();
                    // 刷新 DNS 缓存
                    Flushdns();
                }
                catch (Exception ex)
                {
                    WriteLog($"操作hosts时出错：{ex.Message}", LogLevel.Error);

                    // 如果在操作 hosts 过程中发生异常，则显示错误消息框
                    HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }
            else
            {
                WriteLog($"试图重新加载 hosts 时，文件{hostsFile}不存在。", LogLevel.Error);

                HandyControl.Controls.MessageBox.Show("可供替换的 hosts 文件不存在，请重新部署！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WriteLog("完成ReloadHostsBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }
    }
}
