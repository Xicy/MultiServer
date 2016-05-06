using System;
using Shared.Util;
using Shared.Util.Commands;
using System.Collections.Generic;

namespace Server
{
    class Program
    {
        public static TestServer ts;
        static void Main(string[] args)
        {
            Log.Archive = Environment.CurrentDirectory + @"/Log/";
            Log.LogFile = Log.Archive + AppDomain.CurrentDomain.FriendlyName.Replace(".exe", ".txt");

            Localization.Parse(Environment.CurrentDirectory, "*.lang", System.IO.SearchOption.AllDirectories);
            CliUtil.WriteHeader(Localization.Get("Server.Program.Main.Title"), ConsoleColor.Red);

            CliUtil.LoadingTitle();

            var console = new ConsoleCommands();
            console.Add("status", "<GCollet:Bool>", Localization.Get("Server.Program.Main.ConsoleCommands.Description.Status"), HandleStatus);
            console.Add("ChangeCrypter", "<port>", Localization.Get("Server.Program.Main.ConsoleCommands.Description.ChangeCrypter"), HandleChangeCrypter);
            console.Add("Ping", "<port>", Localization.Get("Server.Program.Main.ConsoleCommands.Description.Ping"), HandlePing);

            ts = new TestServer();
            ts.Start(8080);

            CliUtil.RunningTitle();
            console.Wait();
        }

        private static CommandResult HandleStatus(string command, IList<string> args)
        {
            bool gc = false;
            if (args.Count > 1) { gc = bool.Parse(args[1]); GC.RemoveMemoryPressure(1024 * 1024 * 100); }
            Log.Status(Localization.Get("Server.Program.HandleStatus.Status"), ts.Clients.Count, GC.GetTotalMemory(gc) / 1048576f);
            return CommandResult.Okay;
        }
        private static CommandResult HandleChangeCrypter(string command, IList<string> args)
        {
            /*TestClient tc = ts.Clients.Find(c => ((System.Net.IPEndPoint)c.Socket.RemoteEndPoint).Port == int.Parse(args[1]));
            tc.Crypter(tc.ID);*/
            return CommandResult.Okay;
        }
        private static CommandResult HandlePing(string command, IList<string> args)
        {
            /*TestClient tc = ts.Clients.Find(c => ((System.Net.IPEndPoint)c.Socket.RemoteEndPoint).Port == int.Parse(args[1]));
            tc.Ping();*/
            return CommandResult.Okay;
        }

    }
}
