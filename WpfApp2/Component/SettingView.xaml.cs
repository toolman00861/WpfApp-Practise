using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp2.Services;

namespace WpfApp2.Component
{
    /// <summary>
    /// SettingView.xaml 的交互逻辑
    /// </summary>
    public partial class SettingView : UserControl
    {
        private CancellationTokenSource _previewCts;

        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }

        public SettingView()
        {
            InitializeComponent();
            Loaded += SettingView_Loaded;
            Unloaded += SettingView_Unloaded;
            if (SpecStore.Spec != null)
            {
                MinVoltage.Text = SpecStore.Spec.VoltMin.ToString();
                MaxVoltage.Text = SpecStore.Spec.VoltMax.ToString();
                AppearanceSerialBox.Text = SpecStore.Spec.AppearanceCameraSerial ?? "";
                CodeSerialBox.Text = SpecStore.Spec.CodeCameraSerial ?? "";
            }
        }

        private void SettingView_Loaded(object sender, RoutedEventArgs e)
        {
            if (CameraHub.Get(CameraHub.Appearance) != null || CameraHub.Get(CameraHub.Code) != null)
            {
                StartPreview();
            }
        }

        private void SettingView_Unloaded(object sender, RoutedEventArgs e)
        {
            StopPreview();
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
            FillSerial(AppearanceSerialBox);
        }

        private void FillCode_Click(object sender, RoutedEventArgs e)
        {
            FillSerial(CodeSerialBox);
        }

        private void FillSerial(TextBox box)
        {
            CameraInfo info = DeviceList.SelectedItem as CameraInfo;
            if (info == null || string.IsNullOrEmpty(info.Serial))
            {
                SetStatus("请先刷新并选中一台。");
                return;
            }

            box.Text = info.Serial;
        }

        private void OpenStations_Click(object sender, RoutedEventArgs e)
        {
            ApplyCameraSerialsToSpec();
            StopPreview();
            bool ok = CameraHub.OpenStations();
            if (!ok)
            {
                SetStatus(CameraHub.LastError ?? "打开失败。");
                return;
            }

            StartPreview();
            SetStatus("工位已打开，正在预览。");
        }

        private void StartPreview_Click(object sender, RoutedEventArgs e)
        {
            if (CameraHub.Get(CameraHub.Appearance) == null && CameraHub.Get(CameraHub.Code) == null)
            {
                SetStatus("请先打开工位。");
                return;
            }

            StartPreview();
            SetStatus("预览已开始。");
        }

        private void StopPreview_Click(object sender, RoutedEventArgs e)
        {
            StopPreview();
            SetStatus("预览已停止。");
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
            StopPreview();
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

            SetStatus("已保存 " + path);
        }

        private void StartPreview()
        {
            StopPreview();
            _previewCts = new CancellationTokenSource();
            CancellationToken token = _previewCts.Token;
            Task.Run(() => PreviewLoop(CameraHub.Appearance, AppearancePreview, token));
            Task.Run(() => PreviewLoop(CameraHub.Code, CodePreview, token));
        }

        private void StopPreview()
        {
            if (_previewCts == null)
            {
                return;
            }

            _previewCts.Cancel();
            _previewCts.Dispose();
            _previewCts = null;
        }

        private void PreviewLoop(string station, Image target, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                CameraService camera = CameraHub.Get(station);
                if (camera == null)
                {
                    Thread.Sleep(200);
                    continue;
                }

                CameraFrame frame;
                if (!camera.TryGrabFrame(out frame, 400))
                {
                    continue;
                }

                // BeginInvoke：不要 Invoke，否则关预览时会和 UI 线程互相等。
                target.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        target.Source = ToBitmap(frame);
                    }
                }));
            }
        }

        private static BitmapSource ToBitmap(CameraFrame frame)
        {
            var bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null);
            bitmap.WritePixels(
                new Int32Rect(0, 0, frame.Width, frame.Height),
                frame.Bgr24,
                frame.Width * 3,
                0);
            bitmap.Freeze();
            return bitmap;
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

        private void SetStatus(string text)
        {
            if (Vm != null)
            {
                Vm.StatusMessage = text;
            }
        }
    }
}
