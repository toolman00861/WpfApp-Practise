using System;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.ModBus;

namespace WpfApp2.Services
{
    /// <summary>
    /// Modbus TCP 练习入口（库：HslCommunication）。
    /// 对照相机那一层：界面不直接碰 CCamera，这里界面也不直接碰 ModbusTcpNet。
    ///
    /// 练习 22：四个握手线圈跑一圈，仍连 Demo 服务器 127.0.0.1:502。
    /// 不接相机、Halcon、判定页。「检测」就是 Sleep(500)。
    ///
    /// 线圈点表（地址字符串与 Hsl Demo 一致，从 0 起）：
    ///   0 请检测 — 你在 Demo 界面拨
    ///   1 检测中 — C# 写
    ///   2 结果   — C# 写（练习里 1=OK）
    ///   3 完成   — C# 写
    ///
    /// 生命周期：Init 只 new 客户端 → 练习页点「连接」才 ConnectServer
    /// → 「开始握手」才 Task.Run 循环 → 关窗 Shutdown。
    /// </summary>
    public static class HslService
    {
        /// <summary>线圈 0：请检测。只有 0→1 上升沿才开一圈。</summary>
        public const string CoilRequest = "0";
        /// <summary>线圈 1：检测中。本机占着，告诉 PLC「正在做」。</summary>
        public const string CoilBusy = "1";
        /// <summary>线圈 2：结果。练习里写 true 表示 OK。</summary>
        public const string CoilResult = "2";
        /// <summary>线圈 3：完成。结果已写好，等 PLC 来清请检测。</summary>
        public const string CoilDone = "3";

        /// <summary>保护 _plc 的创建/销毁。握手循环不抢这把锁，避免读线圈被关连接堵住。</summary>
        private static readonly object Sync = new object();
        /// <summary>HSL 的 Modbus TCP 客户端。一个进程共用一台 Demo 服务器。</summary>
        private static ModbusTcpNet _plc;
        /// <summary>
        /// 握手循环的「停止开关」源头。点开始时 new，点停止时 Cancel()。
        /// 循环里拿的是它发出来的 Token，看到 IsCancellationRequested 就退出 while。
        /// </summary>
        private static CancellationTokenSource _handshakeCts;
        /// <summary>
        /// Task.Run 出来的那条后台任务。停止时必须 await 它，等 while 真的结束，
        /// 再去 ConnectClose，否则循环可能还在 ReadBool。
        /// </summary>
        private static Task _handshakeTask;

        /// <summary>最近一次失败原因。界面失败时读这个，和 CameraService.LastError 同一习惯。</summary>
        public static string LastError { get; private set; }
        /// <summary>给人看的阶段文案：未连接 / 轮询请检测 / 检测中 / 等 PLC 清请检测 …</summary>
        public static string Phase { get; private set; }
        /// <summary>ConnectServer 成功后为 true。读写作线圈前都要看这个。</summary>
        public static bool IsConnected { get; private set; }
        /// <summary>后台 HandshakeLoop 还在跑。防止连点两次「开始握手」开两条循环。</summary>
        public static bool IsHandshaking
        {
            get { return _handshakeTask != null && !_handshakeTask.IsCompleted; }
        }

        /// <summary>
        /// 只 new ModbusTcpNet，不连网。对照 CameraHub.Init：启动时探活，不占设备。
        /// 真正 TCP 三次握手在 <see cref="ConnectAsync"/>。
        /// </summary>
        public static void Init()
        {
            lock (Sync)
            {
                if (_plc != null)
                {
                    return;
                }

                // 站号默认 1，和 Hsl Demo 的 Modbus Tcp Server 出厂一致。
                _plc = new ModbusTcpNet("127.0.0.1", 502);
                Phase = "未连接";
                AppLog.Info("HslService 已创建 ModbusTcpNet 127.0.0.1:502");
            }
        }

        /// <summary>
        /// 对照练习 21：ConnectServer。界面点「连接」走这里。
        /// OperateResult 是 HSL 的统一返回包：IsSuccess / Message，不抛异常。
        /// ConfigureAwait(false)：服务层连完不必回到 UI 线程，避免和服务互相等。
        /// </summary>
        public static async Task<bool> ConnectAsync()
        {
            Init();
            LastError = null;
            OperateResult conn = await _plc.ConnectServerAsync().ConfigureAwait(false);
            IsConnected = conn.IsSuccess;
            if (!conn.IsSuccess)
            {
                Phase = "连接失败";
                return Fail(conn.Message);
            }

            Phase = "已连接，待机";
            AppLog.Info("Modbus 已连接 127.0.0.1:502");
            return true;
        }

        /// <summary>
        /// 先停握手再关 TCP。顺序不能反：循环还在 ReadBool 时 Close 会失败或读到半截。
        /// </summary>
        public static async Task DisconnectAsync()
        {
            await StopHandshakeAsync().ConfigureAwait(false);
            if (_plc == null)
            {
                IsConnected = false;
                Phase = "未连接";
                return;
            }

            await _plc.ConnectCloseAsync().ConfigureAwait(false);
            IsConnected = false;
            Phase = "未连接";
            AppLog.Info("Modbus 已断开");
        }

        /// <summary>
        /// 进程退出。对照 CameraHub.Shutdown。关窗走 MainWindow_Closing，比 OnExit 更早。
        /// </summary>
        public static async Task Shutdown()
        {
            try
            {
                await StopHandshakeAsync();
            }
            catch (Exception ex)
            {
                AppLog.Warn("停止握手时异常：" + ex.Message);
            }

            lock (Sync)
            {
                if (_plc != null)
                {
                    try
                    {
                        _plc.ConnectClose();
                    }
                    catch
                    {
                    }

                    _plc.Dispose();
                    _plc = null;
                }

                IsConnected = false;
                Phase = "已关闭";
            }
        }

        /// <summary>
        /// 读单个线圈。地址用字符串 "0"/"1"/…，和 Demo 界面上的线圈序号一致。
        /// HSL：ReadBool("0") ≈ Modbus 功能码 01（读线圈）。
        /// </summary>
        public static bool TryReadCoil(string address, out bool value)
        {
            value = false;
            if (_plc == null || !IsConnected)
            {
                return Fail("尚未连接 Demo 服务器。");
            }

            OperateResult<bool> r = _plc.ReadBool(address);
            if (!r.IsSuccess)
            {
                return Fail("读线圈 " + address + " 失败：" + r.Message);
            }

            value = r.Content;
            return true;
        }

        /// <summary>
        /// 写单个线圈。HSL：Write("1", true) ≈ 功能码 05（写单个线圈）。
        /// 线圈 0 练习里不要由本机写，留给你在 Demo 里拨，才能看出握手双方。
        /// </summary>
        public static bool TryWriteCoil(string address, bool value)
        {
            if (_plc == null || !IsConnected)
            {
                return Fail("尚未连接 Demo 服务器。");
            }

            OperateResult w = _plc.Write(address, value);
            if (!w.IsSuccess)
            {
                return Fail("写线圈 " + address + " 失败：" + w.Message);
            }

            return true;
        }

        /// <summary>
        /// 练习 22：启动握手循环。本方法立刻返回，真正的 while 在线程池里跑。
        /// onPhase 给界面刷新「阶段：xxx」；循环在后台线程调它，界面那边要用 Dispatcher 切回 UI。
        /// </summary>
        public static bool StartHandshake(Action<string> onPhase = null)
        {
            if (_plc == null || !IsConnected)
            {
                return Fail("请先连接 Demo 服务器。");
            }

            if (IsHandshaking)
            {
                return Fail("握手循环已在跑。");
            }

            LastError = null;
            // CTS = 对讲机主机；Token = 分机。Cancel() 之后循环里的灯会亮。
            _handshakeCts = new CancellationTokenSource();
            CancellationToken token = _handshakeCts.Token;
            // HandshakeLoop 里有 Sleep(200)，不能在 UI 线程跑，否则窗口卡住。
            _handshakeTask = Task.Run(() => HandshakeLoop(token, onPhase));
            Phase = "轮询请检测";
            Report(onPhase, Phase);
            AppLog.Info("握手循环已开始");
            return true;
        }

        /// <summary>
        /// 先 Cancel 再 await 任务。只 Cancel 不等待的话，循环可能还堵在 Sleep 或读线圈。
        /// OperationCanceledException：任务被取消时的正常退出，忽略即可。
        /// </summary>
        public static async Task StopHandshakeAsync()
        {
            CancellationTokenSource cts = _handshakeCts;
            Task task = _handshakeTask;
            if (cts == null)
            {
                return;
            }

            cts.Cancel();
            if (task != null)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    AppLog.Warn("握手任务结束异常：" + ex.Message);
                }
            }

            cts.Dispose();
            _handshakeCts = null;
            _handshakeTask = null;
            Phase = IsConnected ? "已连接，待机" : "未连接";
            AppLog.Info("握手循环已停止");
        }

        /// <summary>
        /// 握手状态机（练习 22 要跑通的一圈），在线程池线程上死循环直到 Cancel。
        ///
        /// 1. 每 200ms 读线圈 0（不要 10ms，Demo 和日志都会被刷爆）
        /// 2. 只有 0→1 上升沿才开检；电平一直为 1 不连开第二次
        /// 3. 写线圈 1 = true（检测中）
        /// 4. 假装检测 500ms，写线圈 2、3 = true
        /// 5. 等线圈 0 变 0（你在 Demo 把请检测清掉）
        /// 6. 本机清线圈 1、2、3，回到第 1 步
        ///
        /// 第 5 步若你一直不拨 0：阶段停在「等 PLC 清请检测」，外层不会再当成新的上升沿。
        /// </summary>
        private static void HandshakeLoop(CancellationToken token, Action<string> onPhase)
        {
            // 启动时线圈 0 可能已经是 1（上次没清）。默认 prev=true，这样不会立刻当成上升沿。
            bool prevRequest = true;
            if (TryReadCoil(CoilRequest, out bool initial))
            {
                prevRequest = initial;
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    Phase = "轮询请检测";
                    Report(onPhase, Phase);

                    if (!TryReadCoil(CoilRequest, out bool request))
                    {
                        Thread.Sleep(200);
                        continue;
                    }

                    // 上升沿：上一拍是 0，这一拍是 1。
                    bool rising = !prevRequest && request;
                    prevRequest = request;

                    if (!rising)
                    {
                        Thread.Sleep(200);
                        continue;
                    }

                    // —— 开始一圈 ——
                    Phase = "检测中";
                    Report(onPhase, Phase);
                    if (!TryWriteCoil(CoilBusy, true))
                    {
                        continue;
                    }

                    AppLog.Info("假装检测…");
                    // WaitOne(500) 等效 Sleep，但 Cancel 时能立刻醒，不用干等到 500ms。
                    if (token.WaitHandle.WaitOne(500))
                    {
                        break;
                    }

                    if (!TryWriteCoil(CoilResult, true))
                    {
                        continue;
                    }

                    if (!TryWriteCoil(CoilDone, true))
                    {
                        continue;
                    }

                    Phase = "等 PLC 清请检测";
                    Report(onPhase, Phase);
                    AppLog.Info("结果已写，等待线圈 0 变 0");

                    // 内层循环：只等线圈 0 变 0。请检测一直为 1 就停在这里，不要回到外层再触发。
                    while (!token.IsCancellationRequested)
                    {
                        if (!TryReadCoil(CoilRequest, out bool stillOn))
                        {
                            Thread.Sleep(200);
                            continue;
                        }

                        prevRequest = stillOn;
                        if (!stillOn)
                        {
                            break;
                        }

                        Thread.Sleep(200);
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    Phase = "清本机线圈";
                    Report(onPhase, Phase);
                    // 练习里 2 也一并清掉。现场点表若规定结果由 PLC 清，再改这里。
                    TryWriteCoil(CoilBusy, false);
                    TryWriteCoil(CoilResult, false);
                    TryWriteCoil(CoilDone, false);
                    AppLog.Info("一圈完成，回到轮询");
                }
                catch (Exception ex)
                {
                    Fail("握手异常：" + ex.Message);
                    Report(onPhase, Phase);
                    Thread.Sleep(500);
                }
            }
        }

        /// <summary>
        /// 把阶段文案推给界面。握手线程调这个；界面 OnPhase 里再 Dispatcher.BeginInvoke。
        /// 界面若抛异常，不能把后台循环打死，所以这里吞掉。
        /// </summary>
        private static void Report(Action<string> onPhase, string text)
        {
            try
            {
                onPhase?.Invoke(text);
            }
            catch
            {
            }
        }

        /// <summary>记下 LastError 并打日志，返回 false 给调用方。和 CameraService.Fail 同一习惯。</summary>
        private static bool Fail(string message)
        {
            LastError = message;
            AppLog.Warn(message);
            return false;
        }
    }
}
