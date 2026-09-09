using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp2.Model
{
    public class VoltageSpec
    {
        public double VoltMin { get; set; }
        public double VoltMax { get; set; }

        /// <summary>工位「外观」对应的相机序列号，例如 Vir93553065。</summary>
        public string AppearanceCameraSerial { get; set; }

        /// <summary>工位「码面」对应的相机序列号。</summary>
        public string CodeCameraSerial { get; set; }
    }
}
