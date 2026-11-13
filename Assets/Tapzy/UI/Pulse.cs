using UnityEngine;

namespace Tapzy.UI
{
    /// <summary>Gentle breathing scale that draws the eye to one call-to-action. Add it to a button.</summary>
    public class Pulse : MonoBehaviour
    {
        public float amount = 0.05f;
        public float speed = 5f;

        void Update()
        {
            var s = 1f + amount * Mathf.Sin(Time.unscaledTime * speed);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
