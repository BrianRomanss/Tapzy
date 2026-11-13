using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.AirHockey
{
    /// <summary>
    /// One-on-one air hockey against a computer opponent. Drag the paddle around the bottom half,
    /// knock the puck into the top goal. First to five goals wins; if the 90 second clock runs
    /// out, whoever leads wins.
    ///
    /// Score: 100 per goal scored, 300 for winning.
    /// </summary>
    [MiniGame("air_hockey", "Air Hockey", Kind = "Sports", Colour = "FF3D7F", Order = 9,
        MaxScorePerSecond = 1f, ScoreBase = 900f, MinDuration = 10f, MissionScore = 300)]
    public class AirHockeyGame : MiniGameBase
    {
        const int GoalsToWin = 5;
        const float MatchSeconds = 90f;

        // rink walls
        const float Left = -490f, Right = 490f, Bottom = -900f, Top = 640f;
        const float Mid = (Top + Bottom) * 0.5f;
        const float GoalHalf = 215f;

        const float PuckR = 42f;
        const float PaddleR = 78f;
        const float PuckMaxSpeed = 2400f;
        const float PlayerMaxSpeed = 5000f;
        const float WallBounce = 0.9f;
        const float PaddleBounce = 0.85f;
        const int Substeps = 4;

        Bot bot;
        Color myColour, botColour;

        Vector2 puck, puckVel;
        Vector2 me, meVel, grabOffset;
        Vector2 foe, foeVel, foeAim;
        float shotAimX;
        bool striking;

        int myGoals, foeGoals;
        float clock = MatchSeconds, freeze, stall, lastHitSound = -1f;
        bool over, won;
        float overTimer;

        Image puckImage, meImage, foeImage;
        Text clockText, myGoalsText, foeGoalsText;

        protected override string Hint => "Drag. Score five goals.";

        static Vector2 MyHome => new Vector2(0f, Bottom + 230f);
        static Vector2 FoeHome => new Vector2(0f, Top - 200f);

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1)[0];
            myColour = Theme.Palette[1];
            botColour = Theme.Palette[0];

            var size = new Vector2(Right - Left, Top - Bottom);
            var centre = new Vector2(0f, Mid);
            // goal mouths peek out behind the rink, in the colour of the player who defends them
            Box(new Vector2(0f, Top + 6f), new Vector2(GoalHalf * 2f, 60f), botColour);
            Box(new Vector2(0f, Bottom - 6f), new Vector2(GoalHalf * 2f, 60f), myColour);
            Box(centre, size + new Vector2(16f, 16f), Theme.Panel);
            Box(new Vector2(0f, Top), new Vector2(GoalHalf * 2f - 16f, 16f), botColour, false);
            Box(new Vector2(0f, Bottom), new Vector2(GoalHalf * 2f - 16f, 16f), myColour, false);
            Box(centre, new Vector2(size.x - 8f, 8f), Theme.PanelLight, false);
            Disc(centre, 300f, Theme.PanelLight);
            Disc(centre, 250f, Theme.Panel);

            clockText = Label("", new Vector2(-370f, Mid + 80f), 64, Theme.TextDim, 240f);
            foeGoalsText = Label("0", new Vector2(390f, Mid + 100f), 120, botColour, 200f);
            myGoalsText = Label("0", new Vector2(390f, Mid - 100f), 120, myColour, 200f);
            Label("YOU  vs  " + bot.Label, new Vector2(0f, Top + 70f), 40, Theme.Text, 1000f);

            me = MyHome;
            foe = foeAim = FoeHome;
            meImage = Paddle(me, myColour);
            foeImage = Paddle(foe, botColour);
            puck = new Vector2(0f, Mid - 280f); // the player serves first
            puckImage = Disc(puck, PuckR * 2f, Theme.Coin);
            Ui.Disc(puckImage.transform, Theme.Coin.Shade(0.8f), Vector2.zero, PuckR);
            NewShotAim();
            ShowClock();
        }

        Image Paddle(Vector2 pos, Color colour)
        {
            var img = Disc(pos, PaddleR * 2f, colour);
            Ui.Disc(img.transform, colour.Shade(0.7f), Vector2.zero, PaddleR * 0.9f);
            return img;
        }

        // ---------- main loop ----------

        protected override void OnTick(float dt)
        {
            if (over)
            {
                overTimer -= dt;
                if (overTimer > 0f) return;
                EndRound();
                if (won)
                {
                    // EndRound treats every ending as a failure; a win should not look like one
                    Shake(0f, 0.01f);
                    Flash(Theme.Good, 0.3f);
                }
                return;
            }

            clock -= dt;
            ShowClock();
            if (clock <= 0f)
            {
                EndMatch();
                return;
            }

            // both paddles travel in a straight line this frame; the substeps share that motion
            var meStart = me;
            var meEnd = PlayerTarget(dt);
            meVel = Vector2.Lerp(meVel, (meEnd - meStart) / dt, 0.6f);
            var foeStart = foe;
            var foeEnd = BotTarget(dt);
            foeVel = (foeEnd - foeStart) / dt;

            if (freeze > 0f) freeze -= dt;
            var h = dt / Substeps;
            for (var i = 1; i <= Substeps && !over; i++)
            {
                me = Vector2.Lerp(meStart, meEnd, (float)i / Substeps);
                foe = Vector2.Lerp(foeStart, foeEnd, (float)i / Substeps);
                if (freeze > 0f) continue;

                puck += puckVel * h;
                Hit(me, meVel, true);
                Hit(foe, foeVel, false);
                if (Walls()) break; // a goal was scored
            }

            if (freeze <= 0f)
            {
                puckVel *= Mathf.Exp(-0.22f * dt); // the table is slippery, not frictionless
                Unstick(dt);
            }

            meImage.SetPos(me);
            foeImage.SetPos(foe);
            puckImage.SetPos(puck);
        }

        // ---------- paddles ----------

        /// <summary>Where the player's paddle ends up this frame: it follows the finger, keeping the offset it was grabbed with.</summary>
        Vector2 PlayerTarget(float dt)
        {
            if (!PointerHeld) return me;
            if (PointerDown) grabOffset = me - PointerPos;
            var target = PointerPos + grabOffset;
            target.x = Mathf.Clamp(target.x, Left + PaddleR, Right - PaddleR);
            target.y = Mathf.Clamp(target.y, Bottom + PaddleR, Mid - PaddleR);
            grabOffset = target - PointerPos; // pushing against a wall does not build up slack
            return Vector2.MoveTowards(me, target, PlayerMaxSpeed * dt);
        }

        /// <summary>Where the bot's paddle ends up this frame. Better bots react faster and move faster.</summary>
        Vector2 BotTarget(float dt)
        {
            Vector2 target;
            var inMyHalf = puck.y > Mid - 40f;
            if (freeze > 0f) target = FoeHome;
            else if (inMyHalf && puckVel.y > 900f)
            {
                // puck is flying at the bot's goal: get between it and the goal
                target = new Vector2(Mathf.Clamp(puck.x, -GoalHalf, GoalHalf) * 0.9f, Top - 170f);
            }
            else if (inMyHalf)
            {
                var goal = new Vector2(shotAimX, Bottom);
                var dir = (goal - puck).normalized;
                if (puck.y > Top - 150f)
                {
                    // puck hugging the back wall: chasing it would pin it there, so step aside along
                    // the wall (covering the goal) and let it come loose
                    var away = puck.x > 0f ? -1f : 1f;
                    target = new Vector2(puck.x + away * 260f, Top - PaddleR - 10f);
                }
                else if (foe.y > puck.y + 30f)
                {
                    // line up behind the puck, then drive through it towards the player's goal
                    if (Vector2.Dot((puck - foe).normalized, dir) > 0.99f) striking = true;
                    target = striking ? puck + dir * 400f : puck - dir * (PuckR + PaddleR + 70f);
                }
                else
                {
                    // wrong side of the puck: go around it rather than knock it into the bot's own goal
                    var side = foe.x >= puck.x ? 1f : -1f;
                    target = puck + new Vector2(side * 170f, 190f);
                }
            }
            else target = new Vector2(Mathf.Clamp(puck.x * 0.5f, -200f, 200f), Top - 230f);

            // a committed strike goes in at full speed; everything else eases in, slower for weaker bots
            striking &= inMyHalf && freeze <= 0f && puckVel.y <= 900f && foe.y > puck.y + 30f && puck.y <= Top - 150f;
            var react = Mathf.Lerp(5f, 14f, bot.Skill);
            foeAim = striking ? target : Vector2.Lerp(foeAim, target, 1f - Mathf.Exp(-react * dt));
            var aim = new Vector2(
                Mathf.Clamp(foeAim.x, Left + PaddleR, Right - PaddleR),
                Mathf.Clamp(foeAim.y, Mid + PaddleR, Top - PaddleR));
            return Vector2.MoveTowards(foe, aim, Mathf.Lerp(1000f, 2000f, bot.Skill) * dt);
        }

        /// <summary>Picks where the bot's next shot goes: the side of the goal the player has left open, unless it slips up.</summary>
        void NewShotAim()
        {
            if (bot.Blunders(Rng)) shotAimX = Rand(-GoalHalf, GoalHalf);
            else shotAimX = (me.x > 0f ? -1f : 1f) * GoalHalf * 0.5f;
        }

        // ---------- puck ----------

        void Hit(Vector2 paddle, Vector2 paddleVel, bool mine)
        {
            var d = puck - paddle;
            var dist = d.magnitude;
            const float min = PuckR + PaddleR;
            if (dist >= min) return;

            var n = dist > 0.001f ? d / dist : (mine ? Vector2.up : Vector2.down);
            puck = paddle + n * min;
            var closing = Vector2.Dot(puckVel - paddleVel, n);
            if (closing >= 0f) return;

            puckVel -= (1f + PaddleBounce) * closing * n;
            puckVel = Vector2.ClampMagnitude(puckVel, PuckMaxSpeed);
            if (!mine)
            {
                striking = false;
                NewShotAim();
            }
            HitSound(mine ? 520f : 440f, -closing);
        }

        /// <summary>Bounces the puck off the walls. Returns true if it went into a goal.</summary>
        bool Walls()
        {
            if (puck.x < Left + PuckR)
            {
                puck.x = Left + PuckR;
                puckVel.x = Mathf.Abs(puckVel.x) * WallBounce;
                HitSound(300f, Mathf.Abs(puckVel.x));
            }
            else if (puck.x > Right - PuckR)
            {
                puck.x = Right - PuckR;
                puckVel.x = -Mathf.Abs(puckVel.x) * WallBounce;
                HitSound(300f, Mathf.Abs(puckVel.x));
            }

            var inMouth = Mathf.Abs(puck.x) <= GoalHalf - PuckR * 0.5f;
            if (puck.y > Top - PuckR)
            {
                if (inMouth)
                {
                    if (puck.y > Top + PuckR)
                    {
                        Goal(true);
                        return true;
                    }
                }
                else
                {
                    puck.y = Top - PuckR;
                    puckVel.y = -Mathf.Abs(puckVel.y) * WallBounce;
                    HitSound(300f, Mathf.Abs(puckVel.y));
                }
            }
            else if (puck.y < Bottom + PuckR)
            {
                if (inMouth)
                {
                    if (puck.y < Bottom - PuckR)
                    {
                        Goal(false);
                        return true;
                    }
                }
                else
                {
                    puck.y = Bottom + PuckR;
                    puckVel.y = Mathf.Abs(puckVel.y) * WallBounce;
                    HitSound(300f, Mathf.Abs(puckVel.y));
                }
            }
            return false;
        }

        /// <summary>A puck left lying still gets a push towards the other side, so nobody can stall the match.</summary>
        void Unstick(float dt)
        {
            if (puckVel.magnitude > 80f)
            {
                stall = 0f;
                return;
            }
            stall += dt;
            var mine = puck.y < Mid;
            var wait = mine ? 5f : puck.y > Top - 150f ? 1.2f : 3f;
            if (stall < wait) return;
            stall = 0f;
            var towards = new Vector2(Rand(-200f, 200f), mine ? Mid + 300f : Mid - 300f);
            puckVel = (towards - puck).normalized * 700f;
        }

        void HitSound(float pitch, float strength)
        {
            if (strength < 150f || Elapsed - lastHitSound < 0.06f) return;
            lastHitSound = Elapsed;
            Sfx.Blip(pitch, 0.04f, Mathf.Lerp(0.1f, 0.3f, strength / PuckMaxSpeed));
        }

        // ---------- goals and endings ----------

        void Goal(bool mine)
        {
            if (mine)
            {
                myGoals++;
                Score += 100;
                Pop("GOAL! +100", new Vector2(0f, Mid + 200f), Theme.Good, 110);
                Flash(Theme.Good, 0.25f);
                Sfx.Great();
            }
            else
            {
                foeGoals++;
                Pop(bot.Name + " scores", new Vector2(0f, Mid - 200f), botColour, 90);
                Shake(18f, 0.3f);
                Sfx.Bad();
            }
            myGoalsText.text = myGoals.ToString();
            foeGoalsText.text = foeGoals.ToString();

            // whoever conceded serves
            puck = new Vector2(0f, mine ? Mid + 280f : Mid - 280f);
            puckVel = Vector2.zero;
            puckImage.SetPos(puck);
            stall = 0f;
            freeze = 1f;

            if (myGoals >= GoalsToWin || foeGoals >= GoalsToWin) EndMatch();
        }

        void EndMatch()
        {
            over = true;
            overTimer = 2.2f;
            won = myGoals > foeGoals;
            var message = won ? "YOU WIN!" : myGoals == foeGoals ? "Draw" : bot.Name + " wins";
            Box(new Vector2(0f, Mid), new Vector2(940f, 280f), Theme.Dark);
            Label(message, new Vector2(0f, Mid + (won ? 40f : 0f)), 96, won ? Theme.Coin : Theme.Text, 900f);
            if (won)
            {
                Score += 300;
                Label("+300", new Vector2(0f, Mid - 70f), 64, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else Sfx.Bad();
        }

        void ShowClock()
        {
            var seconds = Mathf.CeilToInt(Mathf.Max(0f, clock));
            clockText.text = seconds / 60 + ":" + (seconds % 60).ToString("00");
            clockText.color = clock <= 10f ? Theme.Bad : Theme.TextDim;
        }
    }
}
