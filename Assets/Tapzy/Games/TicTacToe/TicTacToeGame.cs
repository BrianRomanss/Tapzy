using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.TicTacToe
{
    /// <summary>
    /// A tic-tac-toe match against a computer opponent. You are X, the bot is O. One board is
    /// over too quickly to be a game, so this is a match: the first to win three boards takes
    /// it, and after five boards whoever has won more does. The opening move alternates.
    ///
    /// Score: 100 per board won, 30 per drawn board, 300 for winning the match.
    /// </summary>
    [MiniGame("tic_tac_toe", "Tic-Tac-Toe", Kind = "Board", Colour = "5AA9FF", Order = 16,
        MaxScorePerSecond = 10f, ScoreBase = 700f, MinDuration = 5f, MissionScore = 200)]
    public class TicTacToeGame : MiniGameBase
    {
        const int Empty = 0, Me = 1, Them = 2;
        const int WinsNeeded = 3, MaxBoards = 5;
        const float Cell = 290f, Gap = 16f;
        const float HumanSeconds = 6f;
        const int IdleLimit = 3;            // turns the player may sleep through before the match is forfeited
        const float BarWidth = 600f;

        static readonly Vector2 BoardCentre = new Vector2(0f, 150f);
        static readonly int[][] Lines =
        {
            new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
            new[] { 0, 4, 8 }, new[] { 2, 4, 6 }
        };

        enum Phase { Human, BotThink, BoardEnd, Over }

        readonly int[] cells = new int[9];
        readonly Image[] cellImages = new Image[9];
        readonly List<GameObject> marks = new List<GameObject>();
        readonly int[] wins = new int[3];   // indexed by Me / Them
        Bot bot;

        Phase phase;
        int boardNumber, idleStreak, botMove;
        float timer;
        bool won;

        Text status, tally, boardLabel;
        Image timeBar;

        protected override string Hint => "Three in a row!";

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1, 0.2f, 0.85f)[0];

            Box(BoardCentre, new Vector2(Cell * 3f + Gap * 4f, Cell * 3f + Gap * 4f), Theme.Dark);
            for (var i = 0; i < 9; i++) cellImages[i] = Box(CellPos(i), new Vector2(Cell, Cell), Theme.Panel);

            boardLabel = Label("", new Vector2(0f, 700f), 40, Theme.TextDim, 1000f);
            tally = Label("", new Vector2(0f, -360f), 48, Theme.Text, 1040f);
            status = Label("", new Vector2(0f, -450f), 52, Theme.Text, 1040f);
            Box(new Vector2(0f, -520f), new Vector2(BarWidth, 14f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -520f), new Vector2(BarWidth, 14f), Theme.Coin);
            Ui.Label(Field, "You are X. First to win " + WinsNeeded + " boards takes the match", 30, Theme.TextDim,
                new Vector2(0f, -600f), new Vector2(1000f, 44f));

            NewBoard();
        }

        Vector2 CellPos(int i) => BoardCentre + new Vector2((i % 3 - 1) * (Cell + Gap), (1 - i / 3) * (Cell + Gap));

        void NewBoard()
        {
            boardNumber++;
            foreach (var mark in marks) Destroy(mark);
            marks.Clear();
            for (var i = 0; i < 9; i++)
            {
                cells[i] = Empty;
                cellImages[i].color = Theme.Panel;
            }
            boardLabel.text = "Board " + boardNumber + " of " + MaxBoards;
            tally.text = "YOU  " + wins[Me] + " - " + wins[Them] + "  " + bot.Label;
            BeginTurn(boardNumber % 2 == 1 ? Me : Them); // the opening move alternates
        }

        // ---------- turn flow ----------

        void BeginTurn(int who)
        {
            if (who == Me)
            {
                phase = Phase.Human;
                timer = HumanSeconds;
                status.text = "Your turn!";
                status.color = Theme.Text;
            }
            else
            {
                phase = Phase.BotThink;
                timer = bot.ThinkTime();
                status.text = bot.Name + " is thinking";
                status.color = Theme.Palette[0];
                botMove = BotMove();
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
                        for (var i = 0; i < 9; i++)
                        {
                            var d = PointerPos - CellPos(i);
                            if (cells[i] != Empty || Mathf.Abs(d.x) > Cell * 0.5f || Mathf.Abs(d.y) > Cell * 0.5f) continue;
                            idleStreak = 0;
                            Place(i, Me);
                            break;
                        }
                    }
                    if (phase == Phase.Human && timer <= 0f)
                    {
                        // out of time: play a sound move for the player, or forfeit if they have gone
                        idleStreak++;
                        if (idleStreak >= IdleLimit) GameOver(Them, "Timed out");
                        else Place(BestMove(Me), Me);
                    }
                    break;

                case Phase.BotThink:
                    if (timer <= 0f) Place(botMove, Them);
                    break;

                case Phase.BoardEnd:
                    if (timer <= 0f) AfterBoard();
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

        void Place(int i, int who)
        {
            cells[i] = who;
            DrawMark(i, who);
            Sfx.Blip(who == Me ? 620f : 420f, 0.06f, 0.3f);

            var line = WinningLine(who);
            if (line != null)
            {
                wins[who]++;
                foreach (var c in line) cellImages[c].color = (who == Me ? Theme.Good : Theme.Bad).Shade(0.7f);
                if (who == Me)
                {
                    Score += 100;
                    Pop("+100", BoardCentre, Theme.Good, 110);
                    Sfx.Great();
                }
                else
                {
                    Pop(bot.Name + " takes it", BoardCentre, Theme.Palette[0], 70);
                    Sfx.Bad();
                }
                EndBoard();
            }
            else if (Full())
            {
                Score += 30;
                Pop("Draw  +30", BoardCentre, Theme.Coin, 90);
                Sfx.Good();
                EndBoard();
            }
            else BeginTurn(who == Me ? Them : Me);
        }

        void EndBoard()
        {
            status.text = "";
            tally.text = "YOU  " + wins[Me] + " - " + wins[Them] + "  " + bot.Label;
            phase = Phase.BoardEnd;
            timer = 1.3f;
        }

        void AfterBoard()
        {
            if (wins[Me] >= WinsNeeded) GameOver(Me, "YOU WIN!");
            else if (wins[Them] >= WinsNeeded) GameOver(Them, bot.Name + " wins");
            else if (boardNumber >= MaxBoards)
            {
                if (wins[Me] > wins[Them]) GameOver(Me, "YOU WIN!");
                else if (wins[Me] < wins[Them]) GameOver(Them, bot.Name + " wins");
                else GameOver(Empty, "Match drawn");
            }
            else NewBoard();
        }

        void GameOver(int winner, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = winner == Me;
            status.text = "";
            marks.Add(Box(BoardCentre, new Vector2(940f, 280f), Theme.Dark).gameObject);
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

        // ---------- board logic ----------

        int[] WinningLine(int who)
        {
            foreach (var line in Lines)
                if (cells[line[0]] == who && cells[line[1]] == who && cells[line[2]] == who)
                    return line;
            return null;
        }

        bool Full()
        {
            foreach (var c in cells)
                if (c == Empty)
                    return false;
            return true;
        }

        /// <summary>
        /// The bot's move. A perfect player never loses at tic-tac-toe, so the bot is given
        /// lapses: the weaker it is, the more often it plays a random square. Even then it will
        /// not miss a win that is sitting there.
        /// </summary>
        int BotMove()
        {
            var lapse = Mathf.Lerp(0.5f, 0.12f, bot.Skill);
            if (Rng.NextDouble() >= lapse) return BestMove(Them);

            var free = new List<int>();
            for (var i = 0; i < 9; i++)
            {
                if (cells[i] != Empty) continue;
                cells[i] = Them;
                var wins = WinningLine(Them) != null;
                cells[i] = Empty;
                if (wins) return i;
                free.Add(i);
            }
            return free[Rng.Next(free.Count)];
        }

        /// <summary>A best move for <paramref name="who"/> by full search, chosen at random among equals.</summary>
        int BestMove(int who)
        {
            var best = new List<int>();
            var bestValue = int.MinValue;
            for (var i = 0; i < 9; i++)
            {
                if (cells[i] != Empty) continue;
                cells[i] = who;
                var value = -Search(3 - who, 1);
                cells[i] = Empty;
                if (value > bestValue)
                {
                    bestValue = value;
                    best.Clear();
                }
                if (value == bestValue) best.Add(i);
            }
            return best[Rng.Next(best.Count)];
        }

        /// <summary>Value of the position for the side to move: positive wins, sooner is better.</summary>
        int Search(int toMove, int depth)
        {
            if (WinningLine(3 - toMove) != null) return depth - 20; // the side that just moved has won
            if (Full()) return 0;
            var best = int.MinValue;
            for (var i = 0; i < 9; i++)
            {
                if (cells[i] != Empty) continue;
                cells[i] = toMove;
                best = Mathf.Max(best, -Search(3 - toMove, depth + 1));
                cells[i] = Empty;
            }
            return best;
        }

        // ---------- drawing ----------

        void DrawMark(int i, int who)
        {
            var pos = CellPos(i);
            if (who == Me)
            {
                foreach (var angle in new[] { 45f, -45f })
                {
                    var bar = Box(pos, new Vector2(Cell * 0.78f, 44f), Theme.Palette[1]);
                    bar.SetRot(angle);
                    marks.Add(bar.gameObject);
                }
            }
            else
            {
                var ring = Disc(pos, Cell * 0.72f, Theme.Palette[0]);
                Ui.Disc(ring.transform, Theme.Panel, Vector2.zero, Cell * 0.72f - 84f);
                marks.Add(ring.gameObject);
            }
        }
    }
}
