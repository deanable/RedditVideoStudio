using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Interfaces;
using RedditVideoStudio.Domain.Models;
using RedditVideoStudio.Shared.Models;
using RedditVideoStudio.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Application.Services
{
    public class StoryboardGenerator : IStoryboardGenerator
    {
        private readonly ITextToSpeechService _ttsService;
        private readonly IImageService _imageService;
        private readonly IAppConfiguration _appConfig;
        private readonly ILogger<StoryboardGenerator> _logger;

        public StoryboardGenerator(
            ITextToSpeechService ttsService,
            IImageService imageService,
            IAppConfiguration appConfig,
            ILogger<StoryboardGenerator> logger)
        {
            _ttsService = ttsService;
            _imageService = imageService;
            _appConfig = appConfig;
            _logger = logger;
        }

        public async Task<Storyboard> GenerateAsync(
            IEnumerable<string> content,
            string tempPath,
            string segmentName,
            IProgress<ProgressReport> progress,
            CancellationToken cancellationToken)
        {
            var audioDir = Path.Combine(tempPath, $"{segmentName}_audio");
            var overlayDir = Path.Combine(tempPath, $"{segmentName}_overlay");
            Directory.CreateDirectory(audioDir);
            Directory.CreateDirectory(overlayDir);

            var maxCharsPerPage = _appConfig.Settings.ImageGeneration.MaxCharactersPerPage;

            var allPages = content
                .SelectMany(textItem => TextUtils.SplitTextIntoPages(TextUtils.SanitizePostContent(textItem), maxCharsPerPage))
                .Select((pageText, index) => (Text: pageText, Index: index))
                .ToList();

            var storyboard = new Storyboard();

            _logger.LogInformation("Generating {PageCount} pages sequentially for segment '\"{SegmentName}\"'.", allPages.Count, segmentName);

            foreach (var page in allPages)
            {
                await ProcessPageAsync(storyboard, page.Text, page.Index, audioDir, overlayDir, segmentName, progress, cancellationToken);
            }

            _logger.LogInformation("Total calculated storyboard duration for segment '\"{SegmentName}\"': {Duration}", segmentName, storyboard.GetNextStartTime());

            _logger.LogInformation("Successfully generated all assets for segment '\"{SegmentName}\"'.", segmentName);
            return storyboard;
        }

        private async Task ProcessPageAsync(Storyboard storyboard, string pageText, int index, string audioDir, string overlayDir, string segmentName, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            progress.Report(new ProgressReport { Message = $"Processing {segmentName} part {index + 1}..." });

            var audioPath = Path.Combine(audioDir, $"{segmentName}_{index}.mp3");
            var overlayPath = Path.Combine(overlayDir, $"{segmentName}_{index}.png");

            var speechTask = _ttsService.GenerateSpeechAsync(pageText, audioPath, cancellationToken);
            var imageTask = _imageService.GenerateImageFromTextAsync(pageText, overlayPath, cancellationToken);

            await Task.WhenAll(speechTask, imageTask);

            var speechResult = speechTask.Result;

            // ADDED: Logging for debugging
            _logger.LogInformation("Received speech result for page {PageIndex}: Duration = {Duration}, Path = \"{Path}\"", index, speechResult.Duration, speechResult.FilePath);

            var startTime = storyboard.GetNextStartTime();
            storyboard.Items.Add(new StoryboardItem
            {
                ImagePath = overlayPath,
                AudioPath = speechResult.FilePath,
                StartTime = startTime,
                EndTime = startTime + speechResult.Duration
            });
        }
    }
}