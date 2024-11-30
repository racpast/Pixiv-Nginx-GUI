using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;


namespace Pixiv_Nginx_GUI
{
    public class PublicHelper
    {
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
    }
}