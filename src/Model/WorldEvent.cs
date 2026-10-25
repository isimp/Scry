using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What happened in an event of the world (<see cref="WorldEvent"/>).</summary>
    internal enum Happened
    {
        /// <summary>A creature died, its loot falling.</summary>
        Died,

        /// <summary>A tree, log, rock or other thing broke, or lost a piece, giving what it gives.</summary>
        Broke,

        /// <summary>A pickable was picked, giving what it gives.</summary>
        Picked,

        /// <summary>A tamed creature gave birth.</summary>
        Born,

        /// <summary>A young creature grew up.</summary>
        GrewUp,

        /// <summary>A picked pickable grew back.</summary>
        GrewBack,

        /// <summary>A crop grew.</summary>
        Grew,

        /// <summary>A spawner spawned.</summary>
        Spawned,

        /// <summary>A station handed out what it made.</summary>
        Finished,

        /// <summary>A tame creature ate.</summary>
        Ate,

        /// <summary>An egg hatched.</summary>
        Hatched,
    }

    /// <summary>
    /// Something that happened to a thing in the world, as the game that ran it knows it. The game
    /// runs each thing in one game only, the one of the player who came near it first: that game
    /// alone makes a death and its loot, a break, a pick's yield, a birth, a growing up, a spawn, a
    /// station's output and a creature eating happen, and knows all of it, who did it too. An
    /// event goes by the game that ran it and its number there, so one told twice counts once.
    /// </summary>
    internal sealed class WorldEvent
    {
        private static readonly (string Item, int Amount)[] Nothing = Array.Empty<(string, int)>();
        private static readonly long[] NoOne = Array.Empty<long>();

        public WorldEvent(long game, int number, Happened what, string thing, float x, float y, float z,
            IReadOnlyList<(string Item, int Amount)> came = null, int level = 1, IReadOnlyList<long> doers = null, string with = null, string mark = null)
        {
            Game = game;
            Number = number;
            What = what;
            Thing = thing;
            X = x;
            Y = y;
            Z = z;
            Came = came ?? Nothing;
            Level = level;
            Doers = doers ?? NoOne;
            With = with;
            Mark = mark;
        }

        /// <summary>The game that ran it, by its session in the world.</summary>
        public long Game { get; }

        /// <summary>Its number among the events that game ran.</summary>
        public int Number { get; }

        public Happened What { get; }

        /// <summary>The thing it happened to, by its prefab.</summary>
        public string Thing { get; }

        /// <summary>Where it happened.</summary>
        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        /// <summary>What came of it, by prefab and amount: a death's loot, what a break or a pick gave.</summary>
        public IReadOnlyList<(string Item, int Amount)> Came { get; }

        /// <summary>The level of the creature that died, a level above 1 for each star.</summary>
        public int Level { get; }

        /// <summary>The players who did it, by their game's session: those who hit a creature that died, the one whose hit broke a thing or who picked it; none for what no one does.</summary>
        public IReadOnlyList<long> Doers { get; }

        /// <summary>What a creature ate, by prefab.</summary>
        public string With { get; }

        /// <summary>
        /// The object the event came about through, where another game may tell the same event
        /// from its side: the remains a death left (the ragdoll's id), the thing picked; none for
        /// an event only its own game tells. Two events of one mark are one.
        /// </summary>
        public string Mark { get; }
    }

    /// <summary>How an event concerns the character (<see cref="EventRule"/>).</summary>
    internal enum EventShare
    {
        /// <summary>Not at all: neither done nor seen.</summary>
        None,

        /// <summary>Seen happen.</summary>
        Saw,

        /// <summary>Done by the character.</summary>
        Did,
    }

    /// <summary>The events told to the character's game in this world, by their game and number, so one told again counts once.</summary>
    internal sealed class EventLedger
    {
        private readonly HashSet<(long Game, int Number)> _told = new HashSet<(long, int)>();

        /// <summary>The marks of the events recorded (<see cref="WorldEvent.Mark"/>), for the same event told from another side, each with when it was.</summary>
        private readonly Dictionary<string, double> _marked = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// How long a mark stands, in seconds: longer than the two tellings of one event lie apart
        /// (a ragdoll's loot comes out within its time to live), shorter than a thing takes to
        /// regrow, as a pickable keeps its id and is picked again.
        /// </summary>
        public const double MarkLife = 120.0;

        /// <summary>True the first time an event is told, false every time after.</summary>
        public bool First(WorldEvent told) => _told.Add((told.Game, told.Number));

        /// <summary>Whether an event of this mark was recorded within a mark's life, from whichever side.</summary>
        public bool Marked(WorldEvent told, double now) => told.Mark != null && _marked.TryGetValue(told.Mark, out var at) && now - at <= MarkLife;

        /// <summary>An event recorded, its mark kept from now.</summary>
        public void Recorded(WorldEvent told, double now)
        {
            if (told.Mark != null) _marked[told.Mark] = now;
        }

        /// <summary>On leaving a world.</summary>
        public void Forget()
        {
            _told.Clear();
            _marked.Clear();
        }
    }

    /// <summary>Which deeds a kind of entry's page keeps numbers back for (<see cref="Entry.Deeds"/>).</summary>
    internal static class StrictDeeds
    {
        /// <summary>Whether a deed noted of a prefab's components fits its kind: health until one is killed only for a creature (a trader is never killed), until one is broken only for a resource.</summary>
        public static bool Fits(Deed deed, Kind kind)
        {
            switch (deed)
            {
                case Deed.Killed: return kind == Kind.Creature;
                case Deed.Broke: return kind == Kind.Resource;
                default: return true;
            }
        }
    }

    /// <summary>
    /// The one rule every event is recorded by, for the character: it did it where the event names
    /// its game among those who did it, however far away; else it saw it where it stands within
    /// <see cref="Sighting.PersonReach"/> of it as it is told; else it is none of the character's.
    /// The same event told from two sides, by the game that ran it and by this one seeing what it
    /// left (<see cref="WorldEvent.Mark"/>), is recorded and counted once, with the pairs of both.
    /// </summary>
    internal static class EventRule
    {
        /// <summary>
        /// An event told to the character's game, the character standing at a point: the first time,
        /// recorded in its book where it did it or saw it, and counted in the play counts given;
        /// how it concerns the character, none when told before.
        /// </summary>
        public static EventShare Tell(WorldEvent told, long me, float x, float y, float z, EventLedger ledger, CompendiumBook book, PlayCounts counts,
            Func<float, float, float, bool> inSight = null, double now = 0.0)
        {
            if (told == null || book == null || !ledger.First(told)) return EventShare.None;
            var share = Of(told, me, x, y, z);
            // Seen only with a line from the eye to it, as a place or a thing is (Sighting.DeedInSight).
            var seen = inSight == null || inSight(told.X, told.Y, told.Z);
            if (share == EventShare.Saw && !seen) share = EventShare.None;
            // Up close, where the game marks a creature's stars, and in sight: a death there is a
            // sighting of them, as a look at the creature is; a kill credited from behind a wall is not.
            float ax = told.X - x, ay = told.Y - y, az = told.Z - z;
            var reach = book.World.SeenReach;
            var upClose = seen && reach > 0f && ax * ax + ay * ay + az * az <= reach * reach;
            if (share == EventShare.None) return share;
            if (ledger.Marked(told, now)) Paired(told, book);
            else
            {
                Record(told, book, counts, upClose);
                ledger.Recorded(told, now);
            }
            return share;
        }

        /// <summary>An event recorded already from its other side: only what it names seen together is added, nothing counted again.</summary>
        private static void Paired(WorldEvent told, CompendiumBook book)
        {
            switch (told.What)
            {
                case Happened.Died:
                    foreach (var (item, _) in told.Came) book.SawWith(told.Thing, item, Tie.Drops);
                    return;
                case Happened.Broke:
                case Happened.Picked:
                    foreach (var (item, _) in told.Came) book.SawWith(told.Thing, item, Tie.Gives);
                    return;
                default:
                    return;
            }
        }

        /// <summary>How an event concerns the character standing at a point.</summary>
        private static EventShare Of(WorldEvent told, long me, float x, float y, float z)
        {
            foreach (var doer in told.Doers)
            {
                if (doer == me) return EventShare.Did;
            }
            float dx = told.X - x, dy = told.Y - y, dz = told.Z - z;
            return dx * dx + dy * dy + dz * dz <= Sighting.PersonReach * Sighting.PersonReach ? EventShare.Saw : EventShare.None;
        }

        /// <summary>
        /// An event recorded in the book: a death's loot counted as one kill, each item seen to fall
        /// from it, a starred one showing what its stars change; what a break or a pick gave counted,
        /// each seen to come from it, a break showing its health, none of it where it gave nothing;
        /// what a creature ate paired with it; a birth, a growing up and the rest each showing the
        /// number it tells (<see cref="Deed"/>); the young a birth or a hatching brought, or the one
        /// a young one grew into, known, as a creature seen tamed is, and seen together with what
        /// it came of (<see cref="YoungKnown"/>).
        /// </summary>
        private static void Record(WorldEvent told, CompendiumBook book, PlayCounts counts, bool upClose = false)
        {
            switch (told.What)
            {
                case Happened.Died:
                    foreach (var (item, _) in told.Came) book.SawWith(told.Thing, item, Tie.Drops);
                    book.Dropped(told.Thing, told.Came, told.Level);
                    counts?.Note(PlayEvent.LootCounted);
                    // A death up close is a sighting of the creature's stars, none included, as a look at it is.
                    if (upClose) book.Sighted(told.Thing, told.Level - 1, null);
                    if (told.Level <= 1) return;
                    // A starred fall shows what stars change at all, and what that many stars change,
                    // but only where those stars were seen up close, as the game marks them: before,
                    // or at the death itself.
                    if (!book.StarsSeen(told.Thing).Contains(told.Level - 1)) return;
                    book.FoundOut(told.Thing, EntryKeys.Did(Deed.StarredFell));
                    book.FoundOut(told.Thing, EntryKeys.StarsFell(told.Level - 1));
                    counts?.Note(PlayEvent.StarredFell);
                    return;
                case Happened.Broke:
                case Happened.Picked:
                    if (told.Came.Count == 0) return;
                    book.Yielded(told.Thing, told.Came);
                    foreach (var (item, _) in told.Came) book.SawWith(told.Thing, item, Tie.Gives);
                    if (told.What == Happened.Picked)
                    {
                        counts?.Note(PlayEvent.PickCounted);
                        return;
                    }
                    book.FoundOut(told.Thing, EntryKeys.Did(Deed.Broke));
                    counts?.Note(PlayEvent.BreakCounted);
                    return;
                case Happened.Ate:
                    book.SawWith(told.Thing, told.With, Tie.Eats);
                    return;
                default:
                    book.FoundOut(told.Thing, EntryKeys.Did(DeedOf(told.What)));
                    YoungKnown(told, book);
                    var noted = NotedAs(told.What);
                    if (noted != null) counts?.Note(noted.Value);
                    return;
            }
        }

        /// <summary>
        /// The creature a birth or a hatching brought, or a young one grew into, named as the
        /// event's other thing: known, seen born or seen grow up, and seen together with what it
        /// came of. None where the event names none, as a hen's egg laid alone, an item.
        /// </summary>
        private static void YoungKnown(WorldEvent told, CompendiumBook book)
        {
            if (string.IsNullOrEmpty(told.With)) return;
            CameBy way;
            switch (told.What)
            {
                case Happened.Born:
                case Happened.Hatched:
                    way = CameBy.Born;
                    break;
                case Happened.GrewUp:
                    way = CameBy.GrewUp;
                    break;
                default:
                    return;
            }
            book.Witness(told.Thing, told.With);
            book.Raise(told.With, KnownLevel.Known, way: way);
        }

        /// <summary>The deed something seen done shows (<see cref="Deed"/>).</summary>
        public static Deed DeedOf(Happened what)
        {
            switch (what)
            {
                case Happened.Born: return Deed.Born;
                case Happened.Hatched: return Deed.Hatched;
                case Happened.GrewUp: return Deed.GrewUp;
                case Happened.GrewBack: return Deed.GrewBack;
                case Happened.Grew: return Deed.Grew;
                case Happened.Spawned: return Deed.Spawned;
                default: return Deed.Finished;
            }
        }

        /// <summary>What the play counts count something seen done as; none for a crop grown or a station's work finished, which they do not count.</summary>
        private static PlayEvent? NotedAs(Happened what)
        {
            switch (what)
            {
                case Happened.Born: return PlayEvent.Born;
                case Happened.GrewUp: return PlayEvent.GrewUp;
                case Happened.GrewBack: return PlayEvent.GrewBack;
                case Happened.Spawned: return PlayEvent.Spawned;
                default: return null;
            }
        }
    }
}
