using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static Pixiv_Nginx_GUI.LogHelper;
using static Pixiv_Nginx_GUI.PublicHelper;

namespace Pixiv_Nginx_GUI
{
    /// <summary>
    /// FirstUse.xaml 的交互逻辑
    /// </summary>
    public partial class FirstUse : Window
    {
        // 新的版本号，完成安装时写入配置文件
        private string NewVersion;
        // 先前的证书安装状态，用于回退修改
        private bool PreviousCERState;

        bool _isCancelled;

        bool _isHostsModified = false;

        // 用于下载与解压最新的 Pixiv-Nginx 源码压缩包
        public async Task DownloadZip()
        {
            WriteLog("进入DownloadZip()", LogLevel.Debug);

            // 禁用取消和下一步按钮，防止用户中断下载过程
            CancelBtn.IsEnabled = false;
            NextBtn.IsEnabled = false;
            UnzipText.Text = "等待解压文件";
            // 下面将获取仓库信息的代码单独提取到一个 try...catch 中，防止相同报错信息导致无法判断错误（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
            string LCommitDT ="";
            try
            {
                // 从GitHub API获取仓库信息
                string RepoInfo = await GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");

                WriteLog($"获取到GitHub仓库的最新提交信息RepoInfo：{RepoInfo}", LogLevel.Info);

                JObject repodata = JObject.Parse(RepoInfo);
                // 从仓库信息中提取提交信息的URL
                string CommitInfoURL = repodata["object"]["url"].ToString();

                WriteLog($"获取到最后一次提交信息的URL CommitInfoURL：{CommitInfoURL}", LogLevel.Info);

                // 获取提交信息
                string CommitInfo = await GetAsync(CommitInfoURL);

                WriteLog($"获取到最后一次提交的详细信息CommitInfo：{CommitInfo}", LogLevel.Info);

                JObject commitdata = JObject.Parse(CommitInfo);
                // 提取最后一次提交的日期
                // 注意：dateToken 所返回的格式受系统设置影响，这里需要转换为标准格式（https://github.com/racpast/Pixiv-Nginx-GUI/issues/1）
                JToken dateToken = commitdata["committer"]["date"];
                DateTime dateTime = dateToken.ToObject<DateTime>(); // 或者使用 (DateTime)dateToken 如果确定它是 DateTime 类型
                LCommitDT = dateTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

                WriteLog($"提取到最后一次提交的详细信息LCommitDT：{LCommitDT}", LogLevel.Info);
            }
            catch (OperationCanceledException)
            {
                WriteLog("获取信息请求超时，遇到OperationCanceledException。", LogLevel.Error);

                // 如果获取信息请求超时，显示提示信息
                HandyControl.Controls.MessageBox.Show("从 api.github.com 获取信息超时！\r\n若多次尝试后仍然报错请检查是否可以访问到 api.github.com 或反馈！", "请求超时", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                ChooseBtn.IsEnabled = true;
                RetryBtn.IsEnabled = true;
                return;
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}", LogLevel.Error);

                // 如果发生其他异常，显示错误信息
                HandyControl.Controls.MessageBox.Show($"出现异常：\r\n{ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
                ChooseBtn.IsEnabled = true;
                RetryBtn.IsEnabled = true;
                return;
            }
            string fileUrl = "https://github.com/mashirozx/Pixiv-Nginx/archive/refs/heads/main.zip";
            string ProxyUrl;
            DownloadText.Text = "正在寻找最优代理...";
            string fastestproxy = await FindFastestProxy(proxies, fileUrl);
            if (fastestproxy == "Mirror")
            {
                WriteLog("fastestproxy为Mirror，最优代理为镜像站。", LogLevel.Info);

                ProxyUrl = "https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip";
            }
            else
            {
                WriteLog($"fastestproxy（最优代理）为{fastestproxy}。", LogLevel.Info);

                ProxyUrl = "https://" + fastestproxy + "/https://github.com/mashirozx/Pixiv-Nginx/archive/refs/heads/main.zip";
            }
            // 定义 main.zip 的保存路径
            string destinationPath = Path.Combine(TempDirectory, "Pixiv-Nginx-main.zip");
            // 设置进度更新的超时时间
            TimeSpan progressTimeout = TimeSpan.FromSeconds(10);
            // 创建一个取消令牌源，用于控制下载过程的中断
            CancellationTokenSource cts = new CancellationTokenSource();
            try
            {
                WriteLog($"尝试从{ProxyUrl}下载文件到{destinationPath}。", LogLevel.Info);

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

                WriteLog($"文件下载完成。", LogLevel.Info);

                // 更新UI，表示文件下载完成并开始解压文件
                DownloadText.Text = "文件下载完成！";
                UnzipText.Text = "解压文件中...";

                WriteLog($"开始从文件{destinationPath}解压到{dataDirectory}。", LogLevel.Info);

                // 异步解压文件
                await Task.Run(() => UnZip(destinationPath, dataDirectory, false));

                WriteLog($"文件解压到完成。", LogLevel.Info);

                // 更新UI，表示文件解压完成
                UnzipText.Text = "文件解压完成！";
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

                WriteLog($"NewVersion被设置为{LCommitDT}", LogLevel.Debug);

                // 记录部署完成时的新版本号
                NewVersion = LCommitDT;
                // 启用下一步按钮，允许用户继续操作
                NextBtn.IsEnabled = true;
            }
            catch (OperationCanceledException)
            {
                WriteLog("文件下载超时，遇到OperationCanceledException。", LogLevel.Error);

                // 如果下载被取消或超时，显示提示信息
                HandyControl.Controls.MessageBox.Show("文件下载超时，是正常现象，多重试一两次就好啦\r\n实在不行可以手动下载 main 分支的源码压缩包并从本地安装 QAQ", "下载超时", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                ChooseBtn.IsEnabled = true;
                RetryBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}", LogLevel.Error);

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
                    WriteLog($"删除下载的文件{destinationPath}。", LogLevel.Info);

                    File.Delete(destinationPath);
                }
                cts.Dispose();
                CancelBtn.IsEnabled = true;
            }

            WriteLog("完成DownloadZip()", LogLevel.Debug);
        }

        // 用于安装证书
        public void InstallCertificate()
        {
            WriteLog("进入InstallCertificate()", LogLevel.Debug);

            // 创建一个指向当前用户根证书存储的X509Store对象
            // StoreName.Root表示根证书存储，StoreLocation.CurrentUser表示当前用户的证书存储
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            // 以最大权限打开证书存储，以便进行添加、删除等操作
            store.Open(OpenFlags.MaxAllowed);
            // 获取证书存储中的所有证书
            X509Certificate2Collection collection = store.Certificates;
            // 在证书存储中查找具有指定指纹的证书
            // X509FindType.FindByThumbprint 表示按指纹查找，false 表示不区分大小写（对于指纹查找无效，因为指纹是唯一的）
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            try
            {
                // 检查是否找到了具有该指纹的证书
                if (fcollection != null)
                {
                    WriteLog($"检测到fcollection不为空。", LogLevel.Debug);

                    // 如果找到了证书，则检查证书的数量
                    if (fcollection.Count > 0)
                    {
                        WriteLog($"检测到证书数量为{fcollection.Count}，尝试移除。", LogLevel.Info);

                        // 从存储中移除找到的证书（如果存在多个相同指纹的证书，将移除所有）
                        store.RemoveRange(fcollection);
                    }
                    // 检查指定的证书文件是否存在
                    if (File.Exists(CERFile))
                    {
                        WriteLog($"检测到证书文件{CERFile}存在，尝试安装。", LogLevel.Info);

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
                WriteLog($"遇到错误：{ex}", LogLevel.Error);

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

                WriteLog($"证书存储关闭。", LogLevel.Debug);

                // 启用“取消部署”按钮
                CancelBtn.IsEnabled = true;
            }

            WriteLog("完成InstallCertificate()", LogLevel.Debug);
        }

        // 用于确保基本目录存在
        private void CheckDirectories()
        {
            WriteLog("进入CheckDirectories()", LogLevel.Debug);

            EnsureDirectoryExists(dataDirectory);
            EnsureDirectoryExists(NginxDirectory);
            EnsureDirectoryExists(TempDirectory);

            WriteLog("完成CheckDirectories()", LogLevel.Debug);
        }

        // 构造函数
        public FirstUse()
        {
            WriteLog("进入FirstUse()", LogLevel.Debug);

            InitializeComponent();

            // 窗口可拖动化
            this.TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            WriteLog("完成FirstUse()", LogLevel.Debug);
        }

        // 下一步按钮的点击事件
        private async void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入NextBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 检测步骤条是否在第一步
            if (stepbar.StepIndex == 0)
            {
                WriteLog($"stepbar.StepIndex为{stepbar.StepIndex}，隐藏欢迎页并显示第一页。", LogLevel.Debug);

                // 是则隐藏欢迎页面并显示第一页
                WelcomePage.Visibility = Visibility.Hidden;
                APage.Visibility = Visibility.Visible;
                // 下一步
                stepbar.StepIndex++;
                // 禁用下一步按钮，
                NextBtn.IsEnabled = false;
                CancelBtn.IsEnabled = false;
                await DownloadZip();
                CancelBtn.IsEnabled = true;
            } else if (stepbar.StepIndex == 1)
            {
                WriteLog($"stepbar.StepIndex为{stepbar.StepIndex}，隐藏第一页显示第二页。", LogLevel.Debug);

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
                WriteLog($"stepbar.StepIndex为{stepbar.StepIndex}，隐藏第二页显示第三页。", LogLevel.Debug);

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
                WriteLog($"stepbar.StepIndex为{stepbar.StepIndex}，隐藏第三页显示第四页。", LogLevel.Debug);

                // 步骤条在第四步则隐藏第三页显示第四页
                CPage.Visibility = Visibility.Hidden;
                DPage.Visibility = Visibility.Visible;
                // 下一步
                stepbar.StepIndex++;
            }
            else if (stepbar.StepIndex == 4)
            {
                WriteLog($"stepbar.StepIndex为{stepbar.StepIndex}，弹窗确认是否保存修改。", LogLevel.Debug);

                // 步骤条在第五步则弹窗确认是否保存修改
                if (HandyControl.Controls.MessageBox.Show("完成向导后，将无法通过“取消部署”按钮回退所有修改，继续吗？", "完成向导", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
                {
                    WriteLog($"NewVersion：{NewVersion}被写入配置文件。", LogLevel.Debug);

                    // 将记录的新版本写入配置
                    ConfigINI.INIWrite("程序信息", "CurrentVersionCommitDate", NewVersion, INIPath);
                    // 检测 data\pixiv-nginx.old 是否存在
                    if (Directory.Exists(OldNginxDirectory))
                    {
                        WriteLog($"删除目录{OldNginxDirectory}。", LogLevel.Info);

                        // 存在则删除目录
                        Directory.Delete(OldNginxDirectory, true);
                    }
                    WriteLog($"在配置中将IsFirst标记为false。", LogLevel.Debug);

                    // 将配置中的IsFirst标记为false，表示不再是首次运行
                    ConfigINI.INIWrite("程序信息", "IsFirst", "false", INIPath);
                    // 关闭向导窗口
                    this.Close();
                    // 显示主窗口
                    Application.Current.MainWindow.Show();
                }
            }

            WriteLog("完成NextBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 取消部署按钮的点击事件
        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CancelBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.MaxAllowed);
                X509Certificate2Collection collection = store.Certificates;
                X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
                if (PreviousCERState == false && fcollection.Count > 0)
                {
                    WriteLog($"PreviousCERState为{PreviousCERState}且证书数量为{fcollection.Count}，尝试移除证书。。", LogLevel.Debug);

                    // 移除证书
                    store.RemoveRange(fcollection);
                }
                if (PreviousCERState == true && fcollection.Count == 0)
                {
                    WriteLog($"PreviousCERState为{PreviousCERState}且证书数量为{fcollection.Count}，尝试安装证书。。", LogLevel.Debug);

                    // 检查指定的证书文件是否存在
                    if (File.Exists(CERFile))
                    {
                        WriteLog($"检测到证书文件{CERFile}存在，尝试安装。", LogLevel.Info);

                        // 从文件中加载证书
                        X509Certificate2 x509 = new X509Certificate2(CERFile);
                        // 将证书添加到存储中
                        store.Add(x509);
                    }
                }
                WriteLog($"操作取消指标_isCancelled被设置为{_isCancelled}，继续取消部署。", LogLevel.Debug);

                _isCancelled = true;
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                // 如果在删除证书过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"回退修改时出错！\r\n{ex.Message}", "回退修改", MessageBoxButton.OK, MessageBoxImage.Error);

                WriteLog($"操作取消指标_isCancelled被设置为{_isCancelled}，停止取消部署。", LogLevel.Debug);

                // 回退失败时不进行取消部署
                _isCancelled = false;
            }
            finally
            {
                // 无论是否发生异常，都关闭证书存储
                store.Close();

                WriteLog($"证书存储关闭。", LogLevel.Debug);
            }
            if (_isCancelled)
            {
                WriteLog($"操作取消指标_isCancelled为{_isCancelled}，继续取消部署。", LogLevel.Debug);

                // 删除 data\pixiv-nginx
                Directory.Delete(NginxDirectory, true);

                WriteLog($"目录{OldNginxDirectory}改名为{NginxDirectory}。", LogLevel.Info);

                // 将 data\pixiv-nginx.old 恢复为 data\pixiv-nginx.old
                RenameDirectory(OldNginxDirectory, NginxDirectory);
                if (_isHostsModified)
                {
                    WriteLog($"检测到hosts修改标志_isHostsModified：{_isHostsModified}。", LogLevel.Debug);

                    try
                    {
                        // 检测 hosts 及其备份文件是否存在
                        if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts") && File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts.bak"))
                        {
                            WriteLog("C:\\Windows\\System32\\drivers\\etc\\hosts及备份文件C:\\Windows\\System32\\drivers\\etc\\hosts.bak存在，尝试对hosts文件进行回退。", LogLevel.Debug);

                            // 存在则将备份文件覆盖到hosts
                            File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts.bak", "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                            // 删除备份文件
                            File.Delete("C:\\Windows\\System32\\drivers\\etc\\hosts.bak");
                        }
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
                // 关闭本窗口并打开主窗口
                this.Close();
                Application.Current.MainWindow.Show();
            }

            WriteLog("完成CancelBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 追加按钮的点击事件
        private void AddHosts_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入AddHosts_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

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
                // 启用下一步按钮并禁用替换与追加按钮
                Addhosts.IsEnabled = false;
                Replacehosts.IsEnabled = false;
                NextBtn.IsEnabled = true;
                _isHostsModified = true;
            }
            catch(Exception ex)
            {
                WriteLog($"操作hosts时出错：{ex.Message}", LogLevel.Error);

                // 如果在操作 hosts 过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成AddHosts_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 替换按钮的点击事件
        private void ReplaceHosts_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ReplaceHosts_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            try
            {
                // 检测 hosts 文件是否存在
                if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts"))
                {
                    WriteLog("C:\\Windows\\System32\\drivers\\etc\\hosts存在，备份文件至C:\\Windows\\System32\\drivers\\etc\\hosts.bak。", LogLevel.Info);

                    // 存在则备份 hosts 文件
                    File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts", "C:\\Windows\\System32\\drivers\\etc\\hosts.bak", true);
                }
                WriteLog($"用{hostsFile}替换C:\\Windows\\System32\\drivers\\etc\\hosts。", LogLevel.Info);

                // 删除原 hosts 文件
                File.Delete("C:\\Windows\\System32\\drivers\\etc\\hosts");
                // 复制 hosts 文件
                File.Copy(hostsFile, "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                HostsAdditionalModification();
                // 刷新 DNS 缓存
                Flushdns();
                // 启用下一步按钮并禁用替换与追加按钮
                Addhosts.IsEnabled = false;
                Replacehosts.IsEnabled = false;
                NextBtn.IsEnabled = true;
                _isHostsModified = true;
            }
            catch (Exception ex)
            {
                WriteLog($"操作hosts时出错：{ex.Message}", LogLevel.Error);

                // 如果在操作 hosts 过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成ReplaceHosts_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        //窗口加载完成事件
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WriteLog("进入Window_Loaded(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 确保基本目录存在
            CheckDirectories();
            // 创建一个指向当前用户根证书存储的X509Store对象
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            // 以最大权限打开证书存储，以便进行添加、删除等操作
            store.Open(OpenFlags.MaxAllowed);
            // 获取证书存储中的所有证书
            X509Certificate2Collection collection = store.Certificates;
            // 在证书存储中查找具有指定指纹的证书
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            // 记录先前证书安装状态以方便回退修改
            PreviousCERState = (fcollection != null && fcollection.Count > 0);

            WriteLog($"先前证书状态PreviousCERState记录为{PreviousCERState}。", LogLevel.Debug);

            // 关闭证书存储
            store.Close();

            WriteLog($"证书存储关闭。", LogLevel.Debug);

            WriteLog("完成Window_Loaded(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 重试按钮的点击事件
        private async void RetryBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入RetryBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 禁用重试按钮和从本地文件安装按钮
            RetryBtn.IsEnabled = false;
            ChooseBtn.IsEnabled = false;
            // 等待下载源码压缩包
            await DownloadZip();
            CancelBtn.IsEnabled = true;

            WriteLog("完成RetryBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 从本地文件安装按钮的点击事件
        private async void ChooseBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ChooseBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

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
                        WriteLog($"用户在输入日期对话框中输入了{inputBox.InputText}，无效的日期时间！", LogLevel.Warning);

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

                        WriteLog($"开始从文件{filePath}解压到{dataDirectory}。", LogLevel.Info);

                        // 在后台线程中解压文件
                        await Task.Run(() => UnZip(filePath, dataDirectory, false));

                        WriteLog($"文件解压完成。", LogLevel.Info);

                        // 更新解压状态
                        UnzipText.Text = "文件解压完成！";
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

                        // 更新新版本信息
                        NewVersion = DateTime.Parse(inputBox.InputText).ToString();

                        WriteLog($"NewVersion被设置为{NewVersion}", LogLevel.Debug);

                        // 重新启用下一步按钮
                        NextBtn.IsEnabled = true;

                        WriteLog($"从本地文件安装完成。", LogLevel.Info);

                        // 显示成功提示
                        HandyControl.Controls.MessageBox.Show("从本地文件安装成功！", "本地安装", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"遇到异常：{ex}", LogLevel.Error);

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

            WriteLog("完成ChooseBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }
    }
}