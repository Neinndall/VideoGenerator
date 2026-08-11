using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VideoGenerator.Utils;

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
            string logsDir = Path.Combine(baseDir, "logs");
            
            DirectoriesCreator.CreateDirectory(logsDir);

            _infoLogPath = Path.Combine(logsDir, "application_logs.log");
            _errorLogPath = Path.Combine(logsDir, "application_errors.log");
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public void LogInfo(string message)
        {
            WriteToFile(_infoLogPath, "INFO", message);
            AddToCollection(message);
        }

        public void LogDebug(string message)
        {
            // Technical diagnostics remain available on disk without flooding the UI console.
            WriteToFile(_infoLogPath, "DEBUG", message);
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
                fullMessage += $"{Environment.NewLine}Exception: {ex}";
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
        private sealed class RichLogSubscription
        {
            private readonly WeakReference<RichTextBox> _owner;
            private readonly NotifyCollectionChangedEventHandler _handler;

            public RichLogSubscription(RichTextBox owner, ObservableCollection<string> source)
            {
                _owner = new WeakReference<RichTextBox>(owner);
                Source = source;
                _handler = OnCollectionChanged;
                Source.CollectionChanged += _handler;
            }

            public ObservableCollection<string> Source { get; }

            public void Detach() => Source.CollectionChanged -= _handler;

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
            {
                if (_owner.TryGetTarget(out var owner))
                {
                    CollectionChanged(owner, args);
                }
                else
                {
                    Detach();
                }
            }
        }

        private static readonly ConditionalWeakTable<RichTextBox, RichLogSubscription> RichLogSubscriptions = new();

        public static readonly DependencyProperty RichLogSourceProperty =
            DependencyProperty.RegisterAttached("RichLogSource", typeof(ObservableCollection<string>), typeof(LogExtensions), new PropertyMetadata(null, OnRichLogSourceChanged));

        public static ObservableCollection<string> GetRichLogSource(DependencyObject obj) => (ObservableCollection<string>)obj.GetValue(RichLogSourceProperty);
        public static void SetRichLogSource(DependencyObject obj, ObservableCollection<string> value) => obj.SetValue(RichLogSourceProperty, value);

        private static void OnRichLogSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.RichTextBox richTextBox)
            {
                if (e.OldValue is ObservableCollection<string> oldCollection)
                {
                    if (RichLogSubscriptions.TryGetValue(richTextBox, out var subscription))
                    {
                        subscription.Detach();
                        RichLogSubscriptions.Remove(richTextBox);
                    }
                }

                if (e.NewValue is ObservableCollection<string> newCollection)
                {
                    richTextBox.Document ??= new System.Windows.Documents.FlowDocument();
                    richTextBox.Document.Blocks.Clear();
                    
                    // Add existing items
                    foreach (var item in newCollection)
                    {
                        AppendLogToRichText(richTextBox, item);
                    }

                    RichLogSubscriptions.Add(richTextBox, new RichLogSubscription(richTextBox, newCollection));
                }
            }
        }

        private static void CollectionChanged(System.Windows.Controls.RichTextBox richTextBox, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (string item in e.NewItems)
                {
                    AppendLogToRichText(richTextBox, item);
                }
                richTextBox.ScrollToEnd();
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                richTextBox.Document?.Blocks.Clear();
            }
        }

        private static void AppendLogToRichText(System.Windows.Controls.RichTextBox richTextBox, string message)
        {
            if (richTextBox.Document == null) return;

            var paragraph = new System.Windows.Documents.Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            var run = new System.Windows.Documents.Run($"[{DateTime.Now:HH:mm:ss}] ");
            run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170)); // TextSecondaryColor
            paragraph.Inlines.Add(run);

            var messageRun = new System.Windows.Documents.Run(message);
            
            if (message.StartsWith("!!! ERROR"))
            {
                messageRun.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // ErrorColor
                messageRun.FontWeight = FontWeights.Bold;
            }
            else if (message.StartsWith("[WARN]"))
            {
                messageRun.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)); // WarningColor
                messageRun.FontWeight = FontWeights.Bold;
            }
            else
            {
                messageRun.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 250, 250)); // TextPrimaryColor
            }

            paragraph.Inlines.Add(messageRun);

            // Optimization: Keep paragraph count reasonable
            if (richTextBox.Document.Blocks.Count > 500)
            {
                richTextBox.Document.Blocks.Remove(richTextBox.Document.Blocks.FirstBlock);
            }

            richTextBox.Document.Blocks.Add(paragraph);
        }

        // Legacy ListBox AutoScroll for backwards compatibility (can be removed if no longer used)
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
