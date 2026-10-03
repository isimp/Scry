using System.Collections.Generic;

namespace Scry
{
    /// <summary>A projectile's own figures, as read from the game (<c>Projectile</c>); an attack that fires it gives it the attack's instead (<c>Projectile.Setup</c>).</summary>
    internal sealed class ProjectileFacts
    {
        /// <summary>Its own damage by type; what it spawns on hit may deal it instead (<see cref="SpawnDealsTheDamage"/>).</summary>
        public (string Type, float Amount)[] Damage = System.Array.Empty<(string, float)>();

        public bool SpawnDealsTheDamage;
        public float AreaOfEffect;
        public float Knockback;
        public bool Blockable;
        public bool Dodgeable;

        /// <summary>The status effect it puts on what it hits, by the name the game shows, and its key; null for none.</summary>
        public string OnHit;
        public string OnHitKey;

        public float FliesFor;
        public float Gravity;
        public float Drag;
        public bool Bounces;
        public int MaxBounces;
        public bool StaysAfterHit;
        public float StaysFor;
        public bool LeavesTheWeapon;
        public bool SpawnsOnHit;
        public float SpawnOnHitChance;
    }

    /// <summary>
    /// A projectile's own numbers in words: what a hit does and over how wide, whether it can be
    /// blocked or dodged, the status effect it puts on what it hits, how it flies (how long, how it
    /// falls and slows, whether it bounces or stays) and what it leaves.
    /// </summary>
    internal static class ProjectileWords
    {
        /// <summary>The game's own figure for bouncing without end.</summary>
        private const int EndlessBounces = 99;

        public static string Description(bool shotFromAnAttack) => shotFromAnAttack
            ? "Fired by an attack, it takes that attack's damage, knockback, blocking and status effect in place of its own below, and the attack's speed."
            : "Its speed comes from whatever launches it.";

        public static List<FactPair> Pairs(ProjectileFacts f)
        {
            var pairs = new List<FactPair>();
            void Add(string label, string value, string link = null)
            {
                if (!string.IsNullOrEmpty(value)) pairs.Add(new FactPair(label, value, link));
            }

            if (f.SpawnDealsTheDamage) Add("Own damage", "none; what it spawns on hit deals the damage");
            else Add("Own damage", CombatWords.Damage(f.Damage));
            if (f.AreaOfEffect > 0f) Add("Hits", $"everything within {Numbers.Amount(f.AreaOfEffect)} m of where it lands");
            if (f.Knockback > 0f) Add("Knockback", Numbers.Amount(f.Knockback));
            Add("Can be", f.Blockable && f.Dodgeable ? "blocked or dodged" : f.Blockable ? "blocked" : f.Dodgeable ? "dodged" : "neither blocked nor dodged");
            Add("On hit", f.OnHit, f.OnHitKey);

            if (f.FliesFor > 0f) Add("Flies for", Numbers.Duration(f.FliesFor));
            Add("Falls", f.Gravity != 0f ? $"{Numbers.Amount(f.Gravity)} m/s²" : "no, it flies straight");
            if (f.Drag > 0f) Add("Slows", $"drag {Numbers.Amount(f.Drag)}");
            if (f.Bounces) Add("Bounces", f.MaxBounces < EndlessBounces ? $"up to {Numbers.Count(f.MaxBounces)} times" : "yes");
            if (f.StaysAfterHit) Add("After a hit", $"stays where it struck for {Numbers.Duration(f.StaysFor)}");
            if (f.LeavesTheWeapon) Add("Leaves", "the weapon that threw it, where it lands");
            if (f.SpawnsOnHit && f.SpawnOnHitChance < 1f) Add("Spawns on hit", DropWords.Share(f.SpawnOnHitChance) + " of the time");
            return pairs;
        }
    }
}
