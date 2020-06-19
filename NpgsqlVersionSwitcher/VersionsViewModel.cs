using NuGet;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Net;

namespace NpgsqlVersionSwitcher
{
    public class VersionsViewModel : INotifyPropertyChanged
    {
        #region Properties

        private List<VersionData> _versions = null;
        private string _output;
        private VersionData _selectedVersion;
        private int _progress;
        private string _error;
        private bool _showBottomPart;
        private bool _isBusy;
        private bool _isRefreshing;
        private bool _enableComboBox;

        public double InitialHeight { get; private set; }

        public List<VersionData> Versions
        {
            get { return _versions; }
            set
            {
                _versions = value;
                RaisePropertyChanged("Versions");
            }
        }

        public string Output
        {
            get { return _output; }
            set
            {
                _output = value;
                RaisePropertyChanged("Output");
            }
        }

        public VersionData SelectedVersion
        {
            get { return _selectedVersion; }
            set
            {
                _selectedVersion = value;
                RaisePropertyChanged("SelectedVersion");
            }
        }

        public int Progress
        {
            get { return _progress; }
            set
            {
                _progress = value;
                RaisePropertyChanged("Progress");
            }
        }

        public int MaxProgress { get { return 100; } }
        public int MinProgress { get { return 0; } }
        public int RotateCenterX
        {
            get
            {
                return (int)Math.Ceiling((System.Windows.Application.Current.MainWindow as MainWindow).RefreshImage.ActualWidth / 2);
            }
        }

        public int RotateCenterY
        {
            get
            {
                return (int)Math.Ceiling((System.Windows.Application.Current.MainWindow as MainWindow).RefreshImage.ActualHeight / 2);
            }
        }

        public string Error
        {
            get { return _error; }
            set
            {
                _error = value;
                RaisePropertyChanged("Error");
            }
        }

        public bool ShowBottomPart
        {
            get { return _showBottomPart; }
            set
            {
                _showBottomPart = value;
                RaisePropertyChanged("ShowBottomPart");
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                RaisePropertyChanged("IsBusy");
            }
        }

        public bool IsRefreshing
        {
            get { return _isRefreshing; }
            set
            {
                _isRefreshing = value;
                RaisePropertyChanged("IsRefreshing");
            }
        }

        public bool EnableComboBox
        {
            get { return _enableComboBox; }
            set
            {
                _enableComboBox = value;
                RaisePropertyChanged("EnableComboBox");
            }
        }

        private void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public VersionsViewModel()
        {
            InitialHeight = (System.Windows.Application.Current.MainWindow as MainWindow).ActualHeight;
            ShowBottomPart = false;
            IsBusy = true;
            IsRefreshing = true;
            ChangeVersionCommand = new SwitchCommand(this);
            RefreshPackagesCommand = new RelayCommand((param) =>
                {
                    this.ShowBottomPart = false;
                    this.RefreshPackages();
                }, (param) => { return !this.IsBusy; });
            CloseProgramCommand = new RelayCommand((param) => System.Windows.Application.Current.Shutdown(), (param) => { return !this.IsBusy; });
        }

        public ICommand ChangeVersionCommand { get; private set; }
        public ICommand RefreshPackagesCommand { get; private set; }
        public ICommand CloseProgramCommand { get; private set; }

        public void RefreshPackages()
        {
            SetUIStatus(isBusy: true, showBottomPart: false, isRefreshing: true);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            new System.Threading.Thread(() =>
            {
                var versions = (from p in PackageRepositoryFactory.Default.CreateRepository("http://packages.nuget.org/api/v1").FindPackagesById("Npgsql")
                                where p.IsReleaseVersion()
                                orderby p.Published descending
                                select new VersionData() { Text = (p.IsLatestVersion ? p.Version.Version.ToString() + " (Latest)" : p.Version.Version.ToString()), Value = p.Version.Version.ToString() }
                               ).ToList();

                //var versions = Newtonsoft.Json.JsonConvert.DeserializeObject<List<VersionData>>(System.IO.File.ReadAllText(@"C:\users\public\documents\nuget-npgsql.json"));
                System.Threading.Thread.Sleep(2000);
                Helper.UIDo(() =>
                {
                    this.Versions = versions.Count > 0 ? versions : new VersionData[] { new VersionData() { Text = "-- No packages found --", Value = string.Empty } }.ToList();
                    SetUIStatus(isBusy: false, isRefreshing: false, enableComboBox: versions.Count > 0);
                    this.SelectedVersion = versions.First();
                });
            }).Start();
        }

        public void SetUIStatus(bool? isBusy = null, bool? isRefreshing = null, bool? enableComboBox = null, bool? showBottomPart = null)
        {
            Helper.UIDo(() =>
            {
                if (isBusy.HasValue) this.IsBusy = isBusy.Value;
                if (enableComboBox.HasValue) this.EnableComboBox = enableComboBox.HasValue;
                if (isRefreshing.HasValue)
                {
                    this.IsRefreshing = isRefreshing.Value;
                    if (!enableComboBox.HasValue) this.EnableComboBox = !isRefreshing.Value;
                }
                if (showBottomPart.HasValue) this.ShowBottomPart = showBottomPart.Value;
            });
        }
    }

    public class VersionData
    {
        public string Text { get; set; }
        public string Value { get; set; }
    }
}
