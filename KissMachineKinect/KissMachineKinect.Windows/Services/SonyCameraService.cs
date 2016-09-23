using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Web.Http;
using Kazyx.DeviceDiscovery;
using Kazyx.RemoteApi;
using Kazyx.RemoteApi.Camera;
using KissMachineKinect.Annotations;

namespace KissMachineKinect.Services
{
    public class SonyCameraService : INotifyPropertyChanged
    {
        private const ApiVersion CameraApiVersion = ApiVersion.V1_2;
        public enum CameraStatusValues
        {
            NotConnected,
            Searching,
            Connected,
            RecMode
        }

        public CameraStatusValues CameraStatus
        {
            get { return _cameraStatus; }
            set
            {
                if (_cameraStatus == value) return;
                _cameraStatus = value;
                OnPropertyChanged();
            }
        }

        private CameraApiClient _camera;
        private HttpClient _httpClient;
        private SsdpDiscovery _discovery;
        public string DirectoryName { get; } = "KissMachine";
        public string FileBase { get; } = "Kiss";

        public void Init()
        {
            _discovery = new SsdpDiscovery();
            _discovery.Finished += DiscoveryOnFinished;
            _discovery.SonyCameraDeviceDiscovered += DiscoveryOnSonyCameraDeviceDiscovered;
            _httpClient = new HttpClient();
            CameraStatus = CameraStatusValues.NotConnected;
            Debug.WriteLine("Camera service initialized");
            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
        }

        public void Suspend()
        {
            _discovery.Finished -= DiscoveryOnFinished;
            _discovery.SonyCameraDeviceDiscovered -= DiscoveryOnSonyCameraDeviceDiscovered;
            _discovery = null;
            NetworkInformation.NetworkStatusChanged -= NetworkInformation_NetworkStatusChanged;
            CameraStatus = CameraStatusValues.NotConnected;
        }

        #region Discovery
        private bool _cameraSearchRunning;
        private bool _cameraCheckRunning;

        public void ForceRestart()
        {
            Finish();
            StartCameraSearch();
        }

        public void StartCameraSearch()
        {
            if (_cameraSearchRunning) return;
            _cameraSearchRunning = true;

            _discovery.SearchSonyCameraDevices();
            CameraStatus = CameraStatusValues.Searching;
        }

        public void StopCameraSearch()
        {
            _cameraSearchRunning = false;
            if (CameraStatus == CameraStatusValues.Searching)
            {
                CameraStatus = CameraStatusValues.NotConnected;
            }
        }

        public void Finish()
        {
            StopCameraSearch();
        }

        
        private CameraStatusValues _cameraStatus = CameraStatusValues.NotConnected;

        private void NetworkInformation_NetworkStatusChanged(object sender)
        {
            Debug.WriteLine("*** NetworkInformation NetworkStatusChanged");
            
            // Test if camera was connected and if it is still reachable - if not, restart search
            CheckIfCameraIsStillReachable();
        }

        private async void CheckIfCameraIsStillReachable()
        {
            if (_cameraSearchRunning || _cameraCheckRunning) return;

            _cameraCheckRunning = true;
            Debug.WriteLine("Check if camera is still reachable");
            var cameraIsConnected = true;
            try
            {
                await GetCameraStatusAsync();
                Debug.WriteLine("Camera seems OK, got response.");
            }
            catch (Exception)
            {
                Debug.WriteLine("Exception when trying to get status from camera");
                cameraIsConnected = false;
            }
            _cameraCheckRunning = false;
            if (!cameraIsConnected)
            {
                Debug.WriteLine("Re-starting camera search");
                StartCameraSearch();
            }
        }
        

        private void DiscoveryOnFinished(object sender, EventArgs eventArgs)
        {
            Debug.WriteLine("Device discovery finished");
            if (_cameraSearchRunning)
            {
                // Search is still active - restart another search
                _discovery.SearchSonyCameraDevices();
            }
        }


        private async void DiscoveryOnSonyCameraDeviceDiscovered(object sender, SonyCameraDeviceEventArgs e)
        {
            Debug.WriteLine("Discovered Device");
            var endpoints = e.SonyCameraDevice.Endpoints; // Dictionary of each service name and endpoint.
            _camera = new CameraApiClient(new Uri(endpoints["camera"]));
            CameraStatus = CameraStatusValues.Connected;
            StopCameraSearch();
            try
            {
                await GetCameraStatusAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error getting camera status after device discovery: " + ex);
                // Otherwise, ignore the error for now, maybe it was just too fast for the camera
            }
        }

        private async Task<Event> GetCameraStatusAsync()
        {
            var camStatus = await _camera.GetEventAsync(false, CameraApiVersion);
            Debug.WriteLine("Camera status: " + camStatus.CameraStatus);
            return camStatus;
        }
        #endregion

        #region Take Photo

        public async Task PrepareTakePhoto()
        {
            if (_camera == null) return;

            try
            {
                var camStatus = await _camera.GetEventAsync(false, CameraApiVersion);
                Debug.WriteLine("Camera status: " + camStatus.CameraStatus);

                // We need to start rec mode before taking pictures
                if (camStatus.CameraStatus.Equals(EventParam.NotReady, StringComparison.OrdinalIgnoreCase))
                {
                    // Camera is not ready - start rec mode
                    await _camera.StartRecModeAsync();
                    CameraStatus = CameraStatusValues.RecMode;
                    Debug.WriteLine("Camera rec mode started");
                }

                // Set still size is not supported by RX100 yet
                // Make sure to insert a memory card, otherwise the cam will only record 2M images!
                // With a memory card, the camera will in any case save the full size photo to the card
                // The postview image determines what size is transferred after taking the photo.
                // With a memory card, that should be 2M or Original. In most tests even with a memory
                // card, the camera only offered 2M. But, that's anyway better for this use case, as
                // trasferring a 20M pixel takes several seconds, and we don't want guests to wait that long.
                // Therefore, transffering a 2M postview image is fine, if you then take the pics from the
                // memory card in the camera.
                //var postviewImageSize = await _camera.GetPostviewImageSizeAsync();
                //Debug.WriteLine("Postview image size: " + postviewImageSize);
                //var availableSizes = await _camera.GetAvailablePostviewImageSizeAsync();
                //foreach (var curSize in availableSizes.Candidates)
                //{
                //    Debug.WriteLine("Available: " + curSize);
                //}
                //Debug.WriteLine("Current: " + availableSizes.Current);
            }
            catch (RemoteApiException ex)
            {
                Debug.WriteLine("Exception while preparing to take a photo: " + ex.StatusCode);
            }
        }

        public async Task PutCameraToSleep()
        {
            if (_camera == null) return;
            try
            {
                await _camera.StopRecModeAsync();
                CameraStatus = CameraStatusValues.Connected;
                Debug.WriteLine("Camera rec mode stopped");
            }
            catch (RemoteApiException ex)
            {
                Debug.WriteLine("Exception while putting camera to sleep: " + ex.StatusCode);
            }
        }

        public async Task<StorageFile> TakePhoto()
        {
            if (_camera == null) return null;
            
            Debug.WriteLine("Take photo");
            var picUrls = await _camera.ActTakePictureAsync();

            Debug.WriteLine("Photo taken");
            //foreach (var url in picUrls)
            //{
            var cameraPhotoUrl = picUrls.FirstOrDefault();
            Debug.WriteLine("Photo URL: " + cameraPhotoUrl);
            var picDownloadUrl = new Uri(cameraPhotoUrl);
            var response = await _httpClient.GetAsync(picDownloadUrl, HttpCompletionOption.ResponseContentRead);
            var imageStream = (await response.Content.ReadAsInputStreamAsync()).AsStreamForRead();

            using (imageStream)
            {
                var rootFolder = KnownFolders.PicturesLibrary;
                var folder =
                    await rootFolder.CreateFolderAsync(DirectoryName, CreationCollisionOption.OpenIfExists);
                var filename = string.Format(FileBase + "_{0:yyyyMMdd_HHmmss}.jpg", DateTime.Now);
                var file = await folder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName);

                using (var outStream = await file.OpenStreamForWriteAsync())
                {
                    await imageStream.CopyToAsync(outStream);
                }
                return file;
            }
            //}
        }


        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
