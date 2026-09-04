using System.Windows;

namespace WpfApp2
{
    /// <summary>
    /// 和 MainWindow.xaml 是一对：XAML 画界面，这里写逻辑。
    /// partial 表示类被拆成两部分，编译时会和 XAML 生成的代码合并。
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            // 必须先调用：把 XAML 里的控件创建出来，x:Name 才能用。
            InitializeComponent();
        }

        /// <summary>
        /// 对应 XAML 里 Button 的 Click="GreetButton_Click"
        /// </summary>
        private void GreetButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ResultTextBlock.Text = "请先输入名字。";
                return;
            }

            ResultTextBlock.Text = "你好，" + name + "！这就是 XAML + C# 的基本写法。";
        }
    }
}
