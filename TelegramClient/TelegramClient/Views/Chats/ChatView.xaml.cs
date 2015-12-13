using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Chats;
using TelegramClient.Views.Media;

namespace TelegramClient.Views.Chats
{
    public partial class ChatView
    {
        private ChatViewModel ViewModel { get { return DataContext as ChatViewModel; } }

        private readonly AppBarButton _editButton = new AppBarButton
        {
            Text = AppResources.Edit,
            IconUri = new Uri("/Images/ApplicationBar/appbar.edit.png", UriKind.Relative)
        };

        private readonly AppBarButton _addButton = new AppBarButton
        {
            Text = AppResources.Add,
            IconUri = new Uri("/Images/ApplicationBar/appbar.add.rest.png", UriKind.Relative)
        };

        private readonly AppBarMenuItem _setAdminsMenuItem = new AppBarMenuItem
        {
            Text = AppResources.SetAdmins,
        };

        public ChatView()
        {
            var timer = Stopwatch.StartNew();

            InitializeComponent();

            OptimizeFullHD();

            _editButton.Click += (sender, args) => ViewModel.Edit();
            _addButton.Click += (sender, args) => ViewModel.AddParticipant();
            _setAdminsMenuItem.Click += (sender, args) => ViewModel.SetAdmins();

            Loaded += (sender, args) =>
            {
                TimerString.Text = timer.Elapsed.ToString();

                if (ViewModel.ProfilePhotoViewer != null)
                    ViewModel.ProfilePhotoViewer.PropertyChanged += OnProfileViewerPropertyChanged;

                ViewModel.ChatDetails.PropertyChanged += OnChatDetailsPropertyChanged;
                BuildLocalizedAppBar();
            };

            Unloaded += (sender, args) =>
            {
                ViewModel.ChatDetails.PropertyChanged -= OnChatDetailsPropertyChanged;

                if (ViewModel.ProfilePhotoViewer != null)
                    ViewModel.ProfilePhotoViewer.PropertyChanged -= OnProfileViewerPropertyChanged;
            };
        }

        private void OptimizeFullHD()
        {
#if WP8
            var isFullHD = Application.Current.Host.Content.ScaleFactor == 225;
            //if (!isFullHD) return;
#endif

            Items.HeaderTemplate = (DataTemplate)Application.Current.Resources["FullHDPivotHeaderTemplate"];
        }

        private void OnChatDetailsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ChatDetails.ProfilePhotoViewer))
            {
                ViewModel.ProfilePhotoViewer.PropertyChanged += OnProfileViewerPropertyChanged;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.ChatDetails.IsChannelAdministrator))
            {
                UpdateLocalizedAppBar();
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.ChatDetails.CanEditChat))
            {
                UpdateLocalizedAppBar();
            }
        }

        private IApplicationBar _prevAppBar;

        private void OnProfileViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ProfilePhotoViewer.IsOpen))
            {
                ViewModel.NotifyOfPropertyChange(() => ViewModel.IsViewerOpen);

                if (ViewModel.ProfilePhotoViewer.IsOpen)
                {
                    _prevAppBar = ApplicationBar;

                    var profilePhotoViewerView = ProfilePhotoViewer.Content as ProfilePhotoViewerView;
                    ApplicationBar = profilePhotoViewerView != null ? profilePhotoViewerView.ApplicationBar : null;
                }
                else
                {
                    // wait to finish closing profile viewer animation
                    Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.25), () =>
                    {
                        if (_prevAppBar != null)
                        {
                            ApplicationBar = _prevAppBar;
                        }
                    });
                }
            }
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.Buttons.Add(_addButton);
            ApplicationBar.Buttons.Add(_editButton);

            var chat = ViewModel.Chat as TLChat40;
            if (chat != null && chat.Creator)
            {
                ApplicationBar.MenuItems.Add(_setAdminsMenuItem);
            }

            UpdateLocalizedAppBar();
        }

        private void UpdateLocalizedAppBar()
        {
            if (ApplicationBar == null) return;

            var channel = ViewModel.Chat as TLChannel;
            if (channel != null)
            {
                ApplicationBar.IsVisible = ViewModel.ChatDetails.CanEditChannel;
                return;
            }

            var chat = ViewModel.Chat as TLChat40;
            if (chat != null)
            {
                ApplicationBar.IsVisible = ViewModel.ChatDetails.CanEditChat;
                return;
            }
        }

        private void ChatView_OnBackKeyPress(object sender, CancelEventArgs e)
        {
            if (ViewModel.ProfilePhotoViewer != null
                && ViewModel.ProfilePhotoViewer.IsOpen)
            {
                ViewModel.ProfilePhotoViewer.CloseViewer();
                e.Cancel = true;
                return;
            }
        }

        private bool _once;

        private void TelegramNavigationTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            if (!_once)
            {
                _once = true;
                ViewModel.ForwardInAnimationComplete();
            }
            else
            {
                ViewModel.ChatDetails.UpdateTitles();
            }
        }
    }
}