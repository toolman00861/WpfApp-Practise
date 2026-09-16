using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WpfApp2.Component;
using WpfApp2.Services;

namespace WpfApp2
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();

        // 页只 new 一次，换页时复用，表单内容不会丢。
        private readonly JudgeView _judgeView = new JudgeView();
        private readonly RecordView _recordView = new RecordView();
        private readonly SettingView _settingView = new SettingView();
        // 和设置页一样只 new 一次。握手状态在 HslService 静态字段里，换页不会丢。
        private readonly HSLTest _hslView = new HSLTest();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            Closing += MainWindow_Closing;
            ShowPage(_judgeView, JudgeNavButton);
        }

        /// <summary>
        /// 关窗时先停预览再关设备。OnExit 太晚：预览可能还堵在 GetImageBuffer，
        /// VS 点停止调试时 OnExit 还经常不跑，虚拟相机会一直被占着。
        /// </summary>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _settingView.ReleaseHardware();
            // 握手循环可能还在 Sleep/读线圈，关窗先停它再 Dispose 客户端。
            HslService.Shutdown();
        }

        private void JudgeNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(_judgeView, JudgeNavButton);
        }

        private void RecordNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(_recordView, RecordNavButton);
        }

        private void ShowPage(UIElement page, Button activeButton)
        {
            PageHost.Content = page;
            JudgeNavButton.FontWeight = FontWeights.Normal;
            RecordNavButton.FontWeight = FontWeights.Normal;
            SettingNavButton.FontWeight = FontWeights.Normal;
            HslNavButton.FontWeight = FontWeights.Normal;
            activeButton.FontWeight = FontWeights.SemiBold;
        }

        private void SettingNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(_settingView, SettingNavButton);
        }

        /// <summary>切到 HSL 练习页。控件复用，不重新 new。</summary>
        private void HslNavButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(_hslView, HslNavButton);
        }
    }
}
