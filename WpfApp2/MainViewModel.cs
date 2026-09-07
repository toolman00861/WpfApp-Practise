using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using SqlSugar;
using WpfApp2.Database;
using WpfApp2.Model;
using WpfApp2.Services;

namespace WpfApp2
{
    /// <summary>
    /// 给界面绑定的数据。Draft* 相当于 Vue 里 data() 里的 form。
    /// 两个 UserControl 共用这一份，换页时表单和表格看到的是同一份数据。
    /// </summary>
    public class MainViewModel : BindableBase
    {
        private const int PageSize = 8;

        private int _pageIndex;

        private string _statusMessage = "填写表单后点「新增」。点表格一行可回填，改完可点「保存选中」。";
        private string _pageMessage;
        private string _draftBarcode = "";
        private string _draftVoltage = "";
        private string _draftResult = "OK";
        private CellRecord _selectedRecord;
        private IList<CellRecord> _pagedRecords = new List<CellRecord>();

        public List<string> ResultOptions { get; } = new List<string> { "OK", "NG" };

        /// <summary>
        /// 给「新增」按钮绑的命令。XAML 写 Command="{Binding AddCommand}"。
        /// </summary>
        public ICommand AddCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand PrevCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand SaveSpecCommand { get; }


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
            set
            {
                if (SetProperty(ref _draftBarcode, value) && AddCommand is RelayCommand addCommand)
                {
                    addCommand.RaiseCanExecuteChanged();
                }
            }
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
                }
                // 如果更新
                if (SaveCommand is RelayCommand saveCommand)
                {
                    saveCommand.RaiseCanExecuteChanged();
                }
                if (DeleteCommand is RelayCommand deleteCommand)
                {
                    deleteCommand.RaiseCanExecuteChanged();
                }

            }
        }

        public IList<CellRecord> PagedRecords
        {
            get { return _pagedRecords; }
            private set { SetProperty(ref _pagedRecords, value); }
        }

        public bool CanGoPrev
        {
            get { return _pageIndex > 0; }
        }

        public bool CanGoNext
        {
            get { return _pageIndex < GetTotalPages() - 1; }
        }

        public MainViewModel()
        {
            AddCommand = new RelayCommand(AddFromDraft, CanAddFromDraft);
            SaveCommand = new RelayCommand(SaveSelected, CanSaveSelected);
            ClearCommand = new RelayCommand(ClearDraft);
            DeleteCommand = new RelayCommand(DeleteSelected, CanSaveSelected);
            PrevCommand = new RelayCommand(GoToPrevPage, ()=> CanGoPrev);
            NextCommand = new RelayCommand(GoToNextPage, () => CanGoNext);
            SeedDemoData();
            RefreshPage();
            SpecStore.Changed += OnSpecChanged;
        }

        private bool CanSaveSelected()
        {
            return _selectedRecord != null;
        }

        private bool CanAddFromDraft()
        {
            return !string.IsNullOrWhiteSpace(DraftBarcode);
        }

        private void OnSpecChanged(object sender, EventArgs e)
        {
            ReapplySpec();
        }

        /// <summary>
        /// 阈值变了：每条记录的 Result 重新判定，再换一份 PagedRecords 让表格重绑。
        /// </summary>
        public void ReapplySpec()
        {
            var all = Db.Client.Queryable<CellRecord>().ToList();
            foreach (var record in all)
            {
                record.RecalcResult();
            }

            if (all.Count > 0)
            {
                Db.Client.Updateable(all).ExecuteCommand();
            }

            RefreshPage();
        }

        public void AddFromDraft()
        {
            CellRecord record;
            if (!TryReadDraft(out record))
            {
                return;
            }

            record.Time = DateTime.Now;
            try
            {
                Db.Client.Insertable(record).ExecuteCommand();
            }
            catch (Exception ex)
            {
                AppLog.Error("新增失败 " + record.Barcode, ex);
                StatusMessage = "新增失败，详见日志。";
                return;
            }

            _pageIndex = 0;
            ClearDraft();
            RefreshPage();
            StatusMessage = "已新增 " + record.Barcode + "。";
            AppLog.Info("新增 " + record.Barcode);
        }

        public void SaveSelected()
        {
            if (SelectedRecord == null)
            {
                StatusMessage = "请先在表格里选中一行。";
                return;
            }

            CellRecord draft;
            if (!TryReadDraft(out draft))
            {
                return;
            }

            SelectedRecord.Barcode = draft.Barcode;
            SelectedRecord.Voltage = draft.Voltage;
            SelectedRecord.Result = draft.Result;
            try
            {
                Db.Client.Updateable(SelectedRecord).ExecuteCommand();
            }
            catch (Exception ex)
            {
                AppLog.Error("保存失败 " + draft.Barcode, ex);
                StatusMessage = "保存失败，详见日志。";
                return;
            }

            RefreshPage();
            StatusMessage = "已保存 " + draft.Barcode + "。";
            AppLog.Info("保存 " + draft.Barcode);
        }

        public void ClearDraft()
        {
            SelectedRecord = null;
            DraftBarcode = "";
            DraftVoltage = "";
        }

        public void DeleteSelected()
        {
            var selected = SelectedRecord;
            if (selected == null)
            {
                StatusMessage = "请先在表格里选中一行。";
                return;
            }

            try
            {
                Db.Client.Deleteable(selected).ExecuteCommand();
            }
            catch (Exception ex)
            {
                AppLog.Error("删除失败 " + selected.Barcode, ex);
                StatusMessage = "删除失败，详见日志。";
                return;
            }

            ClearDraft();
            RefreshPage();
            StatusMessage = "已删除 " + selected.Barcode + "。";
            AppLog.Info("删除 " + selected.Barcode);
        }

        public void GoToPrevPage()
        {
            if (_pageIndex > 0)
            {
                _pageIndex--;
                RefreshPage();
            }
        }

        public void GoToNextPage()
        {
            if (_pageIndex < GetTotalPages() - 1)
            {
                _pageIndex++;
                RefreshPage();
            }
        }

        private bool TryReadDraft(out CellRecord record)
        {
            record = null;
            string barcode = (DraftBarcode ?? "").Trim();
            if (string.IsNullOrEmpty(barcode))
            {
                StatusMessage = "条码不能为空。";
                return false;
            }

            double voltage;
            if (!double.TryParse((DraftVoltage ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out voltage)
                && !double.TryParse((DraftVoltage ?? "").Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out voltage))
            {
                StatusMessage = "电压必须是数字，例如 3.72。";
                return false;
            }

            record = new CellRecord
            {
                Barcode = barcode,
                Voltage = voltage
            };
            record.RecalcResult();
            return true;
        }

        private void RefreshPage()
        {
            int totalPages = GetTotalPages();
            if (_pageIndex >= totalPages)
            {
                _pageIndex = totalPages - 1;
            }
            if (_pageIndex < 0)
            {
                _pageIndex = 0;
            }

            var keep = SelectedRecord;
            var page = Db.Client.Queryable<CellRecord>()
                .OrderBy(x => x.Id, OrderByType.Desc)
                .Skip(_pageIndex * PageSize)
                .Take(PageSize)
                .ToList();

            PagedRecords = page;
            if (keep != null)
            {
                SelectedRecord = page.FirstOrDefault(x => x.Id == keep.Id);
            }

            PageMessage = string.Format(
                "第 {0} / {1} 页（共 {2} 条）",
                _pageIndex + 1,
                totalPages,
                GetTotalCount());

            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            (PrevCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private int GetTotalPages()
        {
            var count = Db.Client.Queryable<CellRecord>().Count();
            if (count == 0) return 1;
            return (int)Math.Ceiling(count / (double)PageSize);
        }

        private int GetTotalCount()
        {
            return Db.Client.Queryable<CellRecord>().Count();
        }

        private void SeedDemoData()
        {
            if (GetTotalCount() != 0)
            {
                return;
            }

            var random = new Random(1);
            var records = new List<CellRecord>();
            for (int i = 1; i <= 23; i++)
            {
                double voltage = 3.50 + random.NextDouble() * 0.40;
                var record = new CellRecord
                {
                    Barcode = "CELL-" + i.ToString("000"),
                    Voltage = Math.Round(voltage, 3),
                    Time = DateTime.Now.AddMinutes(-i)
                };
                record.RecalcResult();
                records.Add(record);
            }

            Db.Client.Insertable(records).ExecuteCommand();
        }
    }
}
