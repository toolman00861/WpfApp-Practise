using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfApp2.Services;

namespace WpfApp2.Component
{
    /// <summary>
    /// 练习 22 页。职责只有三件：点按钮、显示阶段、定时把四个线圈读到屏幕上。
    /// Modbus 读写和握手状态机全在 HslService，本页不要 new ModbusTcpNet。
    ///
    /// 按钮对照服务方法：
    ///   连接       → HslService.ConnectAsync
    ///   断开       → HslService.DisconnectAsync（内部会先停握手）
    ///   开始握手   → HslService.StartHandshake(OnPhase)
    ///   停止握手   → HslService.StopHandshakeAsync
    /// </summary>
    public partial class HSLTest : UserControl
    {
        /// <summary>
        /// 每 400ms 读四个线圈刷新格子。握手循环自己是 200ms，这里慢一点即可，避免和循环抢同一条 TCP。
        /// DispatcherTimer 跑在 UI 线程，可以直接改 TextBlock。
        /// </summary>
        private readonly DispatcherTimer _coilTimer = new DispatcherTimer();

        /// <summary>主窗口底栏。DataContext 是 MainWindow 设的 MainViewModel。</summary>
        private MainViewModel Vm
        {
            get { return DataContext as MainViewModel; }
        }

        public HSLTest()
        {
            InitializeComponent();
            _coilTimer.Interval = TimeSpan.FromMilliseconds(400);
            _coilTimer.Tick += CoilTimer_Tick;
            Loaded += HSLTest_Loaded;
            Unloaded += HSLTest_Unloaded;
        }

        /// <summary>
        /// 换到本页时：同步当前阶段；若已经连着（从别的页切回来），把定时器再打开。
        /// 握手循环不随换页停止，因为它活在 HslService 静态字段里。
        /// </summary>
        private void HSLTest_Loaded(object sender, RoutedEventArgs e)
        {
            PhaseText.Text = "阶段：" + (HslService.Phase ?? "未连接");
            if (HslService.IsConnected)
            {
                _coilTimer.Start();
            }
        }

        /// <summary>离开本页只停定时器，不断 TCP、不停握手。避免切去设置页握手就断了。</summary>
        private void HSLTest_Unloaded(object sender, RoutedEventArgs e)
        {
            _coilTimer.Stop();
        }

        /// <summary>
        /// 连 Demo：127.0.0.1:502。失败多半是没开 HslCommunication 的 Modbus Tcp Server。
        /// async void 是 WPF 按钮的常规写法：事件不能返回 Task。
        /// 这里不加 ConfigureAwait(false)，后面要改 PhaseText，必须留在 UI 线程。
        /// </summary>
        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("正在连接 127.0.0.1:502…");
            bool ok = await HslService.ConnectAsync();
            PhaseText.Text = "阶段：" + HslService.Phase;
            if (!ok)
            {
                SetStatus(HslService.LastError ?? "连接失败。先开 HslCommunication Demo 的 Modbus Tcp Server。");
                return;
            }

            _coilTimer.Start();
            RefreshCoils();
            SetStatus("已连接。拨线圈 0 后点「开始握手」。");
        }

        /// <summary>断开：先停定时器，再让服务停握手、关 TCP。格子改回问号。</summary>
        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            _coilTimer.Stop();
            await HslService.DisconnectAsync();
            PhaseText.Text = "阶段：" + HslService.Phase;
            Coil0Text.Text = "0 请检测 = ?";
            Coil1Text.Text = "1 检测中 = ?";
            Coil2Text.Text = "2 结果OK = ?";
            Coil3Text.Text = "3 完成 = ?";
            SetStatus("已断开。");
        }

        /// <summary>
        /// 把 OnPhase 交给服务：循环每换阶段会回调一次。
        /// StartHandshake 立刻返回，while 在线程池里跑，所以界面不会卡住。
        /// </summary>
        private void StartHandshake_Click(object sender, RoutedEventArgs e)
        {
            if (!HslService.StartHandshake(OnPhase))
            {
                SetStatus(HslService.LastError ?? "无法开始握手。");
                return;
            }

            SetStatus("握手循环已开。在 Demo 里把线圈 0 拨成 1。");
        }

        /// <summary>只停循环，不断 TCP。四个线圈仍可被定时器刷新，方便看停在哪一步。</summary>
        private async void StopHandshake_Click(object sender, RoutedEventArgs e)
        {
            await HslService.StopHandshakeAsync();
            PhaseText.Text = "阶段：" + HslService.Phase;
            SetStatus("握手已停止。");
        }

        /// <summary>
        /// 握手线程调用。WPF 规定只有 UI 线程能改控件，所以 BeginInvoke 切回去。
        /// 用 BeginInvoke 而不是 Invoke：循环不等界面画完，避免关页时互相等。
        /// </summary>
        private void OnPhase(string phase)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PhaseText.Text = "阶段：" + phase;
            }));
        }

        /// <summary>定时器心跳：把服务里的 Phase 和四个线圈同步到屏幕。</summary>
        private void CoilTimer_Tick(object sender, EventArgs e)
        {
            if (!HslService.IsConnected)
            {
                return;
            }

            RefreshCoils();
            if (!string.IsNullOrEmpty(HslService.Phase))
            {
                PhaseText.Text = "阶段：" + HslService.Phase;
            }
        }

        /// <summary>四个格子各读一次。显示用，不参与握手判断；判断只在 HandshakeLoop 里做。</summary>
        private void RefreshCoils()
        {
            Coil0Text.Text = FormatCoil(HslService.CoilRequest, "请检测");
            Coil1Text.Text = FormatCoil(HslService.CoilBusy, "检测中");
            Coil2Text.Text = FormatCoil(HslService.CoilResult, "结果OK");
            Coil3Text.Text = FormatCoil(HslService.CoilDone, "完成");
        }

        private static string FormatCoil(string address, string name)
        {
            bool value;
            if (!HslService.TryReadCoil(address, out value))
            {
                return address + " " + name + " = 读失败";
            }

            return address + " " + name + " = " + (value ? "1" : "0");
        }

        /// <summary>写主窗口底栏，和设置页 SetStatus 同一条绑定。</summary>
        private void SetStatus(string text)
        {
            if (Vm != null)
            {
                Vm.StatusMessage = text;
            }
        }
    }
}
