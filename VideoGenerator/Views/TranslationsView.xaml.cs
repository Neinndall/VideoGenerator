using System;
using System.Windows;
using VideoGenerator.Services;
using VideoGenerator.Views.Models;
using UserControl = System.Windows.Controls.UserControl;

namespace VideoGenerator.Views
{
    public partial class TranslationsView : UserControl
    {
        private readonly TranslationsModel _model = new();
        private readonly TranslationService _translationService;

        public TranslationsView(TranslationService translationService)
        {
            InitializeComponent();
            _translationService = translationService;
            DataContext = _model;

            _model.JsonContent = _translationService.GetRawJson();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _translationService.SaveRawJson(_model.JsonContent);
                _model.StatusMessage = "✓ Translations saved successfully.";
            }
            catch (Exception ex)
            {
                _model.StatusMessage = $"✗ Invalid JSON: {ex.Message}";
            }
        }
    }
}
