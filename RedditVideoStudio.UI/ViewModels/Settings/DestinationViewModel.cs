namespace RedditVideoStudio.UI.ViewModels.Settings
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using RedditVideoStudio.Core.Interfaces;
    using System;
    using System.Threading.Tasks;
    using System.Windows; // Added for MessageBox

    public partial class DestinationViewModel : ObservableObject
    {
        private readonly IVideoDestination _destination;
        private readonly ILogger<DestinationViewModel> _logger;

        public string Name => _destination.Name;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDisconnected))]
        private bool _isAuthenticated;

        [ObservableProperty]
        private bool _isEnabled;

        public bool IsDisconnected => !IsAuthenticated;

        [ObservableProperty]
        private bool _isBusy;

        public DestinationViewModel(IVideoDestination destination, ILogger<DestinationViewModel> logger)
        {
            _destination = destination;
            _logger = logger;
            _isAuthenticated = _destination.IsAuthenticated;
            _isEnabled = false;
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
            catch (Exception ex)
            {
                // Log the full exception details for better debugging
                _logger.LogError(ex, "Authentication failed for {DestinationName}", Name);

                // Show a detailed message box to the user
                MessageBox.Show($"Authentication failed for {Name}.\n\nError: {ex.Message}\n\nDetails:\n{ex.ToString()}",
                                "Authentication Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

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
