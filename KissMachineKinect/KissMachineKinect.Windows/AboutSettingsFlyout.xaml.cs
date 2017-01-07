using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;
using KissMachineKinect.Annotations;
using KissMachineKinect.Services;

namespace KissMachineKinect
{
    public sealed partial class AboutSettingsFlyout : SettingsFlyout, INotifyPropertyChanged
    {
        private bool _sonyCameraEnabled;
        private bool _lowPerformanceModeEnabled;

        public bool SonyCameraEnabled
        {
            get { return _sonyCameraEnabled; }
            set
            {
                if (value == _sonyCameraEnabled) return;
                _sonyCameraEnabled = value;
                SettingsService.SonyCameraEnabled = _sonyCameraEnabled;
                OnPropertyChanged();
            }
        }

        public bool LowPerformanceModeEnabled
        {
            get { return _lowPerformanceModeEnabled; }
            set
            {
                if (value == _lowPerformanceModeEnabled) return;
                _lowPerformanceModeEnabled = value;
                SettingsService.LowPerformanceModeEnabled = _lowPerformanceModeEnabled;
                OnPropertyChanged();
            }
        }

        public AboutSettingsFlyout()
        {
            this.InitializeComponent();
            var package = Package.Current;
            var packageId = package.Id;

            // Update version string
            var appVersion = VersionString(packageId.Version);
            if (TextVersion != null && appVersion != null) TextVersion.Text = appVersion;

            SonyCameraEnabled = SettingsService.SonyCameraEnabled;
            LowPerformanceModeEnabled = SettingsService.LowPerformanceModeEnabled;
        }
        private static string VersionString(PackageVersion version)
        {
            return version.Major + "." + version.Minor + "." +
                   version.Build + "." + version.Revision;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
