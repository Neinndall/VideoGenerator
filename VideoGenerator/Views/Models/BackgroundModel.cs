using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace VideoGenerator.Views.Models
{
    public partial class BackgroundModel : ObservableObject
    {
        [ObservableProperty]
        private string _previewText = "DUMMY EVENT TEXT";

        [ObservableProperty]
        private BitmapImage _previewImage;

        [ObservableProperty]
        private string _customBackgroundPath;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<string> _fontNames = new();

        [ObservableProperty]
        private string _selectedFontName = "Segoe UI";
    }
}
