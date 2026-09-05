using System.Windows;
using System.Windows.Controls;
using WpfApp2.Component;

namespace WpfApp2
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();

        // 页只 new 一次，换页时复用，表单内容不会丢。
        private readonly JudgeView _judgeView = new JudgeView();
        private readonly RecordView _recordView = new RecordView();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            ShowPage(_judgeView, JudgeNavButton);
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
            activeButton.FontWeight = FontWeights.SemiBold;
        }
    }
}
