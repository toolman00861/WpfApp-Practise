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
            // 海康链路入口：只读 SDK 版本，不枚举、不占相机。真正 Open 在设置页点「打开工位」。
            CameraHub.Init();
            HalconService.Init();
            // 只 new ModbusTcpNet，不连 502。真正 Connect 在「HSL 练习」页点「连接」。
            HslService.Init();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 对照官方 bnClose：StopGrabbing → CloseDevice → DestroyHandle，避免退出后虚拟相机仍被占用。
            CameraHub.Shutdown();
            // 停握手循环再关 TCP。关窗时 MainWindow_Closing 也会调一次，重复调用是安全的。
            HslService.Shutdown();
            AppLog.Info("程序退出");
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLog.Error("未处理异常", e.Exception);
            MessageBox.Show("发生错误，详情见 Logs 目录下的日志文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
