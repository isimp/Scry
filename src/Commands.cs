namespace Scry
{
    /// <summary>
    /// <c>/scry</c> in the chat or <c>scry</c> in the console. Registered as a normal command, so
    /// the chat accepts it without cheats.
    /// </summary>
    internal static class Commands
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("scry",
                "[text] - opens the prefab previewer, searching for the text if given. 'scry clear' removes every preview from the world; 'scry locations' reads where things are found in locations and dungeons, 'scry locations stop' stops it; 'scry mods' shows what each mod adds and what Scry links for it; 'scry monitor' shows or hides the resource monitor.",
                args =>
                {
                    // A command that fails says so in the chat, and once in the log, rather than
                    // leaving the game's console to show the error.
                    try
                    {
                        Run(args);
                    }
                    catch (System.Exception ex)
                    {
                        Faults.Tell("the /scry command", ex);
                        args.Context?.AddString("Scry: that did not work; the log says why.");
                    }
                });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var rest = args.Length > 1 ? (args.ArgsAll ?? "").Trim() : "";

            if (rest.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
            {
                Previews.ClearWorld();
                args.Context?.AddString("Scry: previews cleared.");
                return;
            }

            if (rest.Equals("locations", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context?.AddString("Scry: " + Locations.Start());
                return;
            }

            if (rest.Equals("locations stop", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context?.AddString("Scry: " + Locations.Stop());
                return;
            }

            if (rest.Equals("selftest", System.StringComparison.OrdinalIgnoreCase))
            {
                // A tool for finding faults, off unless its setting is on; stopping one always works.
                args.Context?.AddString("Scry: " + (Plugin.SelfTestAllowed
                    ? SelfTestHost.Start("it was asked for with /scry selftest")
                    : "the self-test is off; SelfTest under Diagnostics in Scry's settings turns it on."));
                return;
            }

            if (rest.Equals("selftest stop", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context?.AddString("Scry: " + SelfTestHost.Stop());
                return;
            }

            if (rest.Equals("mods", System.StringComparison.OrdinalIgnoreCase))
            {
                Session.Show(null);
                ScryPanel.ShowModReport();
                return;
            }

            if (rest.Equals("monitor", System.StringComparison.OrdinalIgnoreCase))
            {
                Plugin.ShowMonitor = !Plugin.ShowMonitor;
                args.Context?.AddString("Scry: the resource monitor is " + (Plugin.ShowMonitor ? "on." : "off."));
                return;
            }

            if (rest.Equals("dump", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context?.AddString("Scry: " + Stage.Dump());
                return;
            }

            if (rest.Length > 0) Session.Show(rest);
            else Session.Toggle();
        }
    }
}
