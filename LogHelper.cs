using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Pixiv_Nginx_GUI.PublicHelper;

namespace Pixiv_Nginx_GUI
{
    public class LogHelper
    {
        // 锁对象，用于线程安全
        private static readonly object LockObject = new object();

        // 写入日志的方法
        public static void WriteLog(string message, LogLevel logLevel = LogLevel.Info)
        {
            if (OutputLog)
            {
                lock (LockObject)
                {
                    string logMessage = $"{DateTime.Now} [{logLevel}] {message}{Environment.NewLine}";
                    File.AppendAllText(GUILogPath, logMessage, Encoding.UTF8);
                }
            }
        }

        public static void WriteLog(List<(string ip, string domain)> hosts, LogLevel logLevel = LogLevel.Info)
        {
            if (OutputLog)
            {
                lock (LockObject)
                {
                    string logMessage = string.Join(Environment.NewLine, hosts.Select(host => $"ip: {host.ip}, domain: {host.domain}"));
                    logMessage = $"{DateTime.Now} [{logLevel}]{Environment.NewLine}### Start ###{Environment.NewLine}{logMessage}{Environment.NewLine}### End ###{Environment.NewLine}";
                    File.AppendAllText(GUILogPath, logMessage, Encoding.UTF8);
                }
            }
        }

        // 日志级别枚举
        public enum LogLevel
        {
            Error,
            Warning,
            Info,
            Debug
        }
    }
}
