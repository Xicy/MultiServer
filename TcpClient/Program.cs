using System;
using Shared.Util.Commands;
using System.Collections.Generic;
using Shared.Util;

namespace Client
{
    class Program
    {
        static ConsoleCommands console;
        static List<TestClient> tc;
        static Random rand = new Random();
        static void Main(string[] args)
        {
            //Log.Archive = Environment.CurrentDirectory + @"/Log/";
            //Log.LogFile = Log.Archive + AppDomain.CurrentDomain.FriendlyName.Replace(".exe", ".txt");

            tc = new List<TestClient>();
            Shared.Util.CliUtil.WriteHeader("Client", ConsoleColor.Green);

            console = new ConsoleCommands();
            console.Add("AddClient", "<count>", "Add client for test", HandleAddClient);
            console.Add("CloseAll", "Close all client", HandleCloseAll);
            console.Add("Login", "<username> <password>", "Login Request", HandleLogin);

            console.Wait();
        }

        private static CommandResult HandleLogin(string command, IList<string> args)
        {
            var id = rand.Next(tc.Count);
            tc[id].Login(args[1], args[2]);
            return CommandResult.Okay;
        }
        private static CommandResult HandleAddClient(string command, IList<string> args)
        {
            int f;
            if (args.Count != 2 || !int.TryParse(args[1], out f)) { return CommandResult.InvalidArgument; }
            for (int i = 0; i < f; i++) { tc.Add(new TestClient().Connect<TestClient>("127.0.0.1", 8080)); }
            return CommandResult.Okay;
        }
        private static CommandResult HandleCloseAll(string command, IList<string> args)
        {
            tc.ForEach(client => client.Disconnect());
            tc.Clear();
            return CommandResult.Okay;
        }

    }
}
