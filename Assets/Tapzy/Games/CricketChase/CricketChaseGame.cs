using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.CricketChase
{
    /// <summary>
    /// A two-over run chase. The computer side has already batted and set a target; you have
    /// 12 balls and 3 wickets to beat it, facing a computer bowler. Tap as the ball crosses the
    /// yellow line: the better the timing, the more runs. A ball you miss that is heading for
    /// the stumps bowls you; a ball outside the stumps can be left alone.
    ///
    /// Score: 10 per run, 300 for winning plus 20 per ball left over.
    /// </summary>
    [MiniGame("cricket_chase", "Cricket Chase", Kind = "Sports", Colour = "37C871", Order = 18,
        MaxScorePerSecond = 15f, ScoreBase = 1100f, MinDuration = 5f, MissionScore = 250)]
    public class CricketChaseGame : MiniGameBase
    {
        const int Balls = 12, Wickets = 3;
        const float StartY = 560f, BatY = -380f, StumpY = -520f;
        const float WideX = 95f;

        // timing windows, in seconds either side of the perfect moment
        const float SixWindow = 0.02f, FourWindow = 0.045f, TwoWindow = 0.08f, OneWindow = 0.12f, EdgeWindow = 0.17f;

        enum Phase { Intro, RunUp, Flight, Result, Over }

        Bot bot;
        Phase phase;
        int botScore, runs, wickets, ballsBowled;
        float timer;
        bool won;

        // the current delivery
        float ballTime, flight, hitTime;
        float fromX, toX;
        bool onStumps, swung;
        Vector2 ballPos, ballVel;

        Image ball, bat;
        readonly Image[] stumps = new Image[3];
        readonly Text[] history = new Text[Balls];
        Text scoreLine, needLine, status;

        protected override string Hint => "Tap to hit the ball";

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1)[0];
            botScore = Mathf.RoundToInt(Mathf.Lerp(22f, 40f, bot.Skill)) + Rand(-2, 3);

            Box(new Vector2(0f, 20f), new Vector2(1080f, 1300f), Theme.Hex("2E8B4E"), false);
            Box(new Vector2(0f, 20f), new Vector2(300f, 1240f), Theme.Hex("D9B77A"));
            Box(new Vector2(0f, StartY - 40f), new Vector2(300f, 8f), Color.white, false);
            Box(new Vector2(0f, BatY), new Vector2(300f, 14f), Theme.Coin, false);       // the line to hit on
            Box(new Vector2(0f, BatY), new Vector2(300f, 110f), Theme.Coin.Alpha(0.18f), false);

            for (var i = 0; i < 3; i++)
            {
                Box(new Vector2((i - 1) * 26f, StartY + 30f), new Vector2(12f, 70f), Theme.Hex("F4E3B5"));
                stumps[i] = Box(new Vector2((i - 1) * 26f, StumpY - 30f), new Vector2(14f, 90f), Theme.Hex("F4E3B5"));
            }

            // bowler at the far end, batter beside the line
            Disc(new Vector2(-70f, StartY + 70f), 90f, Theme.Palette[0]);
            Ui.Label(Field, bot.Label + " bowling", 30, Theme.Text, new Vector2(230f, StartY + 70f), new Vector2(420f, 44f));
            Disc(new Vector2(120f, BatY - 30f), 100f, Theme.Palette[1]);
            bat = Box(new Vector2(60f, BatY), new Vector2(34f, 180f), Theme.Hex("E8C27A"));
            ball = Disc(Vector2.zero, 46f, Theme.Bad);
            ball.Show(false);

            scoreLine = Label("", new Vector2(0f, 735f), 54, Theme.Text, 1040f);
            needLine = Label("", new Vector2(0f, 678f), 36, Theme.Coin, 1040f);
            status = Label("", new Vector2(0f, 150f), 60, Theme.Text, 1040f);
            for (var i = 0; i < Balls; i++)
            {
                var x = (i - (Balls - 1) * 0.5f) * 78f + (i >= 6 ? 20f : -20f); // a gap between the two overs
                Box(new Vector2(x, -690f), new Vector2(68f, 68f), Theme.Panel);
                history[i] = Label("", new Vector2(x, -690f), 38, Theme.Text, 70f);
            }
            Ui.Label(Field, "Tap as the ball crosses the yellow line", 30, Theme.TextDim, new Vector2(0f, -770f), new Vector2(1000f, 44f));
            Ui.Label(Field, "Miss a straight ball and you are out", 30, Theme.TextDim, new Vector2(0f, -815f), new Vector2(1000f, 44f));

            phase = Phase.Intro;
            timer = 1.8f;
            status.text = bot.Name + " made " + botScore + ". Chase it!";
            ShowScore();
            SetBat(false);
        }

        void ShowScore()
        {
            scoreLine.text = "YOU  " + runs + "/" + wickets + "     Target " + (botScore + 1);
            var need = botScore + 1 - runs;
            var left = Balls - ballsBowled;
            needLine.text = need > 0 ? "Need " + need + " from " + left + (left == 1 ? " ball" : " balls") + "   Wickets left " + (Wickets - wickets) : "";
        }

        void SetBat(bool swinging)
        {
            bat.SetRot(swinging ? 75f : -20f);
            bat.SetPos(swinging ? new Vector2(20f, BatY + 20f) : new Vector2(60f, BatY));
        }

        // ---------- flow ----------

        protected override void OnTick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Intro:
                    if (timer <= 0f) NextBall();
                    break;

                case Phase.RunUp:
                    if (timer <= 0f) Release();
                    break;

                case Phase.Flight:
                    ballTime += dt;
                    if (PointerDown) Swing();
                    if (phase == Phase.Flight)
                    {
                        var t = ballTime / flight;
                        ballPos = new Vector2(Mathf.Lerp(fromX, toX, t), Mathf.Lerp(StartY, StumpY, t));
                        if (ballTime >= flight) Missed();
                    }
                    break;

                case Phase.Result:
                    ballPos += ballVel * dt;
                    if (timer <= 0f) AfterBall();
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
            ball.SetPos(ballPos);
        }

        void NextBall()
        {
            phase = Phase.RunUp;
            timer = Rand(0.6f, 1.1f); // an uneven run-up, so the release cannot be counted in
            status.text = "";
            swung = false;
            SetBat(false);
            ball.Show(false);
            foreach (var s in stumps) s.color = Theme.Hex("F4E3B5");
        }

        /// <summary>The bowler lets go. Better bowlers vary their pace more and bowl straighter.</summary>
        void Release()
        {
            phase = Phase.Flight;
            ballTime = 0f;
            flight = Rand(Mathf.Lerp(0.85f, 0.6f, bot.Skill), 1.1f);
            hitTime = flight * (StartY - BatY) / (StartY - StumpY);
            onStumps = Rng.NextDouble() < Mathf.Lerp(0.45f, 0.75f, bot.Skill);
            fromX = Rand(-50f, 50f);
            toX = onStumps ? Rand(-22f, 22f) : (Rng.Next(2) == 0 ? -1f : 1f) * WideX + Rand(-10f, 10f);
            ballPos = new Vector2(fromX, StartY);
            ballVel = Vector2.zero;
            ball.Show(true);
            ball.SetPos(ballPos);
            Sfx.Blip(300f, 0.05f, 0.2f);
        }

        /// <summary>The batter swings: one swing per ball, judged by how close it is to the perfect moment.</summary>
        void Swing()
        {
            if (phase != Phase.Flight || swung) return;
            swung = true;
            SetBat(true);
            var error = Mathf.Abs(ballTime - hitTime);
            if (error > EdgeWindow) return; // fresh air; the ball carries on

            if (error > OneWindow)
            {
                // a thin edge: sometimes it carries to the keeper
                if (Rng.NextDouble() < 0.4) Out("Caught behind!");
                else BallDone(0, "Edged, no run", Theme.TextDim, new Vector2(Rand(-200f, 200f), -500f));
                return;
            }

            var scored = error <= SixWindow ? 6 : error <= FourWindow ? 4 : error <= TwoWindow ? 2 : 1;
            var angle = Rand(-70f, 70f) * Mathf.Deg2Rad;
            var speed = scored == 6 ? 2600f : scored == 4 ? 1900f : 900f;
            runs += scored;
            Score += scored * 10;
            BallDone(scored, scored == 6 ? "SIX!" : scored == 4 ? "FOUR!" : scored == 2 ? "2 runs" : "1 run",
                scored >= 4 ? Theme.Coin : Theme.Good, new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * speed);
            if (scored >= 4)
            {
                Sfx.Great();
                Shake(scored == 6 ? 14f : 8f, 0.2f);
            }
            else Sfx.Good();
        }

        /// <summary>The ball has gone past the bat untouched.</summary>
        void Missed()
        {
            if (onStumps)
            {
                foreach (var s in stumps) s.color = Theme.Bad;
                Out("Bowled!");
            }
            else BallDone(0, swung ? "Missed" : "Left alone", Theme.TextDim, new Vector2(0f, -900f));
        }

        void Out(string how)
        {
            wickets++;
            BallDone(-1, "OUT! " + how, Theme.Bad, Vector2.zero);
            Sfx.Bad();
            Shake(12f, 0.25f);
        }

        /// <summary>Records one ball: runs scored, or -1 for a wicket.</summary>
        void BallDone(int scored, string message, Color colour, Vector2 velocity)
        {
            history[ballsBowled].text = scored < 0 ? "W" : scored == 0 ? "-" : scored.ToString();
            history[ballsBowled].color = scored < 0 ? Theme.Bad : scored >= 4 ? Theme.Coin : Theme.Text;
            ballsBowled++;
            ballVel = velocity;
            status.text = message;
            status.color = colour;
            ShowScore();
            phase = Phase.Result;
            timer = 0.9f;
        }

        void AfterBall()
        {
            if (runs > botScore) GameOver(true, "YOU WIN!");
            else if (wickets >= Wickets || ballsBowled >= Balls)
                GameOver(false, runs == botScore ? "Tied!" : wickets >= Wickets ? "All out" : bot.Name + " wins");
            else NextBall();
        }

        void GameOver(bool win, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = win;
            status.text = "";
            ball.Show(false);
            Box(new Vector2(0f, 150f), new Vector2(940f, 300f), Theme.Dark);
            Label(message, new Vector2(0f, 200f), 96, won ? Theme.Coin : Theme.Text, 900f);
            if (won)
            {
                var bonus = 300 + (Balls - ballsBowled) * 20;
                Score += bonus;
                Label("+" + bonus, new Vector2(0f, 80f), 64, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else
            {
                Label("You made " + runs + ", needed " + (botScore + 1), new Vector2(0f, 80f), 44, Theme.TextDim, 900f);
                Sfx.Bad();
            }
        }
    }
}
