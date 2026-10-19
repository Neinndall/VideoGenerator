using System.Windows;
using System.Windows.Controls;

namespace VideoGenerator.Views.Settings
{
    public partial class TranscriptionSettingsView : UserControl
    {
        public TranscriptionSettingsView() => InitializeComponent();

        private void TranscriptionCardsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool stacked = e.NewSize.Width < 750;
            TranscriptionSideColumn.Width = stacked ? new GridLength(0) : new GridLength(5, GridUnitType.Star);
            Grid.SetRow(TranscriptionComputeCard, stacked ? 1 : 0);
            Grid.SetColumn(TranscriptionComputeCard, stacked ? 0 : 1);
            Grid.SetRow(TranscriptionCleanupCard, stacked ? 2 : 1);
            Grid.SetRow(TranscriptionCacheCard, stacked ? 3 : 1);
            Grid.SetColumn(TranscriptionCacheCard, stacked ? 0 : 1);
            TranscriptionMainCard.Margin = stacked ? new Thickness(0,0,0,16) : new Thickness(0,0,10,16);
            TranscriptionComputeCard.Margin = stacked ? new Thickness(0,0,0,16) : new Thickness(10,0,0,16);
            TranscriptionCleanupCard.Margin = stacked ? new Thickness(0,0,0,16) : new Thickness(0,0,10,16);
            TranscriptionCacheCard.Margin = stacked ? new Thickness(0,0,0,16) : new Thickness(10,0,0,16);
        }
    }
}
