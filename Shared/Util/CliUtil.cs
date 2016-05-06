using System;
using System.Security.Principal;

namespace Shared.Util
{
    public class CliUtil
    {
        /// <summary>
        /// Prints Aura's ASCII art.
        /// </summary>
        /// <param name="title">Name of this server (for the console's title)</param>
        /// <param name="color">Color of the header</param>
        private static string _title;
        private static ConsoleColor _color = ConsoleColor.DarkGray;
        public static void WriteHeader(string title, ConsoleColor color)
        {
            _title = title;
            _color = color;
            if (title != null) { Console.Title = title; }
            Console.ForegroundColor = color;
            Console.Write(Localization.Get("shared.util.cliutil.writeheader.header"));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("");
            Console.Write(@"________________________________________________________________________________");
            Console.WriteLine("");
        }
        public static void WriteHeader()
        {
            WriteHeader(_title, _color);
        }
        /// <summary>
        /// Prefixes window title with an asterisk.
        /// </summary>
        public static void LoadingTitle()
        {
            if (!Console.Title.StartsWith("* "))
                Console.Title = "* " + Console.Title;
        }

        /// <summary>
        /// Removes asterisks and spaces that were prepended to the window title.
        /// </summary>
        public static void RunningTitle()
        {
            Console.Title = Console.Title.TrimStart('*', ' ');
        }

        /// <summary>
        /// Waits for the return key, and closes the application afterwards.
        /// </summary>
        /// <param name="exitCode"></param>
        /// <param name="wait"></param>
        public static void Exit(int exitCode, bool wait = true)
        {
            if (wait)
            {
                Log.Info(Localization.Get("shared.util.cliutil.exit.pressenter"));
                Console.ReadLine();
            }
            Log.Info(Localization.Get("shared.util.cliutil.exit.exiting"));
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Returns whether the application runs with admin rights or not.
        /// </summary>
        public static bool CheckAdmin()
        {
            var id = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(id);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
