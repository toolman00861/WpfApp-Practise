using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WpfApp2
{
    /// <summary>
    /// 把「点下去做什么」和「现在能不能点」包成按钮能绑定的 ICommand。
    /// 同步：new RelayCommand(ClearDraft)
    /// 异步：new RelayCommand(AddFromDraftAsync, CanAddFromDraft)
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        /// <summary>
        /// ICommand 规定返回 void。异步命令只能写成 async void，按钮没法 await。
        /// 真正的业务在 Func&lt;Task&gt; 里，异常会被推进到 DispatcherUnhandledException。
        /// </summary>
        public async void Execute(object parameter)
        {
            if (_executeAsync != null)
            {
                await _executeAsync();
                return;
            }

            _execute();
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
