namespace Scry
{
    /// <summary>
    /// What a prefab is made of, as far as telling debris and whole models apart needs: read
    /// from its components where it is read (<c>PrefabShapes</c>), decided here.
    /// </summary>
    internal struct PrefabShape
    {
        /// <summary>A body physics moves: a rigidbody that is not kinematic.</summary>
        public bool FreeBody;

        public bool Ragdoll;
        public bool Character;

        /// <summary>An item at its root.</summary>
        public bool Item;

        /// <summary>A skinned mesh, as bodies and capes have.</summary>
        public bool Skinned;

        /// <summary>
        /// Debris: loose parts that fly apart under physics (planks, splinters, stones), which a
        /// falling copy can show as the game does; a ragdoll, a creature or an item never is.
        /// </summary>
        public bool IsDebris => FreeBody && !Ragdoll && !Character && !Item;

        /// <summary>
        /// A whole model rather than an effect: a ragdoll taking a creature's place when it dies,
        /// a creature it splits into, an item, a skinned body, or pieces physics moves. A copy
        /// keeps none of its physics, so such a thing would only stand frozen beside the preview,
        /// and effects leave it out.
        /// </summary>
        public bool IsWholeModel => Ragdoll || Character || Item || Skinned || FreeBody;
    }
}
