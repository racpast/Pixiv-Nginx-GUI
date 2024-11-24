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
        public static void UnZip(string zipedFile, string strDirectory, bool overWrite, string password)
        {
            if (strDirectory == "")
                strDirectory = Directory.GetCurrentDirectory();
            if (!strDirectory.EndsWith("\\"))
                strDirectory += "\\";

            using (ZipInputStream s = new ZipInputStream(File.OpenRead(zipedFile)))
            {
                if (password != null)
                {
                    s.Password = password;
                }
                ZipEntry theEntry;

                while ((theEntry = s.GetNextEntry()) != null)
                {
                    string directoryName = "";
                    string pathToZip = "";
                    pathToZip = theEntry.Name;

                    if (pathToZip != "")
                        directoryName = Path.GetDirectoryName(pathToZip) + "\\";

                    string fileName = Path.GetFileName(pathToZip);

                    Directory.CreateDirectory(strDirectory + directoryName);

                    if (fileName != "")
                    {
                        if ((File.Exists(strDirectory + directoryName + fileName) && overWrite) || (!File.Exists(strDirectory + directoryName + fileName)))
                        {
                            using (FileStream streamWriter = File.Create(strDirectory + directoryName + fileName))
                            {
                                int size = 2048;
                                byte[] data = new byte[2048];
                                while (true)
                                {
                                    size = s.Read(data, 0, data.Length);

                                    if (size > 0)
                                        streamWriter.Write(data, 0, size);
                                    else
                                        break;
                                }
                                streamWriter.Close();
                            }
                        }
                    }
                }
                s.Close();
            }
        }

        public static void UnZip(string zipedFile, string strDirectory, bool overWrite)
        {
            UnZip(zipedFile, strDirectory, overWrite, null);
        }

        public static void UnZip(string zipedFile, string strDirectory)
        {
            UnZip(zipedFile, strDirectory, true);
        }

        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task DownloadFileAsync(
            string fileUrl,
            string destinationPath,
            IProgress<double> progress = null,
            TimeSpan progressTimeout = default,
            CancellationToken cancellationToken = default(CancellationToken))
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
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, bufferSize, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        downloadedBytes += bytesRead;
                        if (progress != null)
                        {
                            double progressPercentage = (double)downloadedBytes / totalBytes * 100;
                            progress.Report(progressPercentage);
                            lastProgressUpdate = DateTime.UtcNow;
                        }
                        if (progress != null && DateTime.UtcNow - lastProgressUpdate > progressTimeout)
                        {
                            throw new TimeoutException("下载进度在指定时间内没有变化，操作超时。");
                        }
                        cancellationToken.ThrowIfCancellationRequested();
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
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();
            return result;
        }
    }
}
