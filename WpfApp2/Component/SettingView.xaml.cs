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
    /// 设置页。海康链路在界面上的对照（官方 BasicDemo 按钮 → 本页）：
    /// 刷新设备 ≈ bnEnum；打开工位 ≈ bnOpen + bnStartGrab；
    /// 预览循环 ≈ ReceiveThreadProcess（拉模式 GetImageBuffer）；
    /// 抓图存 BMP ≈ bnSaveBmp；关闭全部 ≈ bnClose。
    /// 界面不直接碰 CCamera，一律经 CameraHub。
    /// </summary>
    public partial class SettingView : UserControl
    {
        private CancellationTokenSource _previewCts;
        private Task _appearancePreviewTask;
        private Task _codePreviewTask;
        private bool _openingStations;

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

        /// <summary>对照 BasicDemo.bnEnum_Click：只 EnumDevices，不 Open。</summary>
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

        /// <summary>对照官方连点 bnOpen、bnStartGrab。按序列号打开外观/码面两台并开始采集。</summary>
        private async void OpenStations_Click(object sender, RoutedEventArgs e)
        {
            if (_openingStations)
            {
                return;
            }

            ApplyCameraSerialsToSpec();
            StopPreview();
            _openingStations = true;
            SetStatus("正在打开工位…");
            try
            {
                // 只把 SDK 打开丢进线程池。SetStatus / StartPreview 会碰界面，必须 await 回来再做。
                bool ok = await Task.Run(() => CameraHub.OpenStations());
                if (!ok)
                {
                    SetStatus(CameraHub.LastError ?? "打开失败。");
                    return;
                }

                StartPreview();
                SetStatus("工位已打开，正在预览。");
            }
            catch (Exception ex)
            {
                AppLog.Error("打开工位异常", ex);
                SetStatus("打开工位失败，见日志。");
            }
            finally
            {
                _openingStations = false;
            }
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

        private async void AppearanceToGray_Click(object sender, RoutedEventArgs e)
        {
            CameraFrame input = await GrabStationFrameAsync(CameraHub.Appearance);
            if (input == null)
            {
                return;
            }

            CameraFrame output = null;
            bool ok = await Task.Run(() => HalconService.TryToGray(input, out output));
            if (!ok)
            {
                SetStatus(HalconService.LastError ?? "转灰度失败。");
                return;
            }

            ProcessPreview.Source = ToBitmap(output);
            SetStatus("灰度完成 " + output.Width + "x" + output.Height);
        }

        private async void AppearanceMeasure_Click(object sender, RoutedEventArgs e)
        {
            CameraFrame input = await GrabStationFrameAsync(CameraHub.Appearance);
            if (input == null)
            {
                return;
            }

            CameraFrame output = null;
            double mean = 0;
            double deviation = 0;
            bool ok = await Task.Run(() => HalconService.TryMeasureGray(input, out output, out mean, out deviation));
            if (!ok)
            {
                SetStatus(HalconService.LastError ?? "测亮度失败。");
                return;
            }

            ProcessPreview.Source = ToBitmap(output);
            SetStatus("均值 " + mean.ToString("0.0") + "，标准差 " + deviation.ToString("0.0") + "（大约 0～255）");
        }

        private async void AppearanceThreshold_Click(object sender, RoutedEventArgs e)
        {
            int maxGray;
            if (!int.TryParse(ThresholdMinBox.Text, out int minGray) || !int.TryParse(ThresholdMaxBox.Text, out maxGray))
            {
                SetStatus("阈值请填 0～255 的整数。");
                return;
            }

            CameraFrame input = await GrabStationFrameAsync(CameraHub.Appearance);
            if (input == null)
            {
                return;
            }

            CameraFrame output = null;
            bool ok = await Task.Run(() => HalconService.TryThreshold(input, minGray, maxGray, out output));
            if (!ok)
            {
                SetStatus(HalconService.LastError ?? "二值化失败。");
                return;
            }

            ProcessPreview.Source = ToBitmap(output);
            SetStatus("二值化完成，区间 [" + minGray + ", " + maxGray + "]");
        }

        private async void GrabAppearance_Click(object sender, RoutedEventArgs e)
        {
            await GrabAsync(CameraHub.Appearance);
        }

        private async void GrabCode_Click(object sender, RoutedEventArgs e)
        {
            await GrabAsync(CameraHub.Code);
        }

        /// <summary>对照 BasicDemo.bnClose_Click。</summary>
        private void CloseStations_Click(object sender, RoutedEventArgs e)
        {
            ReleaseHardware();
            SetStatus("已关闭全部相机。");
        }

        /// <summary>
        /// 先等预览线程离开 GetImageBuffer，再 StopGrabbing / CloseDevice / DestroyHandle。
        /// 关窗口和「关闭全部」都走这里，避免句柄还在预览里、下一轮 Open 报占用。
        /// </summary>
        internal void ReleaseHardware()
        {
            StopPreview();
            CameraHub.CloseAll();
        }

        /// <summary>拷贝预览缓存的最新帧给 Halcon。不再 GetImageBuffer。</summary>
        private async Task<CameraFrame> GrabStationFrameAsync(string station)
        {
            CameraService camera = CameraHub.Get(station);
            if (camera == null)
            {
                SetStatus("工位「" + station + "」未打开。先打开工位再处理。");
                return null;
            }

            CameraFrame frame = null;
            string error = null;
            bool ok = await Task.Run(() =>
            {
                bool result = camera.TryCloneLatestFrame(out frame);
                if (!result)
                {
                    error = camera.LastError;
                }

                return result;
            });
            if (!ok || frame == null)
            {
                SetStatus(error ?? "还没有预览帧。");
                return null;
            }

            return frame;
        }

        /// <summary>对照 BasicDemo.bnSaveBmp_Click：GetImageBuffer 后 SaveImageToFile。</summary>
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
            _appearancePreviewTask = Task.Run(() => PreviewLoop(CameraHub.Appearance, AppearancePreview, token));
            _codePreviewTask = Task.Run(() => PreviewLoop(CameraHub.Code, CodePreview, token));
        }

        private void StopPreview()
        {
            if (_previewCts == null)
            {
                return;
            }

            _previewCts.Cancel();
            WaitPreviewExit();
            _previewCts.Dispose();
            _previewCts = null;
            _appearancePreviewTask = null;
            _codePreviewTask = null;
        }

        /// <summary>GetImageBuffer 最多堵约 400ms，这里多等一会让循环自己退出，再关设备。</summary>
        private void WaitPreviewExit()
        {
            Task appearance = _appearancePreviewTask;
            Task code = _codePreviewTask;
            try
            {
                if (appearance != null && code != null)
                {
                    Task.WaitAll(new[] { appearance, code }, 1000);
                }
                else if (appearance != null)
                {
                    appearance.Wait(1000);
                }
                else if (code != null)
                {
                    code.Wait(1000);
                }
            }
            catch (AggregateException)
            {
            }
        }

        /// <summary>
        /// 对照 BasicDemo.ReceiveThreadProcess：循环 GetImageBuffer，并写入最新帧缓存。
        /// 官方用 DisplayOneFrame 画到 PictureBox；WPF 没有这套 HWND 接口，所以转 BitmapSource。
        /// 这是外观/码面各自唯一的取流循环，Halcon 不再另开 GetImageBuffer。
        /// </summary>
        private void PreviewLoop(string station, Image target, CancellationToken token)
        {
            try
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
                        try
                        {
                            if (!token.IsCancellationRequested)
                            {
                                target.Source = ToBitmap(frame);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    }));
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// 海康 BGR8_Packed → WPF BitmapSource。stride = Width * 3，没有 BMP 那种 4 字节行对齐。
        /// </summary>
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
