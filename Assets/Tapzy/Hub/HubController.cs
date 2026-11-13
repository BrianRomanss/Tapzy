using Tapzy.Core;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Hub
{
    /// <summary>
    /// The hub shell: top bar, bottom navigation and the screens between them. It displays only
    /// what the services report, and it never names a game: tiles come from the catalogue.
    /// </summary>
    public class HubController : MonoBehaviour
    {
        public enum Tab { Home, Ranks, Redeem, Profile }

        const float TopBarHeight = 230f;
        const float NavHeight = 170f;

        Canvas canvas;
        RectTransform column, topBar, nav, content, popupLayer;
        Text toast;
        float toastLeft;
        Tab tab = Tab.Home;
        bool built;

        public RectTransform PopupLayer => popupLayer;

        void Start()
        {
            Application.targetFrameRate = 60;
            Services.Ensure(Build);
        }

        void OnDestroy()
        {
            Services.Changed -= Redraw;
        }

        void Build()
        {
            canvas = Ui.Canvas("HubCanvas", 0);
            var bg = Ui.Fill(canvas.transform, "Background").gameObject.AddComponent<Image>();
            bg.color = Theme.Bg;
            bg.raycastTarget = false;
            Ui.Backdrop(bg, "ui_bg_hub");

            column = Ui.Column(canvas.transform, "Column");
            content = Ui.Fill(column, "Content", 0f, TopBarHeight, 0f, NavHeight);
            topBar = Ui.Node(column, "TopBar", new Vector2(0f, -TopBarHeight * 0.5f), new Vector2(Ui.W, TopBarHeight), Ui.Top);
            nav = Ui.Node(column, "Nav", new Vector2(0f, NavHeight * 0.5f), new Vector2(Ui.W, NavHeight), Ui.Bottom);
            toast = Ui.Label(column, "", 42, Theme.Text, new Vector2(0f, NavHeight + 70f), new Vector2(980f, 110f), anchor: Ui.Bottom, bold: true);
            popupLayer = Ui.Fill(canvas.transform, "Popups");

            built = true;
            Services.Changed += Redraw;
            Redraw();

            // coming back from a round shows its result; otherwise offer today's login reward
            var reply = Services.SceneLoader.TakePendingReply();
            if (reply != null) HubPopups.Result(this, reply, Services.SceneLoader.LastGame);
            else if (Services.State.loginClaimable) HubPopups.DailyReward(this);
            DevCapture.TryStart(this);
        }

        public void ClosePopups() => Ui.Clear(popupLayer);

        void Update()
        {
            if (toastLeft <= 0f) return;
            toastLeft -= Time.unscaledDeltaTime;
            toast.color = Theme.Text.Alpha(Mathf.Clamp01(toastLeft));
        }

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            toast.text = message;
            toast.transform.SetAsLastSibling();
            toastLeft = 2.5f;
        }

        public void Show(Tab next)
        {
            tab = next;
            Redraw();
        }

        public void OfferDailyReward()
        {
            if (Services.State.loginClaimable) HubPopups.DailyReward(this);
        }

        void Redraw()
        {
            if (!built) return;
            DrawTopBar();
            DrawNav();
            Ui.Clear(content);
            switch (tab)
            {
                case Tab.Home: HubScreens.Home(this, content); break;
                case Tab.Ranks: HubScreens.Ranks(this, content); break;
                case Tab.Redeem: HubScreens.Redeem(this, content); break;
                case Tab.Profile: HubScreens.Profile(this, content); break;
            }
        }

        void DrawTopBar()
        {
            Ui.Clear(topBar);
            var wallet = Services.Wallet;
            if (Ui.Picture(topBar, "ui_logo", new Vector2(-330f, 40f), new Vector2(360f, 130f)) == null)
                Ui.Label(topBar, "TAPZY", 92, Theme.Primary, new Vector2(-300f, 36f), new Vector2(400f, 110f), TextAnchor.MiddleLeft, bold: true);

            // coin balance: always visible
            var pill = Ui.Box(topBar, Theme.Panel, new Vector2(330f, 40f), new Vector2(360f, 100f));
            if (Ui.Picture(pill.transform, "ui_coin", new Vector2(-130f, 0f), new Vector2(84f, 84f)) == null)
                Ui.Disc(pill.transform, Theme.Coin, new Vector2(-130f, 0f), 64f);
            Ui.Label(pill.transform, Ui.Coins(wallet.Balance), 52, Theme.Text, new Vector2(30f, 0f), new Vector2(250f, 90f), bold: true);

            // how many coins are still earnable today, so the cap never feels like a hidden wall
            var remaining = wallet.RemainingToday;
            var text = remaining > 0
                ? remaining + " coins left to earn today"
                : "Daily limit reached. Keep playing for score and rank!";
            Ui.Label(topBar, text, 38, remaining > 0 ? Theme.TextDim : Theme.Coin, new Vector2(0f, -45f), new Vector2(1000f, 50f), TextAnchor.MiddleLeft);
            Bar(topBar, new Vector2(0f, -90f), 1000f, 14f, wallet.DailyCap > 0 ? (float)wallet.EarnedToday / wallet.DailyCap : 0f, Theme.Coin);
        }

        void DrawNav()
        {
            Ui.Clear(nav);
            Ui.Box(nav, Theme.Panel, Vector2.zero, new Vector2(Ui.W, NavHeight), null, false);
            var names = new[] { "Home", "Ranks", "Redeem", "Profile" };
            for (var i = 0; i < names.Length; i++)
            {
                var t = (Tab)i;
                Ui.Btn(nav, names[i], t == tab ? Theme.Primary : Theme.PanelLight,
                    new Vector2(-396f + i * 264f, 0f), new Vector2(244f, 116f), () => Show(t), null, 40);
            }
        }

        /// <summary>A progress bar; fraction is clamped to 0..1.</summary>
        public static void Bar(Transform parent, Vector2 pos, float width, float height, float fraction, Color fill, Vector2? anchor = null)
        {
            var back = Ui.Box(parent, Theme.PanelLight, pos, new Vector2(width, height), anchor);
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= 0f) return;
            var w = Mathf.Max(height, width * fraction);
            Ui.Box(back.transform, fill, new Vector2(-(width - w) * 0.5f, 0f), new Vector2(w, height));
        }

        public void StartGame(GameDefinition game) => Services.SceneLoader.StartGame(game);
    }
}
