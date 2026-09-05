using System.Windows;
using System.Windows.Controls;

namespace WpfApp2.Component
{
    /// <summary>
    /// RecordView.xaml 的交互逻辑
    /// </summary>
    public partial class RecordView : UserControl
    {
        public RecordView()
        {
            InitializeComponent();
        }

        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm != null)
            {
                Vm.GoToPrevPage();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vm != null)
            {
                Vm.GoToNextPage();
            }
        }
    }
}
