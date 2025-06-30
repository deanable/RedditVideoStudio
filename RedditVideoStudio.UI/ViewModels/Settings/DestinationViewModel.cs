using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedditVideoStudio.Core.Interfaces;
using System.Threading.Tasks;

namespace RedditVideoStudio.UI.ViewModels.Settings
{
    /// <summary>
    /// Represents a single destination platform (e.g., YouTube) in the UI.
    /// This class acts as a wrapper around an IVideoDestination service,
    /// providing properties and commands for a View to bind to.
    /// </summary>
    public partial class DestinationViewModel : ObservableObject
    {
        private readonly IVideoDestination _destination;

        /// <summary>
        /// The display name of the destination platform.
        /// </summary>
        public string Name => _destination.Name;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDisconnected))]
        private bool _isAuthenticated;

        /// <summary>
        /// A computed property that is the opposite of IsAuthenticated.
        /// Useful for binding the visibility of a "Connect" button.
        /// </summary>
        public bool IsDisconnected => !IsAuthenticated;

        [ObservableProperty]
        private bool _isBusy;

        public DestinationViewModel(IVideoDestination destination)
        {
            _destination = destination;
            // Initialize the authentication status from the service
            _isAuthenticated = _destination.IsAuthenticated;
        }

        [RelayCommand]
        private async Task Authenticate()
        {
            IsBusy = true;
            try
            {
                await _destination.AuthenticateAsync();
                IsAuthenticated = _destination.IsAuthenticated;
            }
            catch
            {
                // The service itself will log the error. We just need to handle the UI state.
                IsAuthenticated = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SignOut()
        {
            IsBusy = true;
            await _destination.SignOutAsync();
            IsAuthenticated = _destination.IsAuthenticated;
            IsBusy = false;
        }
    }
}