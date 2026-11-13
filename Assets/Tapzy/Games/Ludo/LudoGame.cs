using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.Ludo
{
    /// <summary>
    /// Quick Ludo against three computer opponents. Everyone has two tokens, one already on the
    /// track, so a round lasts a few minutes. The first player to bring both tokens home wins.
    ///
    /// Rules: a six brings a token out of the base. A six, a capture or a token reaching home
    /// gives another roll; a third six in a row passes the turn. Tokens on coloured start squares
    /// and on marked squares cannot be captured. Home needs an exact roll.
    ///
    /// Score: 1 per square moved, 50 per capture, 100 per token home, 300 for winning.
    /// </summary>
    [MiniGame("ludo", "Ludo Dash", Kind = "Board", Colour = "3DE0FF", Order = 8,
        MaxScorePerSecond = 20f, ScoreBase = 600f, MinDuration = 20f, MissionScore = 200)]
    public class LudoGame : MiniGameBase
    {
        const int Players = 4;
        const int TokensEach = 2;
        const int TrackLength = 52;
        const int LastTrackStep = 50; // progress 0..50 is on the shared track
        const int Home = 56;          // 51..55 is the home column, 56 is home
        const int InBase = -1;

        const float Cell = 68f;
        const float TokenSize = 56f;
        const float StepTime = 0.11f;
        const float HumanSeconds = 10f;
        const int IdleLimit = 3; // turns the player may sleep through before the round is forfeited
        const float BarWidth = 600f;

        static readonly Vector2 BoardCentre = new Vector2(0f, 190f);
        static readonly Vector2 DicePos = new Vector2(0f, -660f);
        static readonly Color CellColour = Theme.Hex("EEF2FF");
        static readonly Vector2Int[] Track = BuildTrack();

        // pips: top-left, top-right, mid-left, mid-right, bottom-left, bottom-right, centre
        static readonly bool[][] Faces =
        {
            new[] { false, false, false, false, false, false, true },
            new[] { true, false, false, false, false, true, false },
            new[] { true, false, false, false, false, true, true },
            new[] { true, true, false, false, true, true, false },
            new[] { true, true, false, false, true, true, true },
            new[] { true, true, true, true, true, true, false }
        };

        enum Phase { Roll, Rolling, Pick, Moving, Wait, Over }

        class Token
        {
            public int player, index, progress;
            public Image image;
            public Vector2 pos;
        }

        readonly Color[] colours = { Theme.Palette[1], Theme.Palette[0], Theme.Palette[2], Theme.Palette[3] };
        readonly string[] names = new string[Players];
        readonly Image[] bases = new Image[Players];
        readonly List<Token> tokens = new List<Token>();
        readonly List<Token> movable = new List<Token>();
        readonly Image[] pips = new Image[7];
        Bot[] bots;

        Phase phase;
        int turn, dice = 6, sixes, idleStreak;
        float timer, flicker, dicePunch;
        System.Action afterWait;
        Token moving;
        int stepsLeft;
        float stepT;
        bool won;

        Image diceBack, diceBox, timeBar;
        Text status, rollHint;

        protected override string Hint => "Roll. Race both tokens home.";

        bool HumanTurn => turn == 0;

        // ---------- board geometry ----------

        static Vector2Int[] BuildTrack()
        {
            // clockwise loop of 52 squares, starting on the bottom player's start square
            var t = new List<Vector2Int>();
            for (var y = 1; y <= 5; y++) t.Add(new Vector2Int(6, y));
            for (var x = 5; x >= 0; x--) t.Add(new Vector2Int(x, 6));
            t.Add(new Vector2Int(0, 7));
            for (var x = 0; x <= 5; x++) t.Add(new Vector2Int(x, 8));
            for (var y = 9; y <= 14; y++) t.Add(new Vector2Int(6, y));
            t.Add(new Vector2Int(7, 14));
            for (var y = 14; y >= 9; y--) t.Add(new Vector2Int(8, y));
            for (var x = 9; x <= 14; x++) t.Add(new Vector2Int(x, 8));
            t.Add(new Vector2Int(14, 7));
            for (var x = 14; x >= 9; x--) t.Add(new Vector2Int(x, 6));
            for (var y = 5; y >= 0; y--) t.Add(new Vector2Int(8, y));
            t.Add(new Vector2Int(7, 0));
            t.Add(new Vector2Int(6, 0));
            return t.ToArray();
        }

        /// <summary>Grid position (column, row; row 0 at the bottom) to Field position.</summary>
        static Vector2 CellPos(Vector2 grid) => BoardCentre + (grid - new Vector2(7f, 7f)) * Cell;

        /// <summary>Turns a grid position from the bottom player's point of view into player k's.</summary>
        static Vector2 Rot(Vector2 grid, int k)
        {
            for (var i = 0; i < k; i++) grid = new Vector2(grid.y, 14f - grid.x);
            return grid;
        }

        /// <summary>Index on the shared track for a player's progress (0..50).</summary>
        static int TrackIndex(int player, int progress) => (player * 13 + progress) % TrackLength;

        static bool Safe(int trackIndex) => trackIndex % 13 == 0 || trackIndex % 13 == 8;

        static Vector2 SlotPos(int player, int index, int progress)
        {
            if (progress == InBase)
                return CellPos(Rot(new Vector2(2.5f, 2.5f), player)) + new Vector2((index * 2 - 1) * 66f, -24f);
            if (progress <= LastTrackStep) return CellPos(Track[TrackIndex(player, progress)]);
            return CellPos(Rot(new Vector2(7f, progress - 50), player));
        }

        // ---------- setup ----------

        protected override void OnStart()
        {
            bots = Bots.Pick(Rng, Players - 1);
            names[0] = "YOU";
            for (var i = 1; i < Players; i++) names[i] = bots[i - 1].Label;

            BuildBoard();
            BuildDice();

            for (var p = 0; p < Players; p++)
            for (var i = 0; i < TokensEach; i++)
            {
                // one token starts on the track so nobody waits for a six to get going
                var t = new Token { player = p, index = i, progress = i == 0 ? 0 : InBase };
                t.pos = SlotPos(p, i, t.progress);
                t.image = Disc(t.pos, TokenSize, colours[p]);
                Ui.Disc(t.image.transform, Color.white, Vector2.zero, TokenSize * 0.4f);
                tokens.Add(t);
            }

            BeginTurn(0, true);
        }

        void BuildBoard()
        {
            Box(BoardCentre, new Vector2(15f * Cell + 28f, 15f * Cell + 28f), Theme.Panel);
            var cellSize = new Vector2(Cell - 4f, Cell - 4f);

            for (var i = 0; i < TrackLength; i++)
            {
                var pos = CellPos(Track[i]);
                if (i % 13 == 0)
                {
                    Box(pos, cellSize, colours[i / 13], false);
                }
                else
                {
                    Box(pos, cellSize, CellColour, false);
                    if (Safe(i)) Disc(pos, Cell * 0.6f, Theme.TextDim); // marked safe square
                }
            }

            for (var p = 0; p < Players; p++)
            {
                for (var s = 1; s <= 5; s++) Box(CellPos(Rot(new Vector2(7f, s), p)), cellSize, colours[p], false);

                var centre = CellPos(Rot(new Vector2(2.5f, 2.5f), p));
                bases[p] = Box(centre, new Vector2(6f * Cell - 8f, 6f * Cell - 8f), colours[p]);
                Box(centre + new Vector2(0f, -24f), new Vector2(300f, 180f), CellColour);
                for (var i = 0; i < TokensEach; i++) Disc(SlotPos(p, i, InBase), TokenSize + 22f, colours[p].Shade(0.7f));
                Label(names[p], centre + new Vector2(0f, 128f), p == 0 ? 50 : 38, Theme.Text, 400f);
            }

            Box(CellPos(new Vector2(7f, 7f)), new Vector2(3f * Cell - 4f, 3f * Cell - 4f), Theme.Coin);
            Label("HOME", CellPos(new Vector2(7f, 7f)), 44, Theme.Text, 240f);
        }

        void BuildDice()
        {
            status = Label("", new Vector2(0f, -410f), 52, Theme.Text, 1000f);
            Box(new Vector2(0f, -480f), new Vector2(BarWidth, 14f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -480f), new Vector2(BarWidth, 14f), Theme.Coin);

            diceBack = Box(DicePos, new Vector2(250f, 250f), colours[0]);
            diceBox = Box(DicePos, new Vector2(200f, 200f), Color.white);
            const float o = 52f;
            var spots = new[]
            {
                new Vector2(-o, o), new Vector2(o, o), new Vector2(-o, 0f), new Vector2(o, 0f),
                new Vector2(-o, -o), new Vector2(o, -o), Vector2.zero
            };
            for (var i = 0; i < pips.Length; i++) pips[i] = Ui.Disc(diceBox.transform, Theme.Dark, spots[i], 36f);
            ShowFace(dice);

            rollHint = Label("TAP TO ROLL", new Vector2(0f, -830f), 50, Theme.Coin);
            Ui.Label(Field, "A six, a capture or a token home rolls again", 30, Theme.TextDim,
                new Vector2(0f, -905f), new Vector2(1000f, 44f));
        }

        void ShowFace(int value)
        {
            var face = Faces[Mathf.Clamp(value, 1, 6) - 1];
            for (var i = 0; i < pips.Length; i++) pips[i].Show(face[i]);
        }

        // ---------- turn flow ----------

        protected override void OnTick(float dt)
        {
            TickVisuals(dt);
            timer -= dt;

            switch (phase)
            {
                case Phase.Roll:
                    if (HumanTurn && PointerDown && PointerDownPos.y < BoardCentre.y - 7.5f * Cell)
                    {
                        idleStreak = 0;
                        StartRoll();
                    }
                    else if (timer <= 0f)
                    {
                        if (HumanTurn && Idle()) return;
                        StartRoll();
                    }
                    break;

                case Phase.Rolling:
                    flicker -= dt;
                    if (flicker <= 0f)
                    {
                        flicker = 0.06f;
                        ShowFace(Random.Range(1, 7)); // cosmetic tumble; the real roll comes from the seeded generator
                        diceBox.SetRot(Random.Range(-14f, 14f));
                    }
                    if (timer <= 0f) AfterRoll();
                    break;

                case Phase.Pick:
                    if (HumanTurn && PointerDown)
                    {
                        var picked = TokenAt(PointerDownPos);
                        if (picked != null)
                        {
                            idleStreak = 0;
                            StartMove(picked);
                        }
                    }
                    else if (timer <= 0f)
                    {
                        if (HumanTurn)
                        {
                            if (Idle()) return;
                            StartMove(BestMove());
                        }
                        else
                        {
                            var bot = bots[turn - 1];
                            StartMove(bot.Blunders(Rng) ? movable[Rand(0, movable.Count)] : BestMove());
                        }
                    }
                    break;

                case Phase.Moving:
                    TickMove(dt);
                    break;

                case Phase.Wait:
                    if (timer <= 0f)
                    {
                        var next = afterWait;
                        afterWait = null;
                        next?.Invoke();
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
        }

        void BeginTurn(int player, bool fresh)
        {
            turn = player;
            if (fresh) sixes = 0;
            phase = Phase.Roll;
            timer = HumanTurn ? HumanSeconds : bots[turn - 1].ThinkTime();
            movable.Clear();
            diceBack.color = colours[turn];
            status.text = HumanTurn ? "Your turn!" : names[turn] + " is rolling";
            status.color = colours[turn];
            for (var p = 0; p < Players; p++) bases[p].color = p == turn ? colours[p] : colours[p].Shade(0.55f);
        }

        void StartRoll()
        {
            phase = Phase.Rolling;
            timer = 0.45f;
            flicker = 0f;
            Sfx.Blip(330f, 0.05f, 0.2f);
        }

        void AfterRoll()
        {
            dice = Rand(1, 7);
            ShowFace(dice);
            diceBox.SetRot(0f);
            dicePunch = 1f;
            Sfx.Blip(dice == 6 ? 990f : 660f, 0.07f, 0.25f);

            if (dice == 6) sixes++;
            else sixes = 0;
            if (sixes >= 3)
            {
                Pop("Three sixes!", DicePos + new Vector2(0f, 190f), Theme.Bad, 64);
                Wait(0.9f, () => BeginTurn(NextPlayer(), true));
                return;
            }

            movable.Clear();
            foreach (var t in tokens)
                if (t.player == turn && CanMove(t))
                    movable.Add(t);

            if (movable.Count == 0)
            {
                Pop("No move", DicePos + new Vector2(0f, 190f), Theme.TextDim, 64);
                Wait(0.8f, () => PassOrRepeat(dice == 6));
                return;
            }

            var sameMove = true;
            foreach (var t in movable) sameMove &= t.progress == movable[0].progress;
            if (sameMove)
            {
                var only = movable[0];
                Wait(HumanTurn ? 0.25f : 0.4f, () => StartMove(only));
                return;
            }

            phase = Phase.Pick;
            timer = HumanTurn ? HumanSeconds : bots[turn - 1].ThinkTime() * 0.8f;
            if (HumanTurn) status.text = "Pick a token";
        }

        bool CanMove(Token t)
        {
            if (t.progress == InBase) return dice == 6;
            return t.progress + dice <= Home;
        }

        void StartMove(Token t)
        {
            phase = Phase.Moving;
            moving = t;
            stepsLeft = t.progress == InBase ? 1 : dice;
            stepT = 0f;
            movable.Clear();
            t.image.transform.SetAsLastSibling();
            if (HumanTurn) status.text = "Your turn!";
        }

        void TickMove(float dt)
        {
            var t = moving;
            stepT += dt / StepTime;
            var from = SlotPos(t.player, t.index, t.progress);
            var to = SlotPos(t.player, t.index, t.progress + 1);
            var k = Mathf.Clamp01(stepT);
            t.pos = Vector2.Lerp(from, to, k);
            t.image.SetPos(t.pos);
            t.image.SetScale(1f + 0.35f * Mathf.Sin(Mathf.PI * k));
            if (stepT < 1f) return;

            stepT -= 1f;
            t.progress++;
            stepsLeft--;
            if (HumanTurn) Score += 1;
            Sfx.Blip(520f + 40f * (dice - stepsLeft), 0.03f, 0.12f);
            if (stepsLeft == 0) Land();
        }

        void Land()
        {
            var t = moving;
            moving = null;
            var bonusRoll = dice == 6;

            if (t.progress == Home)
            {
                bonusRoll = true;
                Pop(HumanTurn ? "HOME! +100" : "Home!", t.pos + new Vector2(0f, 60f), colours[turn], 72);
                if (HumanTurn)
                {
                    Score += 100;
                    Sfx.Great();
                }
                else Sfx.Good();
            }
            else if (t.progress <= LastTrackStep)
            {
                var at = TrackIndex(t.player, t.progress);
                if (!Safe(at))
                {
                    foreach (var o in tokens)
                    {
                        if (o.player == t.player || o.progress < 0 || o.progress > LastTrackStep) continue;
                        if (TrackIndex(o.player, o.progress) != at) continue;
                        o.progress = InBase; // glides back to its base
                        bonusRoll = true;
                        if (HumanTurn)
                        {
                            Score += 50;
                            Pop("CAPTURED! +50", t.pos + new Vector2(0f, 60f), Theme.Good, 72);
                            Sfx.Great();
                        }
                        else
                        {
                            Pop("Captured!", t.pos + new Vector2(0f, 60f), colours[turn], 64);
                            if (o.player == 0)
                            {
                                Shake(14f, 0.25f);
                                Sfx.Bad();
                            }
                            else Sfx.Good();
                        }
                    }
                }
            }

            var allHome = true;
            foreach (var o in tokens)
                if (o.player == turn)
                    allHome &= o.progress == Home;
            if (allHome)
            {
                GameOver(turn == 0, turn == 0 ? "YOU WIN!" : names[turn] + " wins");
                return;
            }

            Wait(0.35f, () => PassOrRepeat(bonusRoll));
        }

        void PassOrRepeat(bool again)
        {
            if (again) BeginTurn(turn, false);
            else BeginTurn(NextPlayer(), true);
        }

        int NextPlayer() => (turn + 1) % Players;

        void Wait(float seconds, System.Action then)
        {
            phase = Phase.Wait;
            timer = seconds;
            afterWait = then;
        }

        /// <summary>The player let the clock run out. Returns true if that ended the round.</summary>
        bool Idle()
        {
            idleStreak++;
            if (idleStreak < IdleLimit) return false;
            GameOver(false, "Timed out");
            return true;
        }

        void GameOver(bool playerWon, string message)
        {
            phase = Phase.Over;
            timer = 2.2f;
            won = playerWon;
            movable.Clear();
            status.text = "";
            Box(BoardCentre, new Vector2(940f, 280f), Theme.Dark).transform.SetAsLastSibling();
            Label(message, BoardCentre + new Vector2(0f, playerWon ? 40f : 0f), 96, playerWon ? Theme.Coin : Theme.Text, 900f);
            if (playerWon)
            {
                Score += 300;
                Label("+300", BoardCentre + new Vector2(0f, -70f), 64, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else Sfx.Bad();
        }

        // ---------- choosing a move ----------

        Token TokenAt(Vector2 pos)
        {
            Token best = null;
            var bestDist = 95f;
            foreach (var t in movable)
            {
                var d = Vector2.Distance(pos, t.pos);
                if (d >= bestDist) continue;
                best = t;
                bestDist = d;
            }
            return best;
        }

        Token BestMove()
        {
            Token best = null;
            var bestRating = float.MinValue;
            foreach (var t in movable)
            {
                var r = Rate(t);
                if (r <= bestRating) continue;
                best = t;
                bestRating = r;
            }
            return best;
        }

        /// <summary>How good moving this token with the current roll is. Captures first, then getting home, then safety.</summary>
        float Rate(Token t)
        {
            if (t.progress == InBase) return 50f;

            var dest = t.progress + dice;
            var rating = dest * 0.3f;
            if (dest == Home) rating += 80f;
            else if (dest > LastTrackStep) rating += 15f; // the home column cannot be attacked
            else
            {
                var at = TrackIndex(t.player, dest);
                if (Safe(at)) rating += 25f;
                else
                {
                    foreach (var o in tokens)
                        if (o.player != t.player && o.progress >= 0 && o.progress <= LastTrackStep && TrackIndex(o.player, o.progress) == at)
                            rating += 100f + o.progress;
                    if (Threatened(at, t.player)) rating -= 30f;
                }
            }

            // running away from someone right behind is worth something too
            if (t.progress <= LastTrackStep)
            {
                var here = TrackIndex(t.player, t.progress);
                if (!Safe(here) && Threatened(here, t.player)) rating += 20f;
            }
            return rating;
        }

        /// <summary>True if an opponent sits one to six squares behind this track square.</summary>
        bool Threatened(int trackIndex, int player)
        {
            foreach (var o in tokens)
            {
                if (o.player == player || o.progress < 0 || o.progress > LastTrackStep) continue;
                var behind = (trackIndex - TrackIndex(o.player, o.progress) + TrackLength) % TrackLength;
                if (behind >= 1 && behind <= 6) return true;
            }
            return false;
        }

        // ---------- visuals ----------

        void TickVisuals(float dt)
        {
            var glide = 1f - Mathf.Exp(-16f * dt);
            var pulse = 1f + 0.22f * Mathf.Abs(Mathf.Sin(Elapsed * 6f));

            foreach (var t in tokens)
            {
                if (t == moving) continue;

                // tokens sharing a square shrink and fan out so each one stays visible
                var slot = SlotPos(t.player, t.index, t.progress);
                int sharing = 0, order = 0;
                foreach (var o in tokens)
                {
                    if (o == moving || (SlotPos(o.player, o.index, o.progress) - slot).sqrMagnitude > 1f) continue;
                    if (o == t) order = sharing;
                    sharing++;
                }
                var scale = 1f;
                if (sharing > 1)
                {
                    var angle = 2f * Mathf.PI * order / sharing;
                    slot += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 15f;
                    scale = 0.78f;
                }
                if (phase == Phase.Pick && HumanTurn && movable.Contains(t))
                {
                    scale *= pulse;
                    t.image.transform.SetAsLastSibling();
                }

                t.pos = Vector2.Lerp(t.pos, slot, glide);
                t.image.SetPos(t.pos);
                t.image.SetScale(Mathf.Lerp(t.image.transform.localScale.x, scale, 0.5f));
            }

            if (dicePunch > 0f) dicePunch = Mathf.Max(0f, dicePunch - dt * 5f);
            diceBox.SetScale(1f + 0.25f * dicePunch);

            var waitingOnPlayer = HumanTurn && (phase == Phase.Roll || phase == Phase.Pick);
            timeBar.Show(waitingOnPlayer);
            if (waitingOnPlayer)
                timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / HumanSeconds)), 14f);

            var canRoll = HumanTurn && phase == Phase.Roll;
            rollHint.Show(canRoll);
            if (canRoll)
            {
                rollHint.SetScale(1f + 0.08f * Mathf.Sin(Elapsed * 6f));
                diceBack.SetScale(1f + 0.05f * Mathf.Abs(Mathf.Sin(Elapsed * 4f)));
            }
            else diceBack.SetScale(1f);
        }
    }
}
