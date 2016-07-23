using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using WindowsPreview.Kinect;
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
        // Kinect
        private KinectSensor _sensor;
        private CoordinateMapper _coordinateMapper;
        private int _displayWidth;
        private int _displayHeight;
        private BodyFrameReader _bodyFrameReader;
        private int _maxBodyCount;
        private Body[] _bodies;
        private ColorFrameReader _colorFrameReader;
        private uint _bytesPerPixel;
        private byte[] _colorPixels;
        private WriteableBitmap _bitmap;
        private Stream _colorPixelStream;
        private Canvas _drawingCanvas;
        
        // Initialization
        private bool _kinectInitialized;
        private bool _kinectStarted;
        private bool _gameInitialized;

        // Data
        private List<PlayerInfo> _players;

        // UI
        private MinPlayerLine _minPlayerLine;
        private Timer _photoCountdownTimer;

        private int _photoCountDown = -1;
        public int PhotoCountDown
        {
            get { return _photoCountDown; }
            set {
                if (value == _photoCountDown) return;
                _photoCountDown = value;
                OnPropertyChanged();
            }
        }

        private bool _photoTaken = false;

        private CoreDispatcher _dispatcher;
        private ResourceLoader _resourceLoader;
        private SpeechService _speechService;


        #region Busy Control

        private BusyStatus _busyStatus;

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
            Loaded += MainPage_Loaded;
        }

        #region Init

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // use the window object as the view model in this simple example
            _dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            DataContext = this;
            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, Init);
        }


        private void Init()
        {
            // TODO check if Kinect is available, show message and stop game otherwise
            _resourceLoader = ResourceLoader.GetForCurrentView();
            BusyStatus.SetBusy(_resourceLoader.GetString("BusyLoading"));
            InitKinect();
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
            _speechService = new SpeechService(SpeakerMedia);

            _gameInitialized = true;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Debug.WriteLine("OnNavigatedFrom");

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

            // rgba is 4 bytes per pixel
            _bytesPerPixel = colorFrameDescription.BytesPerPixel;

            // allocate space to put the pixels to be rendered
            _colorPixels = new byte[colorFrameDescription.Width * colorFrameDescription.Height * _bytesPerPixel];

            // create the bitmap to display
            _bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);

            ColorImg.Width = colorFrameDescription.Width;
            ColorImg.Height = colorFrameDescription.Height;

            // get the pixelStream for the writeableBitmap
            _colorPixelStream = _bitmap.PixelBuffer.AsStream();

            // get the color frame details
            var frameDescription = _sensor.ColorFrameSource.FrameDescription;

            // set the display specifics
            _displayWidth = frameDescription.Width;
            _displayHeight = frameDescription.Height;


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

            // TODO show on UI that we're waiting for the kinect

        }

        private async void Kinect_IsAvailableChanged(KinectSensor sender, IsAvailableChangedEventArgs args)
        {
            if (args.IsAvailable && !_kinectStarted && _kinectInitialized)
            {
                _kinectStarted = true;
                BusyStatus.EndBusy(BusyStatus.BusyEndTypes.Fadeout);
                await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, InitGame);

            }

            if (_kinectStarted && !args.IsAvailable && _kinectInitialized)
            {
                // Kinect was already initialized but then later the status changed to not available
                await ShowMessageBoxAsync(_resourceLoader.GetString("ErrorNoKinectAvailableText"),
                    _resourceLoader.GetString("ErrorNoKinectAvailableTitle"));
                BusyStatus.EndBusy(BusyStatus.BusyEndTypes.Error);
                //MainViewbox.Visibility = Visibility.Collapsed;
                _kinectStarted = false;
            }
        }
        #endregion

        #region Color Frames
        private void Reader_ColorFrameArrived(ColorFrameReader sender, ColorFrameArrivedEventArgs args)
        {
            bool colorFrameProcessed = false;

            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = args.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    // verify data and write the new color frame data to the display bitmap
                    if ((colorFrameDescription.Width == _bitmap.PixelWidth) && (colorFrameDescription.Height == _bitmap.PixelHeight))
                    {
                        if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                        {
                            colorFrame.CopyRawFrameDataToArray(_colorPixels);
                        }
                        else
                        {
                            colorFrame.CopyConvertedFrameDataToArray(_colorPixels, ColorImageFormat.Bgra);
                        }

                        colorFrameProcessed = true;
                    }
                }
            }

            // we got a frame, render
            if (colorFrameProcessed)
            {
                RenderColorPixels(_colorPixels);
            }
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        /// <param name="pixels">pixel data</param>
        private void RenderColorPixels(byte[] pixels)
        {
            _colorPixelStream.Seek(0, SeekOrigin.Begin);
            _colorPixelStream.Write(pixels, 0, pixels.Length);
            _bitmap.Invalidate();
            ColorImg.Source = _bitmap;
        }
        #endregion

        #region Player Management
        private void CreateNewPlayer(ulong trackingId, int bodyNum)
        {
            Debug.WriteLine("Create player: " + bodyNum + " / id: " + trackingId);
            _players.Add(new PlayerInfo(_drawingCanvas, trackingId, bodyNum));
        }

        private void RemovePlayer(ulong trackingId, int bodyNum)
        {
            if (_players == null || !_players.Any()) return;
            var playerToRemove = _players.FirstOrDefault(playerInfo => playerInfo.BodyNum == bodyNum);
            if (playerToRemove != null)
            {
                Debug.WriteLine("Remove player: " + bodyNum + " / id: " + trackingId);
                playerToRemove.RemoveFromWorld(_drawingCanvas);
                _players.Remove(playerToRemove);
            }

            //for (var i = _players.Count - 1; i >= 0; i--)
            //{
            //    if (_players[i].TrackingId != trackingId) continue;

            //    Debug.WriteLine("Remove player: " + trackingId);
            //    _players[i].RemoveFromWorld(_drawingCanvas);
            //    _players.RemoveAt(i);
            //}
        }

        private bool IsPlayerTracked(ulong trackingId)
        {
            return _players.Any(curPlayer => curPlayer.TrackingId == trackingId);
        }
        #endregion


        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="args">event arguments</param>
        private void Reader_BodyFrameArrived(BodyFrameReader sender, BodyFrameArrivedEventArgs args)
        {
            using (var bodyFrame = args.FrameReference.AcquireFrame())
            {
                if (bodyFrame == null) return;

                // update body data
                bodyFrame.GetAndRefreshBodyData(_bodies);
            }


            for (int i = 0; i < _bodies.Length; i++)
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
                    RemovePlayer(curBody.TrackingId, i);
                }
            }

            // Check if two players are close enough
            var minPair = CheckPlayersCloseBy();
            if (minPair != null)
            {
                if (minPair.DistanceInM < 4.0f)
                {
                    // Draw line between heads
                    _minPlayerLine.SetPosition(minPair.Player1Pos.X,
                        minPair.Player1Pos.Y,
                        minPair.Player2Pos.X,
                        minPair.Player2Pos.Y);
                    _minPlayerLine.SetVisibility(true);
                }
                else
                {
                    _minPlayerLine.SetVisibility(false);
                }
                if (minPair.DistanceInM < 0.3f)
                {
                    // Start kiss timer?
                    StartKissPhotoTimer();
                }
                else
                {
                    // Stop timer if it was running and reset if a photo was already taken
                    // (people have to get away from each other to start another photo)
                    _photoTaken = false;
                    StopKissPhotoTimer();
                }
                if (_photoCountdownTimer == null && minPair.DistanceInM < 4.0f)
                {
                    // Show hint to kiss
                    SetCountdown(99);
                }
            }

        }


        private KissPositionModel CheckPlayersCloseBy()
        {
            // TODO Simulation only
            if (_players.Count < 1) return null;
            if (_players.Count == 1)
            {
                _players.Add(new PlayerInfo(_drawingCanvas, 1, -1)
                {
                    FacePosInCamera = new CameraSpacePoint { X = -0.0366f, Y = -0.0486f, Z = 1.164f },
                    FacePosInColor = new ColorSpacePoint { X = 972f, Y = 599f }
                });
            }

            // Real code
            if (_players.Count < 2) return null;

            // Iterate over all players
            var minPair = new KissPositionModel();

            for (var i = 0; i < _players.Count - 1; i++)
            {
                for (var j = i + 1; j < _players.Count; j++)
                {
                    var p1Pos = VectorFromPos(_players[i].FacePosInCamera);
                    var p2Pos = VectorFromPos(_players[j].FacePosInCamera);
                    var dist = (p1Pos - p2Pos).Length;
                    if (dist < minPair.DistanceInM)
                    {
                        minPair.DistanceInM = dist;
                        minPair.Player1Pos = _players[i].FacePosInColor;
                        minPair.Player2Pos = _players[j].FacePosInColor;
                    }
                }
            }
            DistTxt.Text = "Minimum distance [m]: " + minPair.DistanceInM;
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

            var faceJoint = curBody.Joints[JointType.Head];
            if (faceJoint.TrackingState == TrackingState.NotTracked)
            {
                player.SetVisibility(false);
            }
            else
            {
                player.SetVisibility(true);
                var facePosInCamera = faceJoint.Position;
                var facePosInColor = _coordinateMapper.MapCameraPointToColorSpace(facePosInCamera);
                player.SetPosition(facePosInCamera, facePosInColor);
            }
        }

        #region Photo Countdown

        private void StartKissPhotoTimer()
        {
            if (_photoCountdownTimer != null || _photoTaken) return;
            PhotoCountDown = 5;
            _photoCountdownTimer = new Timer(PhotoTimerCallback, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        private void PhotoTimerCallback(object state)
        {
            if (PhotoCountDown == 0)
            {
                // TODO Take picture
                StopKissPhotoTimer();
                _photoTaken = true;
            }
            if (PhotoCountDown > 0)
            {
                SetCountdown(PhotoCountDown - 1);
            }

        }

        private void StopKissPhotoTimer()
        {
            if (_photoCountdownTimer == null) return;
            _photoCountdownTimer.Dispose();
            _photoCountdownTimer = null;
            SetCountdown(-1);
        }

        private void SetCountdown(int newValue)
        {
            if (PhotoCountDown == newValue) return;
            // Speak the text
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => 
            {
                PhotoCountDown = newValue;
                // Convert countdown value to text
                var textConverter = new CountdownIntToStringConverter();
                var speakText = (string)textConverter.Convert(PhotoCountDown, typeof(string), null, Windows.Globalization.Language.CurrentInputMethodLanguageTag);
                await _speechService.SpeakTextAsync(speakText);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
        #endregion


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
            dlg.Commands.Add(
               new UICommand("OK", cmd => result = true));
            dlg.Commands.Add(
               new UICommand("Cancel", cmd => result = false));
            dlg.DefaultCommandIndex = 0;
            dlg.CancelCommandIndex = 1;

            await dlg.ShowAsync();
            return result != null && result == true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
