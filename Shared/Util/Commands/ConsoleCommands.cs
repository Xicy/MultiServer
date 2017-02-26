using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Util.Commands
{
    public class ConsoleCommands : CommandManager<ConsoleCommand, ConsoleCommandFunc>
    {
        public ConsoleCommands()
        {
            Commands = new Dictionary<string, ConsoleCommand>();

            Add(Localization.Get("Shared.Util.Commands.ConsoleCommands.Help"), Localization.Get("Shared.Util.Commands.ConsoleCommands.ConsoleCommands.Usage.Help"), Localization.Get("Shared.Util.Commands.ConsoleCommands.ConsoleCommands.Description.Help"), HandleHelp);
            Add(Localization.Get("Shared.Util.Commands.ConsoleCommands.Cls"), Localization.Get("Shared.Util.Commands.ConsoleCommands.ConsoleCommands.Description.CLS"), HandleCleanScreen);
            Add(Localization.Get("Shared.Util.Commands.ConsoleCommands.Exit"), Localization.Get("Shared.Util.Commands.ConsoleCommands.ConsoleCommands.Description.Exit"), HandleExit);
            Add(Localization.Get("Shared.Util.Commands.ConsoleCommands.Status"), Localization.Get("Shared.Util.Commands.ConsoleCommands.ConsoleCommands.Description.Status"), HandleStatus);
            Add(Localization.Get("Shared.Util.Commands.ConsoleCommands.Debug"), Localization.Get("Shared.Util.Commands.ConsoleCommands.ConsoleCommands.Description.Debug"), HandleDebug);
        }

        public void Add(string name, string description, ConsoleCommandFunc handler)
        {
            Add(name, "", description, handler);
        }

        public void Add(string name, string usage, string description, ConsoleCommandFunc handler)
        {
            Commands[name] = new ConsoleCommand(name, usage, description, handler);
        }

        public void Wait()
        {
            Log.Info(Localization.Get("Shared.Util.Commands.ConsoleCommands.Wait.Help"));

            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrEmpty(line)) continue;
                
                var args = ParseLine(line);
                if (args.Count == 0) continue;

                var command = GetCommand(args[0]);
                if (command == null)
                {
                    Log.Info(Localization.Get("Shared.Util.Commands.ConsoleCommands.Wait.Info.Unknown"), args[0]);
                    continue;
                }

                var result = command.Func(line, args);
                if (result == CommandResult.Break) break;

                if (result == CommandResult.Fail)
                    Log.Error(Localization.Get("Shared.Util.Commands.ConsoleCommands.Wait.Error.Fail"), command.Name);
                else if (result == CommandResult.InvalidArgument)
                    Log.Info(Localization.Get("Shared.Util.Commands.ConsoleCommands.Wait.Info.Usage"), command.Name, command.Usage);
            }
        }

        protected CommandResult HandleDebug(string command, IList<string> args)
        {
            Log.Hide ^= LogLevel.Debug;
            Log.Info(Localization.Get("Shared.Util.Commands.ConsoleCommands.HandleDebug.Info"), ((Log.Hide & LogLevel.Debug) != 0 ? Localization.Get("False") : Localization.Get("True")));
            return CommandResult.Okay;
        }
        protected CommandResult HandleCleanScreen(string command, IList<string> args)
        {
            Console.Clear();
            CliUtil.WriteHeader();
            return CommandResult.Okay;
        }
        protected CommandResult HandleHelp(string command, IList<string> args)
        {
            if (args.Count == 1)
            {
                var maxLength = Commands.Values.Max(a => a.Name.Length);

                Log.Info(Localization.Get("Shared.Util.Commands.ConsoleCommands.HandleHelp.Info.Available"));
                foreach (var cmd in Commands.Values.OrderBy(a => a.Name))
                    Log.Info("  {0,-" + (maxLength + 2) + "}{1}", cmd.Name, cmd.Description);
            }
            else
            {
                var consoleCommand = GetCommand(args[1]);
                if (consoleCommand == null)
                {
                    Log.Info(Localization.Get("Shared.Util.Commands.ConsoleCommands.HandleHelp.Info.Unknown"), args[1]);
                    return CommandResult.Fail;
                }
                Log.Info(Localization.Get("Shared.Util.Commands.ConsoleCommands.HandleHelp.Info.Code"), consoleCommand.Name, string.IsNullOrWhiteSpace(consoleCommand.Usage) ? Localization.Get("Shared.Util.Commands.ConsoleCommands.HandleHelp.Info.Null") : consoleCommand.Usage, consoleCommand.Description);
            }
            return CommandResult.Okay;
        }
        protected CommandResult HandleStatus(string command, IList<string> args)
        {
            Log.Status(Localization.Get("Shared.Util.Commands.ConsoleCommands.HandleStatus.Status"), Math.Round(GC.GetTotalMemory(false) / 1024f));
            return CommandResult.Okay;
        }
        protected CommandResult HandleExit(string command, IList<string> args)
        {
            CliUtil.Exit(0, false);
            return CommandResult.Okay;
        }
    }

    public class ConsoleCommand : Command<ConsoleCommandFunc>
    {
        public ConsoleCommand(string name, string usage, string description, ConsoleCommandFunc func) : base(name, usage, description, func) { }
    }

    public delegate CommandResult ConsoleCommandFunc(string command, IList<string> args);
}
