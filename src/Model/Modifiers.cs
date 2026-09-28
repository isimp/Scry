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

        /// <summary>The loudest a preview may play: twice as loud as the game plays the same sound.</summary>
        public const float MaxVolume = 2f;

        private float _scale;
        private int _level;
        private int _maxLevel;
        private Wear _wear;
        private bool _wearAvailable;
        private float _animationSpeed;
        private float _volume;

        /// <summary>
        /// How loud the preview plays, as a share of the game's own loudness, from silent to
        /// <see cref="MaxVolume"/>. Heard at once, so turning it rebuilds nothing; back to the
        /// game's own with every selection, as some previews play as soon as they are selected.
        /// </summary>
        public float Volume
        {
            get => _volume;
            set => _volume = float.IsNaN(value) ? 1f : Math.Max(0f, Math.Min(MaxVolume, value));
        }

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

        private string[] _lookNames = new string[0];
        private int _defaultLook;
        private int _look;

        /// <summary>The look the prefab is shown in, one of <see cref="LookNames"/>.</summary>
        public int Look
        {
            get => _look;
            set => Set(ref _look, _lookNames.Length == 0 ? 0 : Math.Max(0, Math.Min(_lookNames.Length - 1, value)));
        }

        public string[] LookNames => _lookNames;

        /// <summary>Only offered when there is more than one look to choose from.</summary>
        public bool LookAvailable => _lookNames.Length > 1;

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
            _lookNames = entry?.Looks ?? new string[0];
            _defaultLook = _lookNames.Length == 0 ? 0 : Math.Max(0, Math.Min(_lookNames.Length - 1, entry.DefaultLook));
            Reset();
        }

        /// <summary>Whether everything is as the prefab is, so there is nothing to reset.</summary>
        public bool IsDefault => Same(_scale, 1f) && _level == 1 && _wear == Wear.New && Same(_animationSpeed, 1f) && Same(_volume, 1f) && _look == _defaultLook;

        private static bool Same(float a, float b) => Math.Abs(a - b) < 1e-4f;

        /// <summary>Goes back to the prefab as it is, keeping what the current entry offers.</summary>
        public void Reset()
        {
            _scale = 1f;
            _level = 1;
            _wear = Wear.New;
            _animationSpeed = 1f;
            _volume = 1f;
            _look = _defaultLook;
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
