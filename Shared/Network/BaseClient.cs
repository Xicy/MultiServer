using System;
using System.IO;
using System.Net;
using Shared.Util;
using Shared.Security;
using System.Net.Sockets;

namespace Shared.Network
{
    public enum ClientState { Connected, Disconnected }
    public abstract class BaseClient
    {
        private const int BufferDefaultSize = 1024 * 2;
        private readonly object _receiveLock = new object();

        #region Events
        public delegate void HandleBufferEventHandler(BaseClient client, byte[] buffer);
        public event HandleBufferEventHandler HandleBuffer;
        protected internal virtual void OnHandleBuffer(BaseClient client, byte[] buffer)
        {
            HandleBuffer?.Invoke(client, buffer);
        }

        public delegate void ConnectionEventHandler(BaseClient client);

        public event ConnectionEventHandler Connected;
        protected internal virtual void OnConnected(BaseClient client)
        {
            Connected?.Invoke(client);
        }

        public event ConnectionEventHandler Disconnected;
        protected internal virtual void OnDisconnected(BaseClient client)
        {
            Disconnected?.Invoke(client);
        }
        #endregion

        #region Attirbutes
        private Socket Socket { set; get; }
        private byte[] Buffer { set; get; }
        private MemoryStream ReceivedBuffer { set; get; }
        public ICrypter Crypter { set; get; }

        private ClientState _state;
        public ClientState State { set { _state = value; } get { return Socket == null || !Socket.Connected ? ClientState.Disconnected : _state; } }

        private string _address;
        public string Address { get { if (_address != null) return _address; try { _address = Socket.RemoteEndPoint.ToString(); } catch { _address = Localization.Get("Shared.Network.BaseClient.Address.Null"); } return _address; } }
        #endregion

        protected BaseClient()
        {
            Buffer = new byte[BufferDefaultSize];
        }

        protected void Send(byte[] buffer)
        {
            if (State != ClientState.Connected) return;
            Crypter?.EncodeBuffer(ref buffer);
            try
            {
                Array.Resize(ref buffer, buffer.Length + 4);
                Array.Copy(buffer, 0, buffer, 4, buffer.Length - 4);
                BitConverter.GetBytes(buffer.Length).CopyTo(buffer, 0);

                Socket.Send(buffer);

                Array.Clear(buffer, 0, buffer.Length);
            }
            catch (SocketException)
            {
                Log.Debug("Connection lost from '{0}'.", Address);//TODO:HERE
                OnDisconnected(this);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Unable to send packet to '{0}'. ({1})", Address, ex.Message);
            }
        }
        public void Send(Packet packet)
        {
            Send(BuildPacket(packet));
        }

        public TClient Connect<TClient>(string host, int port) where TClient : BaseClient
        {
            return Connect<TClient>(new IPEndPoint(IPAddress.Parse(host), port));
        }
        public TClient Connect<TClient>(IPEndPoint localEp) where TClient : BaseClient
        {
            try
            {
                if (State == ClientState.Disconnected)
                {
                    var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        SendTimeout = 60000,
                        ReceiveTimeout = 60000
                    };
                    sock.Connect(localEp);
                    OnReceive(sock);
                    OnConnected(this);
                    Log.Status("Client ready, listening on {0}.", Address);
                }
                else
                {
                    Log.Warning("Client already connected!");
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
            return (TClient)this;
        }
        internal void OnReceive(Socket socket)
        {
            Socket = socket;
            State = ClientState.Connected;
            ReceivedBuffer = new MemoryStream();
            Socket.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, OnReceive, this);
        }
        private void OnReceive(IAsyncResult result)
        {
            var client = (BaseClient)result.AsyncState;
            lock (_receiveLock)
            {
                try
                {
                    var bytesReceived = client.Socket.EndReceive(result);

                    if (bytesReceived == 0)
                    {
                        Log.Debug("Connection closed from '{0}.", client.Address);
                        OnDisconnected(client);
                        return;
                    }

                    client.ReceivedBuffer.Write(client.Buffer, 0, bytesReceived);

                    while (true)
                    {
                        client.ReceivedBuffer.Position = 0;
                        var packetSize = BitConverter.ToInt32(new[] { (byte)client.ReceivedBuffer.ReadByte(), (byte)client.ReceivedBuffer.ReadByte(), (byte)client.ReceivedBuffer.ReadByte(), (byte)client.ReceivedBuffer.ReadByte() }, 0);
                        if (packetSize >= 4 && client.ReceivedBuffer.Length >= packetSize)
                        {
                            var buffer = new byte[packetSize - 4];
                            client.ReceivedBuffer.Read(buffer, 0, packetSize - 4);
                            client.Crypter?.DecodeBuffer(ref buffer);
                            OnHandleBuffer(client, buffer);

                            var copyData = new byte[client.ReceivedBuffer.Length - packetSize];
                            Array.Copy(client.ReceivedBuffer.ToArray(), packetSize, copyData, 0, copyData.Length);
                            client.ReceivedBuffer.Position = 0;
                            client.ReceivedBuffer.Write(copyData, 0, copyData.Length);
                            client.ReceivedBuffer.SetLength(copyData.Length);
                            client.ReceivedBuffer.Position = copyData.Length;
                            if (copyData.Length == 0) { break; }
                            Array.Clear(copyData, 0, copyData.Length);
                        }
                        else
                        {
                            client.ReceivedBuffer.Position = client.ReceivedBuffer.Length;
                            break;
                        }
                    }

                    if (client.State == ClientState.Disconnected)
                    {
                        Log.Debug("Disconnected connection from '{0}'.", client.Address);
                        OnDisconnected(client);
                        return;
                    }

                    client.Socket.BeginReceive(client.Buffer, 0, client.Buffer.Length, SocketFlags.None, OnReceive, client);
                }
                catch (SocketException)
                {
                    Log.Debug("Connection lost from '{0}'.", client.Address);
                    OnDisconnected(client);
                }
                catch (ObjectDisposedException)
                {
                    Log.Debug("Socket disposed '{0}'.", client.Address);
                    OnDisconnected(client);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "While receiving data from '{0}'.", client.Address);
                    OnDisconnected(client);
                }
            }
        }

        protected virtual byte[] BuildPacket(Packet packet)
        {
            return packet.Build();
        }

        public virtual void Disconnect()
        {
            if (State != ClientState.Disconnected)
            {
                try
                {
                    Socket.Shutdown(SocketShutdown.Both);
                    Socket.Close();
                    Crypter?.Dispose();
                    Crypter = null;
                    ReceivedBuffer?.Close();
                    Array.Clear(Buffer, 0, Buffer.Length);
                }
                finally
                {
                    CleanUp();
                    State = ClientState.Disconnected;
                }
            }
            else
            {
                Log.Warning("Client got disconnected multiple times." + Environment.NewLine + Environment.StackTrace);
            }
        }
        protected virtual void CleanUp() { }
    }
}
