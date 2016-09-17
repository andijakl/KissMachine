﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Web.Http;
using Kazyx.DeviceDiscovery;
using Kazyx.RemoteApi;
using Kazyx.RemoteApi.Camera;

namespace KissMachineKinect.Services
{
    public class SonyCameraService
    {
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
            Debug.WriteLine("Camera service initialized");
            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
        }

        public void Suspend()
        {
            _discovery.Finished -= DiscoveryOnFinished;
            _discovery.SonyCameraDeviceDiscovered -= DiscoveryOnSonyCameraDeviceDiscovered;
            _discovery = null;
            NetworkInformation.NetworkStatusChanged -= NetworkInformation_NetworkStatusChanged;
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
        }

        public void StopCameraSearch()
        {
            _canceller?.Cancel();
            _canceller = null;
            _cameraSearchRunning = false;
        }

        public void Finish()
        {
            StopCameraSearch();
        }
        

        CancellationTokenSource _canceller;

        private void NetworkInformation_NetworkStatusChanged(object sender)
        {
            Debug.WriteLine("*** NetworkInformation NetworkStatusChanged");

            //_discovery.SearchSonyCameraDevices();
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
            // TODO maybe do not stop listening for network status changes
            StopCameraSearch();
            await GetCameraStatusAsync();
        }

        private async Task<Event> GetCameraStatusAsync()
        {
            var camStatus = await _camera.GetEventAsync(false, ApiVersion.V1_2);
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
                var camStatus = await _camera.GetEventAsync(false, ApiVersion.V1_2);
                Debug.WriteLine("Camera status: " + camStatus.CameraStatus);

                // We need to start rec mode before taking pictures
                if (camStatus.CameraStatus.Equals(EventParam.NotReady, StringComparison.OrdinalIgnoreCase))
                {
                    // Camera is not ready - start rec mode
                    await _camera.StartRecModeAsync();
                    Debug.WriteLine("Camera rec mode started");
                }
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

    }
}