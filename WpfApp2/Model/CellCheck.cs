using System;

namespace WpfApp2.Model
{
    /// <summary>
    /// 一条电芯检测记录。DataGrid 的每一行对应一个对象。
    /// 属性名会和表格列的 Binding 对上。
    /// </summary>
    public class CellRecord
    {
        public string Barcode { get; set; }
        public double Voltage { get; set; }
        public string Result { get; set; }
        public DateTime Time { get; set; }
    }
}
