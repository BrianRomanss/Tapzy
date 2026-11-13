using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.Carrom
{
    /// <summary>
    /// Quick carrom against a computer opponent. You play the white coins from the bottom edge,
    /// the bot plays the dark coins from the top. Slide the striker along your line, then pull
    /// back on the board and let go to shoot.
    ///
    /// Rules: pocket one of your own coins or the red queen and you shoot again. A coin of the
    /// other colour still counts for its owner. Pocketing the striker is a foul: one of your
    /// pocketed coins comes back and the turn passes. The first to pocket all six of their
    /// coins wins; after 30 turns each, whoever has pocketed more wins.
    ///
    /// Score: 30 per own coin, 80 for the queen, 300 for winning, minus 20 per foul.
    /// </summary>
    [MiniGame("carrom", "Carrom", Kind = "Board", Colour = "B07CFF", Order = 13,
        MaxScorePerSecond = 3f, ScoreBase = 600f, MinDuration = 20f, MissionScore = 200)]
    public class CarromGame : MiniGameBase
    {
        const int White = 0, Black = 1, Queen = 2, Striker = 3;
        const int CoinsEach = 6;
        const int MaxTurns = 60;

        // board, in units from its centre
        const float Half = 470f;            // playing surface half-size
        const float CoinR = 30f, StrikerR = 38f;
        const float PocketInset = 30f, PocketCatch = 40f;
        const float BaseY = Half - 115f;    // striker lines sit this far from the centre
        const float BaseHalf = 290f;        // and the striker may slide this far along them

        // physics
        const float Friction = 380f;        // steady slowing, units per second squared
        const float Drag = 0.5f;            // extra slowing in proportion to speed
        const float MaxShot = 2600f, MinShot = 500f;
        const float CoinBounce = 0.92f, WallBounce = 0.75f;
        const float StepSeconds = 1f / 240f;

        const float HumanSeconds = 20f;
        const int IdleLimit = 3;            // turns the player may sleep through before the round is forfeited
        const float BarWidth = 600f;
        const float SliderTop = -370f;      // presses below this move the striker instead of aiming

        static readonly Vector2 BoardCentre = new Vector2(0f, 150f);
        static readonly Vector2[] Pockets =
        {
            new Vector2(-Half + PocketInset, -Half + PocketInset), new Vector2(Half - PocketInset, -Half + PocketInset),
            new Vector2(-Half + PocketInset, Half - PocketInset), new Vector2(Half - PocketInset, Half - PocketInset)
        };

        enum Phase { Aim, BotThink, Moving, Pause, Over }

        class Piece
        {
            public int kind;
            public float r, mass;
            public Vector2 pos, vel;
            public bool active = true;
            public Image image;
        }

        struct Shot
        {
            public float x;
            public Vector2 vel;
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly List<Piece> potted = new List<Piece>();
        readonly int[] pocketed = new int[2];
        readonly Image[] aimDots = new Image[8];
        Piece striker;
        Bot bot;

        Phase phase;
        int turn;                // 0 = player, 1 = bot
        int turnsTaken, idleStreak;
        float timer, moveTime, lastClick = -1f;
        bool pulling, won;
        Shot botShot;

        Text status, myTally, botTally;
        Image timeBar, sliderKnob;

        protected override string Hint => "Pull back. Pocket the whites.";

        float MyBaseY => -BaseY;
        float BotBaseY => BaseY;

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1, 0.25f, 0.7f)[0]; // the strongest bots clear the board before the player gets going
            BuildBoard();

            // queen in the middle, a ring of six around it, six more outside, colours alternating
            var spin = Rand(0f, Mathf.PI * 2f);
            AddCoin(Queen, Vector2.zero);
            for (var i = 0; i < 6; i++)
            {
                var a = spin + i * Mathf.PI / 3f;
                AddCoin(i % 2 == 0 ? White : Black, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (CoinR * 2f + 2f));
                var b = a + Mathf.PI / 6f;
                AddCoin(i % 2 == 0 ? Black : White, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * (CoinR * 4f + 4f));
            }

            striker = new Piece { kind = Striker, r = StrikerR, mass = 1.6f };
            striker.image = Disc(Vector2.zero, StrikerR * 2f, Theme.Coin);
            Ui.Disc(striker.image.transform, Theme.Coin.Shade(0.75f), Vector2.zero, StrikerR);
            pieces.Add(striker);

            for (var i = 0; i < aimDots.Length; i++)
            {
                aimDots[i] = Disc(Vector2.zero, 22f - i * 1.5f, Color.white);
                aimDots[i].Show(false);
            }

            BuildControls();
            BeginTurn(0);
            Draw();
        }

        void BuildBoard()
        {
            var wood = Theme.Hex("8A5A2B");
            var felt = Theme.Hex("F2D9A0");
            var mark = Theme.Hex("B5834A");
            Box(BoardCentre, new Vector2(Half * 2f + 70f, Half * 2f + 70f), wood);
            Box(BoardCentre, new Vector2(Half * 2f, Half * 2f), felt, false);
            foreach (var p in Pockets) Disc(BoardCentre + p, 112f, Theme.Dark);
            Disc(BoardCentre, 330f, mark);
            Disc(BoardCentre, 300f, felt);
            foreach (var y in new[] { -BaseY, BaseY })
            {
                Box(BoardCentre + new Vector2(0f, y), new Vector2(BaseHalf * 2f + StrikerR * 2f, StrikerR * 2f + 10f), mark);
                Box(BoardCentre + new Vector2(0f, y), new Vector2(BaseHalf * 2f + StrikerR * 2f - 14f, StrikerR * 2f - 4f), felt, false);
            }
        }

        void BuildControls()
        {
            myTally = Ui.Label(Field, "", 40, Theme.Text, new Vector2(-270f, -420f), new Vector2(500f, 60f), TextAnchor.MiddleLeft, null, true);
            botTally = Ui.Label(Field, "", 40, Theme.Palette[0], new Vector2(270f, -420f), new Vector2(500f, 60f), TextAnchor.MiddleRight, null, true);
            status = Label("", new Vector2(0f, -495f), 50, Theme.Text, 1040f);
            Box(new Vector2(0f, -555f), new Vector2(BarWidth, 14f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -555f), new Vector2(BarWidth, 14f), Theme.Coin);

            Box(new Vector2(0f, -670f), new Vector2(BaseHalf * 2f + 120f, 80f), Theme.Panel);
            sliderKnob = Disc(new Vector2(0f, -670f), 100f, Theme.Coin);
            Ui.Label(Field, "Slide here to place the striker", 30, Theme.TextDim, new Vector2(0f, -760f), new Vector2(1000f, 44f));
            Ui.Label(Field, "Pull back on the board and let go to shoot", 30, Theme.TextDim, new Vector2(0f, -805f), new Vector2(1000f, 44f));
            ShowTally();
        }

        void AddCoin(int kind, Vector2 pos)
        {
            var colour = kind == White ? Theme.Hex("F4F6FF") : kind == Black ? Theme.Hex("3A3F5C") : Theme.Bad;
            var piece = new Piece { kind = kind, r = CoinR, mass = 1f, pos = pos };
            piece.image = Disc(Vector2.zero, CoinR * 2f, colour);
            Ui.Disc(piece.image.transform, colour.Shade(0.8f), Vector2.zero, CoinR);
            pieces.Add(piece);
        }

        void ShowTally()
        {
            myTally.text = "YOU " + pocketed[White] + "/" + CoinsEach;
            botTally.text = bot.Label + " " + pocketed[Black] + "/" + CoinsEach;
        }

        // ---------- turn flow ----------

        void BeginTurn(int player)
        {
            turn = player;
            pulling = false;
            striker.active = true;
            striker.vel = Vector2.zero;
            striker.pos = new Vector2(FreeSpotOnLine(player == 0 ? MyBaseY : BotBaseY, 0f), player == 0 ? MyBaseY : BotBaseY);
            HideAim();

            if (player == 0)
            {
                phase = Phase.Aim;
                timer = HumanSeconds;
                status.text = "Your turn!";
                status.color = Theme.Text;
            }
            else
            {
                phase = Phase.BotThink;
                timer = bot.ThinkTime() + 0.4f;
                status.text = bot.Name + " is aiming";
                status.color = Theme.Palette[0];
                var skill = bot.Blunders(Rng) ? bot.Skill * 0.3f : bot.Skill;
                botShot = PlanShot(Black, BotBaseY, skill);
                striker.pos.x = botShot.x;
                ShowAim(botShot.vel.normalized, Mathf.InverseLerp(MinShot, MaxShot, botShot.vel.magnitude));
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
                    if (timer <= 0f) Shoot(botShot.vel);
                    break;

                case Phase.Moving:
                    moveTime += dt;
                    for (var left = dt; left > 0f; left -= StepSeconds) Step(Mathf.Min(left, StepSeconds));
                    if (AllStill() || moveTime > 10f) EndShot();
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
                pulling = PointerDownPos.y >= SliderTop;
            }

            if (PointerHeld && !pulling)
            {
                striker.pos.x = Mathf.Clamp(PointerPos.x, -BaseHalf, BaseHalf);
                HideAim();
            }
            else if (pulling && (PointerHeld || PointerUp))
            {
                // slingshot: the striker flies the opposite way to the pull, harder the further you pull
                var pull = PointerDownPos - PointerPos;
                var power = Mathf.Clamp01((pull.magnitude - 20f) / 380f);
                var dir = pull.normalized;
                var valid = power > 0.05f && dir.y > 0.05f; // the striker must leave the line forwards
                if (valid) ShowAim(dir, power);
                else HideAim();

                if (PointerUp)
                {
                    pulling = false;
                    if (!valid) HideAim();
                    else if (Overlapping(striker.pos, StrikerR, striker))
                    {
                        HideAim();
                        Pop("Move the striker", BoardCentre + striker.pos + new Vector2(0f, 90f), Theme.Bad, 52);
                        Sfx.Bad();
                    }
                    else Shoot(dir * Mathf.Lerp(MinShot, MaxShot, power));
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
            var shot = PlanShot(White, MyBaseY, 0.7f);
            striker.pos.x = shot.x;
            Shoot(shot.vel);
        }

        void Shoot(Vector2 velocity)
        {
            HideAim();
            striker.vel = velocity;
            potted.Clear();
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
                p.vel = slowed < 12f ? Vector2.zero : p.vel * (slowed / speed);
                p.pos += p.vel * h;
            }

            for (var i = 0; i < pieces.Count; i++)
            {
                var a = pieces[i];
                if (!a.active) continue;
                for (var j = i + 1; j < pieces.Count; j++)
                {
                    var b = pieces[j];
                    if (!b.active) continue;
                    var d = b.pos - a.pos;
                    var min = a.r + b.r;
                    var distSq = d.sqrMagnitude;
                    if (distSq >= min * min || distSq < 0.0001f) continue;

                    var dist = Mathf.Sqrt(distSq);
                    var n = d / dist;
                    var total = a.mass + b.mass;
                    var overlap = min - dist;
                    a.pos -= n * (overlap * b.mass / total);
                    b.pos += n * (overlap * a.mass / total);

                    var closing = Vector2.Dot(b.vel - a.vel, n);
                    if (closing >= 0f) continue;
                    var impulse = -(1f + CoinBounce) * closing / (1f / a.mass + 1f / b.mass);
                    a.vel -= n * (impulse / a.mass);
                    b.vel += n * (impulse / b.mass);
                    Click(700f, -closing);
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
                    Sfx.Blip(p.kind == Striker ? 180f : 990f, 0.1f, 0.3f);
                    break;
                }
                if (!p.active) continue;

                var limit = Half - p.r;
                if (p.pos.x < -limit) { p.pos.x = -limit; Bounce(ref p.vel.x, 1f); }
                else if (p.pos.x > limit) { p.pos.x = limit; Bounce(ref p.vel.x, -1f); }
                if (p.pos.y < -limit) { p.pos.y = -limit; Bounce(ref p.vel.y, 1f); }
                else if (p.pos.y > limit) { p.pos.y = limit; Bounce(ref p.vel.y, -1f); }
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
            var mine = turn == 0 ? White : Black;
            int own = 0;
            bool queen = false, foul = false;
            foreach (var p in potted)
            {
                if (p.kind == Striker) foul = true;
                else if (p.kind == Queen) queen = true;
                else
                {
                    pocketed[p.kind]++;
                    if (p.kind == mine) own++;
                }
            }

            var where = BoardCentre + new Vector2(0f, turn == 0 ? -200f : 200f);
            if (foul)
            {
                // the queen does not count on a foul, and one pocketed coin of the shooter's comes back
                if (queen) Respawn(Queen);
                if (pocketed[mine] > 0)
                {
                    pocketed[mine]--;
                    Respawn(mine);
                }
                queen = false;
                Pop("Foul!", where, Theme.Bad, 90);
                Sfx.Bad();
                if (turn == 0) Score -= 20;
            }
            if (turn == 0)
            {
                var points = own * 30 + (queen ? 80 : 0);
                if (points > 0)
                {
                    Score += points;
                    Pop("+" + points, where + new Vector2(0f, foul ? -100f : 0f), Theme.Good, 90);
                    if (!foul) Sfx.Great();
                }
            }
            else if (!foul && (own > 0 || queen)) Pop(bot.Name + " pockets", where, Theme.Palette[0], 60);

            ShowTally();
            turnsTaken++;
            shootAgain = !foul && (own > 0 || queen);
            phase = Phase.Pause;
            timer = 0.7f;
        }

        bool shootAgain;

        void AfterShot()
        {
            bool whiteDone = pocketed[White] >= CoinsEach, blackDone = pocketed[Black] >= CoinsEach;
            if (whiteDone || blackDone)
            {
                var winner = whiteDone && blackDone ? turn : whiteDone ? 0 : 1;
                GameOver(winner, winner == 0 ? "YOU WIN!" : bot.Name + " wins");
            }
            else if (turnsTaken >= MaxTurns)
            {
                var winner = pocketed[White] > pocketed[Black] ? 0 : pocketed[White] < pocketed[Black] ? 1 : -1;
                GameOver(winner, winner == 0 ? "YOU WIN!" : winner == 1 ? bot.Name + " wins" : "Draw");
            }
            else BeginTurn(shootAgain ? turn : 1 - turn);
        }

        /// <summary>Puts a pocketed coin back on the board, as near the centre as there is room.</summary>
        void Respawn(int kind)
        {
            Piece coin = null;
            foreach (var p in pieces)
                if (!p.active && p.kind == kind)
                    coin = p;
            if (coin == null) return;

            var spot = Vector2.zero;
            for (var i = 1; i < 200 && Overlapping(spot, CoinR, coin); i++)
            {
                var angle = i * 2.4f; // spiral outwards
                spot = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (6f * Mathf.Sqrt(i) * 3f);
            }
            coin.pos = spot;
            coin.vel = Vector2.zero;
            coin.active = true;
        }

        bool Overlapping(Vector2 pos, float r, Piece ignore)
        {
            foreach (var p in pieces)
            {
                if (!p.active || p == ignore || p == striker) continue;
                var min = r + p.r;
                if ((p.pos - pos).sqrMagnitude < min * min) return true;
            }
            return false;
        }

        /// <summary>The spot on a striker line nearest to x where the striker touches no coin.</summary>
        float FreeSpotOnLine(float y, float x)
        {
            for (var off = 0f; off <= BaseHalf * 2f; off += 20f)
            {
                foreach (var sign in new[] { 1f, -1f })
                {
                    var tryX = x + off * sign;
                    if (Mathf.Abs(tryX) <= BaseHalf && !Overlapping(new Vector2(tryX, y), StrikerR, striker)) return tryX;
                }
            }
            return x;
        }

        void GameOver(int winner, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = winner == 0;
            status.text = "";
            HideAim();
            Box(BoardCentre, new Vector2(940f, 280f), Theme.Dark);
            Label(message, BoardCentre + new Vector2(0f, won ? 40f : 0f), 96, won ? Theme.Coin : Theme.Text, 900f);
            if (won)
            {
                Score += 300;
                Label("+300", BoardCentre + new Vector2(0f, -70f), 64, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else Sfx.Bad();
        }

        // ---------- computer player ----------

        /// <summary>
        /// Finds a shot for the side playing <paramref name="mine"/> from the striker line at
        /// <paramref name="baseY"/>. It looks for a coin it can knock straight into a pocket: the
        /// striker has to reach the point directly behind the coin (seen from the pocket) with
        /// nothing in the way. Lower skill means a wobblier aim.
        /// </summary>
        Shot PlanShot(int mine, float baseY, float skill)
        {
            var forward = baseY < 0f ? 1f : -1f;
            var best = new Shot();
            var bestRating = float.MinValue;
            var slow = Friction + Drag * 500f; // rough slowing over a typical shot

            foreach (var coin in pieces)
            {
                if (!coin.active || (coin.kind != mine && coin.kind != Queen)) continue;
                foreach (var pocket in Pockets)
                {
                    var toPocket = pocket - coin.pos;
                    var coinRun = toPocket.magnitude;
                    if (coinRun < 1f) continue;
                    var push = toPocket / coinRun;
                    if (PathBlocked(coin.pos, pocket, CoinR * 2f - 4f, coin)) continue;
                    var contact = coin.pos - push * (CoinR + StrikerR);

                    for (var x = -BaseHalf; x <= BaseHalf; x += BaseHalf / 5f)
                    {
                        var from = new Vector2(x, baseY);
                        if (Overlapping(from, StrikerR, striker)) continue;
                        var path = contact - from;
                        var run = path.magnitude;
                        if (run < 1f) continue;
                        var dir = path / run;
                        if (dir.y * forward < 0.12f) continue;       // must shoot forwards
                        var straight = Vector2.Dot(dir, push);
                        if (straight < 0.35f) continue;              // too thin a cut to trust
                        if (PathBlocked(from, contact, StrikerR + CoinR - 2f, coin)) continue;

                        var rating = straight * 100f - run * 0.03f - coinRun * 0.05f + (coin.kind == Queen ? 15f : 0f);
                        if (rating <= bestRating) continue;
                        bestRating = rating;

                        // enough speed for the coin to reach the pocket with some to spare
                        var coinSpeed = Mathf.Sqrt(2f * slow * coinRun) * 1.5f + 250f;
                        var hitSpeed = coinSpeed / (straight * 1.2f);
                        var speed = Mathf.Sqrt(hitSpeed * hitSpeed + 2f * slow * run) * 1.1f;
                        best = new Shot { x = x, vel = dir * Mathf.Clamp(speed, 700f, MaxShot) };
                    }
                }
            }

            if (bestRating == float.MinValue)
            {
                // nothing lines up: just hit the nearest coin of our own and hope
                var x = FreeSpotOnLine(baseY, 0f);
                var from = new Vector2(x, baseY);
                var aimAt = new Vector2(0f, 0f);
                var nearest = float.MaxValue;
                foreach (var coin in pieces)
                {
                    if (!coin.active || coin.kind != mine) continue;
                    var d = (coin.pos - from).sqrMagnitude;
                    if ((coin.pos.y - baseY) * forward < 40f || d >= nearest) continue;
                    nearest = d;
                    aimAt = coin.pos;
                }
                var dir = (aimAt - from).normalized;
                if (dir.y * forward < 0.12f) dir = new Vector2(0f, forward);
                best = new Shot { x = x, vel = dir * 1500f };
            }

            // aim error: up to a few degrees for a weak player, almost none for a strong one
            var wobble = (float)(Rng.NextDouble() + Rng.NextDouble() - 1.0) * (1f - skill) * 7f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(wobble), sin = Mathf.Sin(wobble);
            best.vel = new Vector2(best.vel.x * cos - best.vel.y * sin, best.vel.x * sin + best.vel.y * cos);
            return best;
        }

        /// <summary>True if any coin other than <paramref name="ignore"/> sits within <paramref name="width"/> of the line from a to b.</summary>
        bool PathBlocked(Vector2 a, Vector2 b, float width, Piece ignore)
        {
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            foreach (var p in pieces)
            {
                if (!p.active || p == ignore || p == striker) continue;
                var t = lenSq > 0f ? Mathf.Clamp01(Vector2.Dot(p.pos - a, ab) / lenSq) : 0f;
                if ((a + ab * t - p.pos).sqrMagnitude < width * width) return true;
            }
            return false;
        }

        // ---------- drawing ----------

        void ShowAim(Vector2 dir, float power)
        {
            var colour = Color.Lerp(Theme.Good, Theme.Bad, power);
            for (var i = 0; i < aimDots.Length; i++)
            {
                aimDots[i].Show(true);
                aimDots[i].color = colour;
                aimDots[i].SetPos(BoardCentre + striker.pos + dir * (StrikerR + 30f + i * (34f + power * 50f)));
            }
        }

        void HideAim()
        {
            foreach (var d in aimDots) d.Show(false);
        }

        void Draw()
        {
            foreach (var p in pieces)
            {
                p.image.Show(p.active);
                if (p.active) p.image.SetPos(BoardCentre + p.pos);
            }

            var myAim = phase == Phase.Aim;
            if (myAim) striker.image.color = Overlapping(striker.pos, StrikerR, striker) ? Theme.Bad : Theme.Coin;
            else striker.image.color = Theme.Coin;
            sliderKnob.SetPos(myAim ? striker.pos.x : 0f, -670f);
            sliderKnob.color = myAim ? Theme.Coin : Theme.PanelLight;

            timeBar.Show(myAim);
            if (myAim) timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / HumanSeconds)), 14f);
        }
    }
}
