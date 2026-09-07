using log4net;
using log4net.Config;
using System;
using System.IO;

namespace WpfApp2.Services
{
    /// <summary>
    /// 日志入口。配置在 log4net.config，文件在 exe 旁 Logs 目录。
    /// </summary>
    public static class AppLog
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AppLog));

        public static void Init()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(dir);
            GlobalContext.Properties["LogDir"] = dir;

            var config = new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.config"));
            XmlConfigurator.ConfigureAndWatch(config);
        }

        public static void Debug(string message)
        {
            Log.Debug(message);
        }

        public static void Info(string message)
        {
            Log.Info(message);
        }

        public static void Warn(string message)
        {
            Log.Warn(message);
        }

        public static void Error(string message, Exception ex = null)
        {
            if (ex == null)
            {
                Log.Error(message);
            }
            else
            {
                Log.Error(message, ex);
            }
        }
    }
}
