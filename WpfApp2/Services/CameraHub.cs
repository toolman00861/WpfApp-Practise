using System;
using System.Collections.Generic;
using MvCamCtrl.NET;

namespace WpfApp2.Services
{
    /// <summary>
    /// 进程级相机入口。官方 BasicDemo 把一台 <c>CCamera</c> 直接放在 Form 里；
    /// 我们拆成两层：Hub 管「有几台、谁打开」，CameraService 管「这一台的 SDK 会话」。
    ///
    /// 对照官方按钮：
    /// Enumerate     ≈ bnEnum / DeviceListAcq（只搜，不占设备）
    /// OpenStations  ≈ 连续两次 bnOpen + bnStartGrab（外观、码面各一台）
    /// Get           ≈ 取出 Form 里那台 m_MyCamera（借用，不要 Dispose）
    /// CloseAll      ≈ bnClose（StopGrabbing → CloseDevice → DestroyHandle）
    ///
    /// 静态的是 Hub，会话仍是实例。调用方 Get 到的对象不要 Dispose。
    /// </summary>
    public static class CameraHub
    {
        public const string Appearance = "外观";
        public const string Code = "码面";

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, CameraService> Stations =
            new Dictionary<string, CameraService>();

        public static string LastError { get; private set; }

        /// <summary>
        /// 对照 <c>CSystem.GetSDKVersion()</c>。只记日志、准备容器，不 Enum、不 Open。
        /// 读版本失败通常是没装 MVS，或进程位数和 MvCamCtrl.Net.dll 不一致。
        /// </summary>
        public static void Init()
        {
            lock (Sync)
            {
                LastError = null;
                try
                {
                    AppLog.Info("MVS SDK " + MvCamCtrl.NET.CSystem.GetSDKVersion());
                }
                catch (Exception ex)
                {
                    AppLog.Error("读取 SDK 版本失败（未安装 MVS 或进程位数不匹配）", ex);
                }
            }
        }

        /// <summary>
        /// 对照 BasicDemo.DeviceListAcq：CSystem.EnumDevices → 转成可读摘要。
        /// 不 Open、不占用。界面「刷新设备」走这里。
        /// </summary>
        public static IReadOnlyList<CameraInfo> Enumerate()
        {
            lock (Sync)
            {
                LastError = null;
                try
                {
                    int nRet = HikSdk.EnumerateRaw(out List<CCameraInfo> raw);
                    if (nRet != CErrorDefine.MV_OK)
                    {
                        LastError = "枚举失败 " + HikSdk.FormatError(nRet);
                        AppLog.Warn(LastError);
                        return new CameraInfo[0];
                    }

                    var list = new List<CameraInfo>(raw.Count);
                    for (int i = 0; i < raw.Count; i++)
                    {
                        list.Add(HikSdk.Describe(raw[i]));
                    }

                    AppLog.Info("枚举到 " + list.Count + " 台相机");
                    return list;
                }
                catch (Exception ex)
                {
                    LastError = "枚举异常：" + ex.Message;
                    AppLog.Error("枚举相机失败", ex);
                    return new CameraInfo[0];
                }
            }
        }

        /// <summary>
        /// 按 spec.json 里的两个序列号打开工位并开始采集。
        /// 一台相机只能独占打开一次（官方默认 MV_ACCESS_EXCLUSIVE），两个工位不能填同一个 Vir…。
        /// </summary>
        public static bool OpenStations()
        {
            lock (Sync)
            {
                LastError = null;
                CloseAllCore();

                VoltageSpecSerials serials = ReadSerials();
                string appearance = (serials.Appearance ?? "").Trim();
                string code = (serials.Code ?? "").Trim();
                if (!string.IsNullOrEmpty(appearance)
                    && !string.IsNullOrEmpty(code)
                    && string.Equals(appearance, code, StringComparison.OrdinalIgnoreCase))
                {
                    LastError = "两个工位填了同一台 " + appearance + "。一台相机只能独占打开一次，请给码面换另一台的 Vir…，或先清空码面只练一台。";
                    AppLog.Warn(LastError);
                    return false;
                }

                int planned = 0;
                int opened = 0;

                if (!string.IsNullOrEmpty(appearance))
                {
                    planned++;
                    if (OpenOne(Appearance, appearance))
                    {
                        opened++;
                    }
                }

                if (!string.IsNullOrEmpty(code))
                {
                    planned++;
                    if (OpenOne(Code, code))
                    {
                        opened++;
                    }
                }

                if (planned == 0)
                {
                    LastError = "还没有配置工位序列号。把刷新列表里的 Vir… 填到外观/码面再打开。";
                    AppLog.Warn(LastError);
                    return false;
                }

                if (opened != planned)
                {
                    if (string.IsNullOrEmpty(LastError))
                    {
                        LastError = "部分工位打开失败，见日志。";
                    }

                    return false;
                }

                AppLog.Info("工位已打开 " + opened + " 台");
                return true;
            }
        }

        /// <summary>借用会话。不要 Dispose，生命周期归 Hub。</summary>
        public static CameraService Get(string station)
        {
            lock (Sync)
            {
                CameraService camera;
                if (station != null && Stations.TryGetValue(station, out camera))
                {
                    return camera;
                }

                return null;
            }
        }

        public static bool IsReady(string station)
        {
            CameraService camera = Get(station);
            return camera != null && camera.IsOpen && camera.IsGrabbing;
        }

        public static void CloseAll()
        {
            lock (Sync)
            {
                CloseAllCore();
            }
        }

        /// <summary>进程退出时走这里。对照官方 bnClose：先停流再关设备再毁句柄。</summary>
        public static void Shutdown()
        {
            lock (Sync)
            {
                CloseAllCore();
                AppLog.Info("CameraHub 已关闭");
            }
        }

        /// <summary>
        /// 一台工位的完整打开：CameraService.Open（CreateHandle→OpenDevice）再 StartGrabbing。
        /// 对照官方：bnOpen_Click 之后再点 bnStartGrab。失败则立刻 Dispose，避免句柄泄漏。
        /// </summary>
        private static bool OpenOne(string station, string serial)
        {
            var camera = new CameraService();
            if (!camera.Open(serial))
            {
                LastError = station + "：" + camera.LastError;
                camera.Dispose();
                return false;
            }

            if (!camera.StartGrabbing())
            {
                LastError = station + "：" + camera.LastError;
                camera.Dispose();
                return false;
            }

            Stations[station] = camera;
            AppLog.Info("工位 " + station + " → " + serial);
            return true;
        }

        private static void CloseAllCore()
        {
            foreach (CameraService camera in Stations.Values)
            {
                camera.Close();
            }

            Stations.Clear();
        }

        private static VoltageSpecSerials ReadSerials()
        {
            var spec = SpecStore.Spec;
            return new VoltageSpecSerials
            {
                Appearance = spec?.AppearanceCameraSerial,
                Code = spec?.CodeCameraSerial
            };
        }

        private struct VoltageSpecSerials
        {
            public string Appearance;
            public string Code;
        }
    }
}
