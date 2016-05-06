using System;
using System.Net;
using Shared.Util;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;

namespace Shared.Network
{
    public abstract class BaseServer<TClient> where TClient : BaseClient, new()
    {
        //TODO:Localizaion
        private Socket SocketListen;
        private ReaderWriterLockSlim ListLock = new ReaderWriterLockSlim();
        public List<TClient> Clients { protected set; get; }
        public PacketHandlerManager<TClient> Handlers { set; get; }

        #region Events
        public delegate void ClientConnectionEventHandler(TClient client);

        public event ClientConnectionEventHandler ClientConnected;
        protected virtual void OnClientConnected(TClient client)
        {
            if (this.ClientConnected != null)
            {
                this.ClientConnected(client);
            }
        }

        public event ClientConnectionEventHandler ClientDisconnected;
        protected virtual void OnClientDisconnected(TClient client)
        {
            if (this.ClientDisconnected != null)
            {
                this.ClientDisconnected(client);
            }
        }

        protected abstract void HandleBuffer(TClient client, byte[] buffer);
        #endregion

        protected BaseServer()
        {
            SocketListen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {NoDelay = true};
            this.Clients = new List<TClient>();
        }

        public void Start(int port)
        {
            this.Start(new IPEndPoint(IPAddress.Any, port));
        }
        public void Start(string host, int port)
        {
            this.Start(new IPEndPoint(IPAddress.Parse(host), port));
        }
        public void Start(IPEndPoint localEP)
        {
            try
            {
                if (this.Handlers == null)
                {
                    Log.Error("No packet handler manager set.");
                    //return;
                }
                SocketListen.Bind(localEP);
                SocketListen.Listen(200);
                SocketListen.BeginAccept(OnAccept, this.SocketListen);
                Log.Status("Server ready, listening on {0}.", this.SocketListen.LocalEndPoint);
            }
            catch (Exception ex) { Log.Exception(ex, "Unable to set up socket; perhaps you're already running a server?"); }
        }
        public void Stop()
        {
            try
            {
                ListLock.EnterReadLock();
                try
                {
                    this.Clients.ForEach(client => this.RemoveClient(client));
                }
                finally { ListLock.ExitReadLock(); }

                SocketListen.Shutdown(SocketShutdown.Both);
                SocketListen.Close();
            }
            catch { }
        }

        private void OnAccept(IAsyncResult result)
        {
            var client = new TClient();
            try
            {
                client.Disconnected += c => { this.RemoveClient(client); };
                client.HandleBuffer += (c, b) => { this.HandleBuffer((TClient)c, b); };
                client.OnReceive((result.AsyncState as Socket).EndAccept(result));

                this.AddClient(client);
                Log.Debug("Connection established from '{0}.", client.Address);
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { Log.Exception(ex, "While accepting connection."); }
            finally { SocketListen.BeginAccept(this.OnAccept, SocketListen); }
        }

        protected void AddClient(TClient client)
        {
            ListLock.EnterWriteLock();
            try
            {
                this.Clients.Add(client);
                this.OnClientConnected(client);
            }
            finally { ListLock.ExitWriteLock(); }

        }
        protected void RemoveClient(TClient client, bool kill = true)
        {
            ListLock.EnterWriteLock();
            try
            {
                this.Clients.Remove(client);
                this.OnClientDisconnected(client);
                if (kill) { client.Disconnect(); }
            }
            finally { ListLock.ExitWriteLock(); }
        }
    }
}
