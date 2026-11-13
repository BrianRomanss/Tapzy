using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.SnakesLadders
{
    /// <summary>
    /// A quick race up a 7x7 board against two computer opponents. Every turn two dice are
    /// rolled and you pick which one to move by, so there is a choice to make: land on a ladder,
    /// dodge a snake. Ladders carry you up, snakes send you down. The first to reach square 49
    /// wins; an exact roll is not needed.
    ///
    /// Score: 5 per square gained (ladders included), 300 for winning plus 5 per square of lead.
    /// </summary>
    [MiniGame("snakes_ladders", "Snakes & Ladders", Kind = "Board", Colour = "4DD8A0", Order = 15,
        MaxScorePerSecond = 20f, ScoreBase = 600f, MinDuration = 8f, MissionScore = 200)]
    public class SnakesLaddersGame : MiniGameBase
    {
        const int Side = 7, Last = Side * Side;
        const int Players = 3;
        const int MaxTurns = 150;
        const float Cell = 140f;
        const float HumanSeconds = 8f;
        const int IdleLimit = 3;            // turns the player may sleep through before the round is forfeited
        const float BarWidth = 600f;
        const float DieSize = 190f, DiceY = -580f, DiceX = 150f;
        const float HopSeconds = 0.1f, SlideSeconds = 0.55f;

        static readonly Vector2 BoardCentre = new Vector2(0f, 180f);

        enum Phase { Choose, BotThink, Moving, Slide, Pause, Over }

        class Die
        {
            public int value;
            public Image frame, face;
            public Image[] pips;
            public Image marker;
            public Color colour;
        }

        readonly int[] jump = new int[Last + 1];      // where a square's snake or ladder leads, 0 for none
        readonly int[] square = new int[Players];     // 1..49
        readonly Vector2[] shown = new Vector2[Players];
        readonly Image[] tokens = new Image[Players];
        readonly Text[] tallies = new Text[Players];
        readonly Color[] colours = new Color[Players];
        readonly Die[] dice = new Die[2];
        Bot[] bots;

        Phase phase;
        int turn, turnsTaken, idleStreak;
        int stepsLeft, slideFrom, botPick;
        float timer;
        bool won;

        Text status;
        Image timeBar;

        protected override string Hint => "Pick a die. Climb!";

        // ---------- setup ----------

        protected override void OnStart()
        {
            bots = Bots.Pick(Rng, Players - 1);
            colours[0] = Theme.Palette[1];
            colours[1] = Theme.Palette[0];
            colours[2] = Theme.Palette[3];

            PlaceSnakesAndLadders();
            BuildBoard();

            for (var i = 0; i < 2; i++) dice[i] = BuildDie(i);
            for (var p = Players - 1; p >= 0; p--)
            {
                square[p] = 1;
                tokens[p] = Disc(Vector2.zero, 74f, Theme.Dark);
                Ui.Disc(tokens[p].transform, colours[p], Vector2.zero, 62f);
                if (p == 0) Ui.Label(tokens[p].transform, "YOU", 22, Theme.Dark, Vector2.zero, new Vector2(80f, 30f));
                shown[p] = TokenSpot(p, 1);
            }

            for (var p = 0; p < Players; p++)
                tallies[p] = Ui.Label(Field, "", 32, colours[p], new Vector2((p - 1) * 345f, -365f), new Vector2(340f, 50f));
            status = Label("", new Vector2(0f, -430f), 48, Theme.Text, 1040f);
            Box(new Vector2(0f, -715f), new Vector2(BarWidth, 14f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -715f), new Vector2(BarWidth, 14f), Theme.Coin);
            Ui.Label(Field, "Tap a die to move that many squares", 30, Theme.TextDim, new Vector2(0f, -775f), new Vector2(1000f, 44f));

            ShowTallies();
            BeginTurn(0);
            Draw();
        }

        int RowOf(int n) => (n - 1) / Side;

        Vector2 SquarePos(int n)
        {
            var row = RowOf(n);
            var i = (n - 1) % Side;
            var col = row % 2 == 0 ? i : Side - 1 - i; // the path snakes back and forth up the board
            return BoardCentre + new Vector2((col - 3) * Cell, (row - 3) * Cell);
        }

        Vector2 TokenSpot(int player, int n) => SquarePos(n) + new Vector2((player - 1) * 30f, player == 0 ? -12f : 12f);

        /// <summary>Four ladders and four snakes, none sharing a square, each spanning at least one row.</summary>
        void PlaceSnakesAndLadders()
        {
            var taken = new HashSet<int> { 1, Last };
            int ladders = 0, snakes = 0;
            for (var tries = 0; tries < 2000 && (ladders < 4 || snakes < 4); tries++)
            {
                var up = ladders < 4 && (snakes >= 4 || tries % 2 == 0);
                var low = Rand(2, Last - 6);
                var high = low + Rand(5, 19);
                if (high >= Last || RowOf(high) == RowOf(low) || taken.Contains(low) || taken.Contains(high)) continue;
                if (!up && high > Last - 3 && snakes > 0) continue; // at most one snake waiting right at the finish
                taken.Add(low);
                taken.Add(high);
                if (up) { jump[low] = high; ladders++; }
                else { jump[high] = low; snakes++; }
            }
        }

        void BuildBoard()
        {
            Box(BoardCentre, new Vector2(Side * Cell + 24f, Side * Cell + 24f), Theme.Dark);
            for (var n = 1; n <= Last; n++)
            {
                var pos = SquarePos(n);
                var colour = n == Last ? Theme.Coin : (RowOf(n) + (n - 1) % Side) % 2 == 0 ? Theme.Panel : Theme.PanelLight;
                Box(pos, new Vector2(Cell - 6f, Cell - 6f), colour);
                Ui.Label(Field, n == Last ? "WIN" : n.ToString(), n == Last ? 40 : 28, n == Last ? Theme.Dark : Theme.TextDim,
                    pos + (n == Last ? Vector2.zero : new Vector2(-38f, 44f)), new Vector2(120f, 44f));
            }

            for (var n = 1; n <= Last; n++)
                if (jump[n] > n)
                    DrawLadder(SquarePos(n), SquarePos(jump[n]));
            var snakeColours = new[] { Theme.Hex("FF5A5A"), Theme.Hex("B56BFF"), Theme.Hex("FF8A3D"), Theme.Hex("3DD68C") };
            var s = 0;
            for (var n = 1; n <= Last; n++)
                if (jump[n] != 0 && jump[n] < n)
                    DrawSnake(SquarePos(n), SquarePos(jump[n]), snakeColours[s++ % snakeColours.Length]);
        }

        void DrawLadder(Vector2 a, Vector2 b)
        {
            var along = b - a;
            var length = along.magnitude;
            var dir = along / length;
            var side = new Vector2(-dir.y, dir.x);
            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var wood = Theme.Hex("E8B04A");
            for (var d = 30f; d < length - 20f; d += 38f)
                Box(a + dir * d, new Vector2(10f, 52f), wood.Shade(0.8f), false).SetRot(angle);
            foreach (var offset in new[] { -24f, 24f })
                Box(a + along * 0.5f + side * offset, new Vector2(length, 12f), wood, false).SetRot(angle);
        }

        void DrawSnake(Vector2 head, Vector2 tail, Color colour)
        {
            var along = tail - head;
            var length = along.magnitude;
            var dir = along / length;
            var side = new Vector2(-dir.y, dir.x);
            var count = Mathf.CeilToInt(length / 13f);
            for (var i = count; i >= 1; i--)
            {
                var t = i / (float)count;
                var sway = Mathf.Sin(t * Mathf.PI * 3f) * 24f * Mathf.Sin(t * Mathf.PI);
                Disc(head + along * t + side * sway, Mathf.Lerp(44f, 18f, t), i % 4 < 2 ? colour : colour.Shade(0.8f));
            }
            Disc(head, 60f, colour);
            foreach (var offset in new[] { -14f, 14f })
            {
                Disc(head - dir * 6f + side * offset, 18f, Color.white);
                Disc(head - dir * 3f + side * offset, 9f, Theme.Dark);
            }
        }

        Die BuildDie(int i)
        {
            var die = new Die { colour = i == 0 ? Theme.Coin : Theme.Hex("3DE0FF") };
            var pos = new Vector2(i == 0 ? -DiceX : DiceX, DiceY);
            die.frame = Box(pos, new Vector2(DieSize + 20f, DieSize + 20f), die.colour);
            die.face = Box(pos, new Vector2(DieSize, DieSize), Color.white);
            // pips: four corners, two middle sides, centre
            var spots = new[]
            {
                new Vector2(-50f, 50f), new Vector2(50f, 50f), new Vector2(-50f, 0f), new Vector2(50f, 0f),
                new Vector2(-50f, -50f), new Vector2(50f, -50f), Vector2.zero
            };
            die.pips = new Image[spots.Length];
            for (var k = 0; k < spots.Length; k++) die.pips[k] = Ui.Disc(die.face.transform, Theme.Dark, spots[k], 34f);
            // a ring on the board showing where this die would take you
            die.marker = Disc(Vector2.zero, 112f, die.colour.Alpha(0.85f));
            Ui.Disc(die.marker.transform, Theme.Dark.Alpha(0.55f), Vector2.zero, 84f);
            die.marker.Show(false);
            return die;
        }

        static readonly bool[][] PipPatterns =
        {
            new[] { false, false, false, false, false, false, true },
            new[] { true, false, false, false, false, true, false },
            new[] { true, false, false, false, false, true, true },
            new[] { true, true, false, false, true, true, false },
            new[] { true, true, false, false, true, true, true },
            new[] { true, true, true, true, true, true, false }
        };

        void ShowDie(Die die, int value, float fade)
        {
            die.value = value;
            for (var k = 0; k < die.pips.Length; k++) die.pips[k].Show(PipPatterns[value - 1][k]);
            die.face.color = Color.white.Alpha(fade);
            die.frame.color = die.colour.Alpha(fade);
        }

        void ShowTallies()
        {
            tallies[0].text = "YOU  " + square[0];
            for (var p = 1; p < Players; p++) tallies[p].text = bots[p - 1].Label + "  " + square[p];
        }

        // ---------- turn flow ----------

        string NameOf(int p) => p == 0 ? "You" : bots[p - 1].Name;

        /// <summary>Where a move of <paramref name="roll"/> from square <paramref name="from"/> ends up, snakes and ladders included.</summary>
        int Landing(int from, int roll)
        {
            var n = Mathf.Min(Last, from + roll);
            return jump[n] != 0 ? jump[n] : n;
        }

        int BestDie() => Landing(square[turn], dice[1].value) > Landing(square[turn], dice[0].value) ? 1 : 0;

        void BeginTurn(int player)
        {
            turn = player;
            for (var i = 0; i < 2; i++) ShowDie(dice[i], Rand(1, 7), 1f);
            Sfx.Blip(520f, 0.05f, 0.2f);

            if (player == 0)
            {
                phase = Phase.Choose;
                timer = HumanSeconds;
                status.text = "Your turn! Pick a die";
                status.color = Theme.Text;
                foreach (var die in dice)
                {
                    die.marker.Show(true);
                    die.marker.SetPos(SquarePos(Mathf.Min(Last, square[0] + die.value)));
                }
                // both dice the same: one ring is enough
                if (dice[0].value == dice[1].value) dice[1].marker.Show(false);
            }
            else
            {
                var bot = bots[player - 1];
                phase = Phase.BotThink;
                timer = bot.ThinkTime();
                status.text = bot.Name + " is choosing";
                status.color = colours[player];
                botPick = bot.Blunders(Rng) ? Rand(0, 2) : BestDie();
            }
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Choose:
                    if (PointerDown)
                    {
                        for (var i = 0; i < 2; i++)
                        {
                            var d = PointerPos - new Vector2(i == 0 ? -DiceX : DiceX, DiceY);
                            if (Mathf.Abs(d.x) > DieSize * 0.5f + 25f || Mathf.Abs(d.y) > DieSize * 0.5f + 25f) continue;
                            idleStreak = 0;
                            Pick(i);
                            break;
                        }
                    }
                    if (phase == Phase.Choose && timer <= 0f)
                    {
                        // out of time: take the better die for the player, or forfeit if they have gone
                        idleStreak++;
                        if (idleStreak >= IdleLimit) GameOver(-1, "Timed out");
                        else Pick(BestDie());
                    }
                    break;

                case Phase.BotThink:
                    if (timer <= 0f) Pick(botPick);
                    break;

                case Phase.Moving:
                    if (timer <= 0f) Hop();
                    break;

                case Phase.Slide:
                    var t = 1f - Mathf.Clamp01(timer / SlideSeconds);
                    shown[turn] = Vector2.Lerp(TokenSpot(turn, slideFrom), TokenSpot(turn, square[turn]), Mathf.SmoothStep(0f, 1f, t));
                    if (timer <= 0f) EndMove();
                    break;

                case Phase.Pause:
                    if (timer <= 0f) NextTurn();
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

        void Pick(int which)
        {
            foreach (var die in dice) die.marker.Show(false);
            ShowDie(dice[1 - which], dice[1 - which].value, 0.25f);
            stepsLeft = Mathf.Min(dice[which].value, Last - square[turn]);
            status.text = NameOf(turn) + (turn == 0 ? " move " : " moves ") + dice[which].value;
            phase = Phase.Moving;
            timer = HopSeconds;
        }

        void Hop()
        {
            square[turn]++;
            stepsLeft--;
            shown[turn] = TokenSpot(turn, square[turn]);
            if (turn == 0) Score += 5;
            Sfx.Blip(440f + square[turn] * 8f, 0.03f, 0.2f);
            timer = HopSeconds;
            if (stepsLeft > 0) return;

            var to = jump[square[turn]];
            if (to == 0)
            {
                EndMove();
                return;
            }

            var ladder = to > square[turn];
            var where = SquarePos(square[turn]) + new Vector2(0f, 90f);
            if (ladder)
            {
                if (turn == 0) Score += (to - square[turn]) * 5;
                Pop(turn == 0 ? "Ladder! +" + (to - square[turn]) * 5 : "Ladder!", where, Theme.Good, 64);
                Sfx.Good();
            }
            else
            {
                Pop("Snake!", where, Theme.Bad, 64);
                Sfx.Bad();
            }
            slideFrom = square[turn];
            square[turn] = to;
            phase = Phase.Slide;
            timer = SlideSeconds;
        }

        void EndMove()
        {
            shown[turn] = TokenSpot(turn, square[turn]);
            ShowTallies();
            turnsTaken++;
            phase = Phase.Pause;
            timer = 0.35f;
        }

        void NextTurn()
        {
            if (square[turn] >= Last)
            {
                GameOver(turn, turn == 0 ? "YOU WIN!" : bots[turn - 1].Name + " wins");
                return;
            }
            if (turnsTaken >= MaxTurns)
            {
                // should never be needed, but a board game must not run for ever
                var lead = 0;
                for (var p = 1; p < Players; p++)
                    if (square[p] > square[lead])
                        lead = p;
                GameOver(lead, lead == 0 ? "YOU WIN!" : bots[lead - 1].Name + " wins");
                return;
            }
            BeginTurn((turn + 1) % Players);
        }

        void GameOver(int winner, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = winner == 0;
            status.text = "";
            foreach (var die in dice) die.marker.Show(false);
            Box(BoardCentre, new Vector2(940f, 280f), Theme.Dark);
            Label(message, BoardCentre + new Vector2(0f, won ? 40f : 0f), 96, won ? Theme.Coin : Theme.Text, 900f);
            if (won)
            {
                // a bigger lead over the nearest bot is worth more
                var bonus = 300 + (Last - Mathf.Max(square[1], square[2])) * 5;
                Score += bonus;
                Label("+" + bonus, BoardCentre + new Vector2(0f, -70f), 64, Theme.Good, 900f);
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else Sfx.Bad();
        }

        // ---------- drawing ----------

        void Draw()
        {
            for (var p = 0; p < Players; p++)
            {
                tokens[p].SetPos(shown[p]);
                tokens[p].SetScale(p == turn && phase != Phase.Over ? 1.15f : 1f);
            }
            var choosing = phase == Phase.Choose;
            timeBar.Show(choosing);
            if (choosing) timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / HumanSeconds)), 14f);
        }
    }
}
