using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.Views.Media;

namespace TelegramClient.Views.Contacts
{
    public partial class ContactView
    {
        private ContactViewModel ViewModel { get { return DataContext as ContactViewModel; } }

        private readonly AppBarButton _editButton = new AppBarButton
        {
            Text = AppResources.Edit,
            IconUri = new Uri("/Images/ApplicationBar/appbar.edit.png", UriKind.Relative)
        };

        private readonly AppBarButton _shareButton = new AppBarButton
        {
            Text = AppResources.Share,
            IconUri = new Uri("/Images/ApplicationBar/appbar.share.png", UriKind.Relative)
        };

        private readonly AppBarMenuItem _addToGroupMenuItem = new AppBarMenuItem
        {
            Text = AppResources.AddToGroup
        };

        private readonly AppBarMenuItem _blockMenuItem = new AppBarMenuItem
        {
            Text = AppResources.BlockContact
        };

        private readonly AppBarMenuItem _unblockMenuItem = new AppBarMenuItem
        {
            Text = AppResources.UnblockContact
        };

        private readonly AppBarMenuItem _addMenuItem = new AppBarMenuItem
        {
            Text = AppResources.AddContact
        };

        private readonly AppBarMenuItem _deleteMenuItem = new AppBarMenuItem
        {
            Text = AppResources.DeleteContact
        };

        private IApplicationBar _prevAppBar;

        public ContactView()
        {
            var timer = Stopwatch.StartNew();

            InitializeComponent();

            OptimizeFullHD();

            _editButton.Click += (sender, args) => ViewModel.Edit();
            _shareButton.Click += (sender, args) => ViewModel.Share();

            _addToGroupMenuItem.Click += (sender, args) => ViewModel.AddToGroup();
            _blockMenuItem.Click += (sender, args) => ViewModel.BlockContact();
            _unblockMenuItem.Click += (sender, args) => ViewModel.UnblockContact();
            _addMenuItem.Click += (sender, args) => ViewModel.AddContact();
            _deleteMenuItem.Click += (sender, args) => ViewModel.DeleteContact();

            Loaded += (sender, args) =>
            {
                _blockMenuItem.Text = ViewModel.ContactDetails.IsBot ? AppResources.StopBot : AppResources.BlockContact;
                _unblockMenuItem.Text = ViewModel.ContactDetails.IsBot ? AppResources.RestartBot : AppResources.UnblockContact;

                TimerString.Text = timer.Elapsed.ToString();

                if (ViewModel.ProfilePhotoViewer != null)
                    ViewModel.ProfilePhotoViewer.PropertyChanged += OnProfileViewerPropertyChanged;

                ViewModel.ContactDetails.BlockedStatusChanged += OnBlockedStatusChanged;
                ViewModel.ContactDetails.ImportStatusChanged += OnImportStatusChanged;
                ViewModel.ContactDetails.PropertyChanged += OnContactDetailsPropertyChanges;

                BuildLocalizedAppBar();
            };

            Unloaded += (sender, args) =>
            {
                if (ViewModel.ProfilePhotoViewer != null)
                    ViewModel.ProfilePhotoViewer.PropertyChanged -= OnProfileViewerPropertyChanged;

                ViewModel.ContactDetails.BlockedStatusChanged -= OnBlockedStatusChanged;
                ViewModel.ContactDetails.ImportStatusChanged -= OnImportStatusChanged;
                ViewModel.ContactDetails.PropertyChanged -= OnContactDetailsPropertyChanges;
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

        private void OnContactDetailsPropertyChanges(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ContactDetails.ProfilePhotoViewer))
            {
                ViewModel.ProfilePhotoViewer.PropertyChanged += OnProfileViewerPropertyChanged;
            }
        }

        private void OnImportStatusChanged(object sender, ImportEventArgs e)
        {
            Execute.OnUIThread(() =>
            {
                if (ApplicationBar == null) return;

                if (e.Imported)
                {
                    ApplicationBar.MenuItems.Remove(_addMenuItem);
                    ApplicationBar.MenuItems.Insert(0, _deleteMenuItem);
                }
                else
                {
                    ApplicationBar.MenuItems.Remove(_deleteMenuItem);
                    ApplicationBar.MenuItems.Insert(0, _addMenuItem);
                }
            });
        }

        private void OnBlockedStatusChanged(object sender, BlockedEventArgs e)
        {
            Execute.OnUIThread(() =>
            {
                if (ApplicationBar == null) return;

                if (e.Blocked)
                {
                    ApplicationBar.MenuItems.Remove(_blockMenuItem);
                    ApplicationBar.MenuItems.Add(_unblockMenuItem);
                }
                else
                {
                    ApplicationBar.MenuItems.Remove(_unblockMenuItem);
                    ApplicationBar.MenuItems.Add(_blockMenuItem);
                }
            });
        }

        private void OnProfileViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ProfilePhotoViewer.IsOpen))
            {
                ViewModel.NotifyOfPropertyChange(() => ViewModel.IsViewerOpen);

                if (ViewModel.ProfilePhotoViewer.IsOpen)
                {
                    _prevAppBar = ApplicationBar;

                    var profilePhotoViewerView = ProfilePhotoViewer.Content as ProfilePhotoViewerView;
                    ApplicationBar = profilePhotoViewerView != null? profilePhotoViewerView.ApplicationBar : null;
                }
                else
                {
                    // wait to finish closing profile viewer animation
                    Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.25),
                        () =>
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

            var userDeleted = ViewModel.Contact as TLUserDeleted;
            if (userDeleted != null)
            {
                return;
            }

            ApplicationBar = new ApplicationBar();

            ApplicationBar.Buttons.Add(_shareButton);
            ApplicationBar.Buttons.Add(_editButton);

            var user = ViewModel.Contact as TLUser;
            if (user != null && user.IsBot && !user.IsBotGroupsBlocked)
            {
                ApplicationBar.MenuItems.Add(_addToGroupMenuItem);
            }

            if (ViewModel.Contact is TLUserContact)
            {
                ApplicationBar.MenuItems.Add(_deleteMenuItem);
            }
            else if (ViewModel.ContactDetails.HasPhone)
            {
                ApplicationBar.MenuItems.Add(_addMenuItem);
            }
        }

        private void ContactView_OnBackKeyPress(object sender, CancelEventArgs e)
        {
            if (ViewModel.ProfilePhotoViewer != null 
                && ViewModel.ProfilePhotoViewer.IsOpen)
            {
                ViewModel.ProfilePhotoViewer.CloseViewer();
                e.Cancel = true;
                return;
            }
        }
    }
}