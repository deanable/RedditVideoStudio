using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RedditVideoStudio.Core.Interfaces;
using System.Threading.Tasks;

namespace RedditVideoStudio.UI.ViewModels.Settings
{
    public partial class DestinationViewModel : ObservableObject
    {
        private readonly IVideoDestination _destination;

        public string Name => _destination.Name;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDisconnected))]
        private bool _isAuthenticated;

        [ObservableProperty]
        private bool _isEnabled;

        public bool IsDisconnected => !IsAuthenticated;

        [ObservableProperty]
        private bool _isBusy;

        public DestinationViewModel(IVideoDestination destination)
        {
            _destination = destination;
            _isAuthenticated = _destination.IsAuthenticated;
            _isEnabled = false; // Default to disabled
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