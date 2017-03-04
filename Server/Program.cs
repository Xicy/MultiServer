using System;
using Shared.Util;
using Shared.Util.Commands;
using System.Collections.Generic;

namespace Server
{
    class Program
    {
        private static GameServer Server;
        public static void Main(string[] args)
        {
            //Log.Hide = (LogLevel)byte.MaxValue;//HACK:Hide for all logs
            CliUtil.LoadingTitle();
            CliUtil.WriteHeader(Localization.Get("Server.Program.Main.Title"), ConsoleColor.Red);

            var console = new ConsoleCommands();
            console.Add("status", "<GCollet:Bool>", Localization.Get("Server.Program.Main.ConsoleCommands.Description.Status"), HandleStatus);
            console.Add("stop", "Stop the server", HandleStop);
            console.Add("start", "Start the server", HandleStart);
            Server = new GameServer();
            Server.Start(8080);

            CliUtil.RunningTitle();
            console.Wait();
        }

        private static CommandResult HandleStart(string command, IList<string> args)
        {
            Server.Start(8080);
            return CommandResult.Okay;
        }

        private static CommandResult HandleStop(string command, IList<string> args)
        {
            Server.Stop();
            return CommandResult.Okay;
        }

        private static CommandResult HandleStatus(string command, IList<string> args)
        {
            bool gc = false;
            if (args.Count > 1) { gc = bool.Parse(args[1]); GC.RemoveMemoryPressure(1024 * 1024 * 100); }
            Log.Status(Localization.Get("Server.Program.HandleStatus.Status"), Server.Clients.Count, GC.GetTotalMemory(gc) / 1048576f);
            return CommandResult.Okay;
        }

    }
}
