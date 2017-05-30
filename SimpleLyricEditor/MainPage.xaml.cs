﻿using HappyStudio.UwpToolsLibrary.Auxiliarys;
using HappyStudio.UwpToolsLibrary.Information;
using Microsoft.Services.Store.Engagement;
using SimpleLyricEditor.Models;
using SimpleLyricEditor.Tools;
using SimpleLyricEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace SimpleLyricEditor
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Main_ViewModel model = App.Locator.Main;
        private bool isListViewGotFocus = false;
        public MainPage()
        {
            this.InitializeComponent();

            model.SelectedItems = Lyrics_ListView.SelectedItems;

            audioPlayer.PositionChanged += (s, e) =>
            {
                if (e.IsUserChange)
                {
                    singleLyricPreview.RepositionLyric();
                    multipleLyricPreview.Reposition();
                }
                else
                {
                    singleLyricPreview.PreviewLyric();
                    multipleLyricPreview.RefreshLyric();
                }
            };

            audioPlayer.MusicFileChanged += (s, e) =>
            {
                model.AssociatedTags(e.NewMusic);

                singleLyricPreview.RepositionLyric();
                multipleLyricPreview.Reposition();
            };

            model.LyricItemChanged += (s, e) =>
            {
                singleLyricPreview.RepositionLyric();
                multipleLyricPreview.Reposition();

                //添加歌词时自动定位到新加的歌词项
                if (e.ChangeType == EventArgss.LyricItemOperationType.Add && Lyrics_ListView.SelectedItem is null)
                    ThreadPoolTimer.CreateTimer(async (t) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (App.IsPressShift)
                        {
                            Lyrics_ListView.SelectedItem = Lyrics_ListView.Items[e.Lyrics.Count - 1];
                            Lyrics_ListView.SelectedItem = null;
                        }
                        else
                            Lyrics_ListView.ScrollIntoView(Lyrics_ListView.Items.LastOrDefault());
                    }), TimeSpan.FromMilliseconds(100));
            };

            CoreWindow window = CoreWindow.GetForCurrentThread();
            window.KeyDown += Window_KeyDown;
            window.KeyUp += Window_KeyUp;

            MiniMode_StackPanel.Visibility = SystemInfo.BuildVersion >= 15063 ? Visibility.Visible : Visibility.Collapsed;
            FeedbackInFeedbackHub_MenuFlyoutItem.Visibility = StoreServicesFeedbackLauncher.IsSupported() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HideFastMenu()
        {
            FastMenu_Grid.Visibility = Visibility.Collapsed;
        }

        private async void GotoThisLyricTime()
        {
            try
            {
                audioPlayer.SetTime((Lyrics_ListView.SelectedItems.SingleOrDefault() as Lyric).Time);
            }
            catch (NullReferenceException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                await MessageBox.ShowAsync(CharacterLibrary.ErrorDialog.GetString("SelectedMultipleItemsError"), CharacterLibrary.ErrorDialog.GetString("Close"));
            }
            model.storeLogger.Log("通过快捷键跳转到选定歌词项的时间上");
        }

        private void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Shift)
            {
                AddLyric_Button.Content = "\uE94D";
                DelLyric_Button.Content = "\uE107";
            }

            if (!App.IsInputBoxGotFocus)
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.Space:
                        this.Focus(FocusState.Pointer);
                        break;
                    case VirtualKey.G:
                        GotoThisLyricTime();
                        break;
                    case VirtualKey.Up:
                        if (Lyrics_ListView.SelectedIndex == -1)
                            Lyrics_ListView.SelectedIndex = Lyrics_ListView.Items.Count - 1;
                        else
                        {
                            //抵消器，防止出现一次跳两行的情况
                            if (isListViewGotFocus && Lyrics_ListView.SelectedIndex != 0)
                                Lyrics_ListView.SelectedIndex++;

                            Lyrics_ListView.SelectedIndex = Lyrics_ListView.SelectedIndex > 0 ? Lyrics_ListView.SelectedIndex - 1 : -1;
                        }
                        this.Focus(FocusState.Pointer);
                        model.storeLogger.Log("选定上一个歌词");
                        break;
                    case VirtualKey.Down:
                        //抵消器，防止出现一次跳两行的情况
                        if (isListViewGotFocus && Lyrics_ListView.SelectedIndex != Lyrics_ListView.Items.Count - 1 && Lyrics_ListView.SelectedIndex != -1)
                            Lyrics_ListView.SelectedIndex--;

                        Lyrics_ListView.SelectedIndex = Lyrics_ListView.SelectedIndex < Lyrics_ListView.Items.Count - 1 ? Lyrics_ListView.SelectedIndex + 1 : -1;
                        this.Focus(FocusState.Pointer);
                        model.storeLogger.Log("选定下一个歌词");
                        break;
                }
            }
        }

        private void Window_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Shift)
            {
                AddLyric_Button.Content = "\uE109";
                DelLyric_Button.Content = "\uE10A";
            }
        }

        #region 歌词编辑区
        private void LyricsContent_TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && sender is TextBox t)
            {
                e.Handled = true;

                model.LyricContent = t.Text;

                void NewLine()
                {
                    t.Text += "\n";
                    t.Select(t.Text.Length, 0);
                    model.storeLogger.Log("歌词文本框--换行");
                }

                if (App.IsPressCtrl)
                    model.AddLyric();
                else if (App.IsPressShift)
                    NewLine();
                else if (Lyrics_ListView.SelectedItems.Any())
                {
                    model.ChangeContent();
                    Lyrics_ListView.SelectedIndex = Lyrics_ListView.SelectedIndex < Lyrics_ListView.Items.Count - 1 ? Lyrics_ListView.SelectedIndex + 1 : -1;
                    
                    if (Lyrics_ListView.SelectedIndex == -1)
                        this.Focus(FocusState.Pointer);
                }
                else
                    NewLine();
            }
        }

        private void MultilineEditMode_Button_Click(object sender, RoutedEventArgs e)
        {
            Lyrics_ListView.SelectionMode = ListViewSelectionMode.Multiple;
            model.IsMultilineEditMode = true;
            model.storeLogger.Log("切换到多行编辑模式");
        }

        private void ExitMultilineEditMode_Button_Click(object sender, RoutedEventArgs e)
        {
            Lyrics_ListView.SelectionMode = ListViewSelectionMode.Single;
            model.IsMultilineEditMode = false;
            model.storeLogger.Log("退出多行编辑模式");
        }

        private void Select_Reverse_MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            foreach (ListViewItem item in Lyrics_ListView.ItemsPanelRoot.Children)
                item.IsSelected = !item.IsSelected;
            model.storeLogger.Log("选择工具--反选");
        }

        private void Select_BeforeItem_MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (Lyrics_ListView.SelectedItem is Lyric)
                foreach (ListViewItem item in Lyrics_ListView.ItemsPanelRoot.Children)
                {
                    if (item.IsSelected)
                        break;
                    item.IsSelected = true;
                }

            model.storeLogger.Log("选择工具--选择之前项");
        }

        private void Select_AfterItem_MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (Lyrics_ListView.SelectedItem is Lyric)
                for (int i = Lyrics_ListView.Items.Count - 1; i > 0; i--)
                {
                    ListViewItem l = (ListViewItem)Lyrics_ListView.ItemsPanelRoot.Children[i];
                    if (l.IsSelected)
                        break;
                    l.IsSelected = true;
                }

            model.storeLogger.Log("选择工具--选择之后项");
        }

        private void Select_Paragraph_MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            //首先确认当前选定项是否有内容
            if (Lyrics_ListView.SelectedItem is Lyric thisSelectedItem && !String.IsNullOrWhiteSpace(thisSelectedItem.Content))
            {
                //当前选定项ID
                int sID = Lyrics_ListView.SelectedIndex;

                //遍历并选定接下来的项目 直到碰到没有内容的项目
                for (int i = sID; i < Lyrics_ListView.Items.Count; i++)
                {
                    if (String.IsNullOrWhiteSpace((Lyrics_ListView.Items[i] as Lyric).Content))
                        break;
                    (Lyrics_ListView.ItemsPanelRoot.Children[i] as ListViewItem).IsSelected = true;
                }

                //跟上面的一样，不过这次是反向遍历
                for (int i = sID; i > 0; i--)
                {
                    if (String.IsNullOrWhiteSpace((Lyrics_ListView.Items[i] as Lyric).Content))
                        break;
                    (Lyrics_ListView.ItemsPanelRoot.Children[i] as ListViewItem).IsSelected = true;
                }
                model.storeLogger.Log("选择工具--选择段落");
            }
        }
        #endregion
        #region 歌词列表
        private void Lyrics_ListView_GotFocus(object sender, RoutedEventArgs e)
        {
            isListViewGotFocus = true;
        }

        private void Lyrics_ListView_LostFocus(object sender, RoutedEventArgs e)
        {
            isListViewGotFocus = false;
        }

        private void Lyrics_ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var currentListView = sender as ListView;
            string content = String.Empty;

            foreach (LyricItem item in e.RemovedItems)
                item.IsSelected = false;

            foreach (LyricItem item in currentListView.SelectedItems)
            {
                content += item.Content + "\r\n";
                item.IsSelected = true;
            }

            model.LyricContent = content.Trim();

            if (currentListView.SelectionMode == ListViewSelectionMode.Single && e.AddedItems.Any())
            {
                int ViewItemsCount = (int)currentListView.ActualHeight / 44;
                int selectedIndex = currentListView.SelectedIndex;
                int resuit = 0;
                ScrollIntoViewAlignment s = ScrollIntoViewAlignment.Leading;
                switch (model.Settings.SelectItemAlwaysStaysIn)
                {
                    case SelectItemAlwaysStaysIn_Enum.Top:
                        resuit = selectedIndex;
                        break;
                    case SelectItemAlwaysStaysIn_Enum.Center:
                        resuit = selectedIndex > (ViewItemsCount / 2) ? selectedIndex - (ViewItemsCount / 2) : 0;
                        break;
                    case SelectItemAlwaysStaysIn_Enum.Bottom:
                        resuit = selectedIndex > ViewItemsCount ? selectedIndex - (ViewItemsCount - 1) : 0;
                        break;
                    case SelectItemAlwaysStaysIn_Enum.ViewableArea:
                        resuit = selectedIndex;
                        s = ScrollIntoViewAlignment.Default;
                        break;
                    default:
                        throw new Exception("未找到与当前值对应的分支");
                }
                currentListView.ScrollIntoView(currentListView.Items[resuit], s);
            }
        }

        private void LyricItemTemplate_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            GotoThisLyricTime();
        }

        private void LyricItemTemplate_GotoThisTime_Click(object sender, EventArgs e)
        {
            audioPlayer.SetTime((sender as Lyric).Time);
            model.storeLogger.Log("通过跳转按钮跳转到选定歌词项的时间上");
        }
        #endregion
        #region 预览区域

        private void Expand_Button_Click(object sender, RoutedEventArgs e)
        {
            model.IsDisplayScrollLyricsPreview = true;
            Grid.SetRow(LyricsPreview_Grid, 1);

            foreach (LyricItem item in Lyrics_ListView.SelectedItems)
                item.IsSelected = false;

            multipleLyricPreview.StartPreview();
            model.storeLogger.Log("切换到滚动歌词预览模式");
        }

        private void Fold_Button_Click(object sender, RoutedEventArgs e)
        {
            model.IsDisplayScrollLyricsPreview = false;
            Grid.SetRow(LyricsPreview_Grid, 2);

            foreach (LyricItem item in Lyrics_ListView.SelectedItems)
                item.IsSelected = true;

            multipleLyricPreview.StopPreview();
            model.storeLogger.Log("切换到编辑模式");
        }

        private async void MiniMode_Button_Click(object sender, RoutedEventArgs e)
        {
            ViewModePreferences c = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
            c.CustomSize = new Size(500, 500);
            bool b = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, c);

            if (b)
            {
                (sender as Button).Visibility = Visibility.Collapsed;
                ExitMiniMode_Button.Visibility = Visibility.Visible;
            }
            else
            {
                ContentDialog dialog = new ContentDialog()
                {
                    Content = CharacterLibrary.ErrorDialog.GetString("MiniModeEnterError"),
                    SecondaryButtonText = CharacterLibrary.ErrorDialog.GetString("Close")
                };
                await dialog.ShowAsync();
            }
            model.storeLogger.Log("切换到迷你模式");
        }

        private async void ExitMiniMode_Button_Click(object sender, RoutedEventArgs e)
        {
            await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
            (sender as Button).Visibility = Visibility.Collapsed;
            MiniMode_Button.Visibility = Visibility.Visible;
            model.storeLogger.Log("退出迷你模式");
        }

        #endregion
        #region 低栏
        private void MenuFlyout_Opened(object sender, object e)
        {
            var menu = sender as MenuFlyout;
            Style s = new Style()
            {
                TargetType = typeof(MenuFlyoutPresenter)
            };
            s.Setters.Add(new Setter { Property = RequestedThemeProperty, Value = model.Settings.PageTheme });
            menu.MenuFlyoutPresenterStyle = s;
        }

        private void Settings_AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            HidePanel_SplitView.IsPaneOpen = !HidePanel_SplitView.IsPaneOpen;
            model.storeLogger.Log("打开设置面板");
        }

        private async void FeedbackInEmail_MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            await EmailEx.SendAsync("kljzndx@outlook.com", $"{AppInfo.Name} {AppInfo.Version} {(AppInfo.Name == "简易歌词编辑器" ? "反馈" : "Feedback")}", String.Empty);
        }

        private async void FeedbackInSkype_MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            DataPackage dp = new DataPackage();
            dp.RequestedOperation = DataPackageOperation.Copy;
            dp.SetText("kljzndx@outlook.com");
            Clipboard.Clear();
            Clipboard.SetContent(dp);

            bool isZhCn = AppInfo.Name == "简易歌词编辑器";
            ContentDialog dialog = new ContentDialog() { Title = isZhCn ? "Skype账号" : "Skype Account", Content = isZhCn ? "Skype账号已复制" : "Skype Account Copied", SecondaryButtonText = isZhCn ? "关闭" : "Close" };
            await dialog.ShowAsync();
        }

        private async void FeedbackInFeedbackHub_MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (!await StoreServicesFeedbackLauncher.GetDefault().LaunchAsync())
            {
                ContentDialog dialog = new ContentDialog() { Title = CharacterLibrary.ErrorDialog.GetString("Title"), Content = CharacterLibrary.ErrorDialog.GetString("ShowFeedbackHubError"), SecondaryButtonText = CharacterLibrary.ErrorDialog.GetString("Close") };
                await dialog.ShowAsync();
            }
        }

        #endregion

        private void Main_Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Link;
            e.DragUIOverride.Caption = CharacterLibrary.DragOrDrop.GetString("Caption");
        }

        private async void Main_Grid_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                return;
            var items = await e.DataView.GetStorageItemsAsync();

            bool isMusicFound = false;
            bool isImageFound = false;

            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    switch (file.FileType.ToLower())
                    {
                        case ".mp3":
                        case ".flac":
                        case ".wav":
                        case ".aac":
                        case ".m4a":
                            audioPlayer.SetSource(await Music.ParseAsync(file));
                            isMusicFound = true;
                            break;
                        case ".png":
                        case ".jpg":
                            if (!isImageFound)
                            {
                                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("BackgroundImage", file);
                                model.Settings.LocalBackgroundImagePath = file.Path;
                            }
                            break;
                    }
                }
                if (isMusicFound && isImageFound)
                    break;
            }
        }

        private void LyricsFileInfo_ContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            model.InputBoxGotFocus();
            model.storeLogger.Log("打开歌词文件信息弹窗");
        }

        private void LyricsFileInfo_ContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            model.InputBoxLostFocus();
        }

        private void SingleLyricPreview_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Lyrics_ListView.SelectedIndex = -1;
        }

    }
}
