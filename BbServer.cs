using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NATUPNPLib;


namespace BrokenBotWatcher
{
    public class BbServer : IDisposable
    {
        #region Fields

        private static BbServer server = new BbServer();
        private const int CLIENT_TIMEOUT_MINUTE = 2;
        private TcpListener listener;
        CancellationTokenSource cancellation = new CancellationTokenSource();
        private int _port;
        private bool _IsDisposed = false;

        #endregion

        #region Ctors

        private BbServer()
        {

        }

        static BbServer()
        {
            Server = server;
        }

        #endregion

        #region Delegates & Events

        internal delegate void _serverOperation(string message);

        internal event _serverOperation MessageArived;

        internal event _serverOperation ServerInitiated;

        #endregion

        #region Props

        public static BbServer Server { get; private set; }

        #endregion

        #region Methods

        public void Start(int port)
        {
            _port = port;

            var ipAddress = Dns.GetHostAddresses(Dns.GetHostName()).First((e) => e.AddressFamily == AddressFamily.InterNetwork);

            // Create Upnp and start listning to the server
            var externalIP = RegisterUpnp(ipAddress);
            var endpoint = new IPEndPoint(ipAddress, _port);
            listener = new TcpListener(endpoint);

            // Start listning
            listener.Start();

            // Start accepting
            AcceptClientsAsync();

            if (ServerInitiated != null)
            {
                ServerInitiated(externalIP);
            }
        }

        public void Stop()
        {
            Dispose();
        }

        async private Task AcceptClientsAsync()
        {
            while (true)
            {
                // Wait for a client
                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);

                // Handle the client async
                HandleClient(client);
            }

        }

        async private Task HandleClient(TcpClient client)
        {
            using (client)
            {
                var buf = new byte[4096];
                var stream = client.GetStream();

                // Announce someone has connected
                Console.WriteLine("A client has been conected. IP: {0}", client.Client.RemoteEndPoint);

                // Start 
                while (!cancellation.IsCancellationRequested)
                {
                    // Create timeout to prevent unchaught disconections
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(CLIENT_TIMEOUT_MINUTE));
                    var amountReadTask = stream.ReadAsync(buf, 0, buf.Length, cancellation.Token);
                    var completedTask = await Task.WhenAny(timeoutTask, amountReadTask)
                                                  .ConfigureAwait(false);
                    // Check if it had timeout
                    if (completedTask == timeoutTask)
                    {
                        var msg = Encoding.ASCII.GetBytes("Client timed out");
                        await stream.WriteAsync(msg, 0, msg.Length);
                        break;
                    }

                    // Get the result
                    var amountRead = amountReadTask.Result;
                    if (amountRead == 0) break; //end of stream.

                    if (MessageArived != null)
                    {
                        MessageArived(Encoding.UTF8.GetString(buf, 0, amountRead).Trim());
                    }

                    //Console.WriteLine(Encoding.UTF8.GetString(buf, 0, amountRead));

                    // Echo the message to client
                    byte[] sendBytes = CombineByteArray(StringToBuffer("Server has recived: "), buf.Take(amountRead).ToArray());
                    await stream.WriteAsync(sendBytes, 0, sendBytes.Length, cancellation.Token)
                                .ConfigureAwait(false);
                }

                Console.WriteLine("Client ('{0}') disconnected", client.Client.RemoteEndPoint);
            }
        }

        private byte[] StringToBuffer(string aString)
        {
            return Encoding.UTF8.GetBytes(aString);
        }
        private string BufferToString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        private byte[] CombineByteArray(byte[] b1, byte[] b2)
        {
            byte[] b = new byte[b1.Length + b2.Length];
            Buffer.BlockCopy(b1, 0, b, 0, b1.Length);
            Buffer.BlockCopy(b2, 0, b, b1.Length, b2.Length);
            return b;
        }

        #region Upnp Methods

        /// <summary>
        /// Register Upnp on the network to allow user connect behind firewall
        /// </summary>
        /// <param name="internalIpAddress">Computer internal IP</param>
        /// <returns>Computer external IP</returns>
        private string RegisterUpnp(IPAddress internalIpAddress)
        {
            var upnpnat = new UPnPNATClass();
            NATUPNPLib.IStaticPortMappingCollection mappings = upnpnat.StaticPortMappingCollection;

            // Open  the port
            var mapped = mappings.Add(_port, "TCP", _port, internalIpAddress.ToString(), true, "BrokenBotWatcher Server");
            Console.WriteLine(mapped.ExternalIPAddress);
            return mapped.ExternalIPAddress;
        }

        private void UnregisterUpnp()
        {
            var upnpnat = new UPnPNATClass();
            NATUPNPLib.IStaticPortMappingCollection mappings = upnpnat.StaticPortMappingCollection;

            mappings.Remove(_port, "TCP");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_IsDisposed)
            {
                // Stop listen to the port
                UnregisterUpnp();

                // Stop the server
                listener.Stop();
                listener = null;
                cancellation.Cancel();

                // Close events
                MessageArived = null;
                ServerInitiated = null;

                _IsDisposed = true; 
            }
        }

        ~BbServer()
        {
            if (!_IsDisposed)
            {
                // Stop listen to the port
                UnregisterUpnp();
            }
        }

        #endregion

        #endregion
    }
}
