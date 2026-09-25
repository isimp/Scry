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
                "[text] - opens the prefab previewer, searching for the text if given. 'scry clear' removes every preview from the world.",
                args =>
                {
                    var rest = args.Length > 1 ? (args.ArgsAll ?? "").Trim() : "";

                    if (rest.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Previews.ClearWorld();
                        args.Context?.AddString("Scry: previews cleared.");
                        return;
                    }

                    if (rest.Length > 0) Session.Show(rest);
                    else Session.Toggle();
                });
        }
    }
}
