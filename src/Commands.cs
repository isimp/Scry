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
            // The game keeps a command as it is made.
            _ = new Terminal.ConsoleCommand("scry",
                "[text] - opens Scry, the compendium and lookup, searching for the text if given. 'scry clear' removes every preview from the world; 'scry locations' reads where things are found in locations and dungeons, 'scry locations stop' stops it; 'scry forget' forgets what Scry kept of the locations and their floors and reads them afresh; 'scry forget maps' forgets this character's maps of the dungeons of this world; 'scry forget chests' forgets what this character counted chests holding, so they count afresh; 'scry forget clips' forgets what Scry saw each animator play and works it out again; 'scry forget server maps' forgets the server's records of them, for an admin; 'scry read traces' has the server read again what was left inside the world's dungeons, for an admin; 'scry map' looks at the explored map again for the compendium, as Look at the map again does; 'scry mods' shows what each mod adds and what Scry links for it; 'scry monitor' shows or hides the resource monitor.",
                args =>
                {
                    // A command that fails says so in the chat, and once in the log, rather than
                    // leaving the game's console to show the error.
                    if (!Guard.Run(Feature.Command, "the /scry command", Run, args)) args.Context.OrNull()?.AddString("Scry: that did not work; the log says why.");
                });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var rest = args.Length > 1 ? (args.ArgsAll ?? "").Trim() : "";

            if (rest.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
            {
                Previews.ClearWorld();
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(ConsoleWords.Cleared));
                return;
            }

            if (rest.Equals("locations", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(Locations.Start()));
                return;
            }

            if (rest.Equals("locations stop", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(Locations.Stop()));
                return;
            }

            if (rest.Equals("map", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(CompendiumMap.Again(CompendiumKeeper.Book)));
                return;
            }

            if (ConsoleWords.ForgetsServerMaps(rest))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(GroupMapsLink.ForgetWorld()));
                return;
            }

            if (rest.Equals("read traces", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(GroupMapsLink.ReadTraces()));
                return;
            }

            if (rest.Equals("forget chests", System.StringComparison.OrdinalIgnoreCase))
            {
                var book = CompendiumKeeper.Book;
                var kinds = book?.ForgetChests();
                if (book != null) CompendiumKeeper.Save(now: true);
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(ConsoleWords.ChestsForgotten(kinds)));
                return;
            }

            if (rest.Equals("forget clips", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(ClipWords.Forgotten(Previews.ForgetClipsSeen())));
                return;
            }

            if (rest.Equals("forget maps", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(DungeonMapWords.WorldForgotten(DungeonWalks.ForgetWorld())));
                return;
            }

            if (rest.Equals("forget", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(Locations.ForgetKept()));
                return;
            }

            // The self-test, where it is installed, answers the words that are its own.
            var answer = SelfTestHost.Answer(rest);
            if (answer != null)
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(answer));
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
                Settings.ShowMonitor = !Settings.ShowMonitor;
                args.Context.OrNull()?.AddString(ConsoleWords.Monitor(Settings.ShowMonitor));
                return;
            }

            if (rest.Equals("dump", System.StringComparison.OrdinalIgnoreCase))
            {
                args.Context.OrNull()?.AddString(ConsoleWords.Reply(Stage.Dump()));
                return;
            }

            if (rest.Length > 0) Session.Show(rest);
            else Session.Toggle();
        }
    }
}
