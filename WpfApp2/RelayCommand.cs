using System;
using System.Windows.Input;

namespace WpfApp2
{
    /// <summary>
    /// 把「点下去做什么」和「现在能不能点」包成按钮能绑定的 ICommand。
    /// 用法：new RelayCommand(AddFromDraft, CanAddFromDraft)
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// 条件变了（例如条码从空变成有字）时喊一声，按钮会重新问 CanExecute。
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
