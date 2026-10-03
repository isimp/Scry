namespace Scry
{
    /// <summary>
    /// One line of an entry's facts as the model words it: a label, its value, what the value goes
    /// to (a prefab, or a key such as "se:" and a status effect's name) and, where Scry is not
    /// sure of it, why (<see cref="UnsureWords"/>). Each kind's words (<c>ProjectileWords</c> and
    /// the like) give its lines in the order they are read; the fact readers add them as given.
    /// </summary>
    internal readonly struct FactPair
    {
        public FactPair(string label, string value, string link = null, string unsure = null)
        {
            Label = label;
            Value = value;
            Link = link;
            Unsure = unsure;
        }

        public string Label { get; }
        public string Value { get; }
        public string Link { get; }
        public string Unsure { get; }

        public override string ToString() => Label + ": " + Value;
    }
}
