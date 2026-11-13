using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.TapBattle
{
    /// <summary>
    /// A tug of war against a computer opponent, decided by tapping speed. Every tap pulls the
    /// rope a little your way; the bot pulls back at its own pace. Drag the flag over your line
    /// to win the pull. If nobody manages it in 12 seconds, whoever is ahead takes it. The
    /// match is the best of three pulls. Tapping before "GO!" is a false start and costs ground.
    ///
    /// Score: 100 per pull won plus 10 per second left on the clock, 300 for winning the match.
    /// </summary>
    [MiniGame("tap_battle", "Tap Battle", Kind = "Sports", Colour = "FF6A3D", Order = 19,
        MaxScorePerSecond = 10f, ScoreBase = 900f, MinDuration = 5f, MissionScore = 300)]
    public class TapBattleGame : MiniGameBase
    {
        const int PullsToWin = 2;
        const float PullSeconds = 12f;
        const float TapPull = 0.06f;          // how far one tap moves the flag; the full run is -1..1
        const float FalseStart = 0.12f;
        const float MidY = 60f, Reach = 380f; // the flag travels this far either side of the middle
        const float BarWidth = 600f;

        enum Phase { Countdown, Pull, PullEnd, Over }

        Bot bot;
        Phase phase;
        int pullNumber, myPulls, botPulls, count, myTaps;
        float timer, flag, botCharge, botDelay, botRate, wobble, pulse;
        bool won;

        Image rope, flagImage, me, them, timeBar;
        Text status, tally, rate;

        protected override string Hint => "Tap fast. Pull the rope!";

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1)[0];
            // taps per second: an easy bot is a relaxed one-finger tapper, a hard one is quick
            botRate = Mathf.Lerp(4.5f, 7.5f, bot.Skill);

            Box(new Vector2(0f, MidY), new Vector2(760f, Reach * 2f + 330f), Theme.Dark);
            Box(new Vector2(0f, MidY + Reach), new Vector2(700f, 14f), Theme.Palette[0], false);  // the bot's line
            Box(new Vector2(0f, MidY - Reach), new Vector2(700f, 14f), Theme.Palette[1], false);  // yours
            Box(new Vector2(0f, MidY), new Vector2(700f, 6f), Theme.PanelLight, false);
            Ui.Label(Field, bot.Label, 34, Theme.Palette[0], new Vector2(210f, MidY + Reach + 60f), new Vector2(320f, 48f));
            Ui.Label(Field, "YOU", 34, Theme.Palette[1], new Vector2(210f, MidY - Reach - 60f), new Vector2(320f, 48f));

            rope = Box(new Vector2(0f, MidY), new Vector2(22f, Reach * 0.7f), Theme.Hex("D9B77A"));
            them = Disc(Vector2.zero, 130f, Theme.Palette[0]);
            me = Disc(Vector2.zero, 130f, Theme.Palette[1]);
            flagImage = Box(Vector2.zero, new Vector2(150f, 44f), Theme.Coin);

            tally = Label("", new Vector2(0f, 720f), 48, Theme.Text, 1040f);
            status = Label("", new Vector2(0f, -640f), 84, Theme.Text, 1040f);
            rate = Label("", new Vector2(0f, -730f), 36, Theme.TextDim, 1040f);
            Box(new Vector2(0f, -790f), new Vector2(BarWidth, 14f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -790f), new Vector2(BarWidth, 14f), Theme.Coin);
            Ui.Label(Field, "Tap anywhere, as fast as you can", 30, Theme.TextDim, new Vector2(0f, -850f), new Vector2(1000f, 44f));

            NewPull();
        }

        void NewPull()
        {
            pullNumber++;
            flag = 0f;
            myTaps = 0;
            botCharge = 0f;
            botDelay = Rand(0.15f, 0.4f);   // the bot's reaction to "GO!"
            wobble = Rand(0f, 6f);
            count = 3;
            phase = Phase.Countdown;
            timer = 0.7f;
            status.text = "3";
            status.color = Theme.Text;
            rate.text = "";
            tally.text = "Pull " + pullNumber + "     YOU  " + myPulls + " - " + botPulls + "  " + bot.Label;
            Sfx.Blip(440f, 0.08f, 0.3f);
            Draw();
        }

        // ---------- flow ----------

        protected override void OnTick(float dt)
        {
            timer -= dt;
            pulse = Mathf.Max(0f, pulse - dt * 6f);
            switch (phase)
            {
                case Phase.Countdown:
                    if (PointerDown)
                    {
                        // jumping the gun gives ground away
                        flag = Mathf.Min(0.5f, flag + FalseStart);
                        Pop("False start!", new Vector2(0f, MidY - 200f), Theme.Bad, 64);
                        Sfx.Bad();
                    }
                    if (timer <= 0f)
                    {
                        count--;
                        if (count > 0)
                        {
                            status.text = count.ToString();
                            timer = 0.7f;
                            Sfx.Blip(440f, 0.08f, 0.3f);
                        }
                        else
                        {
                            phase = Phase.Pull;
                            timer = PullSeconds;
                            status.text = "GO!";
                            status.color = Theme.Coin;
                            Sfx.Blip(880f, 0.15f, 0.35f);
                        }
                    }
                    break;

                case Phase.Pull:
                    if (PointerDown) Tap();
                    BotPull(dt);
                    if (phase != Phase.Pull) break;
                    var seconds = PullSeconds - timer;
                    if (seconds > 0.8f) status.text = "";
                    if (seconds > 0.5f) rate.text = (myTaps / seconds).ToString("0.0") + " taps a second";
                    if (flag <= -1f) EndPull(true, false);
                    else if (flag >= 1f) EndPull(false, false);
                    else if (timer <= 0f) EndPull(flag < 0f, true);
                    break;

                case Phase.PullEnd:
                    if (timer <= 0f)
                    {
                        if (myPulls >= PullsToWin) GameOver(true, "YOU WIN!");
                        else if (botPulls >= PullsToWin) GameOver(false, bot.Name + " wins");
                        else NewPull();
                    }
                    break;

                case Phase.Over:
                    if (timer <= 0f)
                    {
                        EndRound();
                        if (won)
                        {
                            // EndRound treats every ending as a failure; a win should not look like one
                            Shake(0f, 0.01f);
                            Flash(Theme.Good, 0.3f);
                        }
                    }
                    break;
            }
            Draw();
        }

        void Tap()
        {
            if (phase != Phase.Pull) return;
            myTaps++;
            flag -= TapPull;
            pulse = 1f;
            Sfx.Blip(520f + Mathf.Min(myTaps, 60) * 6f, 0.03f, 0.15f);
        }

        /// <summary>The bot taps at its own rate, which drifts up and down so the rope surges and slackens.</summary>
        void BotPull(float dt)
        {
            if (botDelay > 0f)
            {
                botDelay -= dt;
                return;
            }
            var surge = 1f + 0.25f * Mathf.Sin(Elapsed * 1.7f + wobble);
            botCharge += botRate * surge * dt;
            while (botCharge >= 1f)
            {
                botCharge -= 1f;
                flag += TapPull;
            }
        }

        void EndPull(bool mine, bool onTime)
        {
            phase = Phase.PullEnd;
            var left = Mathf.Max(0f, timer);
            timer = 1.5f;
            rate.text = "";
            if (mine)
            {
                myPulls++;
                var points = 100 + Mathf.RoundToInt(left) * 10;
                Score += points;
                status.text = onTime ? "Time! You lead" : "You pulled it!";
                status.color = Theme.Good;
                Pop("+" + points, new Vector2(0f, MidY - 150f), Theme.Good, 96);
                Sfx.Great();
            }
            else
            {
                botPulls++;
                status.text = onTime ? "Time! " + bot.Name + " leads" : bot.Name + " pulled it";
                status.color = Theme.Palette[0];
                Sfx.Bad();
                Shake(10f, 0.25f);
            }
            tally.text = "Pull " + pullNumber + "     YOU  " + myPulls + " - " + botPulls + "  " + bot.Label;
        }

        void GameOver(bool win, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = win;
            status.text = "";
            Box(new Vector2(0f, MidY), new Vector2(940f, 280f), Theme.Dark);
            Label(message, new Vector2(0f, MidY + (won ? 40f : 0f)), 96, won ? Theme.Coin : Theme.Text, 900f);
            if (won)
            {
                Score += 300;
                Label("+300", new Vector2(0f, MidY - 70f), 64, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else Sfx.Bad();
        }

        // ---------- drawing ----------

        void Draw()
        {
            var y = MidY + Mathf.Clamp(flag, -1f, 1f) * Reach;
            rope.SetPos(0f, y);
            flagImage.SetPos(0f, y);
            them.SetPos(0f, y + Reach * 0.35f);
            me.SetPos(0f, y - Reach * 0.35f);
            me.SetScale(1f + pulse * 0.15f);
            var pulling = phase == Phase.Pull;
            timeBar.Show(pulling);
            if (pulling) timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / PullSeconds)), 14f);
        }
    }
}
