﻿using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WamWooWam.Core;
using DSharpPlus.EventArgs;
using System.Threading;
using System.Collections.Concurrent;
using Windows.UI.Core;
using Unicord.Universal.Utilities;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unicord.Universal.Models
{
    public class ChannelViewModel : PropertyChangedBase, IDisposable
    {
        private DiscordChannel _channel;
        private DiscordUser _currentUser;

        private ConcurrentDictionary<ulong, CancellationTokenSource> _typingCancellation;
        private DateTime _typingLastSent;
        private SynchronizationContext _context;

        public ChannelViewModel(DiscordChannel channel)
        {
            _context = SynchronizationContext.Current;
            _typingCancellation = new ConcurrentDictionary<ulong, CancellationTokenSource>();
            _typingLastSent = DateTime.Now - TimeSpan.FromSeconds(10);

            Channel = channel;
            CurrentUser = channel.Guild?.CurrentMember ?? App.Discord.CurrentUser;
            if (channel.Type != ChannelType.Voice)
            {
                App.Discord.TypingStarted += Discord_TypingStarted;
                App.Discord.MessageCreated += OnMessageCreated;
                App.Discord.ChannelUpdated += OnChannelUpdated;

                TypingUsers = new ObservableCollection<DiscordUser>();
            }

            if (channel.Guild != null)
                Permissions = _channel.PermissionsFor(channel.Guild.CurrentMember);
            else
                Permissions = Permissions.Administrator;

            AvailableUsers = new ObservableCollection<DiscordUser> { CurrentUser };
            
            FileUploads = new ObservableCollection<FileUploadModel>();
            FileUploads.CollectionChanged += (o, e) =>
            {
                InvokePropertyChanged(nameof(DisplayUploadSize));
                InvokePropertyChanged(nameof(UploadProgressBarValue));
                InvokePropertyChanged(nameof(CanSend));
                InvokePropertyChanged(nameof(UploadInfoForeground));
                InvokePropertyChanged(nameof(CanUpload));
            };
        }

        private Task OnChannelUpdated(ChannelUpdateEventArgs e)
        {
            if (e.ChannelAfter.Id == _channel.Id)
            {
                Channel = e.ChannelAfter;
                InvokePropertyChanged(string.Empty);
            }

            return Task.CompletedTask;
        }

        public DiscordChannel Channel
        {
            get => _channel;
            set
            {
                OnPropertySet(ref _channel, value);

                if (_channel.Guild != null)
                    Permissions = _channel.PermissionsFor(_channel.Guild.CurrentMember);
                else
                    Permissions = Permissions.Administrator;

                InvokePropertyChanged(string.Empty);
            }
        }

        public DiscordUser CurrentUser
        {
            get => _currentUser;
            set
            {
                OnPropertySet(ref _currentUser, value);

                if (_channel.Guild != null)
                    Permissions = _channel.PermissionsFor(_currentUser as DiscordMember);
                else
                    Permissions = Permissions.Administrator;

                InvokePropertyChanged(string.Empty);
            }
        }

        public Permissions Permissions { get; set; }

        public ObservableCollection<DiscordUser> AvailableUsers { get; set; }

        public ObservableCollection<FileUploadModel> FileUploads { get; set; }

        public ObservableCollection<DiscordUser> TypingUsers { get; set; }

        public string Topic => Channel.Topic != null ? Channel.Topic.Replace(new[] { "\r", "\n" }, " ") : string.Empty;

        /// <summary>
        /// The channel's symbol. (i.e. #, @ etc.)
        /// </summary>
        public string ChannelPrefix =>
            Channel.Guild != null ? "#"
            : Channel.Type == ChannelType.Private ? "@"
            : string.Empty;

        /// <summary>
        /// The actual channel name. (i.e. general, WamWooWam, etc.)
        /// </summary>
        public string ChannelName
        {
            get
            {
                if (!string.IsNullOrEmpty(Channel.Name))
                    return Channel.Name;

                if (Channel is DiscordDmChannel dm)
                {
                    if (dm.Type == ChannelType.Private)
                    {
                        return dm.Recipient.Username;
                    }
                    else
                    {
                        return Strings.NaturalJoin(dm.Recipients.Select(r => r.Username));
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// The suffix of the channel's display name. (i.e. #6402)
        /// </summary>
        public string ChannelSuffix =>
            Channel is DiscordDmChannel dm && dm.Type == ChannelType.Private ? $"#{dm.Recipient.Discriminator}" : string.Empty;

        /// <summary>
        /// The full channel name (i.e. @WamWooWam#6402, #general, etc.)
        /// </summary>
        public string FullChannelName =>
            $"{ChannelPrefix}{ChannelName}{ChannelSuffix}";

        /// <summary>
        /// The current placeholder to display in the message text box
        /// </summary>
        public string ChannelPlaceholder =>
           CanSend ? $"Message {ChannelPrefix}{ChannelName}" : "This channel is read-only";

        /// <summary>
        /// The title for the page displaying the channel
        /// </summary>
        public string TitleText => Channel.Guild != null ? $"{FullChannelName} - {Channel.Guild.Name}" : FullChannelName;

        public Visibility ShowUsersButton =>
            AvailableUsers.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ShowUserImage =>
            _channel is DiscordDmChannel c && c.Type == ChannelType.Private ? Visibility.Visible : Visibility.Collapsed;

        public string UserImageUrl =>
            _channel is DiscordDmChannel c && c.Type == ChannelType.Private ? c.Recipient?.NonAnimatedAvatarUrl : null;

        public bool CanSend
        {
            get
            {
                if (UploadSize > (ulong)UploadLimit)
                    return false;

                if (Channel.Type == ChannelType.Voice)
                    return false;

                if (Channel is DiscordDmChannel)
                    return true;

                if (_currentUser is DiscordMember member)
                {
                    return Permissions.HasPermission(Permissions.SendMessages) && Permissions.HasPermission(Permissions.AccessChannels);
                }

                return false;
            }
        }

        public bool CanUpload
        {
            get
            {

                if (UploadSize > (ulong)UploadLimit)
                    return false;

                if (Channel.Type == ChannelType.Voice)
                    return false;

                if (_channel is DiscordDmChannel)
                    return true;

                if (_currentUser is DiscordMember member)
                {
                    var perms = Permissions;
                    return perms.HasPermission(Permissions.AccessChannels) && perms.HasPermission(Permissions.SendMessages) && perms.HasPermission(Permissions.AttachFiles);
                }

                return false;
            }
        }

        public int UploadLimit => HasNitro ? 50 * 1024 * 1024 : 8 * 1024 * 1024;

        public ulong UploadSize => (ulong)FileUploads.Sum(u => (double)u.Length);

        public string DisplayUploadSize => (UploadSize / (1024d * 1024d)).ToString("F2");

        public double UploadProgressBarValue => Math.Min(UploadSize, (ulong)UploadLimit);

        public Brush UploadInfoForeground => !CanSend ? new SolidColorBrush(Colors.Red) : null;

        public string DisplayUploadLimit => (UploadLimit / (1024d * 1024d)).ToString("F2");

        public Visibility ShowSendButton => CanSend ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ShowUserlistButton => Channel.Type == ChannelType.Group || Channel.Guild != null ? Visibility.Visible : Visibility.Collapsed;

        public bool HasNitro => GetClient().CurrentUser.HasNitro;

        public BaseDiscordClient GetClient()
        {
            if (App.AdditionalUserClients.TryGetValue(CurrentUser.Id, out var client))
            {
                return client;
            }

            return App.Discord;
        }

        /// <summary>
        /// Abstracts sending a message.
        /// </summary>
        /// <returns></returns>
        public async Task SendMessageAsync(TextBox textBox, IProgress<double?> progress = null)
        {
            if (Channel.Type == ChannelType.Voice)
                return;

            if ((!string.IsNullOrWhiteSpace(textBox.Text) || FileUploads.Any()) && CanSend)
            {
                var str = textBox.Text;
                if (str.Length < 2000)
                {
                    textBox.Text = "";

                    try
                    {
                        var client = GetClient();

                        if (FileUploads.Any())
                        {
                            var models = FileUploads.ToArray();
                            FileUploads.Clear();
                            var files = models.ToDictionary(d => d.FileName, e => e.File);
                            await Tools.SendFilesWithProgressAsync(Channel, client, str, files, progress);

                            foreach (var model in models)
                            {
                                model.Dispose();

                                if (model.StorageFile != null && model.IsTemporary)
                                {
                                    await model.StorageFile.DeleteAsync();
                                }
                            }

                            return;
                        }

                        if (client is DiscordRestClient rest)
                        {
                            await rest.CreateMessageAsync(Channel.Id, str, false, null);
                            return;
                        }

                        await Channel.SendMessageAsync(str);
                    }
                    catch
                    {
                        await UIUtilities.ShowErrorDialogAsync(
                            "Failed to send message!",
                            "Oops, sending that didn't go so well, which probably means Discord is having a stroke. Again. Please try again later.");
                    }
                }
            }
        }

        public Visibility ShowSlowMode
            => Channel.PerUserRateLimit.HasValue && Channel.PerUserRateLimit != 0 ? Visibility.Visible : Visibility.Collapsed;

        public string SlowModeText
            => $"Messages can be sent every " +
            $"{TimeSpan.FromSeconds(Channel.PerUserRateLimit.GetValueOrDefault()).ToNaturalString()}!" +
            (Permissions.HasPermission(Permissions.ManageMessages) && Permissions.HasPermission(Permissions.ManageChannels) ? " But, you're immune!" : "");

        public Visibility ShowTypingUsers
            => TypingUsers?.Any() == true ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ShowTypingContainer => ShowSlowMode == Visibility.Visible || ShowTypingUsers == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

        public async Task UpdateAvailableUsers()
        {
            try
            {
                AvailableUsers.Clear();
                AvailableUsers.Add(CurrentUser);

                if (Channel.Guild == null) // for now
                    return;

                foreach (var u in App.AdditionalUserClients)
                {
                    var guild = u.Value.Guilds.FirstOrDefault(g => g.Key == Channel.GuildId).Value;
                    if (guild != null)
                    {
                        var m = await Channel.Guild.GetMemberAsync(u.Key);
                        if (Permissions.HasPermission(Permissions.AccessChannels) && Permissions.HasPermission(Permissions.SendMessages))
                        {
                            AvailableUsers.Add(m);
                        }
                    }
                }
            }
            finally
            {
                InvokePropertyChanged(nameof(ShowUsersButton));
                InvokePropertyChanged(nameof(AvailableUsers));
            }
        }

        private Task OnMessageCreated(MessageCreateEventArgs e)
        {   
            if (_typingCancellation.TryGetValue(e.Author.Id, out var src))
                src.Cancel();

            var usr = TypingUsers.FirstOrDefault(u => u.Id == e.Author.Id);
            if (usr != null)
            {
                _context.Post(a =>
                {
                    TypingUsers.Remove(usr);
                    UnsafeInvokePropertyChange(nameof(ShowTypingUsers));
                }, null);
            }

            return Task.CompletedTask;
        }

        #region Typing

        public async Task TriggerTypingAsync(string text)
        {
            if (!string.IsNullOrWhiteSpace(text) && (DateTime.Now - _typingLastSent).Seconds > 10)
            {
                _typingLastSent = DateTime.Now;

                try
                {
                    var client = GetClient();
                    if (client is DiscordRestClient rest)
                    {
                        await rest.TriggerTypingAsync(Channel.Id);
                        return;
                    }
                    else
                    {
                        await Channel.TriggerTypingAsync();
                    }
                }
                catch { }
            }
        }

        private Task Discord_TypingStarted(TypingStartEventArgs e)
        {
            if (e.Channel.Id == Channel.Id && e.User.Id != CurrentUser.Id)
            {
                if (_typingCancellation.TryRemove(e.User.Id, out var src))
                    src.Cancel();

                _context.Post(o =>
                {
                    TypingUsers.Add(e.User);
                    UnsafeInvokePropertyChange(nameof(ShowTypingUsers));
                }, null);

                new Task(async () => await HandleTypingStartAsync(e)).Start();
            }

            return Task.CompletedTask;
        }

        private async Task HandleTypingStartAsync(TypingStartEventArgs e)
        {
            var source = new CancellationTokenSource();
            _typingCancellation[e.User.Id] = source;

            await Task.Delay(10_000, source.Token).ContinueWith(t =>
            {
                _context.Post(o =>
                {
                    TypingUsers.Remove(e.User);
                    UnsafeInvokePropertyChange(nameof(ShowTypingUsers));
                }, null);
            });
        }

        #endregion

        public virtual void Dispose()
        {
            if (Channel.Type != ChannelType.Voice)
            {
                App.Discord.TypingStarted -= Discord_TypingStarted;
                App.Discord.MessageCreated -= OnMessageCreated;
            }
        }
    }
}
