using System;
using Shared.Network;
using Shared.Schema;
using Shared.Util;

namespace Client
{
    class Program
    {

        private static GameClient _client;
        public static void Main(string[] args)
        {
            CliUtil.LoadingTitle();

            CliUtil.WriteHeader(Localization.Get("Client.Program.Main.Title"), ConsoleColor.Green);

            _client = new GameClient().Connect<GameClient>("127.0.0.1", 8080);
            Console.CursorVisible = false;

            CliUtil.RunningTitle();

            while (true)
            {
                var keyInfo = Console.ReadKey();
                switch (keyInfo.Key)
                {
                    case ConsoleKey.Q: _client.Disconnect(); break;
                    case ConsoleKey.W: _client.Connect<GameClient>("127.0.0.1", 8080); break;
                    case ConsoleKey.LeftArrow: _client.Move(Directions.Left); break;
                    case ConsoleKey.RightArrow: _client.Move(Directions.Right); break;
                    case ConsoleKey.UpArrow: _client.Move(Directions.Up); break;
                    case ConsoleKey.DownArrow: _client.Move(Directions.Down); break;
                }
            }

        }

    }
}
