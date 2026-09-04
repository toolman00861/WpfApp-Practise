using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using WpfApp2.Model;

namespace WpfApp2
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();

        private readonly List<CellRecord> _allRecords = new List<CellRecord>();

        private int _pageIndex;
        private const int PageSize = 8;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            SeedDemoData();
            RefreshPage();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            CellRecord record;
            if (!TryReadDraft(out record))
            {
                return;
            }

            record.Time = DateTime.Now;
            _allRecords.Insert(0, record);
            _pageIndex = 0;
            _vm.ClearDraft();
            RefreshPage();
            _vm.StatusMessage = "已新增 " + record.Barcode + "。";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedRecord == null)
            {
                _vm.StatusMessage = "请先在表格里选中一行。";
                return;
            }

            CellRecord draft;
            if (!TryReadDraft(out draft))
            {
                return;
            }

            _vm.SelectedRecord.Barcode = draft.Barcode;
            _vm.SelectedRecord.Voltage = draft.Voltage;
            _vm.SelectedRecord.Result = draft.Result;
            RefreshPage();
            _vm.StatusMessage = "已保存 " + draft.Barcode + "。";
        }

        private void ClearFormButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.ClearDraft();
            _vm.StatusMessage = "表单已清空。";
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _vm.SelectedRecord;
            if (selected == null)
            {
                _vm.StatusMessage = "请先在表格里选中一行。";
                return;
            }

            _allRecords.Remove(selected);
            _vm.ClearDraft();
            RefreshPage();
            _vm.StatusMessage = "已删除 " + selected.Barcode + "。";
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pageIndex > 0)
            {
                _pageIndex--;
                RefreshPage();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
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
            string barcode = (_vm.DraftBarcode ?? "").Trim();
            if (string.IsNullOrEmpty(barcode))
            {
                _vm.StatusMessage = "条码不能为空。";
                return false;
            }

            double voltage;
            if (!double.TryParse((_vm.DraftVoltage ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out voltage)
                && !double.TryParse((_vm.DraftVoltage ?? "").Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out voltage))
            {
                _vm.StatusMessage = "电压必须是数字，例如 3.72。";
                return false;
            }

            record = new CellRecord
            {
                Barcode = barcode,
                Voltage = voltage,
                Result = string.IsNullOrEmpty(_vm.DraftResult) ? "OK" : _vm.DraftResult
            };
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

            var page = _allRecords
                .Skip(_pageIndex * PageSize)
                .Take(PageSize)
                .ToList();

            var keep = _vm.SelectedRecord;
            RecordGrid.ItemsSource = page;
            if (keep != null && page.Contains(keep))
            {
                RecordGrid.SelectedItem = keep;
            }

            _vm.PageMessage = string.Format(
                "第 {0} / {1} 页（共 {2} 条）",
                _allRecords.Count == 0 ? 0 : _pageIndex + 1,
                _allRecords.Count == 0 ? 0 : totalPages,
                _allRecords.Count);

            PrevButton.IsEnabled = _pageIndex > 0;
            NextButton.IsEnabled = _pageIndex < totalPages - 1 && _allRecords.Count > 0;
        }

        private int GetTotalPages()
        {
            if (_allRecords.Count == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(_allRecords.Count / (double)PageSize);
        }

        private void SeedDemoData()
        {
            var random = new Random(1);
            for (int i = 1; i <= 23; i++)
            {
                double voltage = 3.50 + random.NextDouble() * 0.40;
                _allRecords.Add(new CellRecord
                {
                    Barcode = "CELL-" + i.ToString("000"),
                    Voltage = Math.Round(voltage, 3),
                    Result = voltage >= 3.60 ? "OK" : "NG",
                    Time = DateTime.Now.AddMinutes(-i)
                });
            }
        }
    }
}
