using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Pixiv_Nginx_GUI
{
    /// <summary>
    /// FirstUse.xaml 的交互逻辑
    /// </summary>
    public partial class FirstUse : Window
    {

        public static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

        public static string dataDirectory = Path.Combine(currentDirectory, "data");

        public static string NginxDirectory = Path.Combine(dataDirectory, "pixiv-nginx");

        public async Task DownloadZip()
        {
            string fileUrl = "https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip";
            string destinationPath = Path.Combine(currentDirectory, "data", "temp", "Pixiv-Nginx-main.zip");
            CancelBtn.IsEnabled = false;
            NextBtn.IsEnabled = false;
            CancellationTokenSource cts = new CancellationTokenSource();
            TimeSpan progressTimeout = TimeSpan.FromSeconds(10);
            try
            {
                string RepoInfo = await PublicHelper.GetAsync("https://api.github.com/repos/mashirozx/Pixiv-Nginx/git/refs/heads/main");
                JObject repodata = JObject.Parse(RepoInfo);
                string CommitInfoURL = repodata["object"]["url"].ToString();
                string CommitInfo = await PublicHelper.GetAsync(CommitInfoURL);
                JObject commitdata = JObject.Parse(CommitInfo);
                string LCommitDT = commitdata["committer"]["date"].ToString();
                await PublicHelper.DownloadFileAsync(fileUrl,
                                       destinationPath,
                                       new Progress<double>(progress =>
                                       {
                                           Dispatcher.Invoke(() =>
                                           {
                                               DownloadText.Text = $"下载中({progress:F2}%)";
                                               DownloadProgress.Value = Math.Round(progress);
                                           });
                                       }),
                                       progressTimeout,
                                       cts.Token);
                DownloadText.Text = "文件下载完成！";
                Directory.Delete(NginxDirectory, true);
                UnzipText.Text = "解压文件中...";
                await System.Threading.Tasks.Task.Run(() => PublicHelper.UnZip(destinationPath, dataDirectory, false));
                UnzipText.Text = "文件解压完成！";
                Properties.Settings.Default.CurrentVersionCommitDate = DateTime.Parse(LCommitDT).ToString();
                Properties.Settings.Default.Save();
                NextBtn.IsEnabled = true;
            }
            catch (OperationCanceledException)
            {
                HandyControl.Controls.MessageBox.Show("文件下载超时，请重试！", "下载", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"出现异常：\r\n{ex.Message}", "异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                cts.Dispose();
                CancelBtn.IsEnabled = true;
                ChooseBtn.IsEnabled = true;
                RetryBtn.IsEnabled = true;
            }
        }

        public void InstallCertificate()
        {
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.MaxAllowed);
            X509Certificate2Collection collection = store.Certificates;
            string Thumbprint = "8D8A94C32FAA48EBAFA56490B0031D82279D1AF9";
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            try
            {
                if (fcollection != null)
                {
                    if (fcollection.Count > 0)
                    {
                        store.RemoveRange(fcollection);
                        if (File.Exists(CERFile))
                        {
                            X509Certificate2 x509 = new X509Certificate2(CERFile);
                            store.Add(x509);
                            NextBtn.IsEnabled = true;
                        }
                    }
                    else
                    {
                        if (File.Exists(CERFile))
                        {
                            X509Certificate2 x509 = new X509Certificate2(CERFile);
                            store.Add(x509);
                            NextBtn.IsEnabled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"安装证书失败！\r\n{ex.Message}", "安装证书", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                store.Close();
                CancelBtn.IsEnabled = true;
            }

        }

        readonly string CERFile = Path.Combine(NginxDirectory, "ca.cer");

        readonly string hostsFile = Path.Combine(NginxDirectory, "hosts");

        public void CheckFiles()
        {
            EnsureDirectoryExists(dataDirectory);
            EnsureDirectoryExists(NginxDirectory);
            EnsureDirectoryExists(Path.Combine(dataDirectory, "temp"));
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void Flushdns()
        {
            string command = "ipconfig /flushdns & pause & exit";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{command}\"",
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

        public FirstUse()
        {
            InitializeComponent();
        }

        private async void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (stepbar.StepIndex == 0)
            {
                if (WelcomePage.Visibility == Visibility.Visible)
                {
                    WelcomePage.Visibility = Visibility.Hidden;
                    APage.Visibility = Visibility.Visible;
                    await DownloadZip();
                }
                else
                {
                    APage.Visibility = Visibility.Hidden;
                    BPage.Visibility = Visibility.Visible;
                    stepbar.StepIndex++;
                    NextBtn.IsEnabled = false;
                    CancelBtn.IsEnabled = false;
                    InstallCertificate();
                }
            }
            else if (stepbar.StepIndex == 1)
            {
                BPage.Visibility = Visibility.Hidden;
                CPage.Visibility = Visibility.Visible;
                stepbar.StepIndex++;
                NextBtn.IsEnabled = false;
            }
            else if (stepbar.StepIndex == 2)
            {
                CPage.Visibility = Visibility.Hidden;
                DPage.Visibility = Visibility.Visible;
                stepbar.StepIndex++;
            }
            else if (stepbar.StepIndex == 3)
            {
                this.Close();
                Application.Current.MainWindow.Show();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Directory.Delete(NginxDirectory, true);
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.MaxAllowed);
            X509Certificate2Collection collection = store.Certificates;
            string Thumbprint = "8D8A94C32FAA48EBAFA56490B0031D82279D1AF9";
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            try
            {
                if (fcollection != null)
                {
                    if (fcollection.Count > 0)
                    {
                        store.RemoveRange(fcollection);
                    }
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"删除证书失败！\r\n{ex.Message}", "删除证书", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                store.Close();
            }
            try
            {
                if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts") && File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts.bak"))
                {
                    File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts.bak", "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                }
                Flushdns();
            }
            catch (IOException iox)
            {
                HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{iox.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            this.Close();
            Application.Current.MainWindow.Show();
        }

        private void Addhosts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts"))
                {
                    File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts", "C:\\Windows\\System32\\drivers\\etc\\hosts.bak", true);
                    string sourceContent = File.ReadAllText(hostsFile);
                    File.AppendAllText("C:\\Windows\\System32\\drivers\\etc\\hosts", sourceContent);
                }
                else
                {
                    File.Copy(hostsFile, "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                }
                Flushdns();
                Addhosts.IsEnabled = false;
                Replacehosts.IsEnabled = false;
                NextBtn.IsEnabled = true;
            }
            catch (IOException iox)
            {
                HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{iox.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Replacehosts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists("C:\\Windows\\System32\\drivers\\etc\\hosts"))
                {
                    File.Copy("C:\\Windows\\System32\\drivers\\etc\\hosts", "C:\\Windows\\System32\\drivers\\etc\\hosts.bak", true);
                }
                File.Delete("C:\\Windows\\System32\\drivers\\etc\\hosts");
                File.Copy(hostsFile, "C:\\Windows\\System32\\drivers\\etc\\hosts", true);
                Flushdns();
                Addhosts.IsEnabled = false;
                Replacehosts.IsEnabled = false;
                NextBtn.IsEnabled = true;
            }
            catch (IOException iox)
            {
                HandyControl.Controls.MessageBox.Show($"操作hosts时出错：\r\n{iox.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CheckFiles();
        }

        private async void RetryBtn_Click(object sender, RoutedEventArgs e)
        {
            RetryBtn.IsEnabled = false;
            ChooseBtn.IsEnabled = false;
            await DownloadZip();
        }

        private async void ChooseBtn_Click(object sender, RoutedEventArgs e)
        {
            string destinationPath = Path.Combine(currentDirectory, "data", "temp", "Pixiv-Nginx-main.zip");
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
                    InitialText = $"从本地文件安装时，您需要为该版本指定 Commit 日期（GMT）。\r\n最新版本 Commit 日期：{DateTime.Parse(LCommitDT)}",
                    InitialTitle = "输入"
                };

                bool? result = inputBox.ShowDialog();

                if (result == true)
                {
                    while (!DateTime.TryParse(inputBox.InputText, out DateTime InputdateTime))
                    {
                        HandyControl.Controls.MessageBox.Show("您输入了无效的日期时间！", "输入", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                    NextBtn.IsEnabled = false;
                    CancelBtn.IsEnabled = false;
                    RetryBtn.IsEnabled = false;
                    ChooseBtn.IsEnabled = false;
                    try
                    {
                        Directory.Delete(NginxDirectory, true);
                        DownloadText.Text += $"文件下载完成\r\n";
                        Directory.Delete(NginxDirectory, true);
                        UnzipText.Text = "解压文件中...";
                        await System.Threading.Tasks.Task.Run(() => PublicHelper.UnZip(destinationPath, dataDirectory, false));
                        UnzipText.Text = "文件解压完成！";
                        Properties.Settings.Default.CurrentVersionCommitDate = DateTime.Parse(LCommitDT).ToString();
                        Properties.Settings.Default.Save();
                        NextBtn.IsEnabled = true;
                        HandyControl.Controls.MessageBox.Show("从本地安装成功！", "本地安装", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        HandyControl.Controls.MessageBox.Show($"从本地更新失败！\r\n{ex}", "更新", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        CancelBtn.IsEnabled = true;
                        RetryBtn.IsEnabled = true;
                        ChooseBtn.IsEnabled = true;
                    }
                }
            }
        }
    }
}
