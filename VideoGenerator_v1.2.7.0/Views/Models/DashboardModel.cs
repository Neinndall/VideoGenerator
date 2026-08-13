using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

namespace VideoGenerator.Views.Models
{
    public partial class DashboardModel : ObservableObject
    {
        private readonly HashSet<PreviewEventModel> _selectionObservedEvents = new();
        private bool _isUpdatingSelection;

        public DashboardModel()
        {
            FilteredProcessedEvents.CollectionChanged += FilteredProcessedEvents_CollectionChanged;
        }

        [ObservableProperty]
        private string _audioPath = string.Empty;

        [ObservableProperty]
        private string _selectedLanguage = "ES";



        [ObservableProperty]
        private double _silenceDuration = 0.0;

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private bool _isAnalyzed = false;

        public bool CanRunWorkflow => IsAnalyzed && !IsProcessing && SelectedVisibleEventCount > 0;

        public int VisibleEventCount => FilteredProcessedEvents.Count;

        public int SelectedVisibleEventCount => FilteredProcessedEvents.Count(ev => ev.IsSelected);

        public bool HasVisibleEvents => VisibleEventCount > 0;

        public bool AreAllVisibleEventsSelected => HasVisibleEvents && SelectedVisibleEventCount == VisibleEventCount;

        public string SelectionActionLabel => AreAllVisibleEventsSelected
            ? "DESELECT VISIBLE"
            : "SELECT ALL VISIBLE";

        public string SelectionSummary => $"SELECTED: {SelectedVisibleEventCount}/{VisibleEventCount}";

        public IReadOnlyList<PreviewEventModel> GetSelectedVisibleEvents()
        {
            return FilteredProcessedEvents
                .Where(ev => ev.IsSelected)
                .ToList();
        }

        public void SetVisibleEventsSelection(bool isSelected)
        {
            _isUpdatingSelection = true;
            try
            {
                foreach (var pipelineEvent in FilteredProcessedEvents)
                {
                    pipelineEvent.IsSelected = isSelected;
                }
            }
            finally
            {
                _isUpdatingSelection = false;
                RefreshSelectionState();
            }
        }

        partial void OnIsAnalyzedChanged(bool value)
        {
            RefreshSelectionState();
        }

        partial void OnIsProcessingChanged(bool value)
        {
            RefreshSelectionState();
        }

        [ObservableProperty]
        private PreviewEventModel _selectedEvent;

        [ObservableProperty]
        private ImageSource _previewImageSource;

        [ObservableProperty]
        private string _selectedFilter = "ALL"; // ALL, ERRORS, PENDING

        [ObservableProperty]
        private string _selectedCharacter = "ALL"; // ALL, Ahri, Lucian, etc.

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        public ObservableCollection<string> CharactersList { get; } = new();
        public ObservableCollection<PreviewEventModel> FilteredProcessedEvents { get; } = new();

        public ObservableCollection<string> AvailableLanguages { get; } = new();
        public ObservableCollection<PreviewEventModel> ProcessedEvents { get; } = new();

        private void FilteredProcessedEvents_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var pipelineEvent in _selectionObservedEvents.ToList())
                {
                    pipelineEvent.PropertyChanged -= PreviewEvent_PropertyChanged;
                }

                _selectionObservedEvents.Clear();
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (PreviewEventModel pipelineEvent in e.OldItems)
                    {
                        UnsubscribeFromSelectionChanges(pipelineEvent);
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (PreviewEventModel pipelineEvent in e.NewItems)
                    {
                        SubscribeToSelectionChanges(pipelineEvent);
                    }
                }
            }

            RefreshSelectionState();
        }

        private void SubscribeToSelectionChanges(PreviewEventModel pipelineEvent)
        {
            if (_selectionObservedEvents.Add(pipelineEvent))
            {
                pipelineEvent.PropertyChanged += PreviewEvent_PropertyChanged;
            }
        }

        private void UnsubscribeFromSelectionChanges(PreviewEventModel pipelineEvent)
        {
            if (_selectionObservedEvents.Remove(pipelineEvent))
            {
                pipelineEvent.PropertyChanged -= PreviewEvent_PropertyChanged;
            }
        }

        private void PreviewEvent_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewEventModel.IsSelected) && !_isUpdatingSelection)
            {
                RefreshSelectionState();
            }
        }

        private void RefreshSelectionState()
        {
            OnPropertyChanged(nameof(VisibleEventCount));
            OnPropertyChanged(nameof(SelectedVisibleEventCount));
            OnPropertyChanged(nameof(HasVisibleEvents));
            OnPropertyChanged(nameof(AreAllVisibleEventsSelected));
            OnPropertyChanged(nameof(SelectionActionLabel));
            OnPropertyChanged(nameof(SelectionSummary));
            OnPropertyChanged(nameof(CanRunWorkflow));
        }
    }
}
