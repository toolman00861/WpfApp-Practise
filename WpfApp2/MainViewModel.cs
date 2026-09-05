using System;
using System.Collections.Generic;
using WpfApp2.Model;

namespace WpfApp2
{
    /// <summary>
    /// 给界面绑定的数据。Draft* 相当于 Vue 里 data() 里的 form。
    /// </summary>
    public class MainViewModel : BindableBase
    {
        private string _statusMessage = "填写表单后点「新增」。点表格一行可回填，改完可点「保存选中」。";
        private string _pageMessage;
        private string _draftBarcode = "";
        private string _draftVoltage = "";
        private string _draftResult = "OK";
        private CellRecord _selectedRecord;

        public List<string> ResultOptions { get; } = new List<string> { "OK", "NG" };

        public string StatusMessage
        {
            get { return _statusMessage; }
            set { SetProperty(ref _statusMessage, value); }
        }

        public string PageMessage
        {
            get { return _pageMessage; }
            set { SetProperty(ref _pageMessage, value); }
        }

        public string DraftBarcode
        {
            get { return _draftBarcode; }
            set { SetProperty(ref _draftBarcode, value); }
        }

        public string DraftVoltage
        {
            get { return _draftVoltage; }
            set { SetProperty(ref _draftVoltage, value); }
        }

        [Obsolete]
        public string DraftResult
        {
            get { return _draftResult; }
            set { SetProperty(ref _draftResult, value); }
        }

        public CellRecord SelectedRecord
        {
            get { return _selectedRecord; }
            set
            {
                if (!SetProperty(ref _selectedRecord, value))
                {
                    return;
                }

                if (value != null)
                {
                    DraftBarcode = value.Barcode;
                    DraftVoltage = value.Voltage.ToString("F3");
                    //DraftResult = value.Result;
                }
            }
        }

        public void ClearDraft()
        {
            SelectedRecord = null;
            DraftBarcode = "";
            DraftVoltage = "";
            //DraftResult = "OK";
        }
    }
}
