using System.Windows;
using System.Windows.Controls;

namespace WpfApp2.Component
{
    /// <summary>
    /// JudgeView.xaml 的交互逻辑
    /// </summary>
    public partial class JudgeView : UserControl
    {
        public JudgeView()
        {
            InitializeComponent();
        }

        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm != null)
            {
                Vm.SaveSelected();
            }
        }

        private void ClearFormButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm != null)
            {
                Vm.ClearDraft();
                Vm.StatusMessage = "表单已清空。";
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm != null)
            {
                Vm.DeleteSelected();
            }
        }
    }
}
