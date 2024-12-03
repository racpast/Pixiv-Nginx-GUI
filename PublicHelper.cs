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
        public static string CERFile = Path.Combine(NginxDirectory, "ca.cer");
        public static string hostsFile = Path.Combine(NginxDirectory, "hosts");
        public static string nginxLogPath = Path.Combine(NginxDirectory, "logs");
        public static string nginxLog1Path = Path.Combine(nginxLogPath, "access.log");
        public static string nginxLog2Path = Path.Combine(nginxLogPath, "E-hentai-access.log");
        public static string nginxLog3Path = Path.Combine(nginxLogPath, "E-hentai-error.log");
        public static string nginxLog4Path = Path.Combine(nginxLogPath, "error.log");
        public static string INIPath = Path.Combine(dataDirectory, "config.ini");
        // 创建一个包含所有日志文件路径的列表
        public static List<string> LogfilePaths = new List<string> { nginxLog1Path, nginxLog2Path, nginxLog3Path, nginxLog4Path };
        // 定义包含重要文件路径的数组
        public static string[] ImportantfilePaths = { nginxPath, nginxConfigFile, CERFile, hostsFile };

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

        // 用于杀死所有名为 "nginx" 的进程的异步方法
        public static async Task KillNginx()
        {
            // 获取所有名为 "nginx" 的进程
            Process[] processes = Process.GetProcessesByName("nginx");
            // 如果没有找到名为 "nginx" 的进程，则直接返回
            if (processes.Length == 0)
            {
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
            await Task.WhenAll(tasks);
        }

        // 用于计算给定文件路径列表中的文件总大小（以MB为单位）的静态方法
        public static double GetTotalFileSizeInMB(List<string> filePaths)
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

        // 运行 CMD 命令的方法
        public static void RunCMD(string command,string workingdirectory = "")
        {
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
                // 如果启动进程时发生异常，显示错误消息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                        Console.WriteLine(proxy + " —— " + response.Content.Headers);
                        return (proxy, stopwatch.ElapsedMilliseconds, null);
                    }
                    catch (Exception ex)
                    {
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
                    Console.WriteLine("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + response.Content.Headers);
                    MirrorRpms = stopwatch.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
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
                    Console.WriteLine(proxy + " —— 超时");
                }
                else if (ex != null)
                {
                    Console.WriteLine(proxy + " —— 错误");
                }
                else
                {
                    Console.WriteLine(proxy + " —— " + ElapsedMilliseconds + "ms");
                }
            }
            Console.WriteLine("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip —— " + MirrorRpms + "ms");
            // 排除有错误的结果并排序
            var fastestProxy = proxyResults
                .Where(result => result.ex == null)
                .OrderBy(result => result.ElapsedMilliseconds)
                .First();
            // 如果镜像站延迟有效且比代理低，则返回 Mirror ，否则返回延迟最低的代理地址
            return (MirrorRpms != -1 && fastestProxy.ElapsedMilliseconds > MirrorRpms) ? "Mirror" : fastestProxy.proxy;
        }

        // 解压缩ZIP文件的方法，支持密码和覆盖选项
        public static void UnZip(string zipedFile, string strDirectory, bool overWrite, string password = null)
        {
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
        public static async Task DownloadFileAsync(string fileUrl, string destinationPath, IProgress<double> progress = null, TimeSpan progressTimeout = default, CancellationToken cancellationToken = default(CancellationToken))
        {
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
                    throw new InvalidOperationException("无法获取文件大小。");
                }
                // 总字节数
                long totalBytes = contentLength.Value;
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
                        throw new TimeoutException("下载进度在超时时间内没有变化，操作超时。");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException("下载被取消。");
            }
            catch (Exception ex)
            {
                // 抛出异常
                throw ex;
            }
        }

        // 异步获取URL内容的方法
        public static async Task<string> GetAsync(string url)
        {
            try
            {
                // 设置用户代理
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                // 发起HTTP GET请求
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                // 确保请求成功
                response.EnsureSuccessStatusCode();
                // 返回响应内容
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                // 抛出异常
                throw;
            }
        }

        // 重命名目录的方法
        public static void RenameDirectory(string oldPath, string newPath)
        {
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
                        // 删除新目录及其内容
                        Directory.Delete(newPath, true);
                    }
                    // 重命名目录
                    Directory.Move(oldPath, newPath);
                }
            }
            catch (Exception ex)
            {
                // 抛出异常
                throw new InvalidOperationException($"重命名目录时发生错误: {ex.Message}", ex);
            }
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


            /// 写入INI的方法
            public void INIWrite(string section, string key, string value, string path)
            {
                // section=配置节点名称，key=键名，value=返回键值，path=路径
                WritePrivateProfileString(section, key, value, path);
            }

            //读取INI的方法
            public string INIRead(string section, string key, string path)
            {
                // 每次从ini中读取多少字节
                System.Text.StringBuilder temp = new System.Text.StringBuilder(255);

                // section=配置节点名称，key=键名，temp=上面，path=路径
                GetPrivateProfileString(section, key, "", temp, 255, path);
                return temp.ToString();

            }

            //删除一个INI文件
            public void INIDelete(string FilePath)
            {
                File.Delete(FilePath);
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
                if (string.IsNullOrEmpty(input))
                {
                    return false; // 空字符串或null返回false
                }
                string booleanString = input.Trim().ToLower(); // 去除空格并转换为小写
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
            // 排除无关条目、修改有关条目并添加不存在条目
            public static void AppendHosts(string hostsPath,string newHostsPath)
            {
                // 读取追加的 hosts 文件内容
                var newHosts = ReadHostsFile(newHostsPath);
                // 读取现有的 hosts 文件内容
                var existingHosts = ReadHostsFile(hostsPath);
                // 删除在需要追加的 hosts 出现过的域名的条目
                var updatedHosts = existingHosts
                    .Where(line => !newHosts.Any(newHost => newHost.domain.Equals(line.domain, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                // 添加newhosts的内容到updatedHosts列表末尾
                updatedHosts.AddRange(newHosts);
                // 将更新后的内容写回 hosts 文件
                WriteHostsFile(hostsPath, updatedHosts);
            }

            static List<(string ip, string domain)> ReadHostsFile(string path)
            {
                var hosts = new List<(string ip, string domain)>();
                var regex = new Regex(@"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\s+(.*)$", RegexOptions.Multiline);
                foreach (Match match in regex.Matches(File.ReadAllText(path)))
                {
                    hosts.Add((match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));
                }
                return hosts;
            }

            static void WriteHostsFile(string path, List<(string ip, string domain)> hosts)
            {
                var content = string.Join(Environment.NewLine, hosts.Select(host => $"{host.ip}{new string(' ', 8 - host.ip.Length % 8)}{host.domain}"));
                File.WriteAllText(path, content);
            }
        }
    }
}