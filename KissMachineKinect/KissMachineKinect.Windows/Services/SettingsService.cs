namespace KissMachineKinect.Services
{
    public static class SettingsService
    {
        private static bool _settingsInitialized;
        private static readonly string _sonyCameraSettingsName = "SonyCameraEnabled";
        private static bool _sonyCameraEnabled;

        private static readonly string _lowPerformanceModeSettingsName = "LowPerformanceModeEnabled";
        private static bool _lowPerformanceModeEnabled;

        public static bool SonyCameraEnabled
        {
            get
            {
                if (!_settingsInitialized)
                {
                    InitializeSettings();
                }
                return _sonyCameraEnabled;
            }
            set
            {
                if (_sonyCameraEnabled == value) return;
                _sonyCameraEnabled = value;
                Windows.Storage.ApplicationData.Current.RoamingSettings.Values[_sonyCameraSettingsName] = _sonyCameraEnabled;
            }
        }

        public static bool LowPerformanceModeEnabled
        {
            get
            {
                if (!_settingsInitialized)
                {
                    InitializeSettings();
                }
                return _lowPerformanceModeEnabled;
            }
            set
            {
                if (_lowPerformanceModeEnabled == value) return;
                _lowPerformanceModeEnabled = value;
                Windows.Storage.ApplicationData.Current.RoamingSettings.Values[_lowPerformanceModeSettingsName] = _lowPerformanceModeEnabled;
            }
        }

        public static void InitializeSettings()
        {
            _sonyCameraEnabled =
                Windows.Storage.ApplicationData.Current.RoamingSettings.Values.ContainsKey(_sonyCameraSettingsName) && 
                (bool)Windows.Storage.ApplicationData.Current.RoamingSettings.Values[_sonyCameraSettingsName];

            _lowPerformanceModeEnabled =
                Windows.Storage.ApplicationData.Current.RoamingSettings.Values.ContainsKey(_lowPerformanceModeSettingsName) && 
                (bool)Windows.Storage.ApplicationData.Current.RoamingSettings.Values[_lowPerformanceModeSettingsName];

            _settingsInitialized = true;
        }
    }
}
