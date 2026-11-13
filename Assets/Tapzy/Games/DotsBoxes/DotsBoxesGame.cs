using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.DotsBoxes
{
    /// <summary>
    /// Dots and boxes against a computer opponent on a 5x5 grid of boxes. Take turns drawing a
    /// line between two neighbouring dots. Draw the fourth side of a box and it is yours, and
    /// you go again. When every box is taken, whoever owns more wins; with 25 boxes there is
    /// never a draw.
    ///
    /// Score: 20 per box, 300 for winning.
    /// </summary>
    [MiniGame("dots_boxes", "Dots & Boxes", Kind = "Board", Colour = "FF7AD9", Order = 17,
        MaxScorePerSecond = 10f, ScoreBase = 900f, MinDuration = 10f, MissionScore = 250)]
    public class DotsBoxesGame : MiniGameBase
    {
        const int N = 5;                         // boxes per side
        const int Across = (N + 1) * N;          // lines running left-right; the up-down ones follow
        const int Edges = Across * 2;
        const int Me = 1, Them = 2;
        const float Pitch = 170f, DotSize = 36f;
        const float TapReach = 75f;
        const float HumanSeconds = 10f;
        const int IdleLimit = 3;                 // turns the player may sleep through before the round is forfeited
        const float BarWidth = 600f;

        static readonly Vector2 BoardCentre = new Vector2(0f, 170f);

        enum Phase { Human, BotThink, Over }

        readonly bool[] taken = new bool[Edges];
        readonly int[] owner = new int[N * N];
        readonly Image[] edgeImages = new Image[Edges];
        readonly Image[] boxImages = new Image[N * N];
        readonly int[] boxes = new int[3];       // indexed by Me / Them
        Bot bot;

        Phase phase;
        int idleStreak, botEdge, lastEdge = -1, linesDrawn;
        float timer;
        bool won;

        Text status, tally;
        Image timeBar;

        protected override string Hint => "Close boxes. Own the most.";

        // ---------- geometry ----------

        static int AcrossEdge(int row, int col) => row * N + col;                // row 0..N, col 0..N-1
        static int UpEdge(int row, int col) => Across + row * (N + 1) + col;     // row 0..N-1, col 0..N

        static Vector2 Dot(int row, int col) => BoardCentre + new Vector2((col - N * 0.5f) * Pitch, (row - N * 0.5f) * Pitch);

        static void EdgeEnds(int e, out Vector2 a, out Vector2 b)
        {
            if (e < Across)
            {
                a = Dot(e / N, e % N);
                b = Dot(e / N, e % N + 1);
            }
            else
            {
                e -= Across;
                a = Dot(e / (N + 1), e % (N + 1));
                b = Dot(e / (N + 1) + 1, e % (N + 1));
            }
        }

        static void BoxEdges(int box, int[] into)
        {
            int row = box / N, col = box % N;
            into[0] = AcrossEdge(row, col);
            into[1] = AcrossEdge(row + 1, col);
            into[2] = UpEdge(row, col);
            into[3] = UpEdge(row, col + 1);
        }

        readonly int[] scratch = new int[4];

        int Sides(bool[] lines, int box)
        {
            BoxEdges(box, scratch);
            var n = 0;
            foreach (var e in scratch)
                if (lines[e])
                    n++;
            return n;
        }

        /// <summary>The one or two boxes that line <paramref name="e"/> borders; -1 where there is none.</summary>
        static void BoxesOf(int e, out int first, out int second)
        {
            first = second = -1;
            if (e < Across)
            {
                int row = e / N, col = e % N;
                if (row < N) first = row * N + col;
                if (row > 0) second = (row - 1) * N + col;
            }
            else
            {
                e -= Across;
                int row = e / (N + 1), col = e % (N + 1);
                if (col < N) first = row * N + col;
                if (col > 0) second = row * N + col - 1;
            }
        }

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1)[0];

            Box(BoardCentre, new Vector2(N * Pitch + 110f, N * Pitch + 110f), Theme.Dark);
            for (var box = 0; box < N * N; box++)
            {
                var centre = Dot(box / N, box % N) + new Vector2(Pitch * 0.5f, Pitch * 0.5f);
                boxImages[box] = Box(centre, new Vector2(Pitch - 30f, Pitch - 30f), Theme.Panel.Alpha(0.35f));
            }
            for (var e = 0; e < Edges; e++)
            {
                EdgeEnds(e, out var a, out var b);
                var across = e < Across;
                edgeImages[e] = Box((a + b) * 0.5f, across ? new Vector2(Pitch - DotSize, 16f) : new Vector2(16f, Pitch - DotSize), Theme.PanelLight);
            }
            for (var row = 0; row <= N; row++)
                for (var col = 0; col <= N; col++)
                    Disc(Dot(row, col), DotSize, Theme.Text);

            tally = Label("", new Vector2(0f, -410f), 48, Theme.Text, 1040f);
            status = Label("", new Vector2(0f, -500f), 52, Theme.Text, 1040f);
            Box(new Vector2(0f, -570f), new Vector2(BarWidth, 14f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -570f), new Vector2(BarWidth, 14f), Theme.Coin);
            Ui.Label(Field, "Tap between two dots to draw a line", 30, Theme.TextDim, new Vector2(0f, -650f), new Vector2(1000f, 44f));
            Ui.Label(Field, "Close a box to own it and go again", 30, Theme.TextDim, new Vector2(0f, -695f), new Vector2(1000f, 44f));

            ShowTally();
            BeginTurn(Me, false);
        }

        void ShowTally() => tally.text = "YOU  " + boxes[Me] + " - " + boxes[Them] + "  " + bot.Label;

        // ---------- turn flow ----------

        void BeginTurn(int who, bool again)
        {
            if (who == Me)
            {
                phase = Phase.Human;
                timer = HumanSeconds;
                status.text = again ? "Go again!" : "Your turn!";
                status.color = Theme.Text;
            }
            else
            {
                phase = Phase.BotThink;
                timer = again ? 0.35f : bot.ThinkTime();
                status.text = bot.Name + (again ? " goes again" : " is thinking");
                status.color = Theme.Palette[0];
                botEdge = ChooseEdge(bot.Blunders(Rng) ? 0f : bot.Skill);
            }
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Human:
                    if (PointerDown)
                    {
                        var e = EdgeNear(PointerPos);
                        if (e >= 0)
                        {
                            idleStreak = 0;
                            Draw(e, Me);
                        }
                    }
                    if (phase == Phase.Human && timer <= 0f)
                    {
                        // out of time: draw a sensible line for the player, or forfeit if they have gone
                        idleStreak++;
                        if (idleStreak >= IdleLimit) GameOver(Them, "Timed out");
                        else Draw(ChooseEdge(1f), Me);
                    }
                    break;

                case Phase.BotThink:
                    if (timer <= 0f) Draw(botEdge, Them);
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

            var mine = phase == Phase.Human;
            timeBar.Show(mine);
            if (mine) timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / HumanSeconds)), 14f);
        }

        /// <summary>The free line closest to a tap, or -1 if the tap was not near one.</summary>
        int EdgeNear(Vector2 p)
        {
            var best = -1;
            var bestDist = TapReach;
            for (var e = 0; e < Edges; e++)
            {
                if (taken[e]) continue;
                EdgeEnds(e, out var a, out var b);
                var ab = b - a;
                var t = Mathf.Clamp(Vector2.Dot(p - a, ab) / ab.sqrMagnitude, 0.15f, 0.85f); // dots belong to no line
                var dist = (a + ab * t - p).magnitude;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = e;
            }
            return best;
        }

        void Draw(int e, int who)
        {
            var colour = who == Me ? Theme.Palette[1] : Theme.Palette[0];
            if (lastEdge >= 0) edgeImages[lastEdge].SetScale(1f);
            taken[e] = true;
            lastEdge = e;
            linesDrawn++;
            edgeImages[e].color = colour;
            edgeImages[e].SetScale(1.25f); // the newest line stands out until the next one is drawn
            Sfx.Blip(who == Me ? 620f : 420f, 0.05f, 0.25f);

            BoxesOf(e, out var first, out var second);
            var closed = 0;
            foreach (var box in new[] { first, second })
            {
                if (box < 0 || owner[box] != 0 || Sides(taken, box) < 4) continue;
                owner[box] = who;
                boxes[who]++;
                closed++;
                boxImages[box].color = colour.Alpha(0.75f);
            }

            if (closed > 0)
            {
                ShowTally();
                if (who == Me)
                {
                    Score += closed * 20;
                    EdgeEnds(e, out var a, out var b);
                    Pop("+" + closed * 20, (a + b) * 0.5f + new Vector2(0f, 60f), Theme.Good, 72);
                    Sfx.Great();
                }
                else Sfx.Blip(300f, 0.08f, 0.3f);
            }

            if (boxes[Me] + boxes[Them] >= N * N)
            {
                var winner = boxes[Me] > boxes[Them] ? Me : Them;
                GameOver(winner, winner == Me ? "YOU WIN!" : bot.Name + " wins");
            }
            else BeginTurn(closed > 0 ? who : 3 - who, closed > 0);
        }

        void GameOver(int winner, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = winner == Me;
            status.text = "";
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
        /// Picks a line. In order: finish a box if one is open; otherwise draw a line that leaves
        /// no box with three sides; otherwise give away as few boxes as possible. Skill is the
        /// chance of bothering with each of those checks, so a weak bot misses boxes and hands
        /// out long chains.
        /// </summary>
        int ChooseEdge(float skill)
        {
            var free = new List<int>();
            var closing = new List<int>();
            var safe = new List<int>();
            for (var e = 0; e < Edges; e++)
            {
                if (taken[e]) continue;
                free.Add(e);
                BoxesOf(e, out var first, out var second);
                int a = first >= 0 ? Sides(taken, first) : 0, b = second >= 0 ? Sides(taken, second) : 0;
                if (a == 3 || b == 3) closing.Add(e);
                else if (a < 2 && b < 2) safe.Add(e);
            }

            var careful = Rng.NextDouble() < 0.6 + skill * 0.4;
            if (closing.Count > 0 && (careful || Rng.NextDouble() < 0.6)) return closing[Rng.Next(closing.Count)];
            if (safe.Count > 0 && careful) return safe[Rng.Next(safe.Count)];
            if (!careful) return free[Rng.Next(free.Count)];

            // every line left gives something away: find the cheapest
            var best = free[0];
            var fewest = int.MaxValue;
            var trial = new bool[Edges];
            foreach (var e in free)
            {
                System.Array.Copy(taken, trial, Edges);
                trial[e] = true;
                var given = 0;
                for (var found = true; found;)
                {
                    found = false;
                    for (var box = 0; box < N * N; box++)
                    {
                        if (Sides(trial, box) != 3) continue;
                        BoxEdges(box, scratch);
                        foreach (var side in scratch) trial[side] = true;
                        given++;
                        found = true;
                    }
                }
                if (given >= fewest) continue;
                fewest = given;
                best = e;
            }
            return best;
        }
    }
}
