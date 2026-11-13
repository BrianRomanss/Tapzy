using Tapzy.Core;
using Tapzy.UI;
using UnityEngine;

namespace Tapzy.Hub
{
    /// <summary>The four tabbed screens. Each is rebuilt from the latest state whenever it changes.</summary>
    public static class HubScreens
    {
        static int ranksGame;

        // ---------- Home: missions and the game grid ----------

        public static void Home(HubController hub, RectTransform root)
        {
            var content = Ui.Scroll(root);
            var state = Services.State;
            var y = -50f;

            if (state.loginClaimable)
            {
                Ui.Btn(content, "Claim today's reward  +" + state.loginReward, Theme.Coin, new Vector2(0f, y - 50f),
                    new Vector2(1000f, 120f), hub.OfferDailyReward, Ui.Top);
                y -= 160f;
            }

            Header(content, "Today's missions", ref y);
            foreach (var m in Services.Missions.Today)
            {
                var row = Ui.Box(content, Theme.Panel, new Vector2(0f, y - 70f), new Vector2(1000f, 140f), Ui.Top);
                Ui.Label(row.transform, m.description, 42, Theme.Text, new Vector2(-110f, 26f), new Vector2(720f, 56f), TextAnchor.MiddleLeft, bold: true);
                HubController.Bar(row.transform, new Vector2(-190f, -34f), 560f, 16f, m.target > 0 ? (float)m.progress / m.target : 0f, Theme.Good);
                Ui.Label(row.transform, m.progress.ToString("N0") + " / " + m.target.ToString("N0"), 32, Theme.TextDim,
                    new Vector2(200f, -34f), new Vector2(200f, 44f), TextAnchor.MiddleLeft);

                var right = new Vector2(390f, 0f);
                if (!m.completed)
                    Ui.Label(row.transform, "+" + m.reward, 50, Theme.Coin, right, new Vector2(180f, 70f), bold: true);
                else if (!m.boosted && state.RemainingToday > 0)
                {
                    var mission = m;
                    Ui.Btn(row.transform, "x2 Ad", Theme.Good, right, new Vector2(190f, 96f),
                        () => Services.Missions.Boost(mission, reply => hub.Toast(reply.message)), null, 38)
                        .gameObject.AddComponent<Pulse>();
                }
                else
                    Ui.Label(row.transform, "Done", 44, Theme.Good, right, new Vector2(180f, 70f), bold: true);
                y -= 160f;
            }
            if (Services.Missions.Today.Count == 0)
            {
                Ui.Label(content, "No missions yet. Run Tapzy > Setup Project.", 38, Theme.TextDim, new Vector2(0f, y - 40f), new Vector2(1000f, 60f), anchor: Ui.Top);
                y -= 100f;
            }

            y -= 20f;
            Header(content, "Games", ref y);
            var games = Services.Games;
            const float tileW = 490f, tileH = 340f, gap = 20f;
            for (var i = 0; i < games.Count; i++)
            {
                var game = games[i];
                var col = i % 2;
                var rowIndex = i / 2;
                var pos = new Vector2((col == 0 ? -1f : 1f) * (tileW + gap) * 0.5f, y - tileH * 0.5f - rowIndex * (tileH + gap));
                var tile = Ui.Btn(content, "", game.tileColour, pos, new Vector2(tileW, tileH), () => hub.StartGame(game), Ui.Top);
                var tileArt = game.icon != null ? game.icon : Art.Get("tile_" + game.id);
                if (tileArt != null)
                {
                    var icon = Ui.Box(tile.transform, Color.white, new Vector2(0f, 45f), new Vector2(230f, 230f), null, false);
                    icon.sprite = tileArt;
                    icon.preserveAspect = true;
                }
                else
                {
                    Ui.Disc(tile.transform, Theme.Dark.Alpha(0.25f), new Vector2(0f, 50f), 150f);
                    Ui.Label(tile.transform, game.title.Substring(0, 1), 96, Theme.Text, new Vector2(0f, 50f), new Vector2(150f, 150f), bold: true);
                }
                Ui.Label(tile.transform, game.title, 48, Theme.Dark, new Vector2(0f, -70f), new Vector2(tileW - 30f, 64f), bold: true);
                var best = state.BestFor(game.id);
                Ui.Label(tile.transform, best > 0 ? game.kind + "  •  Best " + best.ToString("N0") : game.kind, 32, Theme.Dark.Alpha(0.75f),
                    new Vector2(0f, -125f), new Vector2(tileW - 30f, 44f));
            }
            y -= Mathf.CeilToInt(games.Count / 2f) * (tileH + gap);
            Ui.SetContentHeight(content, -y + 40f);
        }

        // ---------- Ranks: weekly leaderboard per game ----------

        public static void Ranks(HubController hub, RectTransform root)
        {
            var games = Services.Games;
            if (games.Count == 0)
            {
                Ui.Label(root, "No games yet.", 44, Theme.TextDim, Vector2.zero, new Vector2(900f, 60f));
                return;
            }
            ranksGame = (ranksGame % games.Count + games.Count) % games.Count;
            var game = games[ranksGame];

            Ui.Btn(root, "<", Theme.PanelLight, new Vector2(-440f, -80f), new Vector2(110f, 110f), () =>
            {
                ranksGame--;
                hub.Show(HubController.Tab.Ranks);
            }, Ui.Top);
            Ui.Btn(root, ">", Theme.PanelLight, new Vector2(440f, -80f), new Vector2(110f, 110f), () =>
            {
                ranksGame++;
                hub.Show(HubController.Tab.Ranks);
            }, Ui.Top);
            Ui.Label(root, game.title, 60, Theme.Text, new Vector2(0f, -60f), new Vector2(700f, 76f), anchor: Ui.Top, bold: true);
            Ui.Label(root, "This week  •  top " + Economy.LeaderboardWinners + " win " + Economy.LeaderboardReward + " coins", 34, Theme.TextDim,
                new Vector2(0f, -118f), new Vector2(760f, 46f), anchor: Ui.Top);

            var list = Ui.Scroll(Ui.Fill(root, "List", 0f, 170f, 0f, 70f));
            Ui.Label(root, "Practice board. Real players and prizes arrive when Tapzy goes online.", 30, Theme.TextDim,
                new Vector2(0f, 35f), new Vector2(1000f, 44f), anchor: Ui.Bottom);

            Services.Backend.GetLeaderboard(game.id, board =>
            {
                if (list == null) return; // screen was rebuilt before the reply arrived
                var y = -10f;
                foreach (var e in board.top)
                {
                    var row = Ui.Box(list, e.isPlayer ? Theme.Primary : Theme.Panel, new Vector2(0f, y - 50f), new Vector2(1000f, 92f), Ui.Top);
                    Ui.Label(row.transform, e.rank.ToString(), 44, e.rank <= Economy.LeaderboardWinners ? Theme.Coin : Theme.TextDim,
                        new Vector2(-430f, 0f), new Vector2(110f, 60f), bold: true);
                    Ui.Label(row.transform, e.isPlayer ? e.name + " (you)" : e.name, 42, Theme.Text, new Vector2(-70f, 0f), new Vector2(560f, 60f), TextAnchor.MiddleLeft);
                    Ui.Label(row.transform, e.score.ToString("N0"), 44, Theme.Text, new Vector2(360f, 0f), new Vector2(250f, 60f), TextAnchor.MiddleRight, bold: true);
                    y -= 104f;
                }
                if (board.player == null)
                {
                    Ui.Label(list, "Play a round this week to get on the board.", 38, Theme.TextDim, new Vector2(0f, y - 50f), new Vector2(1000f, 60f), anchor: Ui.Top);
                    y -= 100f;
                }
                Ui.SetContentHeight(list, -y + 30f);
            });
        }

        // ---------- Redeem: gift cards and progress toward each ----------

        public static void Redeem(HubController hub, RectTransform root)
        {
            var content = Ui.Scroll(root);
            var y = -50f;
            Header(content, "Gift cards", ref y);
            Ui.Label(content, Economy.CoinsPerRupee + " coins = ₹1. Coins are free to earn and can only become gift cards.", 34, Theme.TextDim,
                new Vector2(0f, y - 40f), new Vector2(1000f, 90f), TextAnchor.UpperLeft, Ui.Top);
            y -= 110f;

            var balance = Services.Wallet.Balance;
            Services.Backend.GetGiftCards(cards =>
            {
                if (content == null) return;
                foreach (var card in cards)
                {
                    var c = card;
                    var row = Ui.Box(content, Theme.Panel, new Vector2(0f, y - 90f), new Vector2(1000f, 180f), Ui.Top);
                    Ui.Label(row.transform, "₹" + c.rupees + "  " + c.brand, 48, Theme.Text, new Vector2(-120f, 42f), new Vector2(700f, 62f), TextAnchor.MiddleLeft, bold: true);
                    Ui.Label(row.transform, Ui.Coins(Mathf.Min(balance, c.price)) + " / " + Ui.Coins(c.price) + " coins", 34, Theme.TextDim,
                        new Vector2(-120f, -12f), new Vector2(700f, 46f), TextAnchor.MiddleLeft);
                    HubController.Bar(row.transform, new Vector2(-120f, -56f), 700f, 16f, (float)balance / c.price, Theme.Coin);
                    var btn = Ui.Btn(row.transform, "Get", Theme.Good, new Vector2(385f, 0f), new Vector2(190f, 110f),
                        () => Services.Backend.RequestRedemption(c.id, reply =>
                        {
                            Services.Apply(reply.state);
                            hub.Toast(reply.message);
                        }), null, 42);
                    btn.interactable = balance >= c.price;
                    y -= 200f;
                }
                Ui.Label(content,
                    "To redeem you need a phone-verified account that is at least " + Economy.RedeemMinAccountAgeDays +
                    " days old, and you must be 18 or older. Each request is reviewed; codes arrive within 3 days.",
                    32, Theme.TextDim, new Vector2(0f, y - 80f), new Vector2(1000f, 160f), TextAnchor.UpperLeft, Ui.Top);
                y -= 190f;
                Ui.SetContentHeight(content, -y + 30f);
            });
        }

        // ---------- Profile and settings ----------

        public static void Profile(HubController hub, RectTransform root)
        {
            var content = Ui.Scroll(root);
            var state = Services.State;
            var y = -50f;

            Header(content, "Profile", ref y);
            Ui.Label(content, "Display name", 34, Theme.TextDim, new Vector2(0f, y - 20f), new Vector2(1000f, 44f), TextAnchor.MiddleLeft, Ui.Top);
            y -= 50f;
            Ui.Input(content, state.displayName, "Your name", new Vector2(0f, y - 55f), new Vector2(1000f, 110f), value =>
            {
                if (value == Services.State.displayName) return;
                Services.Backend.SetDisplayName(value, reply =>
                {
                    hub.Toast(reply.message);
                    Services.Apply(reply.state);
                });
            }, Ui.Top);
            y -= 130f;
            Ui.Label(content,
                state.isGuest
                    ? "Guest account. Phone sign-in arrives when Tapzy goes online; guests can play but cannot redeem."
                    : "Signed in with a verified phone number.",
                32, Theme.TextDim, new Vector2(0f, y - 45f), new Vector2(1000f, 90f), TextAnchor.UpperLeft, Ui.Top);
            y -= 120f;

            Header(content, "Settings", ref y);
            Ui.Btn(content, Services.SoundOn ? "Sound: On" : "Sound: Off", Theme.PanelLight, new Vector2(0f, y - 60f), new Vector2(1000f, 120f), () =>
            {
                Services.SoundOn = !Services.SoundOn;
                hub.Show(HubController.Tab.Profile);
            }, Ui.Top);
            y -= 140f;
            Ui.Btn(content, "Rules", Theme.PanelLight, new Vector2(0f, y - 60f), new Vector2(1000f, 120f),
                () => HubPopups.Message(hub, "Rules", HubPopups.RulesText), Ui.Top);
            y -= 140f;
            Ui.Btn(content, "Privacy policy", Theme.PanelLight, new Vector2(0f, y - 60f), new Vector2(1000f, 120f),
                () => HubPopups.Message(hub, "Privacy", HubPopups.PrivacyText), Ui.Top);
            y -= 140f;
            Ui.Btn(content, "Delete my data", Theme.Bad, new Vector2(0f, y - 60f), new Vector2(1000f, 120f),
                () => HubPopups.Confirm(hub, "Delete everything?",
                    "Your coins, streak and scores will be erased. This cannot be undone.", "Delete",
                    () => Services.Backend.DeleteMyData(reply =>
                    {
                        Services.Apply(reply.state);
                        hub.Toast(reply.message);
                    })), Ui.Top);
            y -= 160f;
            Ui.SetContentHeight(content, -y + 30f);
        }

        static void Header(Transform parent, string text, ref float y)
        {
            Ui.Label(parent, text, 50, Theme.Text, new Vector2(0f, y - 30f), new Vector2(1000f, 66f), TextAnchor.MiddleLeft, Ui.Top, true);
            y -= 90f;
        }
    }
}
