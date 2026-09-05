using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp2.Services;

namespace WpfApp2.Component
{
    /// <summary>
    /// SettingView.xaml 的交互逻辑
    /// </summary>
    public partial class SettingView : UserControl
    {
        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }
        public SettingView()
        {
            InitializeComponent();
            if (SpecStore.Spec != null)
            {
                MinVoltage.Text = SpecStore.Spec.VoltMin.ToString();
                MaxVoltage.Text = SpecStore.Spec.VoltMax.ToString();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if(double.TryParse(MaxVoltage.Text, out var maxV) 
                && double.TryParse(MinVoltage.Text, out var minV))
            {
                if(maxV > minV)
                {
                    SpecStore.Spec.VoltMax = maxV;
                    SpecStore.Spec.VoltMin = minV;
                    SpecStore.Save();
                    Vm.StatusMessage = "保存成功";
                }
                else
                {
                    Vm.StatusMessage = "输入有误";
                }
            }
        }
    }
}
