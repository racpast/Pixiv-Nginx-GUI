using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Pixiv_Nginx_GUI
{
    public class PublicHelper
    {
        public static void UnZip(string zipedFile, string strDirectory, bool overWrite, string password = null)
        {
            if (string.IsNullOrEmpty(strDirectory))
                strDirectory = Directory.GetCurrentDirectory();

            strDirectory = Path.Combine(strDirectory, ""); 

            using (ZipInputStream zipStream = new ZipInputStream(File.OpenRead(zipedFile)))
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zipStream.Password = password;
                }

                ZipEntry entry;
                while ((entry = zipStream.GetNextEntry()) != null)
                {
                    string entryPath = Path.Combine(strDirectory, entry.Name);
                    string directoryName = Path.GetDirectoryName(entryPath);

                    if (!string.IsNullOrEmpty(directoryName))
                    {
                        Directory.CreateDirectory(directoryName);
                    }

                    string fileName = Path.GetFileName(entryPath);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        bool fileExists = File.Exists(entryPath);
                        if (overWrite || !fileExists)
                        {
                            using (FileStream fileStream = File.Create(entryPath))
                            {
                                byte[] buffer = new byte[2048];
                                int bytesRead;
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

        public static void UnZip(string zipedFile, string strDirectory, bool overWrite)
        {
            UnZip(zipedFile, strDirectory, overWrite, null);
        }

        public static void UnZip(string zipedFile, string strDirectory)
        {
            UnZip(zipedFile, strDirectory, true, null);
        }

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static async Task DownloadFileAsync(string fileUrl, string destinationPath, IProgress<double> progress = null, TimeSpan progressTimeout = default, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength == null)
                {
                    throw new InvalidOperationException("无法获取文件大小。");
                }
                long totalBytes = contentLength.Value;
                long downloadedBytes = 0;
                DateTime lastProgressUpdate = DateTime.UtcNow;
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    const int bufferSize = 8192;
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead;
                    DateTime downloadStartTime = DateTime.UtcNow;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, bufferSize, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        downloadedBytes += bytesRead;

                        progress?.Report((double)downloadedBytes / totalBytes * 100);

                        lastProgressUpdate = DateTime.UtcNow;

                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (DateTime.UtcNow - lastProgressUpdate > progressTimeout)
                    {
                        throw new TimeoutException("下载进度在超时时间内没有变化，操作超时。");
                    }

                    if (downloadedBytes == 0 && DateTime.UtcNow - downloadStartTime > progressTimeout)
                    {
                        throw new TimeoutException("没有数据下载，操作超时。");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException("下载被取消。");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static async Task<string> GetAsync(string url)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                throw;
            }
        }

        public static void RenameDirectory(string oldPath, string newPath)
        {
            try
            {
                if (Directory.Exists(oldPath))
                {
                    string newParentDirectory = Path.GetDirectoryName(newPath);
                    if (!Directory.Exists(newParentDirectory))
                    {
                        Directory.CreateDirectory(newParentDirectory);
                    }
                    if (Directory.Exists(newPath))
                    {
                        Directory.Delete(newPath, true);
                    }
                    Directory.Move(oldPath, newPath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"重命名目录时发生错误: {ex.Message}", ex);
            }
        }
    }
}