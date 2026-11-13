using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.UI
{
    /// <summary>Rolls a number label from one value to another, with a small swell while it counts.</summary>
    public class CountUp : MonoBehaviour
    {
        Text text;
        int from, to;
        float age, seconds;
        string prefix, suffix;

        public static void Run(Text text, int from, int to, float seconds = 0.6f, string prefix = "", string suffix = "")
        {
            var c = text.GetComponent<CountUp>();
            if (c == null) c = text.gameObject.AddComponent<CountUp>();
            c.text = text;
            c.from = from;
            c.to = to;
            c.age = 0f;
            c.seconds = Mathf.Max(0.05f, seconds);
            c.prefix = prefix;
            c.suffix = suffix;
            c.Show(0f);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            var k = Mathf.Clamp01(age / seconds);
            Show(k);
            if (k >= 1f) Destroy(this);
        }

        void Show(float k)
        {
            var eased = 1f - (1f - k) * (1f - k);
            text.text = prefix + Mathf.RoundToInt(Mathf.Lerp(from, to, eased)).ToString("N0") + suffix;
            var s = 1f + 0.15f * Mathf.Sin(k * Mathf.PI);
            text.transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
