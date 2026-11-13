using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.PenaltyShootout
{
    /// <summary>
    /// A penalty shootout against a computer opponent. Each round you take one kick (swipe towards
    /// the spot you want; a faster swipe is harder to save but less accurate) and then face one
    /// (drag the keeper to the ball). Best of five kicks each, then sudden death while level.
    ///
    /// Score: 100 per goal scored, 100 per save, 300 for winning.
    /// </summary>
    [MiniGame("penalty_shootout", "Penalty Shootout", Kind = "Sports", Colour = "7CFF4D", Order = 10,
        MaxScorePerSecond = 15f, ScoreBase = 1500f, MinDuration = 15f, MissionScore = 400)]
    public class PenaltyShootoutGame : MiniGameBase
    {
        const int Kicks = 5;      // kicks each before sudden death
        const int MaxKicks = 10;  // still level after this many each: a draw

        // the goal mouth, seen from the penalty spot
        const float PostX = 400f, GroundY = 260f, BarY = 620f;
        const float BallR = 45f;
        const float Reach = 115f;       // a keeper stops anything this close to their body
        const float KeeperLimitX = 340f, KeeperMinY = 310f, KeeperMaxY = 560f;
        const float BotKeeperSpeed = 700f;
        const float MyKeeperSpeed = 1700f;
        const float AimSeconds = 8f;
        const float BarWidth = 600f;

        static readonly Vector2 Spot = new Vector2(0f, -470f);
        static readonly Vector2 KeeperHome = new Vector2(0f, GroundY + 95f);

        enum Phase { Aim, Ready, Flight, Pause, Over }

        Bot bot;
        Color myColour, botColour;

        Phase phase;
        bool myKick;             // true while the player is the one shooting
        float timer, flightT, flightTime, pressedAt;
        Vector2 target, ballPos, ballVel;
        Vector2 keeperPos, keeperGoal, dragFrom;
        float keeperDelay;
        int myKicks, botKicks, myGoals, botGoals;
        bool won;

        RectTransform keeper;
        Image keeperBody, keeperHead, ball, timeBar;
        Text status;
        readonly Image[] myDots = new Image[Kicks];
        readonly Image[] botDots = new Image[Kicks];

        protected override string Hint => "Swipe shoots. Drag saves.";
        protected override Color Background => Theme.Hex("17703A");

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1)[0];
            myColour = Theme.Palette[1];
            botColour = Theme.Palette[0];

            // pitch markings, net and goal frame
            Box(new Vector2(0f, GroundY - 6f), new Vector2(1080f, 12f), Color.white.Alpha(0.7f), false);
            Box(new Vector2(0f, GroundY - 330f), new Vector2(900f, 8f), Color.white.Alpha(0.35f), false);
            Box(new Vector2(0f, (GroundY + BarY) * 0.5f), new Vector2(PostX * 2f, BarY - GroundY), Theme.Dark.Alpha(0.45f), false);
            Box(new Vector2(-PostX - 12f, (GroundY + BarY) * 0.5f + 12f), new Vector2(28f, BarY - GroundY + 28f), Color.white);
            Box(new Vector2(PostX + 12f, (GroundY + BarY) * 0.5f + 12f), new Vector2(28f, BarY - GroundY + 28f), Color.white);
            Box(new Vector2(0f, BarY + 12f), new Vector2(PostX * 2f + 52f, 28f), Color.white);
            Disc(Spot + new Vector2(0f, -30f), 40f, Color.white.Alpha(0.6f));

            keeper = Ui.Node(Field, "Keeper", KeeperHome, new Vector2(120f, 190f));
            keeperBody = Ui.Box(keeper, botColour, new Vector2(0f, -25f), new Vector2(110f, 140f));
            keeperHead = Ui.Disc(keeper, botColour, new Vector2(0f, 62f), 76f);
            keeperPos = KeeperHome;

            ball = Disc(Spot, BallR * 2f, Color.white);
            Ui.Disc(ball.transform, Theme.Dark, Vector2.zero, BallR * 0.7f);

            status = Label("", new Vector2(0f, -640f), 52, Theme.Text, 1040f);
            Box(new Vector2(0f, -710f), new Vector2(BarWidth, 14f), Theme.Dark.Alpha(0.5f));
            timeBar = Box(new Vector2(0f, -710f), new Vector2(BarWidth, 14f), Theme.Coin);

            BuildScoreRow("YOU", myColour, -810f, myDots);
            BuildScoreRow(bot.Label, botColour, -890f, botDots);

            StartKick();
        }

        void BuildScoreRow(string who, Color colour, float y, Image[] dots)
        {
            Ui.Label(Field, who, 40, colour, new Vector2(-270f, y), new Vector2(460f, 60f), TextAnchor.MiddleRight, null, true);
            for (var i = 0; i < Kicks; i++) dots[i] = Disc(new Vector2(30f + i * 80f, y), 58f, Theme.PanelLight);
        }

        // ---------- flow ----------

        /// <summary>Sets up the next kick: the player shoots first in every pair.</summary>
        void StartKick()
        {
            myKick = myKicks == botKicks;
            var kicks = myKick ? myKicks : botKicks;
            if (kicks >= Kicks && kicks % Kicks == 0)
                foreach (var d in myKick ? myDots : botDots) d.color = Theme.PanelLight; // fresh row for sudden death

            ballPos = Spot;
            ballVel = Vector2.zero;
            ball.SetScale(1f);
            keeperPos = keeperGoal = KeeperHome;
            keeperBody.color = keeperHead.color = myKick ? botColour : myColour;

            var sudden = kicks >= Kicks ? "Sudden death! " : "";
            if (myKick)
            {
                phase = Phase.Aim;
                timer = AimSeconds;
                pressedAt = -1f;
                status.text = sudden + "Swipe to shoot!";
                status.color = myColour;
            }
            else
            {
                phase = Phase.Ready;
                timer = 1.6f;
                status.text = sudden + bot.Name + " shoots. Drag to save!";
                status.color = botColour;
            }
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Aim:
                    if (PointerDown) pressedAt = Elapsed;
                    if (PointerUp && pressedAt >= 0f)
                    {
                        var swipe = PointerPos - PointerDownPos;
                        if (swipe.y > 60f && swipe.magnitude > 100f) PlayerShot(swipe, Elapsed - pressedAt);
                    }
                    else if (timer <= 0f) Resolve(false, "Too slow!");
                    break;

                case Phase.Ready:
                    MoveMyKeeper(dt);
                    if (timer <= 0f) BotShot();
                    break;

                case Phase.Flight:
                    if (myKick) MoveBotKeeper(dt);
                    else MoveMyKeeper(dt);

                    flightT += dt / flightTime;
                    var k = Mathf.Clamp01(flightT);
                    ballPos = Vector2.Lerp(Spot, target, k) + new Vector2(0f, Mathf.Sin(Mathf.PI * k) * 70f);
                    ball.SetScale(Mathf.Lerp(1f, 0.6f, k)); // shrinks as it flies away
                    if (flightT >= 1f) Land();
                    break;

                case Phase.Pause:
                    ballPos += ballVel * dt;
                    ballVel *= Mathf.Exp(-3f * dt);
                    if (timer <= 0f) Next();
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
            keeper.anchoredPosition = keeperPos;
            keeper.SetRot(-Mathf.Clamp((keeperPos.x - KeeperHome.x) / KeeperLimitX, -1f, 1f) * 55f);

            var aiming = phase == Phase.Aim;
            timeBar.Show(aiming);
            if (aiming) timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / AimSeconds)), 14f);
        }

        // ---------- shooting ----------

        /// <summary>
        /// The player's kick. The ball goes where the swipe points, a little over twice as far as
        /// the finger travelled. Faster swipes fly faster but scatter more.
        /// </summary>
        void PlayerShot(Vector2 swipe, float seconds)
        {
            var speed = swipe.magnitude / Mathf.Max(0.03f, seconds);
            var power = Mathf.InverseLerp(900f, 3500f, speed);
            var scatterAngle = Rand(0f, Mathf.PI * 2f);
            var scatter = new Vector2(Mathf.Cos(scatterAngle), Mathf.Sin(scatterAngle)) * (Rand(0f, 1f) * power * power * 120f);
            target = Spot + swipe * 2.3f + scatter;
            flightTime = Mathf.Lerp(0.8f, 0.42f, power);

            // the bot keeper reads the shot after a moment, or guesses the wrong side entirely
            keeperGoal = target;
            if (bot.Blunders(Rng)) keeperGoal.x = -keeperGoal.x;
            keeperGoal = ClampKeeper(keeperGoal);
            keeperDelay = Mathf.Lerp(0.34f, 0.2f, bot.Skill);
            Launch();
        }

        /// <summary>The bot's kick. Better bots hit the corners harder; any bot can scuff one wide.</summary>
        void BotShot()
        {
            var side = Rng.NextDouble() < 0.5 ? -1f : 1f;
            target = new Vector2(
                side * Rand(Mathf.Lerp(110f, 240f, bot.Skill), 355f),
                Rand(GroundY + 50f, Mathf.Lerp(450f, 575f, bot.Skill)));
            if (bot.Blunders(Rng) && Rng.NextDouble() < 0.4)
            {
                if (Rng.NextDouble() < 0.5) target.x = side * Rand(PostX + 30f, PostX + 120f);
                else target.y = BarY + Rand(40f, 110f);
            }
            flightTime = Mathf.Lerp(0.88f, 0.62f, bot.Skill) + Rand(-0.04f, 0.04f);
            Launch();
        }

        void Launch()
        {
            phase = Phase.Flight;
            flightT = 0f;
            status.text = "";
            Sfx.Blip(220f, 0.08f, 0.35f);
        }

        // ---------- keeping ----------

        static Vector2 ClampKeeper(Vector2 p) =>
            new Vector2(Mathf.Clamp(p.x, -KeeperLimitX, KeeperLimitX), Mathf.Clamp(p.y, KeeperMinY, KeeperMaxY));

        void MoveBotKeeper(float dt)
        {
            if (flightT * flightTime < keeperDelay) return;
            keeperPos = Vector2.MoveTowards(keeperPos, keeperGoal, BotKeeperSpeed * dt);
        }

        /// <summary>The player's keeper follows the finger from wherever it was put down, a bit faster than the finger moves.</summary>
        void MoveMyKeeper(float dt)
        {
            if (PointerDown) dragFrom = keeperPos;
            if (!PointerHeld) return;
            var goal = ClampKeeper(dragFrom + (PointerPos - PointerDownPos) * 1.4f);
            keeperPos = Vector2.MoveTowards(keeperPos, goal, MyKeeperSpeed * dt);
        }

        // ---------- results ----------

        void Land()
        {
            ballPos = target;
            var onTarget = Mathf.Abs(target.x) < PostX - 20f && target.y < BarY - 20f && target.y > GroundY;
            if (!onTarget)
            {
                Resolve(false, target.y >= BarY - 20f ? "Over the bar!" : "Wide!");
                return;
            }
            if (Vector2.Distance(keeperPos, target) < Reach)
            {
                ballVel = new Vector2(target.x >= keeperPos.x ? 260f : -260f, -520f); // parried away
                Resolve(false, "Saved!");
                return;
            }
            Resolve(true, "");
        }

        void Resolve(bool goal, string missText)
        {
            var saved = missText == "Saved!";
            var where = new Vector2(0f, 60f);
            if (myKick)
            {
                myDots[myKicks % Kicks].color = goal ? Theme.Good : Theme.Bad;
                myKicks++;
                if (goal)
                {
                    myGoals++;
                    Score += 100;
                    Pop("GOAL! +100", where, Theme.Good, 110);
                    Flash(Theme.Good, 0.25f);
                    Sfx.Great();
                }
                else
                {
                    Pop(missText, where, Theme.Text, 96);
                    Sfx.Bad();
                }
            }
            else
            {
                botDots[botKicks % Kicks].color = goal ? Theme.Good : Theme.Bad;
                botKicks++;
                if (goal)
                {
                    botGoals++;
                    Pop(bot.Name + " scores", where, botColour, 90);
                    Shake(16f, 0.3f);
                    Sfx.Bad();
                }
                else if (saved)
                {
                    Score += 100;
                    Pop("SAVED! +100", where, Theme.Good, 110);
                    Flash(Theme.Good, 0.25f);
                    Sfx.Great();
                }
                else
                {
                    Pop(missText, where, Theme.Text, 96);
                    Sfx.Good();
                }
            }
            phase = Phase.Pause;
            timer = 1.2f;
            status.text = "";
        }

        /// <summary>Ends the shootout if it is decided, otherwise sets up the next kick.</summary>
        void Next()
        {
            var decided = false;
            if (myKicks <= Kicks && botKicks <= Kicks)
            {
                // first five each: stop as soon as one side cannot be caught
                decided = myGoals > botGoals + (Kicks - botKicks) || botGoals > myGoals + (Kicks - myKicks);
            }
            else if (myKicks == botKicks)
            {
                decided = myGoals != botGoals || myKicks >= MaxKicks; // sudden death, pair by pair
            }

            if (decided) GameOver();
            else StartKick();
        }

        void GameOver()
        {
            phase = Phase.Over;
            timer = 2.2f;
            won = myGoals > botGoals;
            var message = won ? "YOU WIN!" : myGoals == botGoals ? "Draw" : bot.Name + " wins";
            Box(new Vector2(0f, -120f), new Vector2(940f, 300f), Theme.Dark);
            Label(message, new Vector2(0f, won ? -60f : -90f), 96, won ? Theme.Coin : Theme.Text, 900f);
            Label(myGoals + " - " + botGoals, new Vector2(0f, won ? -150f : -180f), 60, Theme.TextDim, 900f);
            if (won)
            {
                Score += 300;
                Label("+300", new Vector2(0f, -225f), 56, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else Sfx.Bad();
        }
    }
}
