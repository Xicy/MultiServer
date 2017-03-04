using System;
using System.IO;
using System.Net;
using Shared.Util;
using System.Net.Sockets;

namespace Shared.Network
{
    public enum ClientState : byte { Disconnected, Connected }
    public abstract class BaseClient<TClient> where TClient : BaseClient<TClient>
    {
        #region Fields
        private const int BufferDefaultSize = 1024;
        private readonly object _rwLock = new object();

        private Socket Socket { set; get; }
        private byte[] Buffer { get; }
        private MemoryStream ReceivedBuffer { set; get; }

        public ClientState State { set; get; }

        private string _address;
        public string Address { get { if (_address != null) return _address; try { _address = Socket.RemoteEndPoint.ToString(); } catch { _address = Localization.Get("Shared.Network.BaseClient.Address.Null"); } return _address; } }
        #endregion


        #region Events
        public delegate void HandleBufferEventHandler(TClient client, byte[] buffer);
        public event HandleBufferEventHandler HandleBuffer;
        protected internal virtual void OnHandleBuffer(TClient client, byte[] buffer)
        {
            HandleBuffer?.Invoke(client, buffer);
        }

        public delegate void ConnectionEventHandler(TClient client);

        public event ConnectionEventHandler Connected;
        protected internal virtual void OnConnected(TClient client)
        {
            Connected?.Invoke(client);
        }

        public event ConnectionEventHandler Disconnected;
        protected internal virtual void OnDisconnected(TClient client)
        {
            if (State == ClientState.Connected) Disconnect();
            Disconnected?.Invoke(client);
        }
        #endregion

        protected BaseClient()
        {
            Buffer = new byte[BufferDefaultSize];
        }

        protected void Send(byte[] buffer)
        {
            if (State != ClientState.Connected) return;
            try
            {
                Array.Resize(ref buffer, buffer.Length + 4);
                System.Buffer.BlockCopy(buffer, 0, buffer, 4, buffer.Length - 4);
                BitConverter.GetBytes(buffer.Length).CopyTo(buffer, 0);

                Socket.Send(buffer);
                Array.Clear(buffer, 0, buffer.Length);
            }
            catch (SocketException)
            {
                Log.Debug(Localization.Get("Shared.Network.BaseClient.Send.SocketException"), Address);
                OnDisconnected((TClient)this);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, Localization.Get("Shared.Network.BaseClient.Send.Exception"), Address, ex.Message);
            }
        }

        public TClient Connect(string host, int port)
        {
            return Connect(new IPEndPoint(IPAddress.Parse(host), port));
        }
        public TClient Connect(IPEndPoint localEp)
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
                    OnConnected((TClient)this);
                    Log.Status(Localization.Get("Shared.Network.BaseClient.Connect.Ready"), Address);
                }
                else
                {
                    Log.Warning(Localization.Get("Shared.Network.BaseClient.Connect.AlreadyConnected"));
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
            var client = (TClient)result.AsyncState;
            lock (_rwLock)
            {
                try
                {
                    var bytesReceived = client.Socket.EndReceive(result);

                    if (bytesReceived == 0)
                    {
                        Log.Debug(Localization.Get("Shared.Network.BaseClient.OnReceive.Closed"), client.Address);
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
                            OnHandleBuffer(client, buffer);

                            var copyData = new byte[client.ReceivedBuffer.Length - packetSize];
                            client.ReceivedBuffer.Read(copyData, 0, copyData.Length);
                            client.ReceivedBuffer.Position = 0;
                            client.ReceivedBuffer.Write(copyData, 0, copyData.Length);
                            client.ReceivedBuffer.SetLength(copyData.Length);
                            client.ReceivedBuffer.Position = copyData.Length;
                            if (copyData.Length == 0) break;
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
                        Log.Debug(Localization.Get("Shared.Network.BaseClient.OnReceive.Disconnected"), client.Address);
                        OnDisconnected(client);
                        return;
                    }

                    client.Socket.BeginReceive(client.Buffer, 0, client.Buffer.Length, SocketFlags.None, OnReceive, client);
                }
                catch (SocketException)
                {
                    Log.Debug(Localization.Get("Shared.Network.BaseClient.OnReceive.SocketException"), client.Address);
                    OnDisconnected(client);
                }
                catch (ObjectDisposedException)
                {
                    Log.Debug(Localization.Get("Shared.Network.BaseClient.OnReceive.ObjectDisposedException"), client.Address);
                    OnDisconnected(client);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, Localization.Get("Shared.Network.BaseClient.OnReceive.Exception"), client.Address);
                    OnDisconnected(client);
                }
            }
        }

        public void Disconnect()
        {
            if (State != ClientState.Disconnected)
            {
                try
                {
                    Socket.Shutdown(SocketShutdown.Both);
                    Socket.Close();
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
                Log.Warning(Localization.Get("Shared.Network.BaseClient.Disconnect.AlreadyDisconnected"));
            }
        }
        protected virtual void CleanUp() { }
    }
}
