using System;
using System.Net;
using Shared.Util;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Shared.Network
{
    public abstract class BaseServer<TClient> where TClient : BaseClient<TClient>, new()
    {
        #region Fields
        private bool _status;
        private Socket _socketListen;
        private string _address;
        public string Address { get { if (_address != null) return _address; try { _address = _socketListen.LocalEndPoint.ToString(); } catch { _address = Localization.Get("Shared.Network.BaseServer.Address.Null"); } return _address; } }
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        public List<TClient> Clients { get; }
        #endregion

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
            if (_status)
            {
                Log.Warning(Localization.Get("Shared.Network.BaseServer.Start.AlreadyConnected"));
            }
            else try
                {

                    _socketListen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    _socketListen.Bind(localEp);
                    _socketListen.Listen(200);
                    _address = _socketListen.LocalEndPoint.ToString();
                    _socketListen.BeginAccept(OnAccept, _socketListen);
                    _status = true;
                }
                catch (Exception ex) { Log.Exception(ex, Localization.Get("Shared.Network.BaseServer.Start.Exception")); }
                finally { if (_status) Log.Status(Localization.Get("Shared.Network.BaseServer.Start.ServerReady"), Address); }
        }
        public void Stop()
        {
            _status = false;
            _socketListen.Close();
            using (_rwLock.Write())
                Clients.ToList().ForEach(client => RemoveClient(client));
            Log.Status(Localization.Get("Shared.Network.BaseServer.Start.ServerStop"), Address);
        }

        private void OnAccept(IAsyncResult result)
        {
            var client = new TClient();
            client.Disconnected += c => { RemoveClient(client); };
            client.HandleBuffer += (c, b) => { HandleBuffer(client, b); };
            try { client.OnReceive(((Socket)result.AsyncState).EndAccept(result)); }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { Log.Exception(ex, Localization.Get("Shared.Network.BaseServer.OnAccept.Exception")); }
            finally
            {
                if (_status)
                {
                    AddClient(client);
                    Log.Debug(Localization.Get("Shared.Network.BaseServer.OnAccept.ConnectionEstablished"), client.Address);
                    _socketListen.BeginAccept(OnAccept, _socketListen);
                }
            }
        }

        protected void AddClient(TClient client)
        {
            using (_rwLock.Write())
            {
                Clients.Add(client);
                OnClientConnected(client);
            }
        }
        protected void RemoveClient(TClient client, bool kill = true)
        {
            using (_rwLock.Write())
            {
                Clients.Remove(client);
                OnClientDisconnected(client);
                if (kill && client.State != ClientState.Disconnected) client.Disconnect();
            }
        }
    }
}
