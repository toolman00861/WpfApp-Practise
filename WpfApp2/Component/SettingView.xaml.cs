using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
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
                AppearanceSerialBox.Text = SpecStore.Spec.AppearanceCameraSerial ?? "";
                CodeSerialBox.Text = SpecStore.Spec.CodeCameraSerial ?? "";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(MaxVoltage.Text, out var maxV)
                && double.TryParse(MinVoltage.Text, out var minV))
            {
                if (maxV > minV)
                {
                    ApplyCameraSerialsToSpec();
                    SpecStore.Spec.VoltMax = maxV;
                    SpecStore.Spec.VoltMin = minV;
                    SpecStore.Save();
                    SetStatus("保存成功");
                }
                else
                {
                    SetStatus("输入有误");
                }
            }
        }

        private void RefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            var list = CameraHub.Enumerate();
            DeviceList.ItemsSource = list;
            if (list.Count == 0)
            {
                SetStatus(CameraHub.LastError ?? "没有发现相机。");
            }
            else
            {
                DeviceList.SelectedIndex = 0;
                SetStatus("发现 " + list.Count + " 台。把 Vir… 填到工位后打开。");
            }
        }

        private void FillAppearance_Click(object sender, RoutedEventArgs e)
        {
            CameraInfo info = DeviceList.SelectedItem as CameraInfo;
            if (info == null || string.IsNullOrEmpty(info.Serial))
            {
                SetStatus("请先刷新并选中一台。");
                return;
            }

            AppearanceSerialBox.Text = info.Serial;
        }

        private void FillCode_Click(object sender, RoutedEventArgs e)
        {
            CameraInfo info = DeviceList.SelectedItem as CameraInfo;
            if (info == null || string.IsNullOrEmpty(info.Serial))
            {
                SetStatus("请先刷新并选中一台。");
                return;
            }

            CodeSerialBox.Text = info.Serial;
        }

        private void OpenStations_Click(object sender, RoutedEventArgs e)
        {
            ApplyCameraSerialsToSpec();
            bool ok = CameraHub.OpenStations();
            SetStatus(ok ? "工位已打开并开始采集。" : (CameraHub.LastError ?? "打开失败。"));
        }

        private async void GrabAppearance_Click(object sender, RoutedEventArgs e)
        {
            await GrabAsync(CameraHub.Appearance);
        }

        private async void GrabCode_Click(object sender, RoutedEventArgs e)
        {
            await GrabAsync(CameraHub.Code);
        }

        private void CloseStations_Click(object sender, RoutedEventArgs e)
        {
            CameraHub.CloseAll();
            SetStatus("已关闭全部相机。");
        }

        private async Task GrabAsync(string station)
        {
            CameraService camera = CameraHub.Get(station);
            if (camera == null)
            {
                SetStatus("工位「" + station + "」未打开。");
                return;
            }

            string path = Path.Combine(AppContext.BaseDirectory, "Captures", station + ".bmp");
            bool ok = await Task.Run(() => camera.TryGrabOne(path));
            if (!ok)
            {
                SetStatus(camera.LastError ?? "取图失败。");
                return;
            }

            ShowPreview(path);
            SetStatus("已保存 " + path);
        }

        private void ApplyCameraSerialsToSpec()
        {
            if (SpecStore.Spec == null)
            {
                return;
            }

            SpecStore.Spec.AppearanceCameraSerial = (AppearanceSerialBox.Text ?? "").Trim();
            SpecStore.Spec.CodeCameraSerial = (CodeSerialBox.Text ?? "").Trim();
        }

        private void ShowPreview(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            PreviewImage.Source = image;
        }

        private void SetStatus(string text)
        {
            if (Vm != null)
            {
                Vm.StatusMessage = text;
            }
        }
    }
}
