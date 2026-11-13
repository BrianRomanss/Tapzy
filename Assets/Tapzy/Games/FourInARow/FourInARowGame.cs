using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.FourInARow
{
    /// <summary>
    /// Drop discs into a 7 x 6 grid against a computer opponent. The first to line up four in a
    /// row, column or diagonal wins; a full grid is a draw.
    ///
    /// Score: 10 per disc dropped. A win adds 300 plus 25 for every move saved under 21;
    /// a draw adds 100.
    /// </summary>
    [MiniGame("four_in_a_row", "Four in a Row", Kind = "Board", Colour = "FFD400", Order = 11,
        MaxScorePerSecond = 5f, ScoreBase = 900f, MinDuration = 5f, MissionScore = 300)]
    public class FourInARowGame : MiniGameBase
    {
        const int Cols = 7, Rows = 6;
        const int Me = 1, Foe = 2;
        const int WinValue = 100000;

        const float Pitch = 140f;
        const float DiscSize = 120f;
        const float BoardY = 60f;
        const float DropY = 590f;
        const float Gravity = 7000f;
        const float HumanSeconds = 10f;
        const int IdleLimit = 3; // turns the player may sleep through before the round is forfeited
        const float BarWidth = 600f;

        static readonly int[] ColumnOrder = { 3, 2, 4, 1, 5, 0, 6 }; // centre first: better moves, faster search
        static readonly Vector2Int[][] Lines = BuildLines();

        enum Phase { MyTurn, BotThink, Dropping, Over }

        readonly int[,] grid = new int[Cols, Rows];
        readonly int[] heights = new int[Cols];
        readonly Image[,] discs = new Image[Cols, Rows];
        readonly List<Image> winners = new List<Image>();
        readonly List<int> choices = new List<int>();

        Bot bot;
        Color myColour, botColour;
        Phase phase;
        int mover, myMoves, placed, idleStreak;
        float timer, dropSpeed, dropTo;
        Image dropping, ghost, timeBar;
        int dropCol, dropRow;
        bool won;
        Text status;

        protected override string Hint => "Tap a column. Connect four.";

        // ---------- setup ----------

        static Vector2Int[][] BuildLines()
        {
            var lines = new List<Vector2Int[]>();
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1) };
            for (var c = 0; c < Cols; c++)
            for (var r = 0; r < Rows; r++)
                foreach (var d in dirs)
                {
                    int ec = c + d.x * 3, er = r + d.y * 3;
                    if (ec < 0 || ec >= Cols || er < 0 || er >= Rows) continue;
                    var line = new Vector2Int[4];
                    for (var i = 0; i < 4; i++) line[i] = new Vector2Int(c + d.x * i, r + d.y * i);
                    lines.Add(line);
                }
            return lines.ToArray();
        }

        static Vector2 CellPos(int c, int r) =>
            new Vector2((c - (Cols - 1) * 0.5f) * Pitch, BoardY + (r - (Rows - 1) * 0.5f) * Pitch);

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1, 0.25f, 0.8f)[0]; // a perfect player is no fun to lose to
            myColour = Theme.Palette[3];
            botColour = Theme.Palette[0];

            Label("YOU  vs  " + bot.Label, new Vector2(0f, 700f), 40, Theme.Text, 1000f);
            Box(new Vector2(0f, BoardY), new Vector2(Cols * Pitch + 40f, Rows * Pitch + 40f), Theme.Bg);
            for (var c = 0; c < Cols; c++)
            for (var r = 0; r < Rows; r++)
                Disc(CellPos(c, r), DiscSize, Theme.Dark);

            ghost = Disc(new Vector2(0f, DropY), DiscSize, myColour.Alpha(0.6f));
            ghost.Show(false);

            status = Label("", new Vector2(0f, -470f), 56, Theme.Text, 1040f);
            Box(new Vector2(0f, -545f), new Vector2(BarWidth, 14f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -545f), new Vector2(BarWidth, 14f), Theme.Coin);

            BeginTurn(Rng.NextDouble() < 0.5 ? Me : Foe);
        }

        // ---------- turn flow ----------

        void BeginTurn(int player)
        {
            mover = player;
            if (player == Me)
            {
                phase = Phase.MyTurn;
                timer = HumanSeconds;
                status.text = "Your turn!";
                status.color = myColour;
            }
            else
            {
                phase = Phase.BotThink;
                timer = bot.ThinkTime();
                status.text = bot.Name + " is thinking";
                status.color = botColour;
            }
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.MyTurn:
                    var col = Mathf.FloorToInt(PointerPos.x / Pitch + Cols * 0.5f);
                    var valid = col >= 0 && col < Cols && heights[col] < Rows;
                    ghost.Show(PointerHeld && valid);
                    if (valid) ghost.SetPos(CellPos(col, 0).x, DropY);

                    if (PointerUp && valid)
                    {
                        idleStreak = 0;
                        Drop(col);
                    }
                    else if (timer <= 0f)
                    {
                        // out of time: play a sensible move for the player, or forfeit if they have gone
                        idleStreak++;
                        if (idleStreak >= IdleLimit) GameOver(Foe, "Timed out");
                        else Drop(BestColumn(Me, 4));
                    }
                    break;

                case Phase.BotThink:
                    if (timer <= 0f) Drop(BotColumn());
                    break;

                case Phase.Dropping:
                    dropSpeed += Gravity * dt;
                    var pos = dropping.GetPos();
                    pos.y -= dropSpeed * dt;
                    if (pos.y <= dropTo)
                    {
                        pos.y = dropTo;
                        dropping.SetPos(pos);
                        Landed();
                    }
                    else dropping.SetPos(pos);
                    break;

                case Phase.Over:
                    var pulse = 1f + 0.12f * Mathf.Abs(Mathf.Sin(Elapsed * 7f));
                    foreach (var w in winners) w.SetScale(pulse);
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

            var waiting = phase == Phase.MyTurn;
            timeBar.Show(waiting);
            if (waiting) timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / HumanSeconds)), 14f);
        }

        void Drop(int col)
        {
            ghost.Show(false);
            dropCol = col;
            dropRow = heights[col];
            grid[col, dropRow] = mover;
            heights[col]++;
            placed++;

            var colour = mover == Me ? myColour : botColour;
            var target = CellPos(col, dropRow);
            dropping = Disc(new Vector2(target.x, DropY), DiscSize, colour);
            Ui.Disc(dropping.transform, colour.Shade(0.8f), Vector2.zero, DiscSize * 0.6f);
            discs[col, dropRow] = dropping;
            dropTo = target.y;
            dropSpeed = 600f;
            phase = Phase.Dropping;
            status.text = "";

            if (mover == Me)
            {
                myMoves++;
                Score += 10;
            }
        }

        void Landed()
        {
            Sfx.Blip(mover == Me ? 520f : 400f, 0.06f, 0.3f);
            if (Wins(dropCol, dropRow, mover))
            {
                MarkWinners(mover);
                GameOver(mover, mover == Me ? "YOU WIN!" : bot.Name + " wins");
            }
            else if (placed == Cols * Rows) GameOver(0, "Draw");
            else BeginTurn(mover == Me ? Foe : Me);
        }

        void GameOver(int winner, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = winner == Me;
            status.text = "";
            ghost.Show(false);

            // the banner sits below the grid so the winning line stays in view
            Box(new Vector2(0f, -640f), new Vector2(940f, 300f), Theme.Dark);
            Label(message, new Vector2(0f, won || winner == 0 ? -590f : -640f), 96, won ? Theme.Coin : Theme.Text, 900f);
            if (won)
            {
                var bonus = 300 + Mathf.Max(0, 21 - myMoves) * 25; // quicker wins are worth more
                Score += bonus;
                Label("+" + bonus, new Vector2(0f, -700f), 64, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else if (winner == 0)
            {
                Score += 100;
                Label("+100", new Vector2(0f, -700f), 64, Theme.Good, 900f);
                Sfx.Good();
            }
            else Sfx.Bad();
        }

        // ---------- rules ----------

        /// <summary>True if the disc at (c, r) completes four in a row for this player.</summary>
        bool Wins(int c, int r, int player)
        {
            return Run(c, r, 1, 0, player) + Run(c, r, -1, 0, player) >= 3
                   || Run(c, r, 0, 1, player) + Run(c, r, 0, -1, player) >= 3
                   || Run(c, r, 1, 1, player) + Run(c, r, -1, -1, player) >= 3
                   || Run(c, r, 1, -1, player) + Run(c, r, -1, 1, player) >= 3;
        }

        /// <summary>How many of this player's discs follow (c, r) in one direction.</summary>
        int Run(int c, int r, int dc, int dr, int player)
        {
            var n = 0;
            for (c += dc, r += dr; c >= 0 && c < Cols && r >= 0 && r < Rows && grid[c, r] == player; c += dc, r += dr) n++;
            return n;
        }

        void MarkWinners(int player)
        {
            foreach (var line in Lines)
            {
                var all = true;
                foreach (var cell in line) all &= grid[cell.x, cell.y] == player;
                if (!all) continue;
                foreach (var cell in line)
                {
                    var disc = discs[cell.x, cell.y];
                    if (winners.Contains(disc)) continue;
                    winners.Add(disc);
                    Ui.Disc(disc.transform, Color.white, Vector2.zero, DiscSize * 0.45f);
                }
            }
        }

        // ---------- computer player ----------

        /// <summary>The bot's move: it looks further ahead the better it is, and weaker bots sometimes just drop anywhere.</summary>
        int BotColumn()
        {
            if (bot.Blunders(Rng))
            {
                // even a careless bot takes a win that is sitting right there
                for (var c = 0; c < Cols; c++)
                {
                    if (heights[c] >= Rows) continue;
                    grid[c, heights[c]] = Foe;
                    var wins = Wins(c, heights[c], Foe);
                    grid[c, heights[c]] = 0;
                    if (wins) return c;
                }
                choices.Clear();
                for (var c = 0; c < Cols; c++)
                    if (heights[c] < Rows)
                        choices.Add(c);
                return choices[Rand(0, choices.Count)];
            }
            return BestColumn(Foe, 2 + Mathf.RoundToInt(bot.Skill * 4f));
        }

        /// <summary>The best column for a player, looking <paramref name="depth"/> moves ahead. Equal moves are picked between at random.</summary>
        int BestColumn(int player, int depth)
        {
            var best = int.MinValue;
            choices.Clear();
            foreach (var c in ColumnOrder)
            {
                if (heights[c] >= Rows) continue;
                var v = TryMove(c, player, depth, -WinValue * 2, WinValue * 2);
                if (v > best)
                {
                    best = v;
                    choices.Clear();
                }
                if (v == best) choices.Add(c);
            }
            return choices[Rand(0, choices.Count)];
        }

        /// <summary>Value of dropping into column c for this player (higher is better for them).</summary>
        int TryMove(int c, int player, int depth, int alpha, int beta)
        {
            var r = heights[c];
            grid[c, r] = player;
            heights[c]++;
            int v;
            if (Wins(c, r, player)) v = WinValue + depth; // sooner wins rank higher
            else if (depth <= 1) v = Evaluate(player);
            else v = -Search(3 - player, depth - 1, -beta, -alpha);
            heights[c]--;
            grid[c, r] = 0;
            return v;
        }

        int Search(int player, int depth, int alpha, int beta)
        {
            var best = int.MinValue;
            foreach (var c in ColumnOrder)
            {
                if (heights[c] >= Rows) continue;
                var v = TryMove(c, player, depth, alpha, beta);
                if (v > best) best = v;
                if (best > alpha) alpha = best;
                if (alpha >= beta) break;
            }
            return best == int.MinValue ? 0 : best; // full grid: a draw
        }

        /// <summary>Rough worth of the position for a player: open twos and threes, and discs in the centre column.</summary>
        int Evaluate(int player)
        {
            var total = 0;
            foreach (var line in Lines)
            {
                int mine = 0, theirs = 0;
                foreach (var cell in line)
                {
                    var v = grid[cell.x, cell.y];
                    if (v == player) mine++;
                    else if (v != 0) theirs++;
                }
                if (theirs == 0) total += mine == 3 ? 30 : mine == 2 ? 5 : 0;
                else if (mine == 0) total -= theirs == 3 ? 30 : theirs == 2 ? 5 : 0;
            }
            for (var r = 0; r < Rows; r++)
            {
                var v = grid[Cols / 2, r];
                if (v == player) total += 3;
                else if (v != 0) total -= 3;
            }
            return total;
        }
    }
}
