namespace Scry
{
    /// <summary>
    /// The components whose topic hinges on a thing (<see cref="Entry.DeedHinges"/>): the saddle
    /// for riding, breeding for the young, growing up for the grown form. While that thing was not
    /// met, the page leaves the topic out whole, and names the component nowhere either, in its
    /// details or in a search for what a thing has.
    /// </summary>
    internal static class HingedParts
    {
        /// <summary>The deed a component's topic is, where the topic hinges on a thing; none for any other component.</summary>
        public static Deed? DeedOf(string component)
        {
            switch (component)
            {
                case "Sadle": return Deed.Ridden;
                case "Procreation": return Deed.Born;
                case "Growup": return Deed.GrewUp;
                default: return null;
            }
        }

        /// <summary>The thing a component's topic hinges on for an entry; none where it hinges on nothing.</summary>
        public static string HingeOf(Entry entry, string component) =>
            entry != null && DeedOf(component) is Deed deed && entry.DeedHinges.TryGetValue(deed, out var hinge) ? hinge : null;
    }
}
