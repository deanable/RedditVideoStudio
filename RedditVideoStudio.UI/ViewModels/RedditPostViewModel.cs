using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RedditVideoStudio.UI.ViewModels
{
    /// <summary>
    /// The ViewModel for a single Reddit post displayed in the main window.
    /// It wraps the RedditPostData domain model and implements INotifyPropertyChanged
    /// to support data binding in the UI.
    /// </summary>
    public class RedditPostViewModel : INotifyPropertyChanged
    {
        private string? _id;
        public string? Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private string? _title;
        public string? Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        // --- ADDED: SelfText property to match the data model ---
        private string? _selfText;
        public string? SelfText
        {
            get => _selfText;
            set { _selfText = value; OnPropertyChanged(); }
        }

        private int _score;
        public int Score
        {
            get => _score;
            set { _score = value; OnPropertyChanged(); }
        }

        private string? _subreddit;
        public string? Subreddit
        {
            get => _subreddit;
            set { _subreddit = value; OnPropertyChanged(); }
        }

        private string? _url;
        public string? Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }

        private string? _permalink;
        public string? Permalink
        {
            get => _permalink;
            set { _permalink = value; OnPropertyChanged(); }
        }

        private List<string> _comments = new();
        public List<string> Comments
        {
            get => _comments;
            set { _comments = value; OnPropertyChanged(); }
        }

        private DateTime? _scheduledPublishTimeUtc;
        /// <summary>
        /// The specific date and time this post's video should be published.
        /// </summary>
        public DateTime? ScheduledPublishTimeUtc
        {
            get => _scheduledPublishTimeUtc;
            set
            {
                _scheduledPublishTimeUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScheduledDate));
                OnPropertyChanged(nameof(ScheduledTime));
            }
        }

        /// <summary>
        /// A helper property to bind the DatePicker to just the Date part of the schedule.
        /// </summary>
        public DateTime? ScheduledDate
        {
            get => ScheduledPublishTimeUtc?.Date;
            set
            {
                if (value.HasValue)
                {
                    var currentTime = ScheduledPublishTimeUtc?.TimeOfDay ?? DateTime.Now.TimeOfDay;
                    ScheduledPublishTimeUtc = value.Value.Date + currentTime;
                }
            }
        }

        /// <summary>
        /// A helper property to bind the TextBox to the Time part of the schedule.
        /// </summary>
        public string ScheduledTime
        {
            get => ScheduledPublishTimeUtc?.ToString("HH:mm:ss") ?? string.Empty;
            set
            {
                if (TimeSpan.TryParse(value, out var timeSpan))
                {
                    var currentDate = ScheduledPublishTimeUtc?.Date ?? DateTime.Today;
                    ScheduledPublishTimeUtc = currentDate.Date + timeSpan;
                }
            }
        }

        private bool _isAlreadyUploaded;
        public bool IsAlreadyUploaded
        {
            get => _isAlreadyUploaded;
            set { _isAlreadyUploaded = value; OnPropertyChanged(); }
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}