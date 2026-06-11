using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace VideoGenerator.Services
{
    public class LogService
    {
        private readonly string _infoLogPath;
        private readonly string _errorLogPath;
        private readonly Dispatcher _dispatcher;

        // Centralized collection for UI binding
        public ObservableCollection<string> Logs { get; } = new();

        public LogService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _infoLogPath = Path.Combine(baseDir, "app_information.log");
            _errorLogPath = Path.Combine(baseDir, "app_errors.log");
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public void LogInfo(string message)
        {
            WriteToFile(_infoLogPath, "INFO", message);
            AddToCollection(message);
        }

        public void LogWarn(string message)
        {
            WriteToFile(_infoLogPath, "WARN", message);
            AddToCollection($"[WARN] {message}");
        }

        public void LogError(string message, Exception ex = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $"{Environment.NewLine}Exception: {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}";
            }
            WriteToFile(_errorLogPath, "ERROR", fullMessage);
            AddToCollection($"!!! ERROR: {message}");
        }

        private void AddToCollection(string message)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                Logs.Add(message);
                // Keep only the last 1000 logs in memory for performance
                if (Logs.Count > 1000) Logs.RemoveAt(0);
            }));
        }

        private void WriteToFile(string path, string level, string message)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(path, entry);
            }
            catch
            {
                // Silence logging errors
            }
        }
    }

    public static class LogExtensions
    {
        public static readonly DependencyProperty AutoScrollToEndProperty =
            DependencyProperty.RegisterAttached("AutoScrollToEnd", typeof(bool), typeof(LogExtensions), new PropertyMetadata(false, OnAutoScrollToEndChanged));

        public static bool GetAutoScrollToEnd(DependencyObject obj) => (bool)obj.GetValue(AutoScrollToEndProperty);
        public static void SetAutoScrollToEnd(DependencyObject obj, bool value) => obj.SetValue(AutoScrollToEndProperty, value);

        private static void OnAutoScrollToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.ListBox listBox)
            {
                var itemsSource = listBox.Items.SourceCollection as System.Collections.Specialized.INotifyCollectionChanged;
                if (itemsSource != null)
                {
                    if ((bool)e.NewValue)
                        itemsSource.CollectionChanged += (s, args) => ScrollToEnd(listBox);
                    else
                        itemsSource.CollectionChanged -= (s, args) => ScrollToEnd(listBox);
                }
            }
        }

        private static void ScrollToEnd(System.Windows.Controls.ListBox listBox)
        {
            if (listBox.Items.Count > 0)
            {
                listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
            }
        }
    }
}
