﻿using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WamWooWam.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unicord.Universal.Controls
{
    public sealed partial class AttachmentViewer : UserControl
    {
        DiscordAttachment _attachment;
        private static readonly Lazy<string[]> _mediaExtensions = new Lazy<string[]>(() => new string[] { ".mp4", ".mov", ".webm", ".wmv", ".avi", ".mkv", ".ogv", ".mp3", ".m4a", ".aac", ".wav", ".wma", ".flac", ".ogg", ".oga", ".opus" });

        public AttachmentViewer(DiscordAttachment attachment)
        {
            InitializeComponent();
            _attachment = attachment;

            HorizontalAlignment = HorizontalAlignment.Left;
        }

        private bool _loadDetails = false;

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var url = _attachment.Url;
            var fileExtension = Path.GetExtension(url);

            if (_mediaExtensions.Value.Contains(fileExtension))
            {
                var mediaPlayer = new MediaPlayerControl(_attachment, !(_attachment.Width != 0 && _attachment.Height != 0)) { VerticalContentAlignment = VerticalAlignment.Top };
                mainGrid.Content = mediaPlayer;
            }
            else if (_attachment.Height != 0 && _attachment.Width != 0)
            {
                var imageElement = new ImageElement()
                {
                    ImageWidth = _attachment.Width,
                    ImageHeight = _attachment.Height,
                    ImageUri = new Uri(_attachment.ProxyUrl)
                };

                imageElement.Tapped += Image_Tapped;
                mainGrid.Content = imageElement;
            }
            else
            {
                _loadDetails = true;
                Bindings.Update();
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            //var mediaPlayer = mainGrid.FindChild<MediaPlayerControl>();
            //mediaPlayer?.meda.Pause();

            mainGrid.Content = null;
        }

        private void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.FindParent<MainPage>()?.ShowAttachmentOverlay(
                new Uri(_attachment.ProxyUrl),
                _attachment.Width,
                _attachment.Height,
                openMenuItem_Click,
                saveMenuItem_Click,
                shareMenuItem_Click);
        }

        private async void saveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
                b.IsEnabled = false;
            if (sender is MenuFlyoutItem i)
                i.IsEnabled = false;

            downloadProgressBar.IsIndeterminate = true;

            var picker = new FileSavePicker()
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                SuggestedFileName = Path.GetFileNameWithoutExtension(_attachment.Url),
                DefaultFileExtension = Path.GetExtension(_attachment.Url)
            };

            picker.FileTypeChoices.Add($"Attachment Extension (*{Path.GetExtension(_attachment.Url)})", new List<string>() { Path.GetExtension(_attachment.Url) });

            var file = await picker.PickSaveFileAsync();
            downloadProgressBar.IsIndeterminate = false;

            if (file != null)
            {
                await DownloadAttachmentToFileAsync(file);
            }

            if (sender is Button b1)
                b1.IsEnabled = true;
            if (sender is MenuFlyoutItem i1)
                i1.IsEnabled = true;

            downloadProgressBar.Visibility = Visibility.Collapsed;
        }

        StorageFile _shareFile;
        DataTransferManager _dataTransferManager;

        private async void shareMenuItem_Click(object sender, RoutedEventArgs e)
        {
            downloadProgressBar.Visibility = Visibility.Visible;
            _dataTransferManager = DataTransferManager.GetForCurrentView();
            _shareFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync($"{Guid.NewGuid()}{Path.GetExtension(_attachment.Url)}");

            if (await DownloadAttachmentToFileAsync(_shareFile))
            {
                _dataTransferManager.DataRequested += _dataTransferManager_DataRequested;
                DataTransferManager.ShowShareUI();
            }

            downloadProgressBar.Visibility = Visibility.Collapsed;
        }

        private async Task<bool> DownloadAttachmentToFileAsync(StorageFile file)
        {
            CachedFileManager.DeferUpdates(file);

            var progress = new Progress<HttpProgress>(p =>
            {
                if (p.TotalBytesToReceive.HasValue)
                {
                    downloadProgressBar.Maximum = (double)p.TotalBytesToReceive;
                }
                else
                {
                    downloadProgressBar.IsIndeterminate = true;
                }

                downloadProgressBar.Value = p.BytesReceived;
            });

            var message = new HttpRequestMessage(HttpMethod.Get, new Uri(_attachment.Url));
            var resp = await Tools.HttpClient.SendRequestAsync(message).AsTask(progress);

            var content = await resp.Content.ReadAsInputStreamAsync();
            var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);

            await RandomAccessStream.CopyAndCloseAsync(content, fileStream);

            content.Dispose();
            fileStream.Dispose();

            await CachedFileManager.CompleteUpdatesAsync(file);

            return true;
        }

        private void _dataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var request = args.Request;

            request.Data.Properties.Title = "Sharing attachment";
            request.Data.Properties.Description = Path.GetFileName(_attachment.Url);

            request.Data.SetWebLink(new Uri(_attachment.Url));
            request.Data.SetStorageItems(new[] { _shareFile });

            // REVISIT: Videos now specify these, that causes issues.
            //if (_attachment.Height != 0 && _attachment.Width != 0)
            //{
            //    request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(_shareFile));
            //}

            _dataTransferManager.DataRequested -= _dataTransferManager_DataRequested;
        }

        private async void openMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri(_attachment.Url));
        }
    }
}
