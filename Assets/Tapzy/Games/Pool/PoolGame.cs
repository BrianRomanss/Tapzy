using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.Pool
{
    /// <summary>
    /// A short game of pool against a computer opponent. You play the four blue balls, the bot
    /// plays the four pink ones, and the black ball decides the game. Press anywhere, pull back
    /// and let go to shoot the white cue ball the opposite way.
    ///
    /// Rules: pot one of your own balls and you shoot again. A ball of the other colour still
    /// counts for its owner. Potting the cue ball, or hitting nothing at all, is a foul: the
    /// other side gets two shots. Once your four are down, pot the black to win. Potting the
    /// black any earlier, or together with a foul, loses the game. After 60 shots in total,
    /// whoever has potted more wins.
    ///
    /// Score: 40 per own ball, 300 for winning, minus 20 per foul.
    /// </summary>
    [MiniGame("pool", "Pool", Kind = "Sports", Colour = "1FCB8A", Order = 14,
        MaxScorePerSecond = 3f, ScoreBase = 600f, MinDuration = 15f, MissionScore = 200)]
    public class PoolGame : MiniGameBase
    {
        const int Mine = 0, Theirs = 1, Black = 2, Cue = 3;
        const int BallsEach = 4;
        const int MaxTurns = 60;

        // table, in units from its centre
        const float HalfW = 380f, HalfH = 570f;
        const float BallR = 26f;
        const float PocketCatch = 42f;
        const float Rail = 36f;

        // physics
        const float Friction = 300f;        // steady slowing, units per second squared
        const float Drag = 0.5f;            // extra slowing in proportion to speed
        const float MaxShot = 2800f, MinShot = 350f;
        const float BallBounce = 0.95f, WallBounce = 0.7f;
        const float StepSeconds = 1f / 240f;

        const float HumanSeconds = 20f;
        const int IdleLimit = 3;            // turns the player may sleep through before the round is forfeited
        const float BarWidth = 600f;

        static readonly Vector2 TableCentre = new Vector2(0f, 150f);
        static readonly Vector2 HeadSpot = new Vector2(0f, -HalfH * 0.5f);
        static readonly Vector2 FootSpot = new Vector2(0f, HalfH * 0.42f);
        static readonly Vector2[] Pockets =
        {
            new Vector2(-HalfW + 20f, -HalfH + 20f), new Vector2(HalfW - 20f, -HalfH + 20f),
            new Vector2(-HalfW + 20f, HalfH - 20f), new Vector2(HalfW - 20f, HalfH - 20f),
            new Vector2(-HalfW + 4f, 0f), new Vector2(HalfW - 4f, 0f) // the two side pockets come last
        };

        enum Phase { Aim, BotThink, Moving, Pause, Over }

        class Piece
        {
            public int kind;
            public Vector2 pos, vel;
            public bool active = true;
            public Image image;
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly List<Piece> potted = new List<Piece>();
        readonly int[] pocketed = new int[2];
        readonly Image[] aimDots = new Image[10];
        Image ghost;
        Piece cue, blackBall;
        Bot bot;

        Phase phase;
        int turn;                // 0 = player, 1 = bot
        int extraShots;          // shots still owed to the side at the table after a foul
        int turnsTaken, idleStreak, ownLeftBefore;
        int winner = -1;         // set by the black ball
        float timer, moveTime, lastClick = -1f;
        bool pulling, won, hitSomething, shootAgain;
        string endReason;
        Vector2 botShot;

        Text status, myTally, botTally;
        Image timeBar;

        protected override string Hint => "Pull back. Pot the blues.";

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1, 0.25f, 0.8f)[0];
            BuildTable();

            // a diamond of nine at the far end: black in the middle, colours mixed around it
            var layout = new[] { Mine, Theirs, Mine, Mine, Black, Theirs, Theirs, Mine, Theirs };
            var flip = Rng.Next(2) == 1;
            var counts = new[] { 1, 2, 3, 2, 1 };
            var gap = BallR * 2f + 1f;
            var n = 0;
            for (var row = 0; row < counts.Length; row++)
            {
                for (var i = 0; i < counts[row]; i++)
                {
                    var kind = layout[n++];
                    if (flip && kind != Black) kind = 1 - kind;
                    var pos = FootSpot + new Vector2((i - (counts[row] - 1) * 0.5f) * gap, (row - 2) * gap * 0.866f);
                    AddBall(kind, pos);
                }
            }
            cue = AddBall(Cue, HeadSpot);

            for (var i = 0; i < aimDots.Length; i++)
            {
                aimDots[i] = Disc(Vector2.zero, 14f, Color.white);
                aimDots[i].Show(false);
            }
            ghost = Disc(Vector2.zero, BallR * 2f, Color.white.Alpha(0.4f));
            ghost.Show(false);

            BuildControls();
            BeginTurn(0);
            Draw();
        }

        void BuildTable()
        {
            var wood = Theme.Hex("6B3F1D");
            var felt = Theme.Hex("1F8A5B");
            Box(TableCentre, new Vector2((HalfW + Rail) * 2f, (HalfH + Rail) * 2f), wood);
            Box(TableCentre, new Vector2(HalfW * 2f, HalfH * 2f), felt, false);
            Box(TableCentre + new Vector2(0f, HeadSpot.y), new Vector2(HalfW * 2f, 4f), felt.Shade(1.25f), false);
            Disc(TableCentre + FootSpot, 14f, felt.Shade(1.25f));
            foreach (var p in Pockets) Disc(TableCentre + p, 92f, Theme.Dark);
        }

        void BuildControls()
        {
            myTally = Ui.Label(Field, "", 40, Theme.Palette[1], new Vector2(-270f, -540f), new Vector2(500f, 60f), TextAnchor.MiddleLeft, null, true);
            botTally = Ui.Label(Field, "", 40, Theme.Palette[0], new Vector2(270f, -540f), new Vector2(500f, 60f), TextAnchor.MiddleRight, null, true);
            status = Label("", new Vector2(0f, -615f), 50, Theme.Text, 1040f);
            Box(new Vector2(0f, -675f), new Vector2(BarWidth, 14f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -675f), new Vector2(BarWidth, 14f), Theme.Coin);
            Ui.Label(Field, "Press anywhere, pull back and let go to shoot", 30, Theme.TextDim, new Vector2(0f, -745f), new Vector2(1000f, 44f));
            Ui.Label(Field, "Pot your four blues, then the black", 30, Theme.TextDim, new Vector2(0f, -790f), new Vector2(1000f, 44f));
            ShowTally();
        }

        Piece AddBall(int kind, Vector2 pos)
        {
            var colour = kind == Mine ? Theme.Palette[1] : kind == Theirs ? Theme.Palette[0] : kind == Black ? Theme.Hex("15151F") : Color.white;
            var piece = new Piece { kind = kind, pos = pos };
            piece.image = Disc(Vector2.zero, BallR * 2f, colour);
            // the bot's balls and the black carry a white spot, so the sides differ by more than colour
            if (kind == Mine) Ui.Disc(piece.image.transform, colour.Shade(0.75f), Vector2.zero, BallR);
            else if (kind == Theirs) Ui.Disc(piece.image.transform, Color.white, Vector2.zero, BallR);
            else if (kind == Black) Ui.Disc(piece.image.transform, Color.white, Vector2.zero, BallR * 0.7f);
            pieces.Add(piece);
            if (kind == Black) blackBall = piece;
            return piece;
        }

        int Left(int kind) => BallsEach - pocketed[kind];

        void ShowTally()
        {
            myTally.text = "YOU " + pocketed[Mine] + "/" + BallsEach;
            botTally.text = bot.Label + " " + pocketed[Theirs] + "/" + BallsEach;
        }

        // ---------- turn flow ----------

        void BeginTurn(int player)
        {
            turn = player;
            pulling = false;
            HideAim();
            var onBlack = Left(player == 0 ? Mine : Theirs) == 0;
            var twoShots = extraShots > 0 ? "  (2 shots)" : "";

            if (player == 0)
            {
                phase = Phase.Aim;
                timer = HumanSeconds;
                status.text = (onBlack ? "Pot the black!" : "Your turn!") + twoShots;
                status.color = Theme.Text;
            }
            else
            {
                phase = Phase.BotThink;
                timer = bot.ThinkTime() + 0.5f;
                status.text = bot.Name + " is aiming" + twoShots;
                status.color = Theme.Palette[0];
                var skill = bot.Blunders(Rng) ? bot.Skill * 0.3f : bot.Skill;
                botShot = PlanShot(Theirs, skill);
                ShowAim(botShot.normalized, Mathf.InverseLerp(MinShot, MaxShot, botShot.magnitude));
            }
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Aim:
                    TickAim();
                    break;

                case Phase.BotThink:
                    if (timer <= 0f) Shoot(botShot);
                    break;

                case Phase.Moving:
                    moveTime += dt;
                    for (var left = dt; left > 0f; left -= StepSeconds) Step(Mathf.Min(left, StepSeconds));
                    if (AllStill() || moveTime > 12f) EndShot();
                    break;

                case Phase.Pause:
                    if (timer <= 0f) AfterShot();
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

        void TickAim()
        {
            if (PointerDown)
            {
                idleStreak = 0;
                pulling = true;
            }

            if (pulling && (PointerHeld || PointerUp))
            {
                // slingshot: the cue ball flies the opposite way to the pull, harder the further you pull
                var pull = PointerDownPos - PointerPos;
                var power = Mathf.Clamp01((pull.magnitude - 20f) / 380f);
                var valid = power > 0.03f;
                if (valid)
                {
                    ShowAim(pull.normalized, power);
                    status.text = "Power " + Mathf.RoundToInt(power * 100f) + "%";
                }
                else HideAim();

                if (PointerUp)
                {
                    pulling = false;
                    if (valid) Shoot(pull.normalized * Mathf.Lerp(MinShot, MaxShot, power));
                    else HideAim();
                    return;
                }
            }

            if (timer > 0f) return;
            // out of time: take a reasonable shot for the player, or forfeit if they have gone
            idleStreak++;
            if (idleStreak >= IdleLimit)
            {
                GameOver(1, "Timed out");
                return;
            }
            Shoot(PlanShot(Mine, 0.7f));
        }

        void Shoot(Vector2 velocity)
        {
            HideAim();
            cue.vel = velocity;
            potted.Clear();
            hitSomething = false;
            ownLeftBefore = Left(turn == 0 ? Mine : Theirs);
            phase = Phase.Moving;
            moveTime = 0f;
            status.text = "";
            Sfx.Blip(260f, 0.06f, 0.3f);
        }

        // ---------- physics ----------

        void Step(float h)
        {
            foreach (var p in pieces)
            {
                if (!p.active) continue;
                var speed = p.vel.magnitude;
                if (speed <= 0f) continue;
                var slowed = speed - (Friction + Drag * speed) * h;
                p.vel = slowed < 10f ? Vector2.zero : p.vel * (slowed / speed);
                p.pos += p.vel * h;
            }

            const float min = BallR * 2f;
            for (var i = 0; i < pieces.Count; i++)
            {
                var a = pieces[i];
                if (!a.active) continue;
                for (var j = i + 1; j < pieces.Count; j++)
                {
                    var b = pieces[j];
                    if (!b.active) continue;
                    var d = b.pos - a.pos;
                    var distSq = d.sqrMagnitude;
                    if (distSq >= min * min || distSq < 0.0001f) continue;

                    var dist = Mathf.Sqrt(distSq);
                    var n = d / dist;
                    var overlap = (min - dist) * 0.5f;
                    a.pos -= n * overlap;
                    b.pos += n * overlap;

                    var closing = Vector2.Dot(b.vel - a.vel, n);
                    if (closing >= 0f) continue;
                    if (a == cue || b == cue) hitSomething = true;
                    var impulse = -(1f + BallBounce) * closing * 0.5f; // equal masses
                    a.vel -= n * impulse;
                    b.vel += n * impulse;
                    Click(760f, -closing);
                }
            }

            foreach (var p in pieces)
            {
                if (!p.active) continue;
                foreach (var pocket in Pockets)
                {
                    if ((p.pos - pocket).sqrMagnitude > PocketCatch * PocketCatch) continue;
                    p.active = false;
                    p.vel = Vector2.zero;
                    potted.Add(p);
                    Sfx.Blip(p.kind == Cue ? 180f : 990f, 0.1f, 0.3f);
                    break;
                }
                if (!p.active) continue;

                float limitX = HalfW - BallR, limitY = HalfH - BallR;
                if (p.pos.x < -limitX) { p.pos.x = -limitX; Bounce(ref p.vel.x, 1f); }
                else if (p.pos.x > limitX) { p.pos.x = limitX; Bounce(ref p.vel.x, -1f); }
                if (p.pos.y < -limitY) { p.pos.y = -limitY; Bounce(ref p.vel.y, 1f); }
                else if (p.pos.y > limitY) { p.pos.y = limitY; Bounce(ref p.vel.y, -1f); }
            }
        }

        void Bounce(ref float v, float sign)
        {
            Click(320f, Mathf.Abs(v));
            v = sign * Mathf.Abs(v) * WallBounce;
        }

        void Click(float pitch, float strength)
        {
            if (strength < 120f || Elapsed - lastClick < 0.05f) return;
            lastClick = Elapsed;
            Sfx.Blip(pitch, 0.03f, Mathf.Lerp(0.08f, 0.3f, strength / MaxShot));
        }

        bool AllStill()
        {
            foreach (var p in pieces)
                if (p.active && p.vel.sqrMagnitude > 0f)
                    return false;
            return true;
        }

        // ---------- rules ----------

        /// <summary>Everything has stopped: work out what the shot earned and who plays next.</summary>
        void EndShot()
        {
            foreach (var p in pieces) p.vel = Vector2.zero;
            var mine = turn == 0 ? Mine : Theirs;
            var own = 0;
            bool scratch = false, black = false;
            foreach (var p in potted)
            {
                if (p.kind == Cue) scratch = true;
                else if (p.kind == Black) black = true;
                else
                {
                    pocketed[p.kind]++;
                    if (p.kind == mine) own++;
                }
            }

            var foul = scratch || !hitSomething;
            if (scratch) Respot(cue, HeadSpot);
            if (black)
            {
                if (turnsTaken == 0) Respot(blackBall, FootSpot); // on the opening shot the black just goes back
                else
                {
                    var legal = !foul && ownLeftBefore == 0;
                    winner = legal ? turn : 1 - turn;
                    endReason = legal ? null : "Black potted too early";
                    if (foul && ownLeftBefore == 0) endReason = "Foul on the black";
                }
            }

            var where = TableCentre + new Vector2(0f, turn == 0 ? -200f : 200f);
            if (foul && winner < 0)
            {
                Pop(scratch ? "Foul! Cue ball potted" : "Foul! Nothing hit", where, Theme.Bad, 64);
                Sfx.Bad();
                if (turn == 0) Score -= 20;
            }
            if (turn == 0 && own > 0)
            {
                Score += own * 40;
                Pop("+" + own * 40, where + new Vector2(0f, foul ? -100f : 0f), Theme.Good, 90);
                if (!foul) Sfx.Great();
            }
            else if (turn == 1 && !foul && own > 0) Pop(bot.Name + " pots", where, Theme.Palette[0], 60);

            ShowTally();
            turnsTaken++;
            if (foul)
            {
                shootAgain = false;
                extraShots = 1; // the other side gets two shots
            }
            else if (own > 0) shootAgain = true;
            else if (extraShots > 0)
            {
                extraShots--;
                shootAgain = true;
            }
            else shootAgain = false;

            phase = Phase.Pause;
            timer = 0.7f;
        }

        void AfterShot()
        {
            if (winner >= 0)
                GameOver(winner, winner == 0 ? "YOU WIN!" : bot.Name + " wins");
            else if (turnsTaken >= MaxTurns)
            {
                var ahead = pocketed[Mine] > pocketed[Theirs] ? 0 : pocketed[Mine] < pocketed[Theirs] ? 1 : -1;
                GameOver(ahead, ahead == 0 ? "YOU WIN!" : ahead == 1 ? bot.Name + " wins" : "Draw");
            }
            else BeginTurn(shootAgain ? turn : 1 - turn);
        }

        /// <summary>Puts a potted ball back on the table, as near the given spot as there is room.</summary>
        void Respot(Piece ball, Vector2 spot)
        {
            var pos = spot;
            for (var i = 1; i < 300 && !Free(pos, ball); i++)
            {
                var angle = i * 2.4f; // spiral outwards
                pos = spot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (14f * Mathf.Sqrt(i));
            }
            ball.pos = pos;
            ball.vel = Vector2.zero;
            ball.active = true;
        }

        bool Free(Vector2 pos, Piece ignore)
        {
            if (Mathf.Abs(pos.x) > HalfW - BallR - 30f || Mathf.Abs(pos.y) > HalfH - BallR - 30f) return false;
            foreach (var p in pieces)
            {
                if (!p.active || p == ignore) continue;
                if ((p.pos - pos).sqrMagnitude < BallR * BallR * 4.2f) return false;
            }
            return true;
        }

        void GameOver(int who, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = who == 0;
            status.text = "";
            HideAim();
            Box(TableCentre, new Vector2(940f, 300f), Theme.Dark);
            Label(message, TableCentre + new Vector2(0f, 50f), 96, won ? Theme.Coin : Theme.Text, 900f);
            if (won)
            {
                Score += 300;
                Label(endReason ?? "+300", TableCentre + new Vector2(0f, -70f), endReason == null ? 64 : 44, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else
            {
                if (endReason != null) Label(endReason, TableCentre + new Vector2(0f, -70f), 44, Theme.TextDim, 900f);
                Sfx.Bad();
            }
        }

        // ---------- computer player ----------

        /// <summary>
        /// Finds a shot for the side playing <paramref name="mine"/>. It looks for a ball it can
        /// knock straight into a pocket: the cue ball has to reach the point directly behind the
        /// ball (seen from the pocket) with nothing in the way. Lower skill means a wobblier aim.
        /// </summary>
        Vector2 PlanShot(int mine, float skill)
        {
            var target = Left(mine) > 0 ? mine : Black;
            var best = Vector2.zero;
            var bestRating = float.MinValue;
            var slow = Friction + Drag * 500f; // rough slowing over a typical shot

            foreach (var ball in pieces)
            {
                if (!ball.active || ball.kind != target) continue;
                for (var k = 0; k < Pockets.Length; k++)
                {
                    var toPocket = Pockets[k] - ball.pos;
                    var ballRun = toPocket.magnitude;
                    if (ballRun < 1f) continue;
                    var push = toPocket / ballRun;
                    if (k >= 4 && Mathf.Abs(push.x) < 0.55f) continue; // too shallow an angle into a side pocket
                    if (PathBlocked(ball.pos, Pockets[k], BallR * 2f - 4f, ball)) continue;

                    var contact = ball.pos - push * (BallR * 2f);
                    if (Mathf.Abs(contact.x) > HalfW - BallR || Mathf.Abs(contact.y) > HalfH - BallR) continue;
                    var path = contact - cue.pos;
                    var run = path.magnitude;
                    if (run < 1f) continue;
                    var dir = path / run;
                    var straight = Vector2.Dot(dir, push);
                    if (straight < 0.3f) continue; // too thin a cut to trust
                    if (PathBlocked(cue.pos, contact, BallR * 2f - 2f, ball)) continue;

                    var rating = straight * 100f - run * 0.03f - ballRun * 0.05f;
                    if (rating <= bestRating) continue;
                    bestRating = rating;

                    // enough speed for the ball to reach the pocket with some to spare
                    var ballSpeed = Mathf.Sqrt(2f * slow * ballRun) * 1.4f + 250f;
                    var hitSpeed = ballSpeed / straight;
                    var speed = Mathf.Sqrt(hitSpeed * hitSpeed + 2f * slow * run) * 1.1f;
                    best = dir * Mathf.Clamp(speed, 600f, MaxShot);
                }
            }

            if (bestRating == float.MinValue)
            {
                // nothing lines up: hit the nearest legal ball, one with a clear path if there is one
                Piece pick = null;
                var nearest = float.MaxValue;
                foreach (var ball in pieces)
                {
                    if (!ball.active || ball.kind != target) continue;
                    var d = (ball.pos - cue.pos).magnitude;
                    if (PathBlocked(cue.pos, ball.pos, BallR * 2f - 2f, ball)) d += 5000f;
                    if (d >= nearest) continue;
                    nearest = d;
                    pick = ball;
                }
                var dir = pick != null ? (pick.pos - cue.pos).normalized : Vector2.up;
                best = dir * (turnsTaken == 0 ? MaxShot * 0.9f : 1500f); // the opening shot is a hard break
            }

            // aim error: up to a few degrees for a weak player, almost none for a strong one
            var wobble = (float)(Rng.NextDouble() + Rng.NextDouble() - 1.0) * (1f - skill) * 4f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(wobble), sin = Mathf.Sin(wobble);
            return new Vector2(best.x * cos - best.y * sin, best.x * sin + best.y * cos);
        }

        /// <summary>True if any ball other than <paramref name="ignore"/> sits within <paramref name="width"/> of the line from a to b.</summary>
        bool PathBlocked(Vector2 a, Vector2 b, float width, Piece ignore)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            foreach (var p in pieces)
            {
                if (!p.active || p == ignore || p == cue) continue;
                var t = lenSq > 0f ? Mathf.Clamp01(Vector2.Dot(p.pos - a, ab) / lenSq) : 0f;
                if ((a + ab * t - p.pos).sqrMagnitude < width * width) return true;
            }
            return false;
        }

        // ---------- drawing ----------

        /// <summary>How far the cue ball travels along dir before it touches a ball or a cushion.</summary>
        float CueRun(Vector2 dir, out bool hitsBall)
        {
            hitsBall = false;
            var run = float.MaxValue;
            if (dir.x > 0.0001f) run = Mathf.Min(run, (HalfW - BallR - cue.pos.x) / dir.x);
            else if (dir.x < -0.0001f) run = Mathf.Min(run, (-HalfW + BallR - cue.pos.x) / dir.x);
            if (dir.y > 0.0001f) run = Mathf.Min(run, (HalfH - BallR - cue.pos.y) / dir.y);
            else if (dir.y < -0.0001f) run = Mathf.Min(run, (-HalfH + BallR - cue.pos.y) / dir.y);

            const float reach = BallR * 2f;
            foreach (var p in pieces)
            {
                if (!p.active || p == cue) continue;
                var to = p.pos - cue.pos;
                var along = Vector2.Dot(to, dir);
                if (along <= 0f) continue;
                var offSq = to.sqrMagnitude - along * along;
                if (offSq >= reach * reach) continue;
                var t = along - Mathf.Sqrt(reach * reach - offSq);
                if (t >= run) continue;
                run = Mathf.Max(0f, t);
                hitsBall = true;
            }
            return run;
        }

        void ShowAim(Vector2 dir, float power)
        {
            var colour = Color.Lerp(Theme.Good, Theme.Bad, power);
            var run = CueRun(dir, out var hitsBall);
            for (var i = 0; i < aimDots.Length; i++)
            {
                var along = BallR + 16f + i * 46f;
                var show = along < run - 10f;
                aimDots[i].Show(show);
                aimDots[i].color = colour;
                if (show) aimDots[i].SetPos(TableCentre + cue.pos + dir * along);
            }
            // a pale ball marks where the cue ball will be when it first touches something
            ghost.Show(true);
            ghost.color = (hitsBall ? Color.white : colour).Alpha(0.4f);
            ghost.SetPos(TableCentre + cue.pos + dir * run);
        }

        void HideAim()
        {
            foreach (var d in aimDots) d.Show(false);
            if (ghost != null) ghost.Show(false);
        }

        void Draw()
        {
            foreach (var p in pieces)
            {
                p.image.Show(p.active);
                if (p.active) p.image.SetPos(TableCentre + p.pos);
            }

            var myAim = phase == Phase.Aim;
            timeBar.Show(myAim);
            if (myAim) timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / HumanSeconds)), 14f);
        }
    }
}
