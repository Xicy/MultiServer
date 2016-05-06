using Shared.Network;

namespace Server
{
    public class TestServer : BaseServer<TestClient>
    {
        public TestServer()
        {
            this.Handlers = new PacketHandler();
        }

        protected override void HandleBuffer(TestClient client, byte[] buffer)
        {
            this.Handlers.Handle(client, new Packet(buffer, 0));
        }

        protected override void OnClientConnected(TestClient client)
        {
            //client.Crypter(URandom.Long());
            client.Ping();
            base.OnClientConnected(client);
        }

    }
}
