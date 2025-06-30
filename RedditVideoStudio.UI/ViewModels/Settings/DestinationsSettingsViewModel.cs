using CommunityToolkit.Mvvm.ComponentModel;
using RedditVideoStudio.Core.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RedditVideoStudio.UI.ViewModels.Settings
{
    /// <summary>
    /// The main ViewModel for the Destinations settings page.
    /// It manages a collection of all available video destination platforms.
    /// </summary>
    public partial class DestinationsSettingsViewModel : ObservableObject
    {
        /// <summary>
        /// A collection of view models, one for each available destination platform.
        /// This collection is what the UI will display as a list.
        /// </summary>
        public ObservableCollection<DestinationViewModel> Destinations { get; }

        public DestinationsSettingsViewModel(IEnumerable<IVideoDestination> destinations)
        {
            // The 'destinations' parameter is automatically populated by the Dependency Injection
            // container with all registered IVideoDestination services (like our YouTubeDestination).

            // We create a new DestinationViewModel for each service and add it to our public collection.
            var destinationViewModels = destinations.Select(d => new DestinationViewModel(d));
            Destinations = new ObservableCollection<DestinationViewModel>(destinationViewModels);
        }
    }
}