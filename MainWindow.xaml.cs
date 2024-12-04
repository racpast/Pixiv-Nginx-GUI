using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static Pixiv_Nginx_GUI.PublicHelper;

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
        FilesINI ConfigINI = new FilesINI();
        // 从配置文件读取的程序信息
        string CurrentVersionCommitDate;
        bool IsFirst;
        string GUIVersion;

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

        // 用于检查重要目录是否存在以及日志文件、配置文件是否创建的方法
        public bool CheckFiles()
        {
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
                    // 创建日志文件（如果文件已存在，File.Create 会覆盖原文件，但这里我们更关心的是创建不存在的文件）
                    // 注意：File.Create 打开文件后需要关闭，但这里仅用它来创建文件，因此使用 using 语句或 File.CreateText 会更好
                    // 更好的做法是使用 File.CreateText(filePath).Close(); 或 using (File.Create(filePath)) {}
                    File.CreateText(filePath).Close();
                }
                // 如果文件已存在，则不执行任何操作
            }
            // 如果配置文件不存在，则创建配置文件
            if (!File.Exists(INIPath))
            {
                ConfigINI.INIWrite("程序信息", "CurrentVersionCommitDate", "", INIPath);
                ConfigINI.INIWrite("程序信息", "IsFirst", "true", INIPath);
                ConfigINI.INIWrite("程序信息", "GUIVersion", "V1.3", INIPath);
            }
            // 遍历重要文件路径数组
            foreach (var filePath in ImportantfilePaths)
            {
                // 如果文件不存在
                if (!File.Exists(filePath))
                {
                    // 跳出循环，不再继续检查其他文件
                    return false;
                }
            }
            return true;
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
            DelLogBtn.Content = $"清理所有日志({GetTotalFileSizeInMB(LogfilePaths)}MB)";
        }

        // 更新程序信息
        public void UpdateInfo()
        {
            // 读取程序信息
            CurrentVersionCommitDate = ConfigINI.INIRead("程序信息", "CurrentVersionCommitDate", INIPath);
            IsFirst = StringBoolConverter.StringToBool(ConfigINI.INIRead("程序信息", "IsFirst", INIPath));
            GUIVersion = ConfigINI.INIRead("程序信息", "GUIVersion", INIPath);
        }

        // 自动配置按钮的点击事件
        private async void AutoConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            // 异步调用该方法，杀死所有名为"nginx"的进程
            await KillNginx();
            try
            {
                // 尝试将 data\pixiv-nginx 重命名为 data\pixiv-nginx.old
                RenameDirectory(NginxDirectory, OldNginxDirectory);
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
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 检查文件，当配置文件不存在时可以创建
            CheckFiles();
            // 读取程序信息
            UpdateInfo();
            try
            {
                // 异步获取 GitHub 的最新发布信息
                string LatestReleaseInfo = await GetAsync("https://api.github.com/repos/racpast/Pixiv-Nginx-GUI/releases/latest");
                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(LatestReleaseInfo);
                // 从解析后的JSON中获取最后一次发布的信息
                string LatestReleaseTag = repodata["tag_name"].ToString();
                string LatestReleasePublishedDt = repodata["published_at"].ToString();
                // 比较当前安装的版本与最后一次发布的版本
                if (LatestReleaseTag.ToUpper() != GUIVersion)
                {
                    // 如果有新版本，则弹出提示框
                    HandyControl.Controls.MessageBox.Show($"Pixiv-Nginx-GUI 有新版本可用，请及时获取最新版本！\r\n版本号：{LatestReleaseTag.ToUpper()}\r\n发布时间(GMT)：{LatestReleasePublishedDt}", "主程序更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // 捕获异常并弹出错误提示框
                HandyControl.Controls.MessageBox.Show($"检查更新时遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 检查应用程序的设置，判断是否为首次运行
            if (IsFirst)
            {
                // 如果是首次运行，则模拟点击“自动配置”按钮，这将触发 AutoConfigBtn_Click 方法中的逻辑
                // 注意：这里直接调用事件处理方法可能不是最佳实践，因为它绕过了事件系统
                // 更好的做法是将 AutoConfigBtn_Click 中的逻辑封装到一个单独的方法中，并在这里调用该方法
                AutoConfigBtn_Click(this, e);

                // 将配置中的IsFirst标记为false，表示不再是首次运行
                ConfigINI.INIWrite("程序信息", "IsFirst", "false", INIPath);
            }
            else
            {
                // 如果不是首次运行，则检查文件时关注是否有重要文件缺失
                if (!CheckFiles())
                {
                    HandyControl.Controls.MessageBox.Show("检测到重要文件缺失，请通过重新自动部署解决该问题！","错误",MessageBoxButton.OK,MessageBoxImage.Warning);
                }
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
            RunCMD(command,NginxDirectory);
        }

        // 重载配置按钮的点击事件
        private void ReloadConfBtn_Click(object sender, RoutedEventArgs e)
        {
            // 构建要执行的命令字符串，该命令用于重载配置文件，并在执行后暂停，然后退出
            string command = "nginx -s reload & pause & exit";
            RunCMD(command,NginxDirectory);
        }

        // 查看版本按钮的点击事件
        private void VersionBtn_Click(object sender, RoutedEventArgs e)
        {
            // 构建要执行的命令字符串，该命令用于显示 Nginx 版本，并在执行后暂停，然后退出
            string command = "nginx -V & pause & exit";
            RunCMD(command ,NginxDirectory);
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
                string RepoInfo = await GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");
                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(RepoInfo);
                // 从解析后的JSON中获取最后一次提交的URL
                string CommitInfoURL = repodata["object"]["url"].ToString();
                // 在更新日志文本框中添加获取到的Commit信息URL
                UpdateLogTb.Text += $"获取到最后一次Commit信息URL：{CommitInfoURL}\r\n";
                // 异步获取最后一次提交的详细信息
                string CommitInfo = await GetAsync(CommitInfoURL);
                // 将返回的JSON字符串解析为JObject
                JObject commitdata = JObject.Parse(CommitInfo);
                // 从解析后的JSON中获取提交者的日期、SHA值以及提交信息
                string LCommitDT = commitdata["committer"]["date"].ToString();
                string LCommitSHA = commitdata["sha"].ToString();
                string LCommit = commitdata["message"].ToString();
                // 在更新日志文本框中添加获取到的Commit详细信息
                UpdateLogTb.Text += $"获取到最后一次Commit信息：\r\nCommit SHA：{LCommitSHA}\r\n时间：{LCommitDT}\r\n内容：{LCommit}\r\n";
                // 比较当前安装的版本与 GitHub 上的最新版本
                if (DateTime.Parse(LCommitDT) != DateTime.Parse(CurrentVersionCommitDate))
                {
                    // 如果有新版本，则弹出提示框并启用更新按钮
                    HandyControl.Controls.MessageBox.Show($"Pixiv-Nginx 有新版本可用！\r\nCommit SHA：{LCommitSHA}\r\n时间：{LCommitDT}\r\n内容：{LCommit}", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateBtn.IsEnabled = true;
                    // 更新新版本信息
                    NewVersion = DateTime.Parse(LCommitDT).ToString();
                    // 在更新日志文本框中添加当前版本与新版本的信息
                    UpdateLogTb.Text += $"当前版本Commit时间：{CurrentVersionCommitDate}，Pixiv-Nginx 有新版本可用。\r\n";
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
                ProxyUrl = "https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip";
                UpdateLogTb.Text += "最优代理服务器是: " + ProxyUrl;
            }
            else
            {
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
                await System.Threading.Tasks.Task.Run(() => UnZip(destinationPath, dataDirectory, false));
                // 镜像站与加速代理所下载的压缩包差异的处理
                if (fastestproxy != "Mirror")
                {
                    RenameDirectory(Path.Combine(dataDirectory, "Pixiv-Nginx-main"), NginxDirectory);
                }
                // 更新UI，表示文件解压完成
                UpdateBtn.Content = $"解压完成";
                UpdateLogTb.Text += $"解压新版本压缩包完成！\r\n";
                // 将新的版本号写入应用程序设置并保存
                ConfigINI.INIWrite("程序信息", "CurrentVersionCommitDate", NewVersion, INIPath);
                UpdateInfo();
                VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + CurrentVersionCommitDate;
                // 显示当前 Pixiv-Nginx 版本的提交日期
                VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + CurrentVersionCommitDate;
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
                string RepoInfo = await GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");
                JObject repodata = JObject.Parse(RepoInfo);
                string CommitInfoURL = repodata["object"]["url"].ToString();
                // 异步获取提交详细信息
                string CommitInfo = await GetAsync(CommitInfoURL);
                JObject commitdata = JObject.Parse(CommitInfo);
                // 获取提交的日期时间（GMT）
                string LCommitDT = commitdata["committer"]["date"].ToString();
                // 获取用户选择的ZIP文件路径
                string filePath = openFileDialog.FileName;
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
                        await System.Threading.Tasks.Task.Run(() => UnZip(filePath, dataDirectory, false));
                        // 镜像站与加速代理所下载的压缩包差异的处理
                        if (Directory.Exists(Path.Combine(dataDirectory, "Pixiv-Nginx-main")))
                        {
                            RenameDirectory(Path.Combine(dataDirectory, "Pixiv-Nginx-main"), NginxDirectory);
                        }
                        // 更新解压状态
                        UpdateLogTb.Text += $"解压新版本压缩包完成！\r\n";
                        // 将记录的新版本写入配置文件
                        ConfigINI.INIWrite("程序信息", "CurrentVersionCommitDate", DateTime.Parse(inputBox.InputText).ToString(), INIPath);
                        // 显示当前 Pixiv-Nginx 版本的提交日期
                        VersionInfo.Text = "当前 Pixiv-Nginx 版本 Commit 时间(GMT)：\r\n" + CurrentVersionCommitDate;
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
            DelLogBtn.Content = $"清理所有日志({GetTotalFileSizeInMB(LogfilePaths)}MB)";
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
        }
    }
}
