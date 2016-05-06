using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Util.Commands
{
    public class ConsoleCommands : CommandManager<ConsoleCommand, ConsoleCommandFunc>
    {
        public ConsoleCommands()
        {
            _commands = new Dictionary<string, ConsoleCommand>();

            this.Add("help", "<CommandName>", Localization.Get("shared.util.commands.consolecommands.consolecommands.description.help"), HandleHelp);
            this.Add("cls", Localization.Get("shared.util.commands.consolecommands.consolecommands.description.cls"), HandleCleanScreen);
            this.Add("exit", Localization.Get("shared.util.commands.consolecommands.consolecommands.description.exit"), HandleExit);
            this.Add("status", Localization.Get("shared.util.commands.consolecommands.consolecommands.description.status"), HandleStatus);
            this.Add("debug", Localization.Get("shared.util.commands.consolecommands.consolecommands.description.debug"), HandleDebug);
        }

        public void Add(string name, string description, ConsoleCommandFunc handler)
        {
            this.Add(name, "", description, handler);
        }

        public void Add(string name, string usage, string description, ConsoleCommandFunc handler)
        {
            _commands[name] = new ConsoleCommand(name, usage, description, handler);
        }

        public void Wait()
        {
            Log.Info(Localization.Get("shared.util.commands.consolecommands.wait.help"));

            while (true)
            {
                var line = Console.ReadLine();

                var args = this.ParseLine(line);
                if (args.Count == 0)
                    continue;

                var command = this.GetCommand(args[0]);
                if (command == null)
                {
                    Log.Info(Localization.Get("shared.util.commands.consolecommands.wait.info.unknown"), args[0]);
                    continue;
                }

                var result = command.Func(line, args);
                if (result == CommandResult.Break)
                {
                    break;
                }
                else if (result == CommandResult.Fail)
                {
                    Log.Error(Localization.Get("shared.util.commands.consolecommands.wait.error.fail"), command.Name);
                }
                else if (result == CommandResult.InvalidArgument)
                {
                    Log.Info(Localization.Get("shared.util.commands.consolecommands.wait.info.usage"), command.Name, command.Usage);
                }
            }
        }

        protected virtual CommandResult HandleDebug(string command, IList<string> args)
        {
            Log.Hide ^= LogLevel.Debug;
            Log.Info(Localization.Get("shared.util.commands.consolecommands.handledebug.info"), ((Log.Hide & LogLevel.Debug) != 0 ? Localization.Get("False") : Localization.Get("True")));
            return CommandResult.Okay;
        }
        protected virtual CommandResult HandleCleanScreen(string command, IList<string> args)
        {
            Console.Clear();
            CliUtil.WriteHeader();
            return CommandResult.Okay;
        }
        protected virtual CommandResult HandleHelp(string command, IList<string> args)
        {
            if (args.Count == 1)
            {
                var maxLength = _commands.Values.Max(a => a.Name.Length);

                Log.Info(Localization.Get("shared.util.commands.consolecommands.handlehelp.info.available"));
                foreach (var cmd in _commands.Values.OrderBy(a => a.Name))
                    Log.Info("  {0,-" + (maxLength + 2) + "}{1}", cmd.Name, cmd.Description);
            }
            else
            {
                var consoleCommand = this.GetCommand(args[1]);
                if (consoleCommand == null)
                {
                    Log.Info(Localization.Get("shared.util.commands.consolecommands.handlehelp.info.unknown"), args[1]);
                    return CommandResult.Fail;
                }
                Log.Info(Localization.Get("shared.util.commands.consolecommands.handlehelp.info.code"), consoleCommand.Name, string.IsNullOrWhiteSpace(consoleCommand.Usage) ? "<NULL>" : consoleCommand.Usage, consoleCommand.Description);
            }
            return CommandResult.Okay;
        }
        protected virtual CommandResult HandleStatus(string command, IList<string> args)
        {
            Log.Status(Localization.Get("shared.util.commands.consolecommands.handlestatus.status"), Math.Round(GC.GetTotalMemory(false) / 1024f));

            return CommandResult.Okay;
        }
        protected virtual CommandResult HandleExit(string command, IList<string> args)
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
