using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Turns a windmill copy's blades and millstone as <c>Windmill.Update</c> turns them at full
    /// wind: the blades about their forward axis and the stone about its upright one, each by its
    /// own speed. The windmill's own script is gone from the copy.
    /// </summary>
    internal sealed class Turn : MonoBehaviour
    {
        public Transform Propeller;
        public float PropellerSpeed;
        public Transform Stone;
        public float StoneSpeed;

        private float _propellerAngle;
        private float _stoneAngle;

        private void Update()
        {
            var dt = Time.deltaTime;
            if (Propeller != null)
            {
                _propellerAngle += PropellerSpeed * dt;
                Propeller.localRotation = Quaternion.Euler(0f, 0f, _propellerAngle);
            }
            if (Stone != null)
            {
                _stoneAngle += StoneSpeed * dt;
                Stone.localRotation = Quaternion.Euler(0f, _stoneAngle, 0f);
            }
        }
    }
}
