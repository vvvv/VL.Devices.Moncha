#region Usings
using VL.Serialization.MessagePack;
using MonchaCommonBase;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using CommunityToolkit.HighPerformance;
#endregion

namespace MonchaController32 {

    [Serializable]
    public class NetworkServer {

        #region Fields
        private bool running = true;

        private int port = -1;
        private int bufferSize = 1048576;
        private byte[] buffer;
        
        private Socket serverSocket;
        private Socket clientSocket;
        private Thread listenThread;
        private IPEndPoint localEndpoint;
        
        private readonly object syncobj = new object();
        #endregion

        #region Events
        public event OnNetworkPacketReceived onNetworkPacketReceived;
        #endregion

        #region Lifecycle
        public NetworkServer(int port) {
            this.port = port;
        }

        public void launch() {
            Console.WriteLine("INFO: Start MonchaNET middleware network service @ localhost:"+port+" ...");
            // load settings
            try {
                string filepath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Substring(6);
                string[] lines = File.ReadAllLines(Path.Combine(filepath, "Settings.txt"), Encoding.UTF8);
                this.bufferSize = int.Parse(lines[0].Substring(7));
            } catch(Exception ex) {
                Console.WriteLine("WARNING: Could not read settings file: "+ex.Message);
            }
            // initialize parameters
            this.buffer = new byte[bufferSize];
            this.localEndpoint = new IPEndPoint(IPAddress.Loopback, port);
            this.serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // bind server socket to local endpoint
            serverSocket.Bind(localEndpoint);
            serverSocket.Listen(10);
            // start listening for a single client connection (blocking)
            clientSocket = serverSocket.Accept();
            // disable nagle algorithm
            clientSocket.NoDelay=true;
            // define rx/tx buffer sizes
            clientSocket.SendBufferSize=bufferSize;
            clientSocket.ReceiveBufferSize=bufferSize;
            // create tcp listener instance
            listenThread = new Thread(listen);
            listenThread.Name = "TCP Listener Thread";
            listenThread.Start();
            // log successfull connection establishment
            sendMessage("INFO: Start MonchaNET middleware network service @ localhost:"+port+" ...");
        }

        public void terminate() {

            /* 
             * When using a connection-oriented socket, always call the shutdown method before closing the socket.
             * This ensures that all data is sent and received on the connected socket before it is closed.
             * Call the close method to free all managed and unmanaged resources associated with the socket.
             * Do not attempt to reuse the socket after closing.
             */

            sendMessage("INFO: Terminating TCP network service @ localhost:"+port+" ...");

            // stop listener thread
            running = false;

            // close tcp client socket
            if(clientSocket != null) {
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
            }

            // close tcp server socket
            if(serverSocket != null) {
                serverSocket.Shutdown(SocketShutdown.Both);
                serverSocket.Close();
            }

            // abort thread
            if(listenThread!=null) {
                listenThread.Abort();
            }

        }
        #endregion

        #region Handle Input
        private void listen() {
            try {
                while(running) {
                    if(clientSocket.Available>=4) {
                        int offset = 0;
                        byte[] header = new byte[4];
                        // receive header bytes from tcp stream
                        while(offset < header.Length) {
                            offset += clientSocket.Receive(header, offset, header.Length - offset, SocketFlags.None);
                        }
                        int bytesToRead = BitConverter.ToInt32(header, 0);
                        // receive payload bytes from tcp stream
                        offset = 0;
                        byte[] payload = new byte[bytesToRead];
                        while(offset < payload.Length) {
                            offset += clientSocket.Receive(payload, offset, payload.Length - offset, SocketFlags.None);
                        }
                        // deserialize byte array to network packet
                        NetworkPacket packet = null;
                        packet = MessagePackSerialization.Deserialize<NetworkPacket>(payload.AsMemory());
                        // deliver network packet to listener
                        if(packet!=null) {
                            this.onNetworkPacketReceived?.Invoke(packet);
                        }
                        //sendMessage("FINE: Received "+payload.Length+" [b] of data from TCP socket.");
                    }
                }
            } catch(SocketException) {
                // just terminate gracefully ...
            } catch(ObjectDisposedException) {
                // just terminate gracefully ...
            } catch(ThreadAbortException ex) {
                // allows your thread to terminate gracefully
                if(ex!=null) Thread.ResetAbort();
            }
        }
        #endregion

        #region Handle Output
        public void send(NetworkPacket packet) {
            if(clientSocket!=null) {
                byte[] content = MessagePackSerialization.Serialize(packet);
                byte[] payload = new byte[content.Length+4];
                // prefix with packet length
                Array.Copy(BitConverter.GetBytes(content.Length), 0, payload, 0, 4);
                // append payload after length header
                Array.Copy(content, 0, payload, 4, content.Length);
                // put byte array on wire
                lock(syncobj) {
                    try {
                        clientSocket.Send(payload, 0, payload.Length, 0);
                        //sendMessage("FINE: Sending "+payload.Length+" [b] of data through TCP socket to "+clientSocket.RemoteEndPoint.ToString()+".");
                    } catch(SocketException) {
                        // terminate this process since other network endpoint seems to have shut down
                        terminate();
                    }
                }
            }
        }

        public void sendExceptionCode(string deviceAddress, int code, string message) {
            ExceptionCodePacket exception = new ExceptionCodePacket();
            exception.DeviceAddress = deviceAddress;
            exception.Code = code;
            exception.Message = message;
            this.send(exception);
        }

        public void sendMessage(string message) {
            MessagePacket packet = new MessagePacket();
            packet.Message = message;
            this.send(packet);
        }
        #endregion

    }

}
