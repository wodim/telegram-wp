using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Phone.Tasks;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.EmojiPanel;
using TelegramClient.Helpers;
using Caliburn.Micro;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.Utils;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Search;
using TelegramClient.Views;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Media
{
    public class LinksViewModel<T> : LinksViewModelBase<T>,
        Telegram.Api.Aggregator.IHandle<TLMessageCommon>,
        ISliceLoadable
        where T : IInputPeer
    {
        public ObservableCollection<TimeKeyGroup<TLMessageBase>> Files { get; set; }

        public string EmptyListImageSource
        {
            get { return  "/Images/Messages/link.png"; }
        }

        public LinksViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Files = new ObservableCollection<TimeKeyGroup<TLMessageBase>>();
            Status = AppResources.Loading;
            IsEmptyList = false;
            Items = new ObservableCollection<TLMessage>();

            DisplayName = LowercaseConverter.Convert(AppResources.SharedLinks);
            EventAggregator.Subscribe(this);

            PropertyChanged += (o, e) =>
            {
                if (Property.NameEquals(e.PropertyName, () => IsSelectionEnabled))
                {
                    if (!IsSelectionEnabled)
                    {
                        foreach (var item in Items)
                        {
                            item.IsSelected = false;
                        }
                    }
                }
            };
        }

        protected override void OnInitialize()
        {
            BeginOnThreadPool(LoadNextSlice);

            base.OnInitialize();
        }

        private  bool _isLastSliceLoaded;

        private int _lastMinId;

        public void LoadNextSlice()
        {
            if (LazyItems.Count > 0) return;
            if (IsWorking) return;
            if (_isLastSliceLoaded) return;

            if (CurrentItem is TLBroadcastChat && !(CurrentItem is TLChannel))
            {
                Status = string.Empty;
                if (Items.Count == 0)
                {
                    IsEmptyList = true;
                    NotifyOfPropertyChange(() => IsEmptyList);
                }

                return;
            }

            IsWorking = true;
            MTProtoService.SearchAsync(
                CurrentItem.ToInputPeer(),
                TLString.Empty,
                new TLInputMessagesFilterUrl(),
                new TLInt(0), new TLInt(0), new TLInt(0), new TLInt(_lastMinId), new TLInt(Constants.FileSliceLength),
                messages =>
                {
                    var messagesWithLinks = LinkUtils.ProcessLinks(messages.Messages, _mediaWebPagesCache);

                    BeginOnUIThread(() =>
                    {
                        if (messages.Messages.Count == 0
                            || messages.Messages.Count < Constants.FileSliceLength)
                        {
                            _isLastSliceLoaded = true;
                        }

                        if (messages.Messages.Count > 0)
                        {
                            _lastMinId = messages.Messages.Min(x => x.Index);
                        }
                        AddMessages(messagesWithLinks);

                        Status = string.Empty;
                        if (Items.Count == 0)
                        {
                            IsEmptyList = true;
                            NotifyOfPropertyChange(() => IsEmptyList);
                        }

                        IsWorking = false;
                    });
                },
                error =>
                {
                    Execute.ShowDebugMessage("messages.search error " + error);
                    Status = string.Empty;
                    IsWorking = false;
                });
        }

        protected override void DeleteLinksInternal(IList<TLMessageBase> messages)
        {
            BeginOnUIThread(() =>
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    for (var j = 0; j < Files.Count; j++)
                    {
                        for (var k = 0; k < Files[j].Count; k++)
                        {
                            if (Files[j][k].Index == messages[i].Index)
                            {
                                Files[j].RemoveAt(k);
                                break;
                            }
                        }

                        if (Files[j].Count == 0)
                        {
                            Files.RemoveAt(j--);

                            if (Files.Count == 0)
                            {
                                Files.Clear();
                            }
                        }
                    }
                    messages[i].IsSelected = false;
                    Items.Remove(messages[i]);
                    if (Items.Count == 0)
                    {
                        IsEmptyList = true;
                        NotifyOfPropertyChange(() => IsEmptyList);
                    }
                }
            });
        }

        private void InsertMessages(IEnumerable<TLMessageBase> messages)
        {
            foreach (var messageBase in messages)
            {
                var message = messageBase as TLMessage;
                if (message == null)
                {
                    continue;
                }

                var date = TLUtils.ToDateTime(message.Date);

                var yearMonthKey = new DateTime(date.Year, date.Month, 1);
                var timeKeyGroup = Files.FirstOrDefault(x => x.Key == yearMonthKey);
                if (timeKeyGroup != null)
                {
                    timeKeyGroup.Insert(0, message);
                }
                else
                {
                    Files.Insert(0, new TimeKeyGroup<TLMessageBase>(yearMonthKey) { message });
                }

                Items.Insert(0, message);
            }
        }

        private void AddMessages(IEnumerable<TLMessageBase> messages)
        {
            foreach (var messageBase in messages)
            {
                var message = messageBase as TLMessage;
                if (message == null)
                {
                    continue;
                }

                var date = TLUtils.ToDateTime(message.Date);

                var yearMonthKey = new DateTime(date.Year, date.Month, 1);
                var timeKeyGroup = Files.FirstOrDefault(x => x.Key == yearMonthKey);
                if (timeKeyGroup != null)
                {
                    timeKeyGroup.Add(message);
                }
                else
                {
                    Files.Add(new TimeKeyGroup<TLMessageBase>(yearMonthKey) { message });
                }

                Items.Add(message);
            }
        }

        public void Manage()
        {
            IsSelectionEnabled = !IsSelectionEnabled;
        }

        public override void Search()
        {
            StateService.CurrentInputPeer = CurrentItem;
            var source = new List<TLMessageBase>(Items.Count);
            foreach (var item in Items)
            {
                source.Add(item);
            }

            StateService.Source = source;
            NavigationService.UriFor<SearchLinksViewModel>().Navigate();
        }

        public void Handle(TLMessageCommon message)
        {
            if (message == null) return;

            if (message.ToId is TLPeerUser)
            {
                var user = CurrentItem as TLUserBase;
                if (user != null)
                {
                    if (!message.Out.Value
                        && user.Index == message.FromId.Value)
                    {
                        InsertMessage(message);
                    }
                    else if (message.Out.Value
                        && user.Index == message.ToId.Id.Value)
                    {
                        InsertMessage(message);
                    }
                }
            }
            else if (message.ToId is TLPeerChat)
            {
                var chat = CurrentItem as TLChatBase;
                if (chat != null)
                {
                    if (chat.Index == message.ToId.Id.Value)
                    {
                        InsertMessage(message);
                    }
                }
            }
        }

        private void InsertMessage(TLMessageCommon message)
        {
            var messagesWithLinks = LinkUtils.ProcessLinks(new List<TLMessageBase> { message }, _mediaWebPagesCache);
            if (messagesWithLinks.Count > 0)
            {
                BeginOnUIThread(() =>
                {
                    InsertMessages(messagesWithLinks);

                    Status = string.Empty;
                    if (Items.Count == 0)
                    {
                        IsEmptyList = true;
                        NotifyOfPropertyChange(() => IsEmptyList);
                    }
                });
            }
        }
    }

    public abstract class LinksViewModelBase<T> : ItemsViewModelBase<TLMessage>,
        Telegram.Api.Aggregator.IHandle<DownloadableItem>,
        Telegram.Api.Aggregator.IHandle<TLUpdateWebPage>,
        Telegram.Api.Aggregator.IHandle<MessagesRemovedEventArgs> 
        where T : IInputPeer
    {
        public T CurrentItem { get; set; }

        public bool IsEmptyList { get; protected set; }

        private bool _isSelectionEnabled;

        public bool IsSelectionEnabled
        {
            get { return _isSelectionEnabled; }
            set { SetField(ref _isSelectionEnabled, value, () => IsSelectionEnabled); }
        }

        public void ChangeGroupActionStatus()
        {
            NotifyOfPropertyChange(() => IsGroupActionEnabled);
        }

        public bool IsGroupActionEnabled
        {
            get { return Items.Any(x => x.IsSelected); }
        }

        protected LinksViewModelBase(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {

        }

        public abstract void Search();

        public void DeleteMessage(TLMessageBase message)
        {
            if (message == null) return;

            var messages = new List<TLMessageBase> { message };

            var owner = CurrentItem as TLObject;

            if (CurrentItem is TLBroadcastChat)
            {
                DeleteMessagesInternal(owner, messages);
                return;
            }

            if ((message.Id == null || message.Id.Value == 0) && message.RandomIndex != 0)
            {
                DeleteMessagesInternal(owner, messages);
                return;
            }

            DialogDetailsViewModel.DeleteMessages(MTProtoService, null, null, messages, null, (result1, result2) => DeleteMessagesInternal(owner, result2));
        }

        private void DeleteMessagesInternal(TLObject owner, IList<TLMessageBase> messages)
        {
            var ids = new TLVector<TLInt>();
            for (int i = 0; i < messages.Count; i++)
            {
                ids.Add(messages[i].Id);
            }

            // duplicate: deleting performed through updates
            CacheService.DeleteMessages(ids);

            DeleteLinksInternal(messages);

            EventAggregator.Publish(new DeleteMessagesEventArgs { Owner = owner, Messages = messages });
        }

        protected virtual void DeleteLinksInternal(IList<TLMessageBase> messages) { }

        public void DeleteMessages()
        {
            if (Items == null) return;

            var messages = new List<TLMessageBase>();
            foreach (var item in Items.Where(x => x.IsSelected))
            {
                messages.Add(item);
            }

            if (messages.Count == 0) return;

            var randomItems = messages.Where(x => (x.Id == null || x.Id.Value == 0) && x.RandomId != null).ToList();
            var items = messages.Where(x => x.Id != null && x.Id.Value != 0).ToList();

            if (randomItems.Count == 0 && items.Count == 0)
            {
                return;
            }

            IsSelectionEnabled = false;

            var owner = CurrentItem as TLObject;

            if (CurrentItem is TLBroadcastChat)
            {
                DeleteMessagesInternal(owner, randomItems);
                DeleteMessagesInternal(owner, items);
                return;
            }

            DialogDetailsViewModel.DeleteMessages(MTProtoService, null, randomItems, items, (result1, result2) => DeleteMessagesInternal(owner, result2), (result1, result2) => DeleteMessagesInternal(owner, result2));
        }

        public void ForwardMessage(TLMessageBase message)
        {
            if (message == null) return;

            DialogDetailsViewModel.ForwardMessagesCommon(new List<TLMessageBase> { message }, StateService, NavigationService);
        }

        public void ForwardMessages()
        {
            if (Items == null) return;

            var messages = new List<TLMessageBase>();
            foreach (var item in Items.Where(x => x.IsSelected))
            {
                messages.Add(item);
            }

            if (messages.Count == 0) return;

            IsSelectionEnabled = false;

            DialogDetailsViewModel.ForwardMessagesCommon(messages, StateService, NavigationService);
        }

        public void OpenMedia(TLMessage message)
        {
            if (message == null) return;

            var mediaWebPage = message.Media as TLMessageMediaWebPage;
            if (mediaWebPage != null)
            {
                var webPage = mediaWebPage.WebPage as TLWebPage;
                if (webPage != null)
                {
                    var url = webPage.Url.ToString();
                    OpenLink(url);
                }
            }
        }

        public void OpenLink(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                if (url.ToLowerInvariant().Contains("telegram.me"))
                {
                    DialogDetailsViewModel.OnTelegramLinkActionCommon(MTProtoService, StateService, new TelegramEventArgs{ Uri = url });
                }
                else
                {
                    var task = new WebBrowserTask();
                    task.Uri = new Uri(url);
                    task.Show();
                }
            }
        }

        public void Handle(DownloadableItem item)
        {
            var webPage = item.Owner as TLWebPage;
            if (webPage != null)
            {
                var messages = Items;
                foreach (var m in messages)
                {
                    var media = m.Media as TLMessageMediaWebPage;
                    if (media != null && media.WebPage == webPage)
                    {
                        media.NotifyOfPropertyChange(() => media.Photo);
                        media.NotifyOfPropertyChange(() => media.Self);
                        break;
                    }
                }
            }
        }

        public void Handle(MessagesRemovedEventArgs args)
        {
            var with = CurrentItem as TLObject;
            if (with == args.Dialog.With && args.Messages != null)
            {
                DeleteLinksInternal(args.Messages);
            }
        }

        protected readonly List<TLMessageMediaWebPage> _mediaWebPagesCache = new List<TLMessageMediaWebPage>();

        public void Handle(TLUpdateWebPage updateWebPage)
        {
            Execute.BeginOnUIThread(() =>
            {
                for (var i = 0; i < _mediaWebPagesCache.Count; i++)
                {
                    var mediaWebPage = _mediaWebPagesCache[i];
                    if (mediaWebPage.WebPage.Id.Value == updateWebPage.WebPage.Id.Value)
                    {
                        mediaWebPage.WebPage = updateWebPage.WebPage;

                        foreach (var item in Items)
                        {
                            var itemMediaWebPage = item.Media as TLMessageMediaWebPage;
                            if (itemMediaWebPage != null
                                && itemMediaWebPage.WebPage.Id.Value == mediaWebPage.WebPage.Id.Value)
                            {
                                item.NotifyOfPropertyChange(() => item.Self);
                            }
                        }

                        _mediaWebPagesCache.RemoveAt(i--);
                    }
                }
            });
        }
    }

    public static class LinkUtils
    {
        public static List<TLMessageBase> ProcessLinks(IList<TLMessageBase> messages, IList<TLMessageMediaWebPage> mediaWebPagesCache)
        {
            const string linkPattern = "(https?:\\/\\/)?(([A-Za-zА-Яа-яЁё0-9@][A-Za-zА-Яа-яЁё0-9@\\-_\\.]*[A-Za-zА-Яа-яЁё0-9@])(\\/([A-Za-zА-Яа-я0-9@\\-_#%&?+\\/\\.=;:~]*[^\\.\\,;\\(\\)\\?<\\&\\s:])?)?)";
            const string ipv4Pattern = @"([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}"; 
                //"((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.)\\{3\\}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)";
            var messagesWithLinks = new List<TLMessageBase>();
            foreach (var messageBase in messages)
            {
                var message = messageBase as TLMessage;
                if (message != null)
                {
                    var text = message.Message.ToString();
                    message.Links = new List<string>();
                    foreach (Match m in Regex.Matches(text, linkPattern, RegexOptions.IgnoreCase))
                    {
                        var url = GetUrl(m);
                        if (url != null)
                        {
                            message.Links.Add(url);
                        }
                    }

                    foreach (Match m in Regex.Matches(text, ipv4Pattern, RegexOptions.IgnoreCase))
                    {
                        message.Links.Add("http://" + m.Value);
                    }

                    var mediaEmpty = message.Media as TLMessageMediaEmpty;
                    if (mediaEmpty != null)
                    {
                        if (message.Links.Count > 0)
                        {
                            var title = GetWebPageTitle(message.Links[0]);
                            message.WebPageTitle = title;
                        }
                    }

                    var mediaWebPage = message.Media as TLMessageMediaWebPage;
                    if (mediaWebPage != null)
                    {
                        var webPage = mediaWebPage.WebPage as TLWebPage;
                        if (webPage != null)
                        {
                            if (message.Links.Count == 0)
                            {
                                message.Links.Add(webPage.Url.ToString());
                            }
                        }

                        var webPageEmpty = mediaWebPage.WebPage as TLWebPageEmpty;
                        if (webPageEmpty != null)
                        {
                            if (message.Links.Count > 0)
                            {
                                var title = GetWebPageTitle(message.Links[0]);
                                message.WebPageTitle = title;
                            }
                        }

                        var webPagePending = mediaWebPage.WebPage as TLWebPagePending;
                        if (webPagePending != null)
                        {
                            mediaWebPagesCache.Add(mediaWebPage);

                            if (message.Links.Count > 0)
                            {
                                var title = GetWebPageTitle(message.Links[0]);
                                message.WebPageTitle = title;
                            }
                        }
                    }

                    if (message.Links.Count > 0)
                    {
                        messagesWithLinks.Add(messageBase);
                    }
                    else
                    {
                        
                    }
                }
            }
            return messagesWithLinks;
        }

        private static string GetWebPageTitle(string url)
        {
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                if (!string.IsNullOrEmpty(uri.Host))
                {
                    var parts = uri.Host.Split('.');
                    if (parts.Length >= 2)
                    {
                        return Language.CapitalizeFirstLetter(parts[parts.Length - 2]);
                    }
                }

            }

            return null;
        }

        private static string GetUrl(Match m)
        {
            var protocol = (m.Groups.Count > 1) ? m.Groups[1].Value : "http://";
            if (protocol == string.Empty) protocol = "http://";

            var url = (m.Groups.Count > 2) ? m.Groups[2].Value : string.Empty;
            var domain = (m.Groups.Count > 3) ? m.Groups[3].Value : string.Empty;

            if (domain.IndexOf(".") == -1 || domain.IndexOf("..") != -1) return null;
            if (url.IndexOf("@") != -1) return null;

            var topDomain = domain.Split('.').LastOrDefault();
            if (topDomain.Length > 5 ||
                !("guru,info,name,aero,arpa,coop,museum,mobi,travel,xxx,asia,biz,com,net,org,gov,mil,edu,int,tel,ac,ad,ae,af,ag,ai,al,am,an,ao,aq,ar,as,at,au,aw,az,ba,bb,bd,be,bf,bg,bh,bi,bj,bm,bn,bo,br,bs,bt,bv,bw,by,bz,ca,cc,cd,cf,cg,ch,ci,ck,cl,cm,cn,co,cr,cu,cv,cx,cy,cz,de,dj,dk,dm,do,dz,ec,ee,eg,eh,er,es,et,eu,fi,fj,fk,fm,fo,fr,ga,gd,ge,gf,gg,gh,gi,gl,gm,gn,gp,gq,gr,gs,gt,gu,gw,gy,hk,hm,hn,hr,ht,hu,id,ie,il,im,in,io,iq,ir,is,it,je,jm,jo,jp,ke,kg,kh,ki,km,kn,kp,kr,kw,ky,kz,la,lb,lc,li,lk,lr,ls,lt,lu,lv,ly,ma,mc,md,me,mg,mh,mk,ml,mm,mn,mo,mp,mq,mr,ms,mt,mu,mv,mw,mx,my,mz,na,nc,ne,nf,ng,ni,nl,no,np,nr,nu,nz,om,pa,pe,pf,pg,ph,pk,pl,pm,pn,pr,ps,pt,pw,py,qa,re,ro,ru,rw,sa,sb,sc,sd,se,sg,sh,si,sj,sk,sl,sm,sn,so,sr,st,su,sv,sy,sz,tc,td,tf,tg,th,tj,tk,tl,tm,tn,to,tp,tr,tt,tv,tw,tz,ua,ug,uk,um,us,uy,uz,va,vc,ve,vg,vi,vn,vu,wf,ws,ye,yt,yu,za,zm,zw,рф,cat,pro"
                    .Split(',').Contains(topDomain))) return null;

            return (protocol + url);
        }

    }
}
