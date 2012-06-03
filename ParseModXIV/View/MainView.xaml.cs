﻿// ParseModXIV
// MainView.xaml.cs
//  
// Created by Ryan Wilson.
// Copyright (c) 2010-2012, Ryan Wilson. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using AppModXIV.Classes;
using MahApps.Metro;
using MahApps.Metro.Controls;
using ParseModXIV.Classes;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using MessageBox = System.Windows.MessageBox;

namespace ParseModXIV.View
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : MetroWindow, IDisposable
    {
        #region " VARIABLES "

        public readonly string Lpath = "";
        public readonly string Ipath = "";
        public int Counter;
        public string Mysql = "";
        private readonly AutomaticUpdates _autoUpdates = new AutomaticUpdates();
        public static MainView View;
        private DispatcherTimer _expTimer = new DispatcherTimer();
        private NotifyIcon _myNotifyIcon;
        private readonly XDocument _xAtCodes = XDocument.Load("./Resources/ATCodes.xml");
        private readonly XDocument _xSettings = XDocument.Load("./Resources/Settings_Parse.xml");
        private Color _tsColor;
        private Color _bColor;
        public static readonly List<string[]> BattleLog = new List<string[]>();
        public static readonly List<string[]> HealingLog = new List<string[]>();
        private static string lang = "en";

        #endregion

        public MainView()
        {
            InitializeComponent();
            // Insert code required on object creation below this point.
            View = this;
            Lpath = "./Logs/ParseMod/";
            Ipath = "./ScreenShots/ParseMod/";
            //MessageBox.Show(AppDomain.CurrentDomain.FriendlyName);
        }

        ///// <summary>
        ///// 
        ///// </summary>
        //private static void UpdateNotify(string message)
        //{
        //    System.Threading.Thread thread = new System.Threading.Thread(
        //        new System.Threading.ThreadStart(
        //            delegate()
        //            {
        //                //name of control here:
        //                View.Dispatcher.Invoke(
        //                    new Action(
        //                        delegate()
        //                        {
        //                            //do stuff here
        //                        }
        //                        ));
        //            }
        //            ));
        //    thread.Start();
        //}

        #region " FORM OPEN-CLOSE-STATES "

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLocale();
            DirectoryStructure();
            LoadXml();
            ApplyTheme();
            ApplyColor();
            ApplyFonts();
            ApplySettings();
            CreateNotify();
            CheckUpdates();
            Start();
            Func<bool> d = delegate
            {
                if (File.Exists("ParseModXIV.bak"))
                {
                    File.Delete("ParseModXIV.bak");
                }
                if (File.Exists("AppModXIV.bak"))
                {
                    File.Delete("AppModXIV.bak");
                }
                return true;
            };
            d.BeginInvoke(null, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        private static void ProcessXml(string filePath)
        {
            using (var reader = new XmlTextReader(filePath))
            {
                string key, time;
                var value = key = time = "";
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            while (reader.MoveToNextAttribute())
                            {
                                if (reader.Name == "Value")
                                {
                                    value = reader.Value;
                                }
                                if (reader.Name == "Key")
                                {
                                    key = reader.Value;
                                }
                                if (reader.Name == "Time")
                                {
                                    time = reader.Value;
                                }
                            }
                            break;
                    }
                    if (value == "" || key == "" || time == "")
                    {
                        continue;
                    }
                    ChatWorkerDelegate.OnDebugline(time, key, value);
                    value = key = time = "";
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Constants.FfxivOpen)
            {
                ParseMod.Instance.StopLogging();
            }
            if (App.MArgs == null)
            {
                if (Settings.Default.Gui_ExportXML)
                {
                    //StatGroupToXml.ExportParty();
                    //StatGroupToXml.ExportMonsterStats();
                    //StatGroupToXml.ExportBattleLog();
                    //StatGroupToXml.ExportHealingLog();
                }
                if (Settings.Default.Gui_SaveLog)
                {
                    if (ChatWorkerDelegate.XmlWriteLog.LineCount > 1)
                    {
                        ChatWorkerDelegate.XmlWriteLog.WriteToDisk(Lpath + DateTime.Now.ToString("dd.MM.yyyy.HH.mm.ss") + "_Log.xml");
                    }
                }
                if (ChatWorkerDelegate.XmlWriteUnmatchedLog.LineCount > 1)
                {
                    ChatWorkerDelegate.XmlWriteUnmatchedLog.WriteToDisk(Lpath + DateTime.Now.ToString("dd.MM.yyyy.HH.mm.ss") + "_Unmatched_Log.xml");
                }
            }
            else if (Settings.Default.DebugMode)
            {
                if (ChatWorkerDelegate.XmlWriteUnmatchedLog.LineCount > 1)
                {
                    ChatWorkerDelegate.XmlWriteUnmatchedLog.WriteToDisk(Lpath + DateTime.Now.ToString("dd.MM.yyyy.HH.mm.ss") + "_Unmatched_Log.xml");
                }
            }
            _myNotifyIcon.Visible = false;
            Application.Current.MainWindow.WindowState = WindowState.Normal;
            Settings.Default.Save();
            GC.Collect();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized:
                    ShowInTaskbar = false;
                    _myNotifyIcon.ContextMenu.MenuItems[0].Enabled = true;
                    break;
                case WindowState.Normal:
                    ShowInTaskbar = true;
                    _myNotifyIcon.ContextMenu.MenuItems[0].Enabled = false;
                    break;
            }
        }

        #endregion

        #region " INITIAL LOAD FUNCTIONS "

        /// <summary>
        /// 
        /// </summary>
        private void ApplyLocale()
        {
            ResourceDictionary dict;
            lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            switch (lang)
            {
                case "ja":
                    dict = new ResourceDictionary { Source = new Uri("pack://application:,,,/ParseModXIV;component/Localization/Japanese.xaml") };
                    break;
                case "de":
                    dict = new ResourceDictionary { Source = new Uri("pack://application:,,,/ParseModXIV;component/Localization/German.xaml") };
                    break;
                case "fr":
                    dict = new ResourceDictionary { Source = new Uri("pack://application:,,,/ParseModXIV;component/Localization/French.xaml") };
                    break;
                default:
                    dict = new ResourceDictionary { Source = new Uri("pack://application:,,,/ParseModXIV;component/Localization/English.xaml") };
                    break;
            }
            Resources.MergedDictionaries.Add(dict);
        }

        /// <summary>
        /// 
        /// </summary>
        private void DirectoryStructure()
        {
            if (!Directory.Exists(Lpath))
            {
                Directory.CreateDirectory(Lpath);
            }
            if (!Directory.Exists(Ipath))
            {
                Directory.CreateDirectory(Ipath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void LoadXml()
        {
            var items = from item in _xAtCodes.Descendants("Code") select new XValuePairs { Key = (string)item.Attribute("Key"), Value = (string)item.Attribute("Value") };
            foreach (var item in items)
            {
                Constants.XAtCodes.Add(item.Key, item.Value);
            }
            items = from item in _xSettings.Descendants("Server") select new XValuePairs { Key = (string)item.Attribute("Key"), Value = (string)item.Attribute("Value") };
            foreach (var item in items)
            {
                ParseMod.ServerName.Add(item.Value, item.Key);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private static void ApplySettings()
        {
            Constants.LogErrors = Settings.Default.LogErrors ? 1 : 0;
            if (!String.IsNullOrWhiteSpace(Settings.Default.Server))
            {
                Settings.Default.ServerName = ParseMod.ServerName[Settings.Default.Server];
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void CreateNotify()
        {
            var streamResourceInfo = Application.GetResourceStream(new Uri("pack://application:,,,/ParseModXIV;component/ParseModXIV.ico"));
            if (streamResourceInfo != null)
            {
                using (var iconStream = streamResourceInfo.Stream)
                {
                    _myNotifyIcon = new NotifyIcon { Icon = new Icon(iconStream), Visible = true };
                    iconStream.Dispose();
                    _myNotifyIcon.Text = "ParseModXIV - Minimized";
                    var myNotify = new ContextMenu();
                    myNotify.MenuItems.Add("&Restore Application").Enabled = false;
                    myNotify.MenuItems.Add("&Exit");
                    myNotify.MenuItems[0].Click += Restore_Click;
                    myNotify.MenuItems[1].Click += Exit_Click;
                    _myNotifyIcon.ContextMenu = myNotify;
                    _myNotifyIcon.MouseDoubleClick += MyNotifyIcon_MouseDoubleClick;
                    _myNotifyIcon.BalloonTipClicked += MyNotifyIconBalloonTipClicked;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void CheckUpdates()
        {
            //Func<bool> d = delegate
            //{
            //    return true;
            //};
            //d.BeginInvoke(null, null);
            _autoUpdates.CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Func<bool> checkUpdates = () => _autoUpdates.CheckUpdates("ParseModXIV");
            Func<bool> checkLibrary = () => _autoUpdates.CheckDlls("AppModXIV", "");
            checkUpdates.BeginInvoke(appresult =>
            {
                const int bTipTime = 3000;
                string title, message;
                if (checkUpdates.EndInvoke(appresult))
                {
                    switch (lang)
                    {
                        case "ja":
                            title = "利用可能な更新！";
                            message = "ダウンロードするには \"ParseModに関して\"をクリックしてください。";
                            break;
                        case "de":
                            title = "Update Verfügbar!";
                            message = "Klicken Sie auf \"Über\" zum Download bereit.";
                            break;
                        case "fr":
                            title = "Mise À Jour Possible!";
                            message = "Cliquez sur \"A propos\" pour télécharger.";
                            break;
                        default:
                            title = "Update Available!";
                            message = "Click \"About\" to download.";
                            break;
                    }
                    _myNotifyIcon.ShowBalloonTip(bTipTime, title, message, ToolTipIcon.Info);
                }
                else
                {
                    checkLibrary.BeginInvoke(libresult =>
                    {
                        if (checkLibrary.EndInvoke(libresult))
                        {
                            switch (lang)
                            {
                                case "ja":
                                    title = "利用可能な更新！";
                                    message = "ダウンロードするには \"ParseModに関して\"をクリックしてください。";
                                    break;
                                case "de":
                                    title = "Update Verfügbar!";
                                    message = "Klicken Sie auf \"Über\" zum Download bereit.";
                                    break;
                                case "fr":
                                    title = "Mise À Jour Possible!";
                                    message = "Cliquez sur \"A propos\" pour télécharger.";
                                    break;
                                default:
                                    title = "Update Available!";
                                    message = "Click \"About\" to download.";
                                    break;
                            }
                            _myNotifyIcon.ShowBalloonTip(bTipTime, title, message, ToolTipIcon.Info);
                        }
                    }, null);
                }
            }, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MyNotifyIconBalloonTipClicked(object sender, EventArgs e)
        {
            Process.Start("UpdateModXIV.exe", "ParseModXIV");
        }

        /// <summary>
        /// 
        /// </summary>
        private static void Start()
        {
            ParseMod.Instance.StartLogging();
            if (Settings.Default.DebugMode)
            {
            }
            if (App.MArgs == null)
            {
                return;
            }
            if (File.Exists(App.MArgs[0]))
            {
                ProcessXml(App.MArgs[0]);
            }
        }

        #endregion

        #region " THEME, FONTS AND COLORS "

        /// <summary>
        /// 
        /// </summary>
        private void ApplyFonts()
        {
            if (Settings.Default.Gui_LogFont == null)
            {
                return;
            }
            var font = Settings.Default.Gui_LogFont;
            TabControlView.MA.MobAbility_FLOW.FontFamily = new FontFamily(font.Name);
            TabControlView.MA.MobAbility_FLOW.FontWeight = font.Bold ? FontWeights.Bold : FontWeights.Regular;
            TabControlView.MA.MobAbility_FLOW.FontStyle = font.Italic ? FontStyles.Italic : FontStyles.Normal;
            TabControlView.MA.MobAbility_FLOW.FontSize = font.Size;
            font.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ApplyColor()
        {
            _tsColor = Settings.Default.Color_TimeStamp;
            _bColor = Settings.Default.Color_ChatlogBackground;
            var tColor = new SolidColorBrush { Color = _bColor };
            TabControlView.MA.MobAbility_FLOW.Background = tColor;
        }

        /// <summary>
        /// 
        /// </summary>
        private static void ApplyTheme()
        {
            try
            {
                var split = Settings.Default.Theme.Split('|');
                var accent = split[0];
                var theme = split[1];
                switch (theme)
                {
                    case "Dark":
                        ThemeManager.ChangeTheme(View, ThemeHelper.DefaultAccents.First(a => a.Name == accent), Theme.Dark);
                        break;
                    case "Light":
                        ThemeManager.ChangeTheme(View, ThemeHelper.DefaultAccents.First(a => a.Name == accent), Theme.Light);
                        break;
                }
            }
            catch
            {
            }
        }

        #endregion

        #region " NOTIFY FUNCTIONS "

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Restore_Click(object sender, EventArgs e)
        {
            WindowState = WindowState.Normal;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Exit_Click(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region " PARSEMOD OPTIONS "

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyNotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            WindowState = WindowState.Normal;
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="insert"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static bool SubmitData(string insert, string message)
        {
            var url = string.Format("http://ffxiv-app.com/battles/insert/?insert={0}&q={1}", insert, HttpUtility.UrlEncode(message));
            var httpWebRequest = (HttpWebRequest) WebRequest.Create(url);
            httpWebRequest.ContentType = "text/json";
            httpWebRequest.Method = "POST";

            var httpResponse = (HttpWebResponse) httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var responseText = streamReader.ReadToEnd();
                return true;
            }
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            _myNotifyIcon.Dispose();
        }

        #endregion
    }
}