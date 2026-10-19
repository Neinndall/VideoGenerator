using System.Windows;
using System.Windows.Controls;

namespace VideoGenerator.Views.Settings
{
    public partial class ProductionSettingsView : UserControl
    {
        public ProductionSettingsView() => InitializeComponent();

        private void ProductionCardsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool stacked = e.NewSize.Width < 750;
            ProductionSideColumn.Width = stacked ? new GridLength(0) : new GridLength(5, GridUnitType.Star);
            Grid.SetRow(ProductionSideCard, stacked ? 1 : 0);
            Grid.SetColumn(ProductionSideCard, stacked ? 0 : 1);
            ProductionMainCard.Margin = stacked ? new Thickness(0, 0, 0, 16) : new Thickness(0, 0, 10, 16);
            ProductionSideCard.Margin = stacked ? new Thickness(0, 0, 0, 16) : new Thickness(10, 0, 0, 16);
        }
    }
}
