using System;

namespace Scry
{
    /// <summary>The look of a piece by the damage it has taken.</summary>
    public enum Wear
    {
        New,
        Worn,
        Broken,
    }

    /// <summary>
    /// The adjustments applied to a preview. Each one only offers what the selected prefab has,
    /// and every change is kept inside what makes sense for it.
    /// </summary>
    public sealed class Modifiers
    {
        public const float MinScale = 0.1f;
        public const float MaxScale = 10f;
        public const float MaxAnimationSpeed = 3f;

        private float _scale;
        private int _level;
        private int _maxLevel;
        private Wear _wear;
        private bool _wearAvailable;
        private float _animationSpeed;

        /// <summary>Starts out set for nothing in particular: first level, new, normal size.</summary>
        public Modifiers()
        {
            _maxLevel = 1;
            Reset();
        }

        /// <summary>Changes with every adjustment, so a view can tell something needs redoing.</summary>
        public int Version { get; private set; }

        public float Scale
        {
            get => _scale;
            set => Set(ref _scale, float.IsNaN(value) ? 1f : Math.Max(MinScale, Math.Min(MaxScale, value)));
        }

        /// <summary>1 is the plain creature, each level above adds a star.</summary>
        public int Level
        {
            get => _level;
            set => Set(ref _level, Math.Max(1, Math.Min(_maxLevel, value)));
        }

        public int MaxLevel => _maxLevel;

        public Wear Wear
        {
            get => _wear;
            set => Set(ref _wear, _wearAvailable ? value : Wear.New);
        }

        public bool WearAvailable => _wearAvailable;

        public float AnimationSpeed
        {
            get => _animationSpeed;
            set => Set(ref _animationSpeed, float.IsNaN(value) ? 1f : Math.Max(0f, Math.Min(MaxAnimationSpeed, value)));
        }

        /// <summary>Goes back to the prefab as it is, and offers only what this entry has.</summary>
        public void ResetFor(Entry entry)
        {
            _maxLevel = 1 + Math.Max(0, entry?.ExtraLevels ?? 0);
            _wearAvailable = entry != null && entry.HasWear;
            Reset();
        }

        /// <summary>Goes back to the prefab as it is, keeping what the current entry offers.</summary>
        public void Reset()
        {
            _scale = 1f;
            _level = 1;
            _wear = Wear.New;
            _animationSpeed = 1f;
            Version++;
        }

        private void Set<T>(ref T field, T value)
        {
            if (Equals(field, value)) return;
            field = value;
            Version++;
        }
    }
}
