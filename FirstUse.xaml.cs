using HandyControl.Controls;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static Pixiv_Nginx_GUI.PublicHelper;

namespace Pixiv_Nginx_GUI
{
    /// <summary>
    /// FirstUse.xaml 的交互逻辑
    /// </summary>
    public partial class FirstUse : System.Windows.Window
    {
        // 新的版本号，完成安装时写入配置文件
        private string NewVersion;
        // 先前的证书安装状态，用于回退修改
        private bool PreviousCERState;

        FilesINI ConfigINI = new FilesINI();

        bool _isCancelled;

        bool _isHostsModified = false;

        // 用于下载与解压最新的 Pixiv-Nginx 源码压缩包
        public async Task DownloadZip()
        {
            UnzipText.Text = "等待解压文件";
            string fileUrl = "https://github.com/mashirozx/Pixiv-Nginx/archive/refs/heads/main.zip";
            string ProxyUrl;
            DownloadText.Text = "正在寻找最优代理...";
            string fastestproxy = await FindFastestProxy(proxies, fileUrl);
            if (fastestproxy == "Mirror")
            {
                ProxyUrl = "https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip";
                Console.WriteLine("最优代理服务器是: " + ProxyUrl);
            }
            else
            {
                ProxyUrl = "https://" + fastestproxy + "/https://github.com/mashirozx/Pixiv-Nginx/archive/refs/heads/main.zip";
                Console.WriteLine("最优代理服务器是: " + fastestproxy);
            }
            // 定义 main.zip 的保存路径
            string destinationPath = Path.Combine(TempDirectory, "Pixiv-Nginx-main.zip");
            // 禁用取消和下一步按钮，防止用户中断下载过程
            CancelBtn.IsEnabled = false;
            NextBtn.IsEnabled = false;
            // 创建一个取消令牌源，用于控制下载过程的中断
            CancellationTokenSource cts = new CancellationTokenSource();
            // 设置进度更新的超时时间
            TimeSpan progressTimeout = TimeSpan.FromSeconds(10);
            try
            {
                // 从GitHub API获取仓库信息
                string RepoInfo = await GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");
                JObject repodata = JObject.Parse(RepoInfo);
                // 从仓库信息中提取提交信息的URL
                string CommitInfoURL = repodata["object"]["url"].ToString();
                // 获取提交信息
                string CommitInfo = await GetAsync(CommitInfoURL);
                JObject commitdata = JObject.Parse(CommitInfo);
                // 提取最后一次提交的日期
                string LCommitDT = commitdata["committer"]["date"].ToString();
                // 异步下载文件，并更新下载进度
                await DownloadFileAsync(ProxyUrl,
                                       destinationPath,
                                       new Progress<double>(progress =>
                                       {
                                           // 使用 Dispatcher 更新UI线程上的控件
                                           Dispatcher.Invoke(() =>
                                           {
                                               DownloadText.Text = $"下载中({progress:F2}%)";
                                               DownloadProgress.Value = Math.Round(progress);
                                           });
                                       }),
                                       progressTimeout,
                                       cts.Token);
                // 更新UI，表示文件下载完成并开始解压文件
                DownloadText.Text = "文件下载完成！";
                UnzipText.Text = "解压文件中...";
                // 异步解压文件
                await Task.Run(() => UnZip(destinationPath, dataDirectory, false));
                // 镜像站与加速代理所下载的压缩包差异的处理
                if (fastestproxy != "Mirror")
                {
                    RenameDirectory(Path.Combine(dataDirectory, "Pixiv-Nginx-main"), NginxDirectory);
                }
                // 更新UI，表示文件解压完成
                UnzipText.Text = "文件解压完成！";
                // 记录部署完成时的新版本号
                NewVersion = DateTime.Parse(LCommitDT).ToString();
                // 启用下一步按钮，允许用户继续操作
                // 下载成功后不需要重新启用重试以及从本地文件安装按钮防止误导（https://github.com/racpast/Pixiv-Nginx-GUI/issues/1）
                NextBtn.IsEnabled = true;
            }
            catch (OperationCanceledException)
            {
                // 如果下载被取消或超时，显示提示信息
                HandyControl.Controls.MessageBox.Show("文件下载超时，是正常现象，多重试一两次就好啦\r\n实在不行可以手动下载 main 分支的源码压缩包并从本地安装 QAQ", "下载超时", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                ChooseBtn.IsEnabled = true;
                RetryBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                // 如果发生其他异常，显示错误信息
                HandyControl.Controls.MessageBox.Show($"出现异常：\r\n{ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
                ChooseBtn.IsEnabled = true;
                RetryBtn.IsEnabled = true;
            }
            finally
            {
                // 清理操作：删除下载的ZIP文件，释放取消令牌源，重置按钮状态
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                cts.Dispose();
                CancelBtn.IsEnabled = true;
            }
        }

        // 用于安装证书
        public void InstallCertificate()
        {
            // 创建一个指向当前用户根证书存储的X509Store对象
            // StoreName.Root表示根证书存储，StoreLocation.CurrentUser表示当前用户的证书存储
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            // 以最大权限打开证书存储，以便进行添加、删除等操作
            store.Open(OpenFlags.MaxAllowed);
            // 获取证书存储中的所有证书
            X509Certificate2Collection collection = store.Certificates;
            // 指定要查找的证书的指纹（一个唯一的标识符）
            string Thumbprint = "8D8A94C32FAA48EBAFA56490B0031D82279D1AF9";
            // 在证书存储中查找具有指定指纹的证书
            // X509FindType.FindByThumbprint表示按指纹查找，false表示不区分大小写（对于指纹查找无效，因为指纹是唯一的）
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            try
            {
                // 检查是否找到了具有该指纹的证书
                if (fcollection != null)
                {
                    // 如果找到了证书，则检查证书的数量
                    if (fcollection.Count > 0)
                    {
                        // 从存储中移除找到的证书（如果存在多个相同指纹的证书，将移除所有）
                        store.RemoveRange(fcollection);
                    }
                    // 检查指定的证书文件是否存在
                    if (File.Exists(CERFile))
                    {
                        // 从文件中加载证书
                        X509Certificate2 x509 = new X509Certificate2(CERFile);
                        // 将证书添加到存储中
                        store.Add(x509);
                        // 启用“下一步”按钮
                        NextBtn.IsEnabled = true;
                    }
                }
                // 如果没有找到证书集合（理论上不应该发生，除非Thumbprint为空或格式错误）
            }
            catch (Exception ex)
            {
                // 如果在安装证书过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"安装证书失败！\r\n{ex.Message}", "安装证书", MessageBoxButton.OK, MessageBoxImage.Error);
                // 用户取消等，都回退到上一步
                stepbar.StepIndex--;
                APage.Visibility = Visibility.Visible;
                BPage.Visibility = Visibility.Hidden;
                NextBtn.IsEnabled = true;
            }
            finally
            {
                // 无论是否发生异常，都关闭证书存储
                store.Close();
                // 启用“取消部署”按钮
                CancelBtn.IsEnabled = true;
            }

        }

        // 用于确保基本目录存在
        private void CheckFiles()
        {
            EnsureDirectoryExists(dataDirectory);
            EnsureDirectoryExists(NginxDirectory);
            EnsureDirectoryExists(TempDirectory);
        }

        // 用于刷新DNS缓存的方法
        private void Flushdns()
        {
            // 构建要执行的命令字符串，该命令用于刷新DNS缓存，并在执行后暂停，然后退出
            string command = "ipconfig /flushdns & pause & exit";
            RunCMD(command);
        }

        // 构造函数
        public FirstUse()
        {
            InitializeComponent();
        }

        // 下一步按钮的点击事件
        private async void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            // 检测步骤条是否在第一步
            if (stepbar.StepIndex == 0)
            {
                // 是则隐藏欢迎页面并显示第一页
                WelcomePage.Visibility = Visibility.Hidden;
                APage.Visibility = Visibility.Visible;
                // 下一步
                stepbar.StepIndex++;
                // 禁用下一步按钮，
                NextBtn.IsEnabled = false;
                CancelBtn.IsEnabled = false;
                await DownloadZip();
            } else if (stepbar.StepIndex == 1)
            {
                // 步骤条在第二步则隐藏第一页显示第二页
                APage.Visibility = Visibility.Hidden;
                BPage.Visibility = Visibility.Visible;
                // 下一步
                stepbar.StepIndex++;
                // 禁用“取消部署”与“下一步”按钮防止证书安装中断，需要在 InstallCertificate() 方法中重新启用
                NextBtn.IsEnabled = false;
                CancelBtn.IsEnabled = false;
                // 安装证书
                InstallCertificate();
            }
            else if (stepbar.StepIndex == 2)
            {
                // 步骤条在第三步则隐藏第二页显示第三页
                BPage.Visibility = Visibility.Hidden;
                CPage.Visibility = Visibility.Visible;
                // 下一步
                stepbar.StepIndex++;
                // 禁用下一步按钮，需要在 ReplaceHosts_Click 和 AddHosts_Click 中重新启用
                NextBtn.IsEnabled = false;
            }
            else if (stepbar.StepIndex == 3)
            {
                // 步骤条在第四步则隐藏第三页显示第四页
                CPage.Visibility = Visibility.Hidden;
                DPage.Visibility = Visibility.Visible;
                // 下一步
                stepbar.StepIndex++;
            }
            else if (stepbar.StepIndex == 4)
            {
                // 步骤条在第五步则弹窗确认是否保存修改
                if (HandyControl.Controls.MessageBox.Show("完成向导后，将无法通过“取消部署”按钮回退所有修改，继续吗？", "完成向导", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                {
                    // 将记录的新版本写入配置
                    ConfigINI.INIWrite("程序信息", "CurrentVersionCommitDate", DateTime.Parse(NewVersion).ToString(), INIPath);
                    // 检测 data\pixiv-nginx.old 是否存在
                    if (Directory.Exists(OldNginxDirectory))
                    {
                        // 存在则删除目录
                        Directory.Delete(OldNginxDirectory, true);
                    }
                    // 关闭向导窗口
                    this.Close();
                    // 显示主窗口
                    Application.Current.MainWindow.Show();
                }
            }
        }

        // 取消部署按钮的点击事件
        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.MaxAllowed);
                X509Certificate2Collection collection = store.Certificates;
                string Thumbprint = "8D8A94C32FAA48EBAFA56490B0031D82279D1AF9";
                X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
                if (PreviousCERState == false && fcollection.Count > 0)
                {
                    // 移除证书
                    store.RemoveRange(fcollection);
                }
                if (PreviousCERState == true && fcollection.Count == 0)
                {
                    // 检查指定的证书文件是否存在
                    if (File.Exists(CERFile))
                    {
                        // 从文件中加载证书
                        X509Certificate2 x509 = new X509Certificate2(CERFile);
                        // 将证书添加到存储中
                        store.Add(x509);
                    }
                }
                _isCancelled = true;
            }
            catch (Exception ex)
            {
                // 如果在删除证书过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"回退修改时出错！\r\n{ex.Message}", "回退修改", MessageBoxButton.OK, MessageBoxImage.Error);
                // 回退失败时不进行取消部署
                _isCancelled = false;
            }
            finally
            {
                // 无论是否发生异常，都关闭证书存储
                store.Close();
            }
            if (_isCancelled)
            {
                // 删除 data\pixiv-nginx
                Directory.Delete(NginxDirectory, true);
                // 将 data\pixiv-nginx.old 恢复为 data\pixiv-nginx.old
                RenameDirectory(OldNginxDirectory, NginxDirectory);
                if (_isHostsModified)
                {
                    try
                    {
                        // 检测 hosts 及其备份文件是否存在
                        if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts") && File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts.bak"))
                        {
                            // 存在则将备份文件覆盖到hosts
                            File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts.bak", "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                            // 删除备份文件
                            File.Delete("C:\\Windows\\System32\\drivers\\etc\\hosts.bak");
                        }
                        // 刷新 DNS 缓存
                        Flushdns();
                    }
                    catch (IOException iox)
                    {
                        // 如果在操作 hosts 过程中发生异常，则显示错误消息框
                        HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{iox.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                // 关闭本窗口并打开主窗口
                this.Close();
                Application.Current.MainWindow.Show();
            }
        }

        // 追加按钮的点击事件
        private void AddHosts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检测 hosts 文件是否存在
                if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts"))
                {
                    // 存在则备份 hosts 文件
                    File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts", "C:\\Windows\\System32\\drivers\\etc\\hosts.bak", true);
                    // 排除无关条目、修改有关条目并添加不存在条目
                    WriteHosts.AppendHosts("C:\\Windows\\System32\\drivers\\etc\\hosts", hostsFile);
                }
                else
                {
                    // 不存在则直接复制 hosts 文件
                    File.Copy(hostsFile, "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                }
                // 刷新 DNS 缓存
                Flushdns();
                // 启用下一步按钮并禁用替换与追加按钮
                Addhosts.IsEnabled = false;
                Replacehosts.IsEnabled = false;
                NextBtn.IsEnabled = true;
                _isHostsModified = true;
            }
            catch (IOException iox)
            {
                // 如果在操作 hosts 过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{iox.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 替换按钮的点击事件
        private void ReplaceHosts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检测 hosts 文件是否存在
                if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts"))
                {
                    // 存在则备份 hosts 文件
                    File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts", "C:\\Windows\\System32\\drivers\\etc\\hosts.bak", true);
                }
                // 删除原 hosts 文件
                File.Delete("C:\\Windows\\System32\\drivers\\etc\\hosts");
                // 复制 hosts 文件
                File.Copy(hostsFile, "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                // 刷新 DNS 缓存
                Flushdns();
                // 启用下一步按钮并禁用替换与追加按钮
                Addhosts.IsEnabled = false;
                Replacehosts.IsEnabled = false;
                NextBtn.IsEnabled = true;
                _isHostsModified = true;
            }
            catch (IOException iox)
            {
                // 如果在操作 hosts 过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{iox.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //窗口加载完成事件
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保基本目录存在
            CheckFiles();
            // 创建一个指向当前用户根证书存储的X509Store对象
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            // 以最大权限打开证书存储，以便进行添加、删除等操作
            store.Open(OpenFlags.MaxAllowed);
            // 获取证书存储中的所有证书
            X509Certificate2Collection collection = store.Certificates;
            // 指定要查找的证书的指纹（一个唯一的标识符）
            string Thumbprint = "8D8A94C32FAA48EBAFA56490B0031D82279D1AF9";
            // 在证书存储中查找具有指定指纹的证书
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            // 记录先前证书安装状态以方便回退修改
            PreviousCERState = (fcollection != null && fcollection.Count > 0);
            // 关闭证书存储
            store.Close();
        }

        // 重试按钮的点击事件
        private async void RetryBtn_Click(object sender, RoutedEventArgs e)
        {
            // 禁用重试按钮和从本地文件安装按钮
            RetryBtn.IsEnabled = false;
            ChooseBtn.IsEnabled = false;
            // 等待下载源码压缩包
            await DownloadZip();
        }

        // 从本地文件安装按钮的点击事件
        private async void ChooseBtn_Click(object sender, RoutedEventArgs e)
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
                    InitialText = $"从本地文件安装时，您需要为该版本指定 Commit 日期（GMT）。\r\n最新版本 Commit 日期：{DateTime.Parse(LCommitDT)}",
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
                    NextBtn.IsEnabled = false;
                    CancelBtn.IsEnabled = false;
                    RetryBtn.IsEnabled = false;
                    ChooseBtn.IsEnabled = false;
                    try
                    {
                        //恢复初始文本
                        DownloadText.Text = "下载文件(0%)";
                        DownloadProgress.Value = 0;
                        // 显示解压状态
                        UnzipText.Text = "解压文件中...";
                        // 在后台线程中解压文件
                        await Task.Run(() => UnZip(filePath, dataDirectory, false));
                        // 镜像站与加速代理所下载的压缩包差异的处理
                        if (Directory.Exists(Path.Combine(dataDirectory, "Pixiv-Nginx-main"))){
                            RenameDirectory(Path.Combine(dataDirectory, "Pixiv-Nginx-main"), NginxDirectory);
                        }
                        // 更新解压状态
                        UnzipText.Text = "文件解压完成！";
                        // 更新新版本信息
                        NewVersion = DateTime.Parse(inputBox.InputText).ToString();
                        // 重新启用下一步按钮
                        NextBtn.IsEnabled = true;
                        // 显示成功提示
                        HandyControl.Controls.MessageBox.Show("从本地文件安装成功！", "本地安装", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        // 捕获异常并显示错误提示
                        HandyControl.Controls.MessageBox.Show($"从本地文件安装失败！\r\n{ex}", "本地安装", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        // 无论是否发生异常，都重新启用相关按钮
                        CancelBtn.IsEnabled = true;
                        RetryBtn.IsEnabled = true;
                        ChooseBtn.IsEnabled = true;
                    }
                }
            }
        }
    }
}