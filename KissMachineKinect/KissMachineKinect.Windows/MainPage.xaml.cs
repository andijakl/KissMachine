using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using WindowsPreview.Kinect;
using KissMachineKinect.Controls;
using KissMachineKinect.Converter;
using KissMachineKinect.Models;
using KissMachineKinect.Properties;
using KissMachineKinect.Services;

namespace KissMachineKinect
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        // General config
        private const bool LowPerformanceMode = true;
        private const bool UseSonyCamera = true;

        // Distance config
        private const float ShowHintKissDistanceInM = 1.0f;
        private const float TriggerKissCountdownDistanceInM = 0.3f;

        private const double CountdownSpeedInS = 1.5;
        private const int CountdownStartValue = 3;
        private const int ClearPictureSpeedInS = 10;

        // Screenshots
        private const FileFormat DefaultPhotoFileFormat = FileFormat.Png;
        private const string DefaultPhotoFileName = "kiss_{0:yyyy-MM-dd_HH-mm-ss}";
        private readonly StorageFolder _defaultPhotoFolder = KnownFolders.PicturesLibrary;
        private const string DefaultPhotoSubFolder = "Wedding";

        private enum FileFormat
        {
            Jpeg,
            Png
        }

        // Kinect
        private KinectSensor _sensor;
        private CoordinateMapper _coordinateMapper;
        private int _displayWidth;
        private int _displayHeight;
        private BodyFrameReader _bodyFrameReader;
        private int _maxBodyCount;
        private Body[] _bodies;
        private ColorFrameReader _colorFrameReader;
        private WriteableBitmap _bitmap;
        private Canvas _drawingCanvas;

        // Initialization
        private bool _kinectInitialized;
        private bool _kinectStarted;
        private bool _gameInitialized;

        // Data
        private List<PlayerInfo> _players;

        // UI
        public KissPositionModel _minPair { get; set; }
        private MinPlayerLine _minPlayerLine;
        private Timer _photoCountdownTimer;
        private int _lowPerformanceFrameCounter;

        private int _photoCountDown = (int)KissCountdownStatusService.SpecialKissTexts.Invisible;
        public int PhotoCountDown
        {
            get { return _photoCountDown; }
            set
            {
                if (value == _photoCountDown) return;
                _photoCountDown = value;
                OnPropertyChanged();
            }
        }

        private string _photoCountDownText = string.Empty;
        public string PhotoCountDownText
        {
            get { return _photoCountDownText; }
            set
            {
                if (value == _photoCountDownText) return;
                _photoCountDownText = value;
                OnPropertyChanged();
            }
        }

        private bool _photoTaken;


        private bool _showTakenPhoto;
        public bool ShowTakenPhoto
        {
            get { return _showTakenPhoto; }
            set
            {
                if (value == _showTakenPhoto) return;
                _showTakenPhoto = value;
                OnPropertyChanged();
            }
        }
        
        private int _photoCounter;
        public int PhotoCounter
        {
            get { return _photoCounter; }
            set
            {
                if (value == _photoCounter) return;
                _photoCounter = value;
                OnPropertyChanged();
            }
        }

        private const string PhotoCounterSettingName = "photoCounter";

        private WriteableBitmap _takenPhotoBitmap;
        public WriteableBitmap TakenPhotoBitmap
        {
            get { return _takenPhotoBitmap; }
            set
            {
                if (value == _takenPhotoBitmap) return;
                _takenPhotoBitmap = value;
                OnPropertyChanged();
            }
        }

        public SonyCameraService.CameraStatusValues CameraStatus => _sonyCameraService?.CameraStatus ?? SonyCameraService.CameraStatusValues.NotConnected;

        // System
        private CoreDispatcher _dispatcher;
        private ResourceLoader _resourceLoader;
        private SpeechService _speechService;


        #region Busy Control

        private BusyStatus _busyStatus;
        private DispatcherTimer _kinectInitWaitTimer;
        private SonyCameraService _sonyCameraService;

        /// <summary>
        /// Full-screen busy indicator that blocks the rest of the UI.
        /// If needed, don't forget to also include it in the XAML of the page!
        /// </summary>
        public BusyStatus BusyStatus
        {
            get { return _busyStatus ?? (_busyStatus = new BusyStatus()); }
            set
            {
                if (value == _busyStatus) return;
                _busyStatus = value;
                OnPropertyChanged();
            }
        }


        public ICommand BusyEndedCommand { get; set; }
        #endregion

        public MainPage()
        {
            InitializeComponent();
            BusyEndedCommand = new DelegateCommand<object>(BusyEndedMethod);


#if DEBUG
            DistTxt.Visibility = Visibility.Visible;
#endif

            Loaded += MainPage_Loaded;
            Window.Current.CoreWindow.KeyUp += CoreWindow_KeyUp;
        }

        public void BusyEndedMethod(object busyEndType)
        {
            var endedType = busyEndType as BusyStatus.BusyEndedTypes?;
            if (endedType == BusyStatus.BusyEndedTypes.MiddleOfAnimation)
            {
                // Don't navigate away yet
                return;
            }
            BusyStatus.IsBusy = false;
        }


        #region Init

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // use the window object as the view model in this simple example
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, Init);
        }


        private void Init()
        {
            _resourceLoader = ResourceLoader.GetForCurrentView();
            BusyStatus.SetBusy(_resourceLoader.GetString("BusyLoading"));
            InitKinect();
        }

        private void InitCamera()
        {
            if (_sonyCameraService == null)
            {
                _sonyCameraService = new SonyCameraService();
                _sonyCameraService.PropertyChanged += SonyCameraServiceOnPropertyChanged;
            }
            _sonyCameraService.Init();
        }

        private async void SonyCameraServiceOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName.Equals("CameraStatus"))
            {
                await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => OnPropertyChanged("CameraStatus"));
            }
        }


        private void InitGame()
        {
            if (_gameInitialized || !_kinectInitialized || !_kinectStarted) return;
            // ----------------------------------------------------------------------------------
            // Setup drawing canvas

            // Instantiate a new Canvas
            // set the clip rectangle to prevent rendering outside the canvas
            _drawingCanvas = new Canvas
            {
                Clip = new RectangleGeometry { Rect = new Rect(0.0, 0.0, _displayWidth, _displayHeight) }
            };
            _minPlayerLine = new MinPlayerLine(_drawingCanvas, Colors.Red);
            DisplayGrid.Children.Add(_drawingCanvas);

            // ----------------------------------------------------------------------------------
            // Initialize game
            _players = new List<PlayerInfo>();

            _gameInitialized = true;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            DataContext = this;

            InitCamera();
            _sonyCameraService?.ForceRestart();

            if (_speechService == null)
            {
                _speechService = new SpeechService(SpeakerMedia);
            }
            _speechService.Init();

        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Debug.WriteLine("OnNavigatedFrom");

            _speechService?.Suspend();

            _sonyCameraService?.StopCameraSearch();
            _sonyCameraService?.Suspend();

            // Body is IDisposables
            if (_bodies != null)
            {
                foreach (var body in _bodies)
                {
                    body?.Dispose();
                }
            }

            if (_bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                _bodyFrameReader.Dispose();
                _bodyFrameReader = null;
            }

            if (_sensor != null)
            {
                _sensor.Close();
                _sensor = null;
                _kinectInitialized = false;
                _kinectStarted = false;
            }
        }

        #endregion

        #region Kinect Init
        private void InitKinect()
        {
            if (_sensor != null)
            {
                Debug.WriteLine("Already initialized");
                return;
            }

            _sensor = KinectSensor.GetDefault();
            _sensor.IsAvailableChanged += Kinect_IsAvailableChanged;


            // get the coordinate mapper
            _coordinateMapper = _sensor.CoordinateMapper;

            // ----------------------------------------------------------------------------------
            // Color frames

            // open the reader for the color frames
            _colorFrameReader = _sensor.ColorFrameSource.OpenReader();

            // create the colorFrameDescription from the ColorFrameSource using rgba format
            var colorFrameDescription = _sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
            
            // create the bitmap to display
            _bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);

            ColorImg.Width = colorFrameDescription.Width;
            ColorImg.Height = colorFrameDescription.Height;
            
            // get the color frame details
            var frameDescription = _sensor.ColorFrameSource.FrameDescription;

            // set the display specifics
            _displayWidth = frameDescription.Width;
            _displayHeight = frameDescription.Height;

            ColorImg.Source = _bitmap;


            // ----------------------------------------------------------------------------------
            // Body frames

            // open the reader for the body frames
            _bodyFrameReader = _sensor.BodyFrameSource.OpenReader();

            // set the maximum number of bodies that would be tracked by Kinect
            _maxBodyCount = _sensor.BodyFrameSource.BodyCount;

            // allocate storage to store body objects
            _bodies = new Body[_maxBodyCount];


            // ----------------------------------------------------------------------------------
            // Start tracking

            // wire handler for frame arrival
            _colorFrameReader.FrameArrived += Reader_ColorFrameArrived;

            if (_bodyFrameReader != null)
            {
                // wire handler for body frame arrival
                _bodyFrameReader.FrameArrived += Reader_BodyFrameArrived;
            }
            _kinectInitialized = true;

            // open the sensor
            _sensor.Open();

            // Show on UI that we're waiting for the kinect
            BusyStatus.SetBusy(_resourceLoader.GetString("BusyInitializingKinect"));
        }

        private async void Kinect_IsAvailableChanged(KinectSensor sender, IsAvailableChangedEventArgs args)
        {
            if (args.IsAvailable && !_kinectStarted && _kinectInitialized)
            {
                Debug.WriteLine("Found Kinect");
                _kinectInitWaitTimer?.Stop();
                _kinectInitWaitTimer = null;
                _kinectStarted = true;
                BusyStatus.EndBusy(BusyStatus.BusyEndTypes.Fadeout);
                await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, InitGame);
                return;
            }

            if (!_kinectStarted && !args.IsAvailable)
            {
                if (_kinectInitWaitTimer != null && _kinectInitWaitTimer.IsEnabled)
                {
                    // Wait timer is already running - do nothing
                    return;
                }
                // If no Kinect is available, we'll only get one unavailable callback
                // Wait for a few seconds, then show error info
                Debug.WriteLine("Kinect not available - starting timer for error message and to give time for re-connect");
                _kinectInitWaitTimer = new DispatcherTimer();
                _kinectInitWaitTimer.Tick += KinectInitWaitTimerOnTick;
                _kinectInitWaitTimer.Interval = TimeSpan.FromSeconds(5);
                _kinectInitWaitTimer.Start();
                return;
            }

            if (_kinectStarted && !args.IsAvailable && _kinectInitialized)
            {
                Debug.WriteLine("Kinect was already running, but is no longer available");
                // Kinect was already running, but is no longer available
                await ShowNoKinectAvailableMessageAsync();
            }


        }

        private async void KinectInitWaitTimerOnTick(object sender, object o)
        {
            // Only do one callback
            _kinectInitWaitTimer?.Stop();
            _kinectInitWaitTimer = null;
            await ShowNoKinectAvailableMessageAsync();
        }

        private async Task ShowNoKinectAvailableMessageAsync()
        {
            // Kinect was already initialized but then later the status changed to not available
            await ShowMessageBoxAsync(_resourceLoader.GetString("ErrorNoKinectAvailableText"),
                _resourceLoader.GetString("ErrorNoKinectAvailableTitle"));
            BusyStatus.EndBusy(BusyStatus.BusyEndTypes.Error);
            //MainViewbox.Visibility = Visibility.Collapsed;
            _kinectStarted = false;
        }

        #endregion

        #region Color Frames
        private void Reader_ColorFrameArrived(ColorFrameReader sender, ColorFrameArrivedEventArgs args)
        {
            if (ShowTakenPhoto) return;
            if (LowPerformanceMode)
            {
                _lowPerformanceFrameCounter++;
                if (_lowPerformanceFrameCounter%2 != 0) return;
            }

            var colorFrameProcessed = false;

            // ColorFrame is IDisposable
            using (var colorFrame = args.FrameReference.AcquireFrame())
            {
                var colorFrameDescription = colorFrame?.FrameDescription;

                // verify data and write the new color frame data to the display bitmap
                if (colorFrameDescription?.Width == _bitmap.PixelWidth && (colorFrameDescription.Height == _bitmap.PixelHeight))
                {
                    if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                    {
                        colorFrame.CopyRawFrameDataToBuffer(_bitmap.PixelBuffer);
                    }
                    else
                    {
                        colorFrame.CopyConvertedFrameDataToBuffer(_bitmap.PixelBuffer, ColorImageFormat.Bgra);
                    }

                    colorFrameProcessed = true;
                }
            }

            // we got a frame, render
            if (colorFrameProcessed)
            {
                _bitmap.Invalidate();
            }
        }


        private async void CoreWindow_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            var key = args.VirtualKey;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (key)
            {
                case VirtualKey.Space:
                    // Take screenshot when releasing the space key
                    if (UseSonyCamera && _sonyCameraService != null) await _sonyCameraService.PrepareTakePhoto();
                    await TakePhoto();
                    break;
                case VirtualKey.D:
                    // Disable / Enable debug mode
                    DistTxt.Visibility = DistTxt.Visibility == Visibility.Visible
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                    break;
                case VirtualKey.R:
                    // Reset all players
                    await ResetAllPlayersAsync();
                    break;
                case VirtualKey.C:
                    // Counter -> clear
                    PhotoCounter = 0;
                    ApplicationData.Current.RoamingSettings.Values[PhotoCounterSettingName] = 0;
                    Debug.WriteLine("Photo counter reset");
                    break;
                case VirtualKey.A:
                    {
//#if DEBUG
                        // Add new player (for debug)
                        Debug.WriteLine("Simulated player added");
                        var newPlayer = new PlayerInfo(_drawingCanvas, 1, -1)
                        {
                            FacePosInCamera = new CameraSpacePoint { X = -0.0366f, Y = -0.0486f, Z = 1.164f },
                            FacePosInColor = new ColorSpacePoint { X = 972f, Y = 599f }
                        };
                        newPlayer.SetVisibility(true);
                        _players.Add(newPlayer);
//#endif
                    }
                    break;
            }
        }

        private async Task ResetAllPlayersAsync()
        {
            for (var i = _players.Count - 1; i >= 0; i--)
            {
                var curPlayer = _players[i];
                DoRemovePlayer(curPlayer);
            }
            _minPlayerLine?.SetVisibility(false);
            await StopKissPhotoTimer();
        }

        private async Task<StorageFile> SavePhotoToFile(WriteableBitmap wb)
        {
            var fileName = string.Format(DefaultPhotoFileName, DateTime.Now);
            Guid bitmapEncoderGuid;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (DefaultPhotoFileFormat)
            {
                //case FileFormat.Jpeg:
                //    fileName += ".jpeg";
                //    bitmapEncoderGuid = BitmapEncoder.JpegEncoderId;
                //    break;

                case FileFormat.Png:
                    fileName += ".png";
                    bitmapEncoderGuid = BitmapEncoder.PngEncoderId;
                    break;
            }

            var targetFolder = await _defaultPhotoFolder.CreateFolderAsync(DefaultPhotoSubFolder, CreationCollisionOption.OpenIfExists);
            var file = await targetFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(bitmapEncoderGuid, stream);
                var pixelStream = wb.PixelBuffer.AsStream();
                var pixels = new byte[pixelStream.Length];
                await pixelStream.ReadAsync(pixels, 0, pixels.Length);

                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)wb.PixelWidth, (uint)wb.PixelHeight, 96.0, 96.0, pixels);
                await encoder.FlushAsync();
            }
            return file;
        }

        #endregion

        #region Player Management

        private void CreateNewPlayer(ulong trackingId, int bodyNum)
        {
            Debug.WriteLine("Create player: " + bodyNum + " / id: " + trackingId);
            _players.Add(new PlayerInfo(_drawingCanvas, trackingId, bodyNum));
        }

        private async Task RemovePlayer(ulong trackingId, int bodyNum)
        {
            if (_players == null || !_players.Any()) return;
            //Debug.WriteLine("Attempt to remove player: " + trackingId);
            // We need to use the body number here instead of the tracking id, as
            // the tracking ID is already cleared by Kinect when it lost the body
            var playerToRemove = _players.FirstOrDefault(playerInfo => playerInfo.BodyNum == bodyNum);
            if (playerToRemove != null)
            {
                Debug.WriteLine("Remove player: " + playerToRemove.BodyNum + " / id: " + playerToRemove.TrackingId);
                if (_minPair != null && (_minPair.Player1TrackingId == playerToRemove.TrackingId ||
                                         _minPair.Player2TrackingId == playerToRemove.TrackingId))
                {
                    // Removing one of the players that is part of the minimum pair!
                    if (IsInCountdownPhase() && _speechService != null)
                    {
                        await _speechService.SpeakTextAsync(_resourceLoader.GetString("RemovedMinPairPlayerHint"));
                    }
                    _minPair = null;
                    _minPlayerLine?.SetVisibility(false);
                }
                DoRemovePlayer(playerToRemove);
                if (_players.Count < 2)
                {
                    // If less than two players are left...
                    _photoTaken = false;
                    await StopKissPhotoTimer();
                }
            }
        }

        private void DoRemovePlayer(PlayerInfo playerToRemove)
        {
            playerToRemove.RemoveFromWorld(_drawingCanvas);
            _players.Remove(playerToRemove);
        }

        private bool IsPlayerTracked(ulong trackingId)
        {
            return _players.Any(curPlayer => curPlayer.TrackingId == trackingId);
        }

        #endregion

        #region Player Detection & Calculations
        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="args">event arguments</param>
        private async void Reader_BodyFrameArrived(BodyFrameReader sender, BodyFrameArrivedEventArgs args)
        {
            using (var bodyFrame = args.FrameReference.AcquireFrame())
            {
                if (bodyFrame == null) return;

                // update body data
                bodyFrame.GetAndRefreshBodyData(_bodies);
            }


            for (var i = 0; i < _bodies.Length; i++)
            {
                var curBody = _bodies[i];
                if (curBody.IsTracked)
                {
                    // Check if we have already registered this player
                    if (!IsPlayerTracked(curBody.TrackingId))
                    {
                        CreateNewPlayer(curBody.TrackingId, i);
                    }
                    // Update player position
                    UpdatePlayer(curBody);
                }
                else
                {
                    // collapse this body from canvas as it goes out of view
                    await RemovePlayer(curBody.TrackingId, i);
                }
            }

            // Check if two players are close enough
            if (ShowTakenPhoto || _photoTaken)
            {
                _minPlayerLine.SetVisibility(false);
                return;
            }

            _minPair = CheckPlayersCloseBy();
            if (_minPair != null)
            {
                if (_minPair.DistanceInM < ShowHintKissDistanceInM)
                {
                    // Draw line between heads
                    _minPlayerLine.SetPosition(_minPair.Player1Pos.X, _minPair.Player1Pos.Y, _minPair.Player2Pos.X,
                        _minPair.Player2Pos.Y);
                    _minPlayerLine.SetVisibility(true);
                }
                else
                {
                    // Players are too far apart
                    _minPlayerLine.SetVisibility(false);
                }

                // Trigger or stop photo?
                if (_minPair.DistanceInM < TriggerKissCountdownDistanceInM && !_photoTaken)
                {
                    // Start kiss timer?
                    await StartKissPhotoTimer();
                }
                else
                {
                    // Stop timer if it was running and reset if a photo was already taken
                    // (people have to get away from each other to start another photo)
                    if (IsInCountdownPhase())
                    {
                        await StopKissPhotoTimer(false);
                    }
                    if (_minPair.DistanceInM > ShowHintKissDistanceInM)
                    {
                        _photoTaken = false;
                    }
                }

                if (_photoCountdownTimer == null &&
                    _minPair.DistanceInM < ShowHintKissDistanceInM &&
                    !_photoTaken && !ShowTakenPhoto)
                {
                    // Show hint to kiss
                    await SetCountdown((int)KissCountdownStatusService.SpecialKissTexts.GiveAKiss);
                }
            }
            else
            {
                // Remove line if we don't have a pair
                _minPlayerLine?.SetVisibility(false);
            }
        }



        private KissPositionModel CheckPlayersCloseBy()
        {
            if (_players == null) return null;
            if (_players.Count < 2) return null;

            // Iterate over all players
            var minPair = new KissPositionModel();
            var foundMinPairDistance = false;

            for (var i = 0; i < _players.Count - 1; i++)
            {
                for (var j = i + 1; j < _players.Count; j++)
                {
                    var p1Pos = VectorFromPos(_players[i].FacePosInCamera);
                    var p2Pos = VectorFromPos(_players[j].FacePosInCamera);
                    var dist = (p1Pos - p2Pos).Length;
                    if (dist < minPair.DistanceInM)
                    {
                        foundMinPairDistance = true;
                        minPair.DistanceInM = dist;
                        minPair.Player1Pos = _players[i].FacePosInColor;
                        minPair.Player1BodyNum = _players[i].BodyNum;
                        minPair.Player1TrackingId = _players[i].TrackingId;
                        minPair.Player2Pos = _players[j].FacePosInColor;
                        minPair.Player2BodyNum = _players[j].BodyNum;
                        minPair.Player2TrackingId = _players[j].TrackingId;
                    }
                }
            }
            if (DistTxt.Visibility == Visibility.Visible && foundMinPairDistance)
            {
                DistTxt.Text = string.Format(_resourceLoader.GetString("MinDistanceText"), minPair.DistanceInM);
            }

            return minPair;
        }

        private static Vec3 VectorFromPos(CameraSpacePoint pos)
        {
            return new Vec3(pos.X, pos.Y, pos.Z);
        }

        private void UpdatePlayer(Body curBody)
        {
            // Find player
            var player = _players.FirstOrDefault(playerInfo => playerInfo.TrackingId == curBody.TrackingId);
            if (player == null) return;

            // Only do the processing for real players, not for simulated ones

            var faceJoint = curBody.Joints[JointType.Head];
            if (player.BodyNum < 0) return;
            if (faceJoint.TrackingState == TrackingState.NotTracked)
            {
                player.SetVisibility(false);
            }
            else
            {
                player.SetVisibility(!(_photoTaken || ShowTakenPhoto));
                var facePosInCamera = faceJoint.Position;
                var facePosInColor = _coordinateMapper.MapCameraPointToColorSpace(facePosInCamera);
                player.SetPosition(facePosInCamera, facePosInColor);
            }
        }
        #endregion

        #region Photo Countdown

        private async Task StartKissPhotoTimer()
        {
            if (_photoCountdownTimer != null || _photoTaken || ShowTakenPhoto) return;
            Debug.WriteLine("Start kiss photo timer");
            await SetCountdown(CountdownStartValue);
            _photoCountdownTimer = new Timer(PhotoTimerCallback, null, TimeSpan.FromSeconds(CountdownSpeedInS), TimeSpan.FromSeconds(CountdownSpeedInS));

            // Prepare camera for taking a photo
            if (UseSonyCamera)
            {
                try
                {
                    await _sonyCameraService.PrepareTakePhoto();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error preparing camera: " + ex);
                    // Ignore the error for now, let's hope taking a photo will work without preparation as well.
                    // Otherwise, error will be handled during capture, which is better UX than showing an error
                    // already now.
                }
            }
        }


        private async void PhotoTimerCallback(object state)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                switch (PhotoCountDown)
                {
                    case (int)KissCountdownStatusService.SpecialKissTexts.PhotoTaken:
                        Debug.WriteLine("Clear taken photo from screen");
                        // Clear taken picture from screen
                        ShowTakenPhoto = false;
                        TakenPhotoBitmap = null;
                        var stopTimer = true;
                        // Are people still standing together?
                        var minPair = CheckPlayersCloseBy();
                        if (minPair?.DistanceInM < ShowHintKissDistanceInM)
                        {
                            // Don't stop timer yet
                            await SetCountdown((int)KissCountdownStatusService.SpecialKissTexts.AnotherPhoto);
                            stopTimer = false;
                        }
                        if (stopTimer)
                        {
                            // People are not standing together anymore - we can stop the timer and hide the text
                            await StopKissPhotoTimer();
                            _photoTaken = false;
                        }
                        return;
                    case (int)KissCountdownStatusService.SpecialKissTexts.AnotherPhoto:
                        // Additional time has passed - disable the lock so that another photo can be taken!
                        await StopKissPhotoTimer();
                        _photoTaken = false;
                        return;
                    case (int)KissCountdownStatusService.SpecialKissTexts.Kiss:
                        if (BusyStatus.IsBusy)
                        {
                            // Still downloading photo? -> keep short 1.5s timer active and do not start
                            // 10 seconds show photo timer yet...
                            Debug.WriteLine("App is busy - do not set to PhotoTaken yet...");
                            return;
                        }
                        // Stop kiss countdown timer
                        await StopKissPhotoTimer(false);
                        // Set countdown timer to clear picture from screen after 10 seconds
                        await SetCountdown((int)KissCountdownStatusService.SpecialKissTexts.PhotoTaken);
                        Debug.WriteLine("Start clear picture photo countdown timer");
                        _photoCountdownTimer.Dispose();
                        _photoCountdownTimer = new Timer(PhotoTimerCallback, null, TimeSpan.FromSeconds(ClearPictureSpeedInS), TimeSpan.FromSeconds(ClearPictureSpeedInS));
                        return;
                    case 1:
                        await SetCountdown((int)KissCountdownStatusService.SpecialKissTexts.Kiss);
                        // Keep current picture on the screen
                        ShowTakenPhoto = true;
                        // Don't take another picture until a longer time has passed or people are gone
                        _photoTaken = true;
                        // Save photo to file
                        await TakePhoto();
                        return;
                }
                if (PhotoCountDown > 1)
                {
                    await SetCountdown(PhotoCountDown - 1);
                }
            });
        }
        
        private bool IsInCountdownPhase()
        {
            return PhotoCountDown >= (int)KissCountdownStatusService.SpecialKissTexts.Kiss &&
                   PhotoCountDown <= CountdownStartValue;
        }

        private async Task TakePhoto()
        {
            IncreasePhotoCounter();
            var takePhotoWithCam = UseSonyCamera;
            if (UseSonyCamera)
            {
                try
                {
                    // Try to take the picture with the connected Sony camera
                    Debug.WriteLine("Set to busy - downloading photo ...");
                    BusyStatus.SetBusy(_resourceLoader.GetString("DownloadingPhoto"));
                    await Task.Delay(10);
                    var photoFile = await _sonyCameraService.TakePhoto();
                    await LoadFileToViewfinderBitmap(photoFile);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed taking photo with camera: " + e);
                    takePhotoWithCam = false;
                }
            }
            if (!takePhotoWithCam)
            {
                try
                {
                    // Store current image from Kinect if Camera fails.
                    await SavePhotoToFile(_bitmap);
                    TakenPhotoBitmap = _bitmap;
                    TakenPhotoBitmap.Invalidate();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error saving photo from Kinect: " + ex);
                    await ShowMessageBoxAsync(_resourceLoader.GetString("ErrorSavingPhotoFromKinectText"),
                        _resourceLoader.GetString("ErrorSavingPhotoFromKinectTitle"));
                }
            }
            // Outside of try/catch to end it even if issues with Camera download
            BusyStatus.EndBusy(BusyStatus.BusyEndTypes.Fadeout);
        }

        private void IncreasePhotoCounter()
        {
            var counterValue = ApplicationData.Current.RoamingSettings.Values[PhotoCounterSettingName] as int?;
            ApplicationData.Current.RoamingSettings.Values[PhotoCounterSettingName] = counterValue != null ? counterValue + 1 : 1;
            PhotoCounter = (int)ApplicationData.Current.RoamingSettings.Values[PhotoCounterSettingName];
        }

        private async Task LoadFileToViewfinderBitmap(StorageFile photoFileName)
        {
            using (var stream = await photoFileName.OpenAsync(FileAccessMode.ReadWrite))
            {
                var tmpBmp = await BitmapFactory.New(1, 1).FromStream(stream);
                Debug.WriteLine("Photo size: " + tmpBmp.PixelWidth + " / " + tmpBmp.PixelHeight);
                // Flip image from camea horizontally.
                // Kinect shows images as flipped to match the viewfinder with a mirror.
                // External camera has everything as it should be.
                // -> we need to rotate external camera for display so that it corresponds
                // to what people are expecting.
                // Do not flip the final image, as it'd be wrong actually.
                TakenPhotoBitmap = tmpBmp.Flip(WriteableBitmapExtensions.FlipMode.Vertical);
                TakenPhotoBitmap.Invalidate();
            }
        }

        private async Task StopKissPhotoTimer(bool setToInvisible = true)
        {
            // If no timer is running, don't do anything.
            // Do not stop the timer if currently showing the photo so that it will be cleared again.
            // If in "give a kiss" state, in any case proceed to setting text to invisbile - no timer is running.
            if (PhotoCountDown != (int)KissCountdownStatusService.SpecialKissTexts.GiveAKiss 
                && (_photoCountdownTimer == null || ShowTakenPhoto)) return;

            //Debug.WriteLine("Stopping kiss timer? (set to invisible: " + setToInvisible + ")");
            _photoCountdownTimer?.Dispose();
            _photoCountdownTimer = null;
            //Debug.WriteLine("-> stopped");
            if (setToInvisible)
            {
                // Set to invisible
                await SetCountdown((int)KissCountdownStatusService.SpecialKissTexts.Invisible);
                await _sonyCameraService.PutCameraToSleep();
            }
        }
        
        private async Task SetCountdown(int newValue)
        {
            if (PhotoCountDown == newValue) return;
            // Speak the text
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (PhotoCountDown == newValue) return; // We're in another thread, so check again before doing anything
                PhotoCountDown = newValue;
                if (PhotoCountDown == (int) KissCountdownStatusService.SpecialKissTexts.Invisible)
                {
                    PhotoCountDownText = string.Empty;
                    Debug.WriteLine("Set countdown to invisible");
                    return;
                }
                // Convert countdown value to text
                var textConverter = new CountdownIntToStringConverter();
                PhotoCountDownText = (string)textConverter.Convert(PhotoCountDown, typeof(string), null, Windows.Globalization.Language.CurrentInputMethodLanguageTag);
                Debug.WriteLine("Set countdown to: " + newValue + " -> " + PhotoCountDownText);
                await _speechService.SpeakTextAsync(PhotoCountDownText);
            });
        }

        #endregion

        #region UI Dialogs
        /// <summary>
        /// Shows a message box containing a specified message and a specified title.
        /// </summary>
        /// <param name="message">Message to be displayed.</param>
        /// <param name="title">Title to be displayed.</param>
        public async Task ShowMessageBoxAsync(string message, string title)
        {
            try
            {
                await new MessageDialog(message, title).ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception when showing message! " + ex);
            }
        }

        public async Task<bool> ShowOkCancelQueryAsync(string message, string title)
        {
            bool? result = null;
            var dlg = new MessageDialog(message, title);
            dlg.Commands.Add(new UICommand("OK", cmd => result = true));
            dlg.Commands.Add(new UICommand("Cancel", cmd => result = false));
            dlg.DefaultCommandIndex = 0;
            dlg.CancelCommandIndex = 1;

            await dlg.ShowAsync();
            return result != null && result == true;
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
