using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Net;
#if WINDOWS_PHONE||DOT_NET
using System.Net.Sockets;
#elif WINDOWS_PHONE_APP || WINDOWS_APP || NETFX_CORE
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Web.Http;
#endif

namespace Kazyx.DeviceDiscovery
{
    public class SsdpDiscovery
    {
        private const string MULTICAST_ADDRESS = "239.255.255.250";

#if WINDOWS_PHONE_APP || WINDOWS_APP || NETFX_CORE
        private readonly HostName MULTICAST_HOST = new HostName(MULTICAST_ADDRESS);
#endif

        private const int SSDP_PORT = 1900;
        private const int RESULT_BUFFER = 8192;

        public const string ST_ALL = "ssdp:all";

        private uint _MX = 1;
        public uint MX
        {
            set { _MX = value; }
            get { return _MX; }
        }

        private readonly TimeSpan DEFAULT_TIMEOUT = new TimeSpan(0, 0, 5);

        public delegate void SonyCameraDeviceHandler(object sender, SonyCameraDeviceEventArgs e);

        public event SonyCameraDeviceHandler SonyCameraDeviceDiscovered;

        protected void OnDiscovered(SonyCameraDeviceEventArgs e)
        {
            if (SonyCameraDeviceDiscovered != null)
            {
                SonyCameraDeviceDiscovered(this, e);
            }
        }

        public delegate void DeviceDescriptionHandler(object sender, DeviceDescriptionEventArgs e);

        public event DeviceDescriptionHandler DescriptionObtained;

        protected void OnDiscovered(DeviceDescriptionEventArgs e)
        {
            if (DescriptionObtained != null)
            {
                DescriptionObtained(this, e);
            }
        }

        public event EventHandler Finished;

        protected void OnTimeout(EventArgs e)
        {
            if (Finished != null)
            {
                Finished(this, e);
            }
        }

        private async void Search(string st, TimeSpan? timeout = null)
        {
            Log("Search");

            var ssdp_data = new StringBuilder()
                .Append("M-SEARCH * HTTP/1.1").Append("\r\n")
                .Append("HOST: ").Append(MULTICAST_ADDRESS).Append(":").Append(SSDP_PORT.ToString()).Append("\r\n")
                .Append("MAN: ").Append("\"ssdp:discover\"").Append("\r\n")
                .Append("MX: ").Append(MX.ToString()).Append("\r\n")
                .Append("ST: ").Append(st).Append("\r\n")
                .Append("\r\n")
                .ToString();
            var data_byte = Encoding.UTF8.GetBytes(ssdp_data);

            var timeout_called = false;

#if WINDOWS_PHONE||DOT_NET
            var DD_Handler = new AsyncCallback(ar =>
            {
                if (timeout_called)
                {
                    return;
                }

                var req = ar.AsyncState as HttpWebRequest;

                try
                {
                    var res = req.EndGetResponse(ar) as HttpWebResponse;
                    using (var reader = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                    {
                        try
                        {
                            var response = reader.ReadToEnd();
                            OnDiscovered(new DeviceDescriptionEventArgs(response));

                            var camera = AnalyzeDescription(response);
                            if (camera != null)
                            {
                                OnDiscovered(new SonyCameraDeviceEventArgs(camera, req.RequestUri));
                            }
                        }
                        catch (Exception)
                        {
                            Log("Invalid XML");
                            //Invalid XML.
                        }
                    }
                }
                catch (WebException)
                {
                    //Invalid DD location or network error.
                }
            });
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SendBufferSize = data_byte.Length;

            var snd_event_args = new SocketAsyncEventArgs();
            snd_event_args.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(MULTICAST_ADDRESS), SSDP_PORT);
            snd_event_args.SetBuffer(data_byte, 0, data_byte.Length);

            var rcv_event_args = new SocketAsyncEventArgs();
            rcv_event_args.SetBuffer(new byte[RESULT_BUFFER], 0, RESULT_BUFFER);

            var SND_Handler = new EventHandler<SocketAsyncEventArgs>((sender, e) =>
            {
                if (e.SocketError == SocketError.Success && e.LastOperation == SocketAsyncOperation.SendTo)
                {
                    try
                    {
                        socket.ReceiveBufferSize = RESULT_BUFFER;
                        socket.ReceiveAsync(rcv_event_args);
                    }
                    catch (ObjectDisposedException)
                    {
                        Log("Socket is already disposed.");
                    }
                }
            });
            snd_event_args.Completed += SND_Handler;

            var RCV_Handler = new EventHandler<SocketAsyncEventArgs>((sender, e) =>
            {
                if (e.SocketError == SocketError.Success && e.LastOperation == SocketAsyncOperation.Receive)
                {
                    string result = Encoding.UTF8.GetString(e.Buffer, 0, e.BytesTransferred);
                    //Log(result);

                    GetDeviceDescriptionAsync(DD_Handler, result);

                    try
                    {
                        socket.ReceiveAsync(e);
                    }
                    catch (ObjectDisposedException)
                    {
                        Log("Socket is already disposed.");
                    }
                }
            });
            rcv_event_args.Completed += RCV_Handler;
            socket.SendToAsync(snd_event_args);
#elif WINDOWS_PHONE_APP||WINDOWS_APP||NETFX_CORE
            var handler = new TypedEventHandler<DatagramSocket, DatagramSocketMessageReceivedEventArgs>(async (sender, args) =>
            {
                Log("Datagram message received");
                if (timeout_called || args == null)
                {
                    return;
                }
                string data;
                using (var reader = args.GetDataReader())
                {
                    data = reader.ReadString(reader.UnconsumedBufferLength);
                }
                Log(data);
                await GetDeviceDescriptionAsync(data, args.LocalAddress).ConfigureAwait(false);
            });

            var adapters = await GetActiveAdaptersAsync().ConfigureAwait(false);

            await Task.WhenAll(adapters.Select(async adapter =>
            {
                using (var socket = new DatagramSocket())
                {
                    socket.Control.DontFragment = true;
                    socket.MessageReceived += handler;

                    try
                    {
                        await socket.BindServiceNameAsync("", adapter);
                        socket.JoinMulticastGroup(MULTICAST_HOST);

                        using (var output = await socket.GetOutputStreamAsync(MULTICAST_HOST, SSDP_PORT.ToString()))
                        {
                            using (var writer = new DataWriter(output))
                            {
                                writer.WriteBytes(data_byte);
                                await writer.StoreAsync();
                            }
                        }
                        await Task.Delay((timeout == null) ? DEFAULT_TIMEOUT : timeout.Value).ConfigureAwait(false);
                        Log("Search Timeout");
                        timeout_called = true;
                    }
                    catch (Exception e)
                    {
                        Log("Failed to send multicast: " + e.StackTrace);
                    }
                    finally
                    {
                        socket.MessageReceived -= handler;
                    }
                }
            })).ConfigureAwait(false);
#endif
#if WINDOWS_PHONE||DOT_NET
            await Task.Delay((timeout == null) ? DEFAULT_TIMEOUT : timeout.Value).ConfigureAwait(false);

            Log("Search Timeout");
            timeout_called = true;
            snd_event_args.Completed -= SND_Handler;
            rcv_event_args.Completed -= RCV_Handler;
            socket.Close();
#endif
            OnTimeout(new EventArgs());
        }

#if WINDOWS_PHONE_APP || WINDOWS_APP || NETFX_CORE
        public static Task<IList<NetworkAdapter>> GetActiveAdaptersAsync()
        {
            var tcs = new TaskCompletionSource<IList<NetworkAdapter>>();

            Task.Run(() =>
            {
                var profiles = NetworkInformation.GetConnectionProfiles();
                var list = new List<NetworkAdapter>();
                foreach (var profile in profiles)
                {
                    if (profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.None)
                    {
                        // Historical profiles.
                        // Log("ConnectivityLevel None: " + profile.ProfileName);
                        continue;
                    }

                    var adapter = profile.NetworkAdapter;

                    switch (adapter.IanaInterfaceType)
                    {
                        case 6: // Ethernet
                        case 71: // 802.11
                            break;
                        default:
                            // Log("Type mismatch: " + profile.ProfileName);
                            continue;
                    }

                    if (!list.Contains(adapter))
                    {
                        Log("Active Adapter: " + profile.ProfileName);
                        list.Add(adapter);
                    }
                }

                tcs.SetResult(list);
            });

            return tcs.Task;
        }
#endif

        /// <summary>
        /// Search sony camera devices and retrieve the endpoint URLs.
        /// </summary>
        /// <param name="timeout">Timeout to end up search.</param>
        public void SearchSonyCameraDevices(TimeSpan? timeout = null)
        {
            Search("urn:schemas-sony-com:service:ScalarWebAPI:1", timeout);
        }

        /// <summary>
        /// Search UPnP devices and retrieve the device description.
        /// </summary>
        /// <param name="st">Search Target parameter for SSDP.</param>
        /// <param name="timeout">Timeout to end up search.</param>
        public void SearchUpnpDevices(string st = ST_ALL, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(st))
            {
                st = ST_ALL;
            }
            Search(st, timeout);
        }

#if WINDOWS_PHONE||DOT_NET
        private static void GetDeviceDescriptionAsync(AsyncCallback ac, string data)
        {
            var dd_location = ParseLocation(data);
            if (dd_location != null)
            {
                try
                {
                    var req = HttpWebRequest.Create(new Uri(dd_location)) as HttpWebRequest;
                    req.Method = "GET";
                    req.BeginGetResponse(ac, req);
                }
                catch (Exception)
                {
                    //Invalid DD location.
                }
            }
        }
#elif WINDOWS_PHONE_APP||WINDOWS_APP||NETFX_CORE
        private HttpClient HttpClient = new HttpClient();

        private async Task GetDeviceDescriptionAsync(string data, HostName remoteAddress)
        {
            var dd_location = ParseLocation(data);
            if (dd_location != null)
            {
                try
                {
                    var uri = new Uri(dd_location);
                    var res = await HttpClient.GetAsync(uri);
                    if (res.IsSuccessStatusCode)
                    {
                        var response = await res.Content.ReadAsStringAsync();
                        OnDiscovered(new DeviceDescriptionEventArgs(response, uri, remoteAddress));

                        var camera = AnalyzeDescription(response);
                        if (camera != null)
                        {
                            OnDiscovered(new SonyCameraDeviceEventArgs(camera, uri, remoteAddress));
                        }
                    }
                }
                catch (Exception)
                {
                    //Invalid DD location.
                }
            }
        }
#endif

        private static string ParseLocation(string response)
        {
            var reader = new StringReader(response);
            var line = reader.ReadLine();
            if (line != "HTTP/1.1 200 OK")
            {
                return null;
            }

            while (true)
            {
                line = reader.ReadLine();
                if (line == null)
                    break;
                if (line == "")
                    continue;

                int divider = line.IndexOf(':');
                if (divider < 1)
                    continue;

                string name = line.Substring(0, divider).Trim();
                if (name == "LOCATION" || name == "location")
                {
                    return line.Substring(divider + 1).Trim();
                }
            }

            return null;
        }

        private const string upnp_ns = "{urn:schemas-upnp-org:device-1-0}";
        private const string sony_ns = "{urn:schemas-sony-com:av}";

        public static SonyCameraDeviceInfo AnalyzeDescription(string response)
        {
            //Log(response);
            var endpoints = new Dictionary<string, string>();

            var xml = XDocument.Parse(response);
            var device = xml.Root.Element(upnp_ns + "device");
            if (device == null)
            {
                return null;
            }
            var f_name = device.Element(upnp_ns + "friendlyName").Value;
            var m_name = device.Element(upnp_ns + "modelName").Value;
            var udn = device.Element(upnp_ns + "UDN").Value;
            var info = device.Element(sony_ns + "X_ScalarWebAPI_DeviceInfo");
            if (info == null)
            {
                return null;
            }
            var list = info.Element(sony_ns + "X_ScalarWebAPI_ServiceList");

            foreach (var service in list.Elements())
            {
                var name = service.Element(sony_ns + "X_ScalarWebAPI_ServiceType").Value;
                var url = service.Element(sony_ns + "X_ScalarWebAPI_ActionList_URL").Value;
                if (name == null || url == null)
                    continue;

                string endpoint;
                if (url.EndsWith("/"))
                    endpoint = url + name;
                else
                    endpoint = url + "/" + name;

                endpoints.Add(name, endpoint);
            }

            if (endpoints.Count == 0)
            {
                return null;
            }

            return new SonyCameraDeviceInfo(udn, m_name, f_name, endpoints);
        }

        private static void Log(string message)
        {
            Debug.WriteLine("[SoDiscovery] " + message);
        }
    }

    public class DeviceDescriptionEventArgs : EventArgs
    {
        public string Description { private set; get; }

#if WINDOWS_PHONE||DOT_NET
        public DeviceDescriptionEventArgs(string description)
        {
            Description = description;
        }
#elif WINDOWS_PHONE_APP || WINDOWS_APP || NETFX_CORE

        public DeviceDescriptionEventArgs(string description, Uri location, HostName local)
        {
            Description = description;
            Location = location;
            LocalAddress = local;
        }

        public Uri Location { private set; get; }
        public HostName LocalAddress { private set; get; }
#endif
    }

    public class SonyCameraDeviceEventArgs : EventArgs
    {
        public SonyCameraDeviceInfo SonyCameraDevice { private set; get; }

#if WINDOWS_PHONE||DOT_NET
        public SonyCameraDeviceEventArgs(SonyCameraDeviceInfo info, Uri location)
        {
            SonyCameraDevice = info;
        }
#elif WINDOWS_PHONE_APP || WINDOWS_APP || NETFX_CORE

        public SonyCameraDeviceEventArgs(SonyCameraDeviceInfo info, Uri location, HostName local)
        {
            SonyCameraDevice = info;
            Location = location;
            LocalAddress = local;
        }

        public Uri Location { private set; get; }
        public HostName LocalAddress { private set; get; }
#endif
    }
}
