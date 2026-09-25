using UnityEngine;

namespace Scry
{
    /// <summary>Remembers a world copy's own size, so the scale modifier multiplies it.</summary>
    internal sealed class Keep : MonoBehaviour
    {
        public Vector3 BaseScale = Vector3.one;
    }
}
