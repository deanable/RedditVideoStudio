namespace RedditVideoStudio.UI.ViewModels.Settings
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.Extensions.Logging;
    using RedditVideoStudio.Core.Interfaces;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public partial class DestinationsSettingsViewModel : ObservableObject
    {
        public ObservableCollection<DestinationViewModel> Destinations { get; }

        public DestinationsSettingsViewModel(IEnumerable<IVideoDestination> destinations, ILoggerFactory loggerFactory)
        {
            var destinationViewModels = destinations.Select(d =>
                new DestinationViewModel(d, loggerFactory.CreateLogger<DestinationViewModel>()));
            Destinations = new ObservableCollection<DestinationViewModel>(destinationViewModels);
        }
    }
}
