using Tapzy.Core;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tapzy.Hub
{
    /// <summary>Boot scene: splash, create the shared services, sign the player in, load the hub.</summary>
    public class BootController : MonoBehaviour
    {
        const float MinSplashSeconds = 0.8f;

        bool ready;
        float shown;

        void Start()
        {
            Application.targetFrameRate = 60;
            var canvas = Ui.Canvas("Splash", 0);
            var bg = Ui.Fill(canvas.transform, "Background").gameObject.AddComponent<Image>();
            bg.color = Theme.Bg;
            Ui.Backdrop(bg, "ui_bg_hub");
            Ui.Disc(canvas.transform, Theme.Coin, new Vector2(0f, 200f), 220f);
            Ui.Label(canvas.transform, "T", 150, Theme.Dark, new Vector2(0f, 200f), new Vector2(220f, 220f), bold: true);
            Ui.Label(canvas.transform, "Tapzy", 150, Theme.Text, new Vector2(0f, -40f), new Vector2(900f, 200f), bold: true);
            Ui.Label(canvas.transform, "Quick games. Free rewards.", 48, Theme.TextDim, new Vector2(0f, -180f), new Vector2(900f, 70f));
            Services.Ensure(() => ready = true);
        }

        void Update()
        {
            shown += Time.unscaledDeltaTime;
            if (!ready || shown < MinSplashSeconds) return;
            enabled = false;
            SceneManager.LoadScene(Services.HubScene);
        }
    }
}
