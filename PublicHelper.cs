using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using static Pixiv_Nginx_GUI.LogHelper;


namespace Pixiv_Nginx_GUI
{
    public class PublicHelper
    {
        // 定义基本路径
        public static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static string dataDirectory = Path.Combine(currentDirectory, "data");
        public static string TempDirectory = Path.Combine(dataDirectory, "temp");
        public static string NginxDirectory = Path.Combine(dataDirectory, "pixiv-nginx");
        public static string OldNginxDirectory = Path.Combine(dataDirectory, "pixiv-nginx.old");
        public static string nginxPath = Path.Combine(NginxDirectory, "nginx.exe");
        public static string nginxConfigFile = Path.Combine(NginxDirectory, "conf", "nginx.conf");
        public static string CERFile = Path.Combine(dataDirectory, "ca.cer");
        public static string CRTFile = Path.Combine(dataDirectory, "pixiv.net.crt");
        public static string KeyFile = Path.Combine(dataDirectory, "pixiv.net.key");
        public static string CADirectory = Path.Combine(NginxDirectory, "conf", "ca");
        public static string OldCERFile = Path.Combine(CADirectory, "ca.cer");
        public static string OldCRTFile = Path.Combine(CADirectory, "pixiv.net.crt");
        public static string OldKeyFile = Path.Combine(CADirectory, "pixiv.net.key");
        public static string hostsFile = Path.Combine(NginxDirectory, "hosts");
        public static string nginxLogPath = Path.Combine(NginxDirectory, "logs");
        public static string nginxLog1Path = Path.Combine(nginxLogPath, "access.log");
        public static string nginxLog2Path = Path.Combine(nginxLogPath, "E-hentai-access.log");
        public static string nginxLog3Path = Path.Combine(nginxLogPath, "E-hentai-error.log");
        public static string nginxLog4Path = Path.Combine(nginxLogPath, "error.log");
        public static string INIPath = Path.Combine(dataDirectory, "config.ini");
        public static string GUILogDirectory = Path.Combine(dataDirectory, "logs");
        public static string GUILogPath = Path.Combine(GUILogDirectory, "GUI.log");
        // 创建一个包含所有日志文件路径的列表
        public static List<string> LogfilePaths = new List<string> { nginxLog1Path, nginxLog2Path, nginxLog3Path, nginxLog4Path };
        // 定义包含重要文件路径的数组
        public static string[] ImportantfilePaths = { nginxPath, nginxConfigFile, CERFile, hostsFile };
        // 是否输出日志
        public static bool OutputLog = false;
        // 证书指纹
        public static string Thumbprint = "BF19E93137660E4A517DDBF4DDC015CDC8760E37";

        public static FilesINI ConfigINI = new FilesINI();

        // 既定版本号，更新时需要修改
        public static string PresetGUIVersion = "V1.5";

        // 该方法用于确保指定路径的目录存在，如果目录不存在，则创建它
        public static void EnsureDirectoryExists(string path)
        {
            WriteLog($"EnsureDirectoryExists(string path)被调用，参数path：{path}。", LogLevel.Debug);

            // 如果目录不存在
            if (!Directory.Exists(path))
            {
                WriteLog($"目录{path}不存在，创建目录。", LogLevel.Info);

                // 创建目录
                Directory.CreateDirectory(path);
            }
            // 如果目录已存在，则不执行任何操作

            WriteLog($"EnsureDirectoryExists(string path)完成。", LogLevel.Debug);
        }

        // 用于杀死所有名为 "nginx" 的进程的异步方法
        public static async Task KillNginx()
        {
            WriteLog($"KillNginx()被调用", LogLevel.Debug);

            // 获取所有名为 "nginx" 的进程
            Process[] processes = Process.GetProcessesByName("nginx");
            // 如果没有找到名为 "nginx" 的进程，则直接返回
            if (processes.Length == 0)
            {
                WriteLog($"未找到名为\"nginx\"的进程，返回。", LogLevel.Info);

                return;
            }
            // 创建一个任务列表，用于存储每个杀死进程任务的任务对象
            List<Task> tasks = new List<Task>();
            // 遍历所有找到的 "nginx" 进程
            foreach (Process process in processes)
            {
                // 为每个进程创建一个异步任务，该任务尝试杀死进程并处理可能的异常
                Task task = Task.Run(() =>
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
                            WriteLog($"进程{process.ProcessName}在超时时间内没有退出。", LogLevel.Warning);

                            HandyControl.Controls.MessageBox.Show($"进程 {process.ProcessName} 在超时时间内没有退出。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"杀死进程{process.ProcessName}时遇到错误：{ex}", LogLevel.Error);

                        // 如果在杀死进程的过程中发生异常，则显示错误消息框
                        HandyControl.Controls.MessageBox.Show($"无法杀死进程 {process.ProcessName}: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
                // 将创建的任务添加到任务列表中
                tasks.Add(task);
            }
            // 等待所有杀死进程的任务完成
            await Task.WhenAll(tasks);

            WriteLog($"KillNginx()完成。", LogLevel.Debug);
        }

        // 用于计算给定文件路径列表中的文件总大小（以MB为单位）的静态方法
        public static double GetTotalFileSizeInMB(List<string> filePaths)
        {
            WriteLog($"GetTotalFileSizeInMB(List<string> filePaths)被调用，参数filePaths：{filePaths}。", LogLevel.Debug);

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

            WriteLog($"GetTotalFileSizeInMB(List<string> filePaths)完成，返回{totalSizeInMB}。", LogLevel.Debug);

            // 返回文件总大小（以MB为单位）
            return totalSizeInMB;
        }

        // 运行 CMD 命令的方法
        public static void RunCMD(string command,string workingdirectory = "")
        {
            WriteLog($"RunCMD(string command,string workingdirectory = \"\")被调用，参数command：{command}，参数workingdirectory：{workingdirectory}。", LogLevel.Debug);

            // 创建一个ProcessStartInfo对象，用于配置如何启动一个进程
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                // 指定要启动的进程的文件名
                FileName = "cmd.exe",
                // 指定传递给cmd.exe的参数，/k表示执行完命令后保持窗口打开，\"{command}\"是要执行的命令
                Arguments = $"/k \"{command}\"",
                // 设置进程的工作目录
                WorkingDirectory = workingdirectory,
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
                WriteLog($"遇到异常: {ex}。", LogLevel.Error);

                // 如果启动进程时发生异常，显示错误消息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog($"RunCMD(string command,string workingdirectory = \"\")完成。", LogLevel.Debug);
        }

        // Github 文件下载加速代理列表
        public static readonly List<string> proxies = new List<string>{
            "gh.tryxd.cn",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccc.cc",
            "gh.222322.xyz",
            "ghproxy.cc",
            "gh.catmak.name",
            "gh.nxnow.top",
            "ghproxy.cn",
            "ql.133.info",
            "cf.ghproxy.cc",
            "ghproxy.imciel.com",
            "g.blfrp.cn",
            "gh-proxy.ygxz.in",
            "ghp.keleyaa.com",
            "gh.pylas.xyz",
            "githubapi.jjchizha.com",
            "ghp.arslantu.xyz",
            "githubapi.jjchizha.com",
            "ghp.arslantu.xyz",
            "git.40609891.xyz",
            "firewall.lxstd.org",
            "gh.monlor.com",
            "slink.ltd",
            "github.geekery.cn",
            "gh.jasonzeng.dev",
            "github.tmby.shop",
            "gh.sixyin.com",
            "liqiu.love",
            "git.886.be",
            "github.xxlab.tech",
            "github.ednovas.xyz",
            "gh.xx9527.cn",
            "gh-proxy.linioi.com",
            "gitproxy.mrhjx.cn",
            "github.wuzhij.com",
            "git.speed-ssr.tech"
            };

        // 寻找最优代理的方法
        public static async Task<string> FindFastestProxy(List<string> proxies, string targetUrl)
        {
            WriteLog($"FindFastestProxy(List<string> proxies, string targetUrl)被调用，参数proxies：{proxies}，参数targetUrl：{targetUrl}。", LogLevel.Debug);

            long MirrorRpms = -1;
            // 逐个测试代理延迟
            var proxyTasks = proxies.Select(async proxy =>
            {
                var proxyUri = new Uri($"https://{proxy}");
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    try
                    {
                        var response = await client.GetAsync(proxyUri + targetUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        WriteLog(proxy + " —— " + response.Content.Headers, LogLevel.Debug);
                        Console.WriteLine(proxy + " —— " + response.Content.Headers);
                        return (proxy, stopwatch.ElapsedMilliseconds, null);
                    }
                    catch (Exception ex)
                    {
                        WriteLog(proxyUri + targetUrl + " —— " + ex, LogLevel.Debug);
                        Console.WriteLine(proxyUri + targetUrl + " —— " + ex);
                        return (proxy, stopwatch.ElapsedMilliseconds, ex);
                    }
                    finally
                    {
                        stopwatch.Stop();
                    }
                }
            }).ToList();
            // 测试镜像站延迟
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                try
                {
                    var response = await client.GetAsync("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip", HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    WriteLog("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + response.Content.Headers, LogLevel.Debug);
                    Console.WriteLine("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + response.Content.Headers);
                    MirrorRpms = stopwatch.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    WriteLog("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + ex, LogLevel.Debug);
                    Console.WriteLine("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + ex);
                }
                finally
                {
                    stopwatch.Stop();
                }
            }
            // 等待测试全部完成
            var proxyResults = await Task.WhenAll(proxyTasks);
            // 输出到控制台，调试用
            foreach (var (proxy, ElapsedMilliseconds, ex) in proxyResults)
            {
                if (ex is TaskCanceledException)
                {
                    WriteLog(proxy + " —— 超时", LogLevel.Debug);
                    Console.WriteLine(proxy + " —— 超时");
                }
                else if (ex != null)
                {
                    WriteLog(proxy + " —— 错误", LogLevel.Debug);
                    Console.WriteLine(proxy + " —— 错误");
                }
                else
                {
                    WriteLog(proxy + " —— " + ElapsedMilliseconds + "ms", LogLevel.Debug);
                    Console.WriteLine(proxy + " —— " + ElapsedMilliseconds + "ms");
                }
            }
            WriteLog("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip —— " + MirrorRpms + "ms", LogLevel.Debug);
            Console.WriteLine("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip —— " + MirrorRpms + "ms");

            // 排除有错误的结果并排序
            var fastestProxy = proxyResults
                .Where(result => result.ex == null)
                .OrderBy(result => result.ElapsedMilliseconds)
                .First();

            string Output = (MirrorRpms != -1 && fastestProxy.ElapsedMilliseconds > MirrorRpms) ? "Mirror" : fastestProxy.proxy;

            WriteLog($"FindFastestProxy(List<string> proxies, string targetUrl)完成，返回{Output}。", LogLevel.Debug);

            // 如果镜像站延迟有效且比代理低，则返回 Mirror ，否则返回延迟最低的代理地址
            return Output;
        }

        // 解压缩ZIP文件的方法，支持密码和覆盖选项
        public static void UnZip(string zipedFile, string strDirectory, bool overWrite, string password = null)
        {
            WriteLog($"UnZip(string zipedFile, string strDirectory, bool overWrite, string password = null)被调用，参数zipedFile：{zipedFile}，参数strDirectory：{strDirectory}，参数overWrite：{overWrite}，参数password：{password}。", LogLevel.Debug);

            // 如果目标目录为空，则使用当前工作目录
            if (string.IsNullOrEmpty(strDirectory))
                strDirectory = Directory.GetCurrentDirectory();
            // 确保目录路径格式正确
            strDirectory = Path.Combine(strDirectory, "");
            // 使用 ZipInputStream 读取ZIP文件
            using (ZipInputStream zipStream = new ZipInputStream(File.OpenRead(zipedFile)))
            {
                // 如果提供了密码，则设置密码
                if (!string.IsNullOrEmpty(password))
                {
                    zipStream.Password = password;
                }
                ZipEntry entry;
                // 遍历ZIP文件中的每个条目
                while ((entry = zipStream.GetNextEntry()) != null)
                {
                    // 构造文件完整路径
                    string entryPath = Path.Combine(strDirectory, entry.Name);
                    // 获取目录部分
                    string directoryName = Path.GetDirectoryName(entryPath);
                    // 如果目录不为空，则创建目录
                    if (!string.IsNullOrEmpty(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    // 获取文件名
                    string fileName = Path.GetFileName(entryPath);
                    // 如果文件名不为空
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        // 检查文件是否已存在
                        bool fileExists = File.Exists(entryPath);
                        // 如果需要覆盖或文件不存在，则创建文件并写入数据
                        if (overWrite || !fileExists)
                        {
                            using (FileStream fileStream = File.Create(entryPath))
                            {
                                // 缓冲区
                                byte[] buffer = new byte[2048];
                                int bytesRead;
                                // 从ZIP流中读取数据并写入文件
                                while ((bytesRead = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    fileStream.Write(buffer, 0, bytesRead);
                                }
                            }
                        }
                    }
                }
            }

            WriteLog($"UnZip(string zipedFile, string strDirectory, bool overWrite, string password = null)完成。", LogLevel.Debug);
        }

        // 解压缩ZIP文件的方法，无密码，支持覆盖选项
        public static void UnZip(string zipedFile, string strDirectory, bool overWrite)
        {
            // 调用带有密码参数的方法，密码参数传递null
            UnZip(zipedFile, strDirectory, overWrite, null);
        }

        // 解压缩ZIP文件的方法，无密码，默认覆盖
        public static void UnZip(string zipedFile, string strDirectory)
        {
            // 调用带有覆盖和密码参数的方法，默认覆盖，密码为null
            UnZip(zipedFile, strDirectory, true, null);
        }

        // 定义一个静态的HttpClient实例，用于HTTP请求
        private static readonly HttpClient _httpClient = new HttpClient
        {
            // 设置超时时间为10秒
            Timeout = TimeSpan.FromSeconds(10)
        };

        // 异步下载文件的方法，支持进度报告和取消操作
        public static async Task DownloadFileAsync(string fileUrl, string destinationPath, IProgress<double> progress = null, TimeSpan progressTimeout = default, CancellationToken cancellationToken = default)
        {
            WriteLog($"DownloadFileAsync(string fileUrl, string destinationPath, IProgress<double> progress = null, TimeSpan progressTimeout = default, CancellationToken cancellationToken = default)被调用，参数fileUrl：{fileUrl}，参数destinationPath：{destinationPath}，参数progress：{progress}，参数progressTimeout：{progressTimeout}，参数cancellationToken：{cancellationToken}。", LogLevel.Debug);

            try
            {
                // 发起HTTP GET请求获取文件头信息
                HttpResponseMessage response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                // 确保请求成功
                response.EnsureSuccessStatusCode();
                // 获取文件大小
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength == null)
                {
                    WriteLog($"无法获取文件大小。", LogLevel.Warning);

                    throw new InvalidOperationException("无法获取文件大小。");
                }
                // 总字节数
                long totalBytes = contentLength.Value;

                WriteLog($"成功获取到文件大小：{totalBytes}。", LogLevel.Info);

                // 已下载字节数
                long downloadedBytes = 0;
                // 上次进度更新时间
                DateTime lastProgressUpdate = DateTime.UtcNow;
                // 使用 FileStream 写入文件，使用读取的流读取文件内容
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    // 缓冲区大小
                    const int bufferSize = 8192;
                    // 缓冲区
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead;
                    // 循环读取文件内容直到完成
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, bufferSize, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        // 更新已下载字节数
                        downloadedBytes += bytesRead;
                        // 报告进度
                        progress?.Report((double)downloadedBytes / totalBytes * 100);
                        // 更新上次进度更新时间
                        lastProgressUpdate = DateTime.UtcNow;
                        // 检查是否请求取消
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    // 检查进度是否超时
                    if (DateTime.UtcNow - lastProgressUpdate > progressTimeout)
                    {
                        WriteLog($"下载进度在超时时间内没有变化，操作超时。", LogLevel.Error);

                        throw new TimeoutException("下载进度在超时时间内没有变化，操作超时。");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                // 抛出异常
                throw ex;
            }

            WriteLog($"DownloadFileAsync(string fileUrl, string destinationPath, IProgress<double> progress = null, TimeSpan progressTimeout = default, CancellationToken cancellationToken = default)完成。", LogLevel.Debug);
        }

        // 异步获取URL内容的方法
        public static async Task<string> GetAsync(string url)
        {
            WriteLog($"GetAsync(string url)被调用，参数url：{url}。", LogLevel.Debug);

            try
            {
                // 设置用户代理
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

                // 发起HTTP GET请求
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                // 确保请求成功
                response.EnsureSuccessStatusCode();

                string Output = await response.Content.ReadAsStringAsync();

                WriteLog($"GetAsync(string url)完成，返回{Output}。", LogLevel.Debug);

                // 返回响应内容
                return Output;
            }
            catch(Exception ex)
            {
                WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                // 抛出异常
                throw ex;
            }
        }

        // 重命名目录的方法
        public static void RenameDirectory(string oldPath, string newPath)
        {
            WriteLog($"RenameDirectory(string oldPath, string newPath)被调用，参数oldPath：{oldPath}，参数newPath：{newPath}。", LogLevel.Debug);

            try
            {
                // 如果旧目录存在
                if (Directory.Exists(oldPath))
                {
                    // 获取新目录的父目录
                    string newParentDirectory = Path.GetDirectoryName(newPath);
                    // 如果新父目录不存在
                    if (!Directory.Exists(newParentDirectory))
                    {
                        // 创建新父目录
                        Directory.CreateDirectory(newParentDirectory);
                    }
                    // 如果新目录已存在
                    if (Directory.Exists(newPath))
                    {
                        WriteLog($"新目录已存在，删除新目录及其内容。", LogLevel.Warning);

                        // 删除新目录及其内容
                        Directory.Delete(newPath, true);
                    }
                    // 重命名目录
                    Directory.Move(oldPath, newPath);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                // 抛出异常
                throw new InvalidOperationException($"重命名目录时发生错误，{ex.Message}", ex);
            }

            WriteLog($"RenameDirectory(string oldPath, string newPath)完成。", LogLevel.Debug);
        }

        // 操作配置文件的类
        public class FilesINI
        {
            // 声明INI文件的写操作函数 WritePrivateProfileString()
            [System.Runtime.InteropServices.DllImport("kernel32")]
            private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

            // 声明INI文件的读操作函数 GetPrivateProfileString()
            [System.Runtime.InteropServices.DllImport("kernel32")]
            private static extern int GetPrivateProfileString(string section, string key, string def, System.Text.StringBuilder retVal, int size, string filePath);


            // 写入INI的方法
            public void INIWrite(string section, string key, string value, string path)
            {
                WriteLog($"INIWrite(string section, string key, string value, string path)被调用，参数section：{section}，参数key：{key}，参数value：{value}，参数path：{path}。", LogLevel.Debug);

                // section=配置节点名称，key=键名，value=返回键值，path=路径
                WritePrivateProfileString(section, key, value, path);

                WriteLog($"INIWrite(string section, string key, string value, string path)完成。", LogLevel.Debug);
            }

            //读取INI的方法
            public string INIRead(string section, string key, string path)
            {
                WriteLog($"INIRead(string section, string key, string path)被调用，参数section：{section}，参数key：{key}，参数path：{path}。", LogLevel.Debug);

                // 每次从ini中读取多少字节
                System.Text.StringBuilder temp = new System.Text.StringBuilder(255);

                // section=配置节点名称，key = 键名，temp = 上面，path = 路径
                GetPrivateProfileString(section, key, "", temp, 255, path);

                WriteLog($"INIRead(string section, string key, string path)完成，返回{temp}。", LogLevel.Debug);

                return temp.ToString();
            }

            //删除一个INI文件
            public void INIDelete(string FilePath)
            {
                WriteLog($"INIDelete(string FilePath)被调用，参数FilePath：{FilePath}。", LogLevel.Debug);

                File.Delete(FilePath);

                WriteLog($"INIDelete(string FilePath)完成。", LogLevel.Debug);
            }

        }

        // 字符串转换为布尔值的类
        public class StringBoolConverter
        {
            /// <summary>
            /// 将字符串转换为布尔值。
            /// 支持 "true" 和 "false"（不区分大小写），其他值返回 false。
            /// </summary>
            /// <param name="input">要转换的字符串</param>
            /// <returns>转换后的布尔值</returns>
            public static bool StringToBool(string input)
            {
                WriteLog($"StringToBool(string input)被调用，参数input：{input}。", LogLevel.Debug);

                if (string.IsNullOrEmpty(input))
                {
                    return false; // 空字符串或null返回false
                }
                string booleanString = input.Trim().ToLower(); // 去除空格并转换为小写

                WriteLog($"StringToBool(string input)完成，返回{booleanString == "true"}。", LogLevel.Debug);

                // 检查字符串是否为 "true"
                return booleanString == "true";
            }

            // 扩展方法，支持处理null值，null值返回false
            public static bool? StringToBoolNullable(string input)
            {
                if (input == null)
                {
                    return null; // null值返回null（可空布尔值）
                }
                return StringToBool(input); // 调用非可空版本的方法
            }
        }

        // 更新 hosts 文件的类
        public class WriteHosts
        {
            public static void ModifySingleRecord(string hostsPath,string ip,string domain)
            {
                WriteLog($"ModifySingleRecord(string hostsPath,string ip,string domain)被调用，参数hostsPath：{hostsPath}，参数ip：{ip}，参数domain：{domain}。", LogLevel.Debug);

                try
                {
                    // 读取现有的 hosts 和追加的 hosts
                    var existingHosts = ReadHostsFile(hostsPath);
                    var newHosts = new List<(string ip, string domain)>();

                    // 对某些网站做额外的访问支持
                    newHosts.Add((ip, domain));

                    // 获取新条目中的域名
                    var newDomains = new HashSet<string>(newHosts.Select(h => h.domain), StringComparer.OrdinalIgnoreCase);

                    // 从现有条目中排除掉新条目中出现过的域名
                    var updatedHosts = existingHosts
                        .Where(h => !newDomains.Contains(h.domain))
                        .ToList();

                    // 将追加文件中的条目合并到更新后的列表
                    updatedHosts.AddRange(newHosts);

                    // 写回结果到 hosts 文件
                    WriteHostsFile(hostsPath, updatedHosts);
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                    throw ex;
                }

                WriteLog($"ModifySingleRecord(string hostsPath,string ip,string domain)完成。", LogLevel.Debug);
            }
            public static void AppendHosts(string hostsPath, string newHostsPath)
            {
                WriteLog($"AppendHosts(string hostsPath, string newHostsPath)被调用，参数hostsPath：{hostsPath}，参数newHostsPath：{newHostsPath}。", LogLevel.Debug);

                try
                {
                    // 读取现有的 hosts 和追加的 hosts
                    var existingHosts = ReadHostsFile(hostsPath);
                    var newHosts = ReadHostsFile(newHostsPath);

                    // 获取新条目中的域名
                    var newDomains = new HashSet<string>(newHosts.Select(h => h.domain), StringComparer.OrdinalIgnoreCase);

                    // 从现有条目中排除掉新条目中出现过的域名
                    var updatedHosts = existingHosts
                        .Where(h => !newDomains.Contains(h.domain))
                        .ToList();

                    // 将追加文件中的条目合并到更新后的列表
                    updatedHosts.AddRange(newHosts);

                    // 写回结果到 hosts 文件
                    WriteHostsFile(hostsPath, updatedHosts);
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                    throw ex;
                }

                WriteLog($"AppendHosts(string hostsPath, string newHostsPath)完成。", LogLevel.Debug);
            }

            // 读取 hosts 文件，解析为 (IP, domain) 形式
            private static List<(string ip, string domain)> ReadHostsFile(string path)
            {
                WriteLog($"ReadHostsFile(string path)被调用，参数path：{path}。", LogLevel.Debug);

                var hosts = new List<(string ip, string domain)>();

                /* RegExp:  /^(?!#)(\S+)\s+(\S+)/gm
                * ^：这个符号表示匹配字符串的开始位置。
                * (?!#)：这是一个负向前瞻断言（negative lookahead assertion）。它的作用是确保接下来的字符不是 #。换句话说，它排除了以 # 开头的字符串。
                * (\S+)：这部分是一个捕获组（capturing group），用来匹配并捕获一个或多个非空白字符（\S 表示非空白字符，+ 表示一个或多个）。这意味着，在断言之后，它将会匹配并捕获第一个连续的非空白字符序列。
                * \s+：这部分匹配一个或多个空白字符（\s 表示空白字符，+ 表示一个或多个）。它用于分隔前面捕获的非空白字符序列和后面的部分。
                * (\S+)：这是第二个捕获组，它的作用与第一个捕获组相同，也是匹配并捕获一个或多个非空白字符。
                */

                var regex = new Regex(@"^(?!#)(\S+)\s+(\S+)", RegexOptions.Multiline);

                try
                {
                    foreach (Match match in regex.Matches(File.ReadAllText(path)))
                    {
                        hosts.Add((match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                    throw ex;
                }

                WriteLog($"ReadHostsFile(string path)完成，返回{hosts}。", LogLevel.Debug);

                WriteLog($"以下内容是从{path}中读取到的。", LogLevel.Info);

                WriteLog(hosts, LogLevel.Info);

                return hosts;
            }

            // 写回 hosts 文件
            private static void WriteHostsFile(string path, List<(string ip, string domain)> hosts)
            {
                WriteLog($"WriteHostsFile(string path, List<(string ip, string domain)> hosts)被调用，参数path：{path}，参数hosts：{hosts}。", LogLevel.Debug);

                WriteLog($"以下内容将会被写回{path}。", LogLevel.Info);

                WriteLog(hosts,LogLevel.Info);

                try
                {
                    var content = string.Join(Environment.NewLine, hosts.Select(h => $"{h.ip}\t\t{h.domain}"));
                    File.WriteAllText(path, content);
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                    throw ex;
                }

                WriteLog($"WriteHostsFile(string path, List<(string ip, string domain)> hosts)完成。", LogLevel.Debug);
            }
        }

        /// <summary>
        /// 释放resx里面的普通类型文件
        /// </summary>
        /// <param name="resource">resx里面的资源</param>
        /// <param name="path">释放到的路径</param>
        public static void ExtractNormalFileInResx(byte[] resource, String path)
        {
            WriteLog($"ExtractNormalFileInResx(byte[] resource, String path)被调用，参数resource：{resource}，参数path：{path}。", LogLevel.Debug);

            FileStream file = new FileStream(path, FileMode.Create);
            file.Write(resource, 0, resource.Length);
            file.Flush();
            file.Close();

            WriteLog($"ExtractNormalFileInResx(byte[] resource, String path)完成。", LogLevel.Debug);
        }

        // hosts 文件追加处理（根据需求修改）
        public static void HostsAdditionalModification()
        {
            WriteLog($"HostsAdditionalModification()被调用。", LogLevel.Debug);

            // 确保 api.github.com 可以正常访问
            EnsureGithubAPI();
            // 某站的支持
            WriteHosts.ModifySingleRecord("C:\\Windows\\System32\\drivers\\etc\\hosts", "127.0.0.1", "nyaa.si");
            WriteHosts.ModifySingleRecord("C:\\Windows\\System32\\drivers\\etc\\hosts", "127.0.0.1", "www.nyaa.si");
            WriteHosts.ModifySingleRecord("C:\\Windows\\System32\\drivers\\etc\\hosts", "127.0.0.1", "sukebei.nyaa.si");

            WriteLog($"HostsAdditionalModification()完成。", LogLevel.Debug);
        }

        // 检测是否可以 Ping 通的方法
        public static bool PingHost(string host)
        {
            WriteLog($"PingHost(string host)被调用，参数host：{host}。", LogLevel.Debug);

            bool pingable = false;
            Ping pingSender = new Ping();
            try
            {
                PingReply reply = pingSender.Send(host);
                if (reply.Status == IPStatus.Success)
                {
                    pingable = true;
                }
            }
            catch (PingException pex)
            {
                WriteLog($"遇到异常：{pex}。", LogLevel.Error);
            }

            WriteLog($"PingHost(string host)完成，返回{pingable}。", LogLevel.Debug);

            return pingable;
        }

        // 确保 api.github.com 可以正常访问的方法
        // 解决由于 api.github.com 访问异常引起的有关问题（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
        public static void EnsureGithubAPI()
        {
            WriteLog($"EnsureGithubAPI()被调用。", LogLevel.Debug);

            // api.github.com DNS A记录 IPv4 列表
            List<string> APIIPAddress = new List<string>
            {
                "20.205.243.168",
                "140.82.113.5",
                "140.82.116.6",
                "4.237.22.34"
            };
            foreach (string IPAddress in APIIPAddress)
            {
                bool isReachable = PingHost(IPAddress);

                WriteLog($"{IPAddress}测试完成，Ping结果：{isReachable}。", LogLevel.Info);

                if (isReachable)
                {
                    WriteHosts.ModifySingleRecord("C:\\Windows\\System32\\drivers\\etc\\hosts", IPAddress, "api.github.com");
                    break;
                }
            }

            WriteLog($"EnsureGithubAPI()完成。", LogLevel.Debug);
        }

        // 用于刷新DNS缓存的方法
        public static void Flushdns()
        {
            WriteLog($"Flushdns()被调用。", LogLevel.Debug);

            // 构建要执行的命令字符串，该命令用于刷新DNS缓存然后退出
            string command = "ipconfig /flushdns & exit";
            RunCMD(command);

            WriteLog($"Flushdns()完成。", LogLevel.Debug);
        }

        // 清理非必要文件的方法，该方法删除了ca.cer，注意应该在解压后马上运行
        public static void CleanUnnecessary()
        {
            WriteLog($"CleanUnnecessary()被调用。", LogLevel.Debug);

            List<string> UnnecessaryDirectories = new List<string>
            {
                Path.Combine(NginxDirectory,"docs"),
                Path.Combine(NginxDirectory,"图片无法显示备用配置"),
                Path.Combine(NginxDirectory,"自签证书傻瓜式批处理包"),
            };
            List<string> UnnecessaryFiles = new List<string>
            {
                Path.Combine(NginxDirectory,".gitattributes"),
                Path.Combine(NginxDirectory,".gitignore"),
                Path.Combine(NginxDirectory,"0.注意上方地址栏路径必须为纯英文"),
                Path.Combine(NginxDirectory,"1.第一次运行程序时弹窗处理.PNG"),
                Path.Combine(NginxDirectory,"2.请同意防火墙权限（重要）.PNG"),
                Path.Combine(NginxDirectory,"3.可视化工具说明.PNG"),
                Path.Combine(NginxDirectory,"4.可视化工具（这个操作简单）.exe"),
                Path.Combine(NginxDirectory,"5.调试工具（这个功能全）.bat"),
                Path.Combine(NginxDirectory,"6.安全及隐私声明.txt"),
                Path.Combine(NginxDirectory,"7.更多信息及更新.html"),
                Path.Combine(NginxDirectory,"BouncyCastle.dll"),
                Path.Combine(NginxDirectory,"LICENSE"),
                Path.Combine(NginxDirectory,"README.md"),
                Path.Combine(NginxDirectory,"ca.cer")
            };
            foreach (var directory in UnnecessaryDirectories)
            {
                if(Directory.Exists(directory))
                {
                    WriteLog($"非必要目录清理：{directory}", LogLevel.Info);

                    Directory.Delete(directory, true);
                }
            }
            foreach(var file in UnnecessaryFiles)
            {
                if (File.Exists(file))
                {
                    WriteLog($"非必要文件清理：{file}", LogLevel.Info);

                    File.Delete(file);
                }
            }

            WriteLog($"CleanUnnecessary()完成。", LogLevel.Debug);
        }
    }
}