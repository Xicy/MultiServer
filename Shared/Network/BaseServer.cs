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
        private readonly Socket _socketListen;
        private readonly ReaderWriterLockSlim _listLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public List<TClient> Clients { protected set; get; }
        public PacketHandlerManager<TClient> Handlers { set; get; }

        #region Events
        public delegate void ClientConnectionEventHandler(TClient client);

        public event ClientConnectionEventHandler ClientConnected;
        protected virtual void OnClientConnected(TClient client)
        {
            ClientConnected?.Invoke(client);
        }

        public event ClientConnectionEventHandler ClientDisconnected;
        protected virtual void OnClientDisconnected(TClient client)
        {
            ClientDisconnected?.Invoke(client);
        }

        protected abstract void HandleBuffer(TClient client, byte[] buffer);
        #endregion

        protected BaseServer()
        {
            _socketListen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            Clients = new List<TClient>();
        }

        public void Start(int port)
        {
            Start(new IPEndPoint(IPAddress.Any, port));
        }
        public void Start(string host, int port)
        {
            Start(new IPEndPoint(IPAddress.Parse(host), port));
        }
        public void Start(IPEndPoint localEp)
        {
            try
            {
                if (Handlers == null)
                    Log.Error(Localization.Get("Shared.Network.BaseServer.Start.HandlersNull"));

                _socketListen.Bind(localEp);
                _socketListen.Listen(200);
                _socketListen.BeginAccept(OnAccept, _socketListen);
                Log.Status(Localization.Get("Shared.Network.BaseServer.Start.ServerReady"), _socketListen.LocalEndPoint);
            }
            catch (Exception ex) { Log.Exception(ex, Localization.Get("Shared.Network.BaseServer.Start.Exception")); }
        }
        public void Stop()
        {
            using (_listLock.Read())
                Clients.ForEach(client => RemoveClient(client));

            _socketListen.Shutdown(SocketShutdown.Both);
            _socketListen.Close();
            /*
            try
            {
                _listLock.EnterReadLock();
                try
                {
                    Clients.ForEach(client => RemoveClient(client));
                }
                finally { _listLock.ExitReadLock(); }

                _socketListen.Shutdown(SocketShutdown.Both);
                _socketListen.Close();
            }
            catch
            {
                // ignored
            }
            */
        }

        private void OnAccept(IAsyncResult result)
        {
            var client = new TClient();
            try
            {
                client.Disconnected += c => { RemoveClient(client); };
                client.HandleBuffer += (c, b) => { HandleBuffer(client, b); };
                client.OnReceive(((Socket)result.AsyncState).EndAccept(result));

                AddClient(client);
                Log.Debug(Localization.Get("Shared.Network.BaseServer.OnAccept.ConnectionEstablished"), client.Address);
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { Log.Exception(ex, Localization.Get("Shared.Network.BaseServer.OnAccept.Exception")); }
            finally { _socketListen.BeginAccept(OnAccept, _socketListen); }
        }

        protected void AddClient(TClient client)
        {
            using (_listLock.Write())
            {
                Clients.Add(client);
                OnClientConnected(client);
            }
        }
        protected void RemoveClient(TClient client, bool kill = true)
        {
            using (_listLock.Write())
            {
                Clients.Remove(client);
                OnClientDisconnected(client);
                if (kill && client.State != ClientState.Disconnected) client.Disconnect();
            }
        }
    }
}
