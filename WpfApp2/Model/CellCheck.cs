using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CellCheck
{
    public class CellRecord
    {
        public string Barcode { get; set; }
        public double Voltage { get; set; }
        public string Result { get; set; }
        public DateTime Time { get; set; }
    
        public async Task Func()
        {
            await Task.Delay(1000);
        }
    }


}
