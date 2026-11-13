using System;
using System.Collections;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Core
{
    /// <summary>The four placements from the GDD. Milestone 4 adds a real ad network behind this.</summary>
    public interface IAds
    {
        /// <summary>Player-chosen ad. Calls back true only if it was watched to the end.</summary>
        void ShowRewarded(Action<bool> done);
        void ShowInterstitial(Action done);
    }

    /// <summary>A labelled stand-in so the ad flows can be built and tested before any ad SDK exists.</summary>
    public class PlaceholderAds : IAds
    {
        const float Seconds = 2f;

        public void ShowRewarded(Action<bool> done) => Services.Runner.StartCoroutine(Play("Rewarded ad", () => done?.Invoke(true)));
        public void ShowInterstitial(Action done) => Services.Runner.StartCoroutine(Play("Ad break", done));

        static IEnumerator Play(string title, Action done)
        {
            var canvas = Ui.Canvas("AdPlaceholder", 5000);
            Ui.Dim(canvas.transform, 1f);
            Ui.Label(canvas.transform, title, 90, Theme.Text, new Vector2(0f, 120f), new Vector2(900f, 130f), bold: true);
            Ui.Label(canvas.transform, "Placeholder. Real ads arrive in milestone 4.", 44, Theme.TextDim, Vector2.zero, new Vector2(900f, 70f));
            var count = Ui.Label(canvas.transform, "", 70, Theme.Coin, new Vector2(0f, -140f), new Vector2(400f, 100f), bold: true);
            var left = Seconds;
            while (left > 0f)
            {
                count.text = Mathf.CeilToInt(left).ToString();
                left -= Time.unscaledDeltaTime;
                yield return null;
            }
            UnityEngine.Object.Destroy(canvas.gameObject);
            done?.Invoke();
        }
    }
}
