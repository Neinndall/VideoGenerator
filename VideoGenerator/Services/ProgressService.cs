using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;

namespace VideoGenerator.Services
{
    public class ProgressService : ObservableObject
    {
        private const string ReadyStatus = "STABLE - READY";
        private const string CanceledStatus = "CANCELED - TASK ANNULLED";

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        private string _statusText = ReadyStatus;
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        private double _value;
        public double Value
        {
            get => _value;
            private set => SetProperty(ref _value, value);
        }

        private bool _isIndeterminate = true;
        private double _totalWork;
        private double _completedWork;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set => SetProperty(ref _isIndeterminate, value);
        }

        public void Start(string status = "PROCESSING MEDIA...", bool indeterminate = true)
        {
            Update(() =>
            {
                IsBusy = true;
                StatusText = NormalizeStatus(status);
                Value = 0;
                IsIndeterminate = indeterminate;
            });
        }

        public void StartWork(string status, double totalWork)
        {
            Update(() =>
            {
                _totalWork = Math.Max(0, totalWork);
                _completedWork = 0;
                IsBusy = true;
                StatusText = FormatWorkStatus(status);
                Value = 0;
                IsIndeterminate = _totalWork <= 0;
            });
        }

        public void Advance(double completedWork, string status = null)
        {
            Update(() =>
            {
                if (_totalWork <= 0) return;
                _completedWork = Math.Clamp(_completedWork + Math.Max(0, completedWork), 0, _totalWork);
                IsBusy = true;
                IsIndeterminate = false;
                Value = _completedWork / _totalWork * 100.0;
                if (!string.IsNullOrWhiteSpace(status))
                    StatusText = FormatWorkStatus(status);
            });
        }

        public void FinishWork(string status)
        {
            Update(() =>
            {
                _completedWork = _totalWork;
                IsBusy = true;
                IsIndeterminate = false;
                Value = 100;
                StatusText = FormatWorkStatus(status);
            });
        }

        public void Report(double value, string status = null)
        {
            Update(() =>
            {
                IsBusy = true;
                Value = Math.Clamp(value, 0, 100);
                IsIndeterminate = false;
                if (!string.IsNullOrWhiteSpace(status))
                    StatusText = _totalWork > 0 ? FormatWorkStatus(status) : NormalizeStatus(status);
            });
        }

        public void SetStatus(string status, bool? indeterminate = null)
        {
            Update(() =>
            {
                if (!string.IsNullOrWhiteSpace(status))
                    StatusText = _totalWork > 0 ? FormatWorkStatus(status) : NormalizeStatus(status);
                if (indeterminate.HasValue)
                    IsIndeterminate = indeterminate.Value;
            });
        }

        public void Complete()
        {
            Update(() =>
            {
                IsBusy = false;
                _totalWork = 0;
                _completedWork = 0;
                Value = 0;
                IsIndeterminate = true;
                if (!string.Equals(StatusText, CanceledStatus, StringComparison.Ordinal))
                    StatusText = ReadyStatus;
            });
        }

        public void Cancel()
        {
            Update(() =>
            {
                IsBusy = false;
                _totalWork = 0;
                _completedWork = 0;
                StatusText = CanceledStatus;
                Value = 0;
                IsIndeterminate = true;
            });
        }

        private static string NormalizeStatus(string status) => status.Trim().ToUpperInvariant();

        private string FormatWorkStatus(string status) =>
            $"[{_completedWork:0}/{_totalWork:0}] {NormalizeStatus(status)}";

        private static void Update(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
    }
}
