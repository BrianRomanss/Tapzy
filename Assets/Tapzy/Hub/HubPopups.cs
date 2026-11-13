using System;
using Tapzy.Core;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Hub
{
    /// <summary>Popups over the hub: daily reward, game result, messages and confirmations.</summary>
    public static class HubPopups
    {
        public const string RulesText =
            "Tapzy is free to play.\n\n" +
            "• Coins are earned only by logging in and playing. They can never be bought.\n" +
            "• Coins are never bet, staked or spent to enter a game.\n" +
            "• You earn for your own results. Nobody loses coins when someone else wins.\n" +
            "• Coins can only be exchanged for gift cards. They cannot be transferred, sold or turned into cash.\n" +
            "• You can earn up to 100 coins a day. After that, games stay open for score and rank.";

        public const string PrivacyText =
            "Draft. The final policy is published before release.\n\n" +
            "Tapzy stores your display name, your play history and, once you sign in, your phone number. " +
            "It is used to run the game, prevent abuse and deliver rewards. " +
            "You can erase your data at any time with Delete my data in Profile.";

        static RectTransform Open(HubController hub, float height, out RectTransform panel)
        {
            var root = Ui.Fill(hub.PopupLayer, "Popup");
            Ui.Dim(root);
            var box = Ui.Box(root, Theme.Panel, Vector2.zero, new Vector2(960f, height));
            box.raycastTarget = true;
            panel = box.rectTransform;
            return root;
        }

        static void Close(RectTransform root) => UnityEngine.Object.Destroy(root.gameObject);

        // ---------- daily login reward with the 7-day streak ----------

        public static void DailyReward(HubController hub)
        {
            var state = Services.State;
            var root = Open(hub, 980f, out var panel);
            Ui.Label(panel, "Daily reward", 76, Theme.Text, new Vector2(0f, 390f), new Vector2(880f, 100f), bold: true);
            Ui.Label(panel, "Come back every day. Miss a day and the streak restarts.", 34, Theme.TextDim, new Vector2(0f, 310f), new Vector2(880f, 50f));

            for (var day = 1; day <= Economy.MaxStreakDay; day++)
            {
                // two rows: days 1-4, then 5-7
                var firstRow = day <= 4;
                var index = firstRow ? day - 1 : day - 5;
                var count = firstRow ? 4 : 3;
                var pos = new Vector2((index - (count - 1) * 0.5f) * 210f, firstRow ? 150f : -80f);
                var isToday = day == state.streakDay;
                var done = day < state.streakDay;
                var cell = Ui.Box(panel, isToday ? Theme.Coin : done ? Theme.Good.Shade(0.55f) : Theme.PanelLight, pos, new Vector2(190f, 200f));
                var ink = isToday ? Theme.Dark : Theme.Text;
                Ui.Label(cell.transform, "Day " + day, 34, ink, new Vector2(0f, 60f), new Vector2(180f, 46f));
                Ui.Label(cell.transform, "+" + Economy.LoginReward(day), 62, ink, new Vector2(0f, -15f), new Vector2(180f, 80f), bold: true);
                if (done) Ui.Label(cell.transform, "claimed", 26, Theme.Text.Alpha(0.8f), new Vector2(0f, -70f), new Vector2(180f, 36f));
            }

            Ui.Btn(panel, "Claim +" + state.loginReward + " coins", Theme.Good, new Vector2(0f, -330f), new Vector2(760f, 150f), () =>
            {
                Services.Backend.ClaimDailyLogin(reply =>
                {
                    Close(root);
                    Services.Apply(reply.state);
                    if (!reply.granted) return;
                    Sfx.Coin();
                    hub.Toast(reply.coins > 0 ? "+" + reply.coins + " coins. Day " + reply.streakDay + " streak!" : "Daily limit reached.");
                });
            }, null, 52);
        }

        // ---------- game result: score, personal best, mission progress, coins earned ----------

        public static void Result(HubController hub, RoundReply reply, GameDefinition game)
        {
            var root = Open(hub, 1500f, out var panel);
            Ui.Label(panel, game != null ? game.title : "Result", 60, Theme.TextDim, new Vector2(0f, 660f), new Vector2(880f, 80f), bold: true);
            var scoreLabel = Ui.Label(panel, reply.score.ToString("N0"), 170, Theme.Text, new Vector2(0f, 520f), new Vector2(900f, 210f), bold: true);
            if (reply.score > 0) CountUp.Run(scoreLabel, 0, reply.score, 0.7f);

            // a near miss is the best reason to play again, so say it out loud
            var gap = reply.personalBest - reply.score;
            if (!reply.accepted)
                Ui.Label(panel, reply.rejectReason, 40, Theme.Bad, new Vector2(0f, 385f), new Vector2(860f, 60f));
            else if (reply.newBest)
            {
                Ui.Label(panel, "New personal best!", 50, Theme.Coin, new Vector2(0f, 385f), new Vector2(860f, 66f), bold: true);
                Sfx.Great();
            }
            else if (gap > 0 && reply.score * 5 >= reply.personalBest * 4)
                Ui.Label(panel, "So close! Only " + gap.ToString("N0") + " from your best", 46, Theme.Coin, new Vector2(0f, 385f), new Vector2(860f, 62f), bold: true);
            else
                Ui.Label(panel, "Personal best " + reply.personalBest.ToString("N0"), 44, Theme.TextDim, new Vector2(0f, 385f), new Vector2(860f, 60f));

            // coins earned, line by line, exactly as the backend reported them
            var y = 290f;
            var coins = Ui.Box(panel, Theme.PanelLight, new Vector2(0f, y - 110f), new Vector2(860f, 280f));
            Text coinsHead = null;
            if (reply.coinsEarned > 0)
            {
                Sfx.Coin();
                coinsHead = Ui.Label(coins.transform, "+" + reply.coinsEarned + " coins", 70, Theme.Coin, new Vector2(0f, 85f), new Vector2(820f, 90f), bold: true);
                CountUp.Run(coinsHead, 0, reply.coinsEarned, 0.6f, "+", " coins");
                var ly = 20f;
                for (var i = 0; i < reply.lines.Count && i < 4; i++)
                {
                    var line = reply.lines[i];
                    if (line.coins <= 0) continue;
                    Ui.Label(coins.transform, line.label, 32, Theme.Text, new Vector2(-60f, ly), new Vector2(660f, 42f), TextAnchor.MiddleLeft);
                    Ui.Label(coins.transform, "+" + line.coins, 34, Theme.Coin, new Vector2(350f, ly), new Vector2(120f, 42f), TextAnchor.MiddleRight, bold: true);
                    ly -= 44f;
                }
            }
            else
            {
                var why = !reply.accepted ? "No coins for this round."
                    : reply.capReached || Services.Wallet.RemainingToday == 0 ? "Daily limit reached.\nKeep playing for score and rank!"
                    : "No coins this round.\nFinish missions to earn more.";
                Ui.Label(coins.transform, why, 42, Theme.TextDim, Vector2.zero, new Vector2(800f, 200f));
            }

            // mission progress
            y = -30f;
            Ui.Label(panel, "Missions", 40, Theme.Text, new Vector2(0f, y), new Vector2(860f, 54f), TextAnchor.MiddleLeft, bold: true);
            y -= 70f;
            foreach (var m in Services.Missions.Today)
            {
                Ui.Label(panel, m.description, 34, m.completed ? Theme.Good : Theme.TextDim, new Vector2(-110f, y), new Vector2(640f, 46f), TextAnchor.MiddleLeft);
                Ui.Label(panel, m.completed ? "Done" : m.progress.ToString("N0") + " / " + m.target.ToString("N0"), 34,
                    m.completed ? Theme.Good : Theme.Text, new Vector2(320f, y), new Vector2(220f, 46f), TextAnchor.MiddleRight, bold: true);
                y -= 60f;
            }

            // the rewarded-ad hook: offered at the moment the coins land, never forced
            if (reply.doubleCoins > 0 && coinsHead != null)
            {
                var slot = new Vector2(0f, -318f);
                var label = reply.doubleCoins == reply.coinsEarned
                    ? "Double it!  Watch ad  +" + reply.doubleCoins
                    : "Watch ad for +" + reply.doubleCoins + " more";
                Button twice = null;
                twice = Ui.Btn(panel, label, Theme.Good, slot, new Vector2(760f, 104f), () =>
                {
                    twice.interactable = false;
                    Services.Wallet.DoubleLastRound(r =>
                    {
                        if (root == null) return; // popup was closed meanwhile
                        if (!r.ok)
                        {
                            twice.interactable = true;
                            hub.Toast(r.message);
                            return;
                        }
                        UnityEngine.Object.Destroy(twice.gameObject);
                        Ui.Label(panel, r.coins > 0 ? "Bonus claimed!  +" + r.coins + " coins" : r.message, 44, Theme.Good, slot, new Vector2(860f, 60f), bold: true);
                        if (r.coins <= 0) return;
                        Sfx.Coin();
                        CountUp.Run(coinsHead, reply.coinsEarned, reply.coinsEarned + r.coins, 0.8f, "+", " coins");
                    });
                }, null, 44);
                twice.gameObject.AddComponent<Pulse>();
            }

            Ui.Btn(panel, "Play again", Theme.Primary, new Vector2(0f, -470f), new Vector2(760f, 150f), () =>
            {
                if (game != null) hub.StartGame(game);
                else Close(root);
            }, null, 54);
            Ui.Btn(panel, "Home", Theme.PanelLight, new Vector2(0f, -640f), new Vector2(760f, 130f), () =>
            {
                Close(root);
                hub.OfferDailyReward();
            });
        }

        // ---------- generic ----------

        public static void Message(HubController hub, string title, string body)
        {
            var root = Open(hub, 1300f, out var panel);
            Ui.Label(panel, title, 70, Theme.Text, new Vector2(0f, 550f), new Vector2(880f, 90f), bold: true);
            Ui.Label(panel, body, 38, Theme.TextDim, new Vector2(0f, 30f), new Vector2(840f, 900f), TextAnchor.UpperLeft);
            Ui.Btn(panel, "OK", Theme.Primary, new Vector2(0f, -540f), new Vector2(560f, 130f), () => Close(root));
        }

        public static void Confirm(HubController hub, string title, string body, string yes, Action onYes)
        {
            var root = Open(hub, 760f, out var panel);
            Ui.Label(panel, title, 64, Theme.Text, new Vector2(0f, 270f), new Vector2(880f, 84f), bold: true);
            Ui.Label(panel, body, 40, Theme.TextDim, new Vector2(0f, 110f), new Vector2(820f, 200f));
            Ui.Btn(panel, yes, Theme.Bad, new Vector2(0f, -110f), new Vector2(640f, 130f), () =>
            {
                Close(root);
                onYes?.Invoke();
            });
            Ui.Btn(panel, "Cancel", Theme.PanelLight, new Vector2(0f, -270f), new Vector2(640f, 120f), () => Close(root));
        }
    }
}
