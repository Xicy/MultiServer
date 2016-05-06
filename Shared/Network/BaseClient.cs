using System;
using System.IO;
using System.Net;
using Shared.Util;
using Shared.Security;
using System.Net.Sockets;
using System.Threading;

namespace Shared.Network
{
    public enum ClientState { Connected, Disconnected }
    public abstract class BaseClient
    {
        //TODO:Localization
        private const int BufferDefaultSize = 2 * 1024;
        private object ReceiveLock = new object();

        #region Events
        public delegate void HandleBufferEventHandler(BaseClient client, byte[] buffer);
        public event HandleBufferEventHandler HandleBuffer;
        protected virtual void OnHandleBuffer(BaseClient client, byte[] buffer)
        {
            if (this.HandleBuffer != null)
            {
                this.HandleBuffer(client, buffer);
            }
        }

        public delegate void ConnectionEventHandler(BaseClient client);

        public event ConnectionEventHandler Connected;
        protected virtual void OnConnected(BaseClient client)
        {
            if (this.Connected != null)
            {
                this.Connected(client);
            }
        }

        public event ConnectionEventHandler Disconnected;
        protected virtual void OnDisconnected(BaseClient client)
        {
            if (this.Disconnected != null)
            {
                this.Disconnected(client);
            }
        }
        #endregion

        #region Constructors
        private Socket Socket { set; get; }
        private byte[] Buffer { set; get; }
        private MemoryStream ReceivedBuffer { set; get; }
        public ClientState State { set; get; }
        public ICrypter Crypter { set; get; }

        private string _address;
        public string Address { get { if (_address == null) { try { _address = this.Socket.RemoteEndPoint.ToString(); } catch { _address = "<NULL>"; } } return _address; } }
        #endregion

        public BaseClient()
        {
            this.Buffer = new byte[BufferDefaultSize];
            this.ReceivedBuffer = new MemoryStream();
        }

        protected void Send(byte[] buffer)
        {
            if (State != ClientState.Connected) { return; }
            if (Crypter != null) { this.Crypter.EncodeBuffer(ref buffer); }
            try
            {
                Array.Resize(ref buffer, buffer.Length + 4);
                Array.Copy(buffer, 0, buffer, 4, buffer.Length - 4);
                BitConverter.GetBytes(buffer.Length).CopyTo(buffer, 0);

                this.Socket.Send(buffer);

                Array.Clear(buffer, 0, buffer.Length);
            }
            catch(SocketException)
            {
                Log.Debug("Connection lost from '{0}'.", this.Address);
                this.OnDisconnected(this);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Unable to send packet to '{0}'. ({1})", this.Address, ex.Message);
            }
        }
        public void Send(Packet packet)
        {
            this.Send(this.BuildPacket(packet));
        }

        protected abstract byte[] BuildPacket(Packet packet);

        public TClient Connect<TClient>(string host, int port) where TClient : BaseClient
        {
            return this.Connect<TClient>(new IPEndPoint(IPAddress.Parse(host), port));
        }
        public TClient Connect<TClient>(IPEndPoint localEP) where TClient : BaseClient
        {
            try
            {
                if (Socket == null)
                {
                    var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    sock.SendTimeout = 60000;
                    sock.ReceiveTimeout = 60000;
                    
                    sock.Connect(localEP);
                    this.OnReceive(sock);
                    this.OnConnected(this);
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
            this.Socket = socket;
            this.Socket.BeginReceive(this.Buffer, 0, this.Buffer.Length, SocketFlags.None, this.OnReceive, this);
        }
        private void OnReceive(IAsyncResult result)
        {
            var client = (BaseClient)result.AsyncState;
            lock (ReceiveLock)
            {
                try
                {
                    int bytesReceived = client.Socket.EndReceive(result);

                    if (bytesReceived == 0)
                    {
                        Log.Debug("Connection closed from '{0}.", client.Address);
                        this.OnDisconnected(client);
                        return;
                    }

                    client.ReceivedBuffer.Write(client.Buffer, 0, bytesReceived);

                    while (true)
                    {
                        client.ReceivedBuffer.Position = 0;
                        int PacketSize = BitConverter.ToInt32(new byte[] { (byte)client.ReceivedBuffer.ReadByte(), (byte)client.ReceivedBuffer.ReadByte(), (byte)client.ReceivedBuffer.ReadByte(), (byte)client.ReceivedBuffer.ReadByte() }, 0);
                        if (PacketSize >= 4 && client.ReceivedBuffer.Length >= PacketSize)
                        {
                            var buffer = new byte[PacketSize - 4];
                            client.ReceivedBuffer.Read(buffer, 0, PacketSize - 4);
                            if (client.Crypter != null) { client.Crypter.DecodeBuffer(ref buffer); }
                            this.OnHandleBuffer(client, buffer);

                            byte[] copyData = new byte[client.ReceivedBuffer.Length - PacketSize];
                            Array.Copy(client.ReceivedBuffer.ToArray(), PacketSize, copyData, 0, copyData.Length);
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
                        this.OnDisconnected(client);
                        return;
                    }

                    client.Socket.BeginReceive(client.Buffer, 0, client.Buffer.Length, SocketFlags.None, this.OnReceive, client);
                }
                catch (SocketException)
                {
                    Log.Debug("Connection lost from '{0}'.", client.Address);
                    this.OnDisconnected(client);
                }
                catch (ObjectDisposedException)
                {
                    Log.Debug("Socket disposed '{0}'.", client.Address);
                    this.OnDisconnected(client);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "While receiving data from '{0}'.", client.Address);
                    this.OnDisconnected(client);
                }
            }
        }

        public virtual void Disconnect()
        {
            if (this.State != ClientState.Disconnected)
            {
                try
                {
                    this.Socket.Shutdown(SocketShutdown.Both);
                    this.Socket.Close();
                    Array.Clear(Buffer, 0, Buffer.Length);
                    if (this.Crypter != null) { this.Crypter.Dispose(); }
                    this.ReceivedBuffer.Close();
                }
                catch
                { }
                this.CleanUp();
                this.State = ClientState.Disconnected;
            }
            else
            {
                Log.Warning("Client got disconnected multiple times." + Environment.NewLine + Environment.StackTrace);
            }
        }
        protected virtual void CleanUp() { }
    }  
}
