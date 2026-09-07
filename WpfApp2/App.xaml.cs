using System;
using System.Windows;
using System.Windows.Threading;
using WpfApp2.Database;
using WpfApp2.Services;

namespace WpfApp2
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppLog.Init();
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppLog.Info("程序启动");
            AppLog.Info("加载配置");
            SpecStore.Load();
            Db.Init();
            
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLog.Error("未处理异常", e.Exception);
            MessageBox.Show("发生错误，详情见 Logs 目录下的日志文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
