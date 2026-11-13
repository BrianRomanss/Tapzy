using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.GemRush
{
    /// <summary>Swipe a gem to swap it with its neighbour. Match three or more, chain cascades for big scores.</summary>
    [MiniGame("gem_rush", "Gem Rush", Kind = "Puzzle", Colour = "FF5CC8", Order = 7,
        MaxScorePerSecond = 250f, ScoreBase = 500f, MinDuration = 20f, MissionScore = 800)]
    public class GemRushGame : MiniGameBase
    {
        const int Cols = 7;
        const int Rows = 8;
        const float Pitch = 140f;
        const float BoardY = -60f;
        const float SwapTime = 0.12f;
        const float ClearTime = 0.12f;
        const float FallSpeed = 2000f;

        enum Phase { Idle, Swapping, SwapBack, Clearing, Falling }

        class Gem
        {
            public Image image;
            public int kind;
            public Vector2 to;
            public float speed;
        }

        // Row 0 is the bottom row; "up" is +row everywhere (positions, gravity, swipes).
        readonly Gem[,] grid = new Gem[Cols, Rows];
        readonly bool[,] mark = new bool[Cols, Rows];
        readonly List<Gem> clearing = new List<Gem>();
        Phase phase;
        int chain;
        float clearTimer;
        Vector2Int swapA, swapB;

        protected override float TimeLimit => 60f;
        protected override string Hint => "Swipe gems to match three";

        static Vector2 CellPos(int c, int r) => new Vector2((c - (Cols - 1) * 0.5f) * Pitch, BoardY + (r - (Rows - 1) * 0.5f) * Pitch);
        static bool InBoard(int c, int r) => c >= 0 && c < Cols && r >= 0 && r < Rows;

        protected override void OnStart()
        {
            Box(new Vector2(0f, BoardY), new Vector2(Cols * Pitch + 30f, Rows * Pitch + 30f), Theme.Panel);
            Rebuild();
        }

        protected override void OnTick(float dt)
        {
            var moving = MoveGems(dt);

            switch (phase)
            {
                case Phase.Idle:
                    if (Swipe != Vector2Int.zero) TrySwipe();
                    break;

                case Phase.Swapping:
                    if (moving) break;
                    if (FindMatches(out _)) { chain = 1; BeginClear(); }
                    else
                    {
                        Sfx.Blip(200f, 0.08f, 0.15f);
                        SwapCells(swapA, swapB);
                        phase = Phase.SwapBack;
                    }
                    break;

                case Phase.SwapBack:
                    if (!moving) phase = Phase.Idle;
                    break;

                case Phase.Clearing:
                    clearTimer += dt;
                    var s = Mathf.Max(0f, 1f - clearTimer / ClearTime);
                    foreach (var g in clearing) g.image.SetScale(s);
                    if (clearTimer >= ClearTime) FinishClear();
                    break;

                case Phase.Falling:
                    if (moving) break;
                    if (FindMatches(out _)) { chain++; BeginClear(); }
                    else
                    {
                        phase = Phase.Idle;
                        if (!HasMove()) { Pop("Shuffle", new Vector2(0f, BoardY), Theme.Text, 80); Rebuild(); }
                    }
                    break;
            }
        }

        // ----- input and swapping -----

        void TrySwipe()
        {
            var c = Mathf.FloorToInt((PointerDownPos.x + Cols * Pitch * 0.5f) / Pitch);
            var r = Mathf.FloorToInt((PointerDownPos.y - BoardY + Rows * Pitch * 0.5f) / Pitch);
            var a = new Vector2Int(c, r);
            var b = a + Swipe;
            if (!InBoard(a.x, a.y) || !InBoard(b.x, b.y)) return;
            swapA = a;
            swapB = b;
            SwapCells(a, b);
            Sfx.Blip(520f, 0.04f, 0.15f);
            phase = Phase.Swapping;
        }

        void SwapCells(Vector2Int a, Vector2Int b)
        {
            (grid[a.x, a.y], grid[b.x, b.y]) = (grid[b.x, b.y], grid[a.x, a.y]);
            SetTarget(a, Pitch / SwapTime);
            SetTarget(b, Pitch / SwapTime);
        }

        void SetTarget(Vector2Int cell, float speed)
        {
            var g = grid[cell.x, cell.y];
            g.to = CellPos(cell.x, cell.y);
            g.speed = speed;
        }

        bool MoveGems(float dt)
        {
            var moving = false;
            foreach (var g in grid)
            {
                if (g == null) continue;
                var p = g.image.GetPos();
                if (p == g.to) continue;
                g.image.SetPos(Vector2.MoveTowards(p, g.to, g.speed * dt));
                moving = true;
            }
            return moving;
        }

        // ----- matching, scoring, gravity -----

        int Kind(int c, int r) => grid[c, r] != null ? grid[c, r].kind : -1;

        /// <summary>Marks every gem in a run of 3+ and reports the run-length bonus (before the chain multiplier).</summary>
        bool FindMatches(out int bonus)
        {
            bonus = 0;
            var any = false;
            System.Array.Clear(mark, 0, mark.Length);
            for (var line = 0; line < 2; line++)
            {
                var horizontal = line == 0;
                var outer = horizontal ? Rows : Cols;
                var inner = horizontal ? Cols : Rows;
                for (var o = 0; o < outer; o++)
                {
                    var start = 0;
                    for (var i = 1; i <= inner; i++)
                    {
                        var k = Kind(horizontal ? start : o, horizontal ? o : start);
                        var same = i < inner && k >= 0 && Kind(horizontal ? i : o, horizontal ? o : i) == k;
                        if (same) continue;
                        var len = i - start;
                        if (k >= 0 && len >= 3)
                        {
                            any = true;
                            bonus += len >= 5 ? 50 : len == 4 ? 20 : 0;
                            for (var j = start; j < i; j++) mark[horizontal ? j : o, horizontal ? o : j] = true;
                        }
                        start = i;
                    }
                }
            }
            return any;
        }

        void BeginClear()
        {
            FindMatches(out var bonus);
            clearing.Clear();
            var centre = Vector2.zero;
            for (var c = 0; c < Cols; c++)
            for (var r = 0; r < Rows; r++)
            {
                if (!mark[c, r]) continue;
                clearing.Add(grid[c, r]);
                centre += CellPos(c, r);
                grid[c, r] = null;
            }
            centre /= clearing.Count;

            var points = (clearing.Count * 10 + bonus) * chain;
            Score += points;
            Pop("+" + points, centre, Theme.Text, 64);
            if (chain >= 2) Pop("Chain x" + chain, centre + new Vector2(0f, 90f), Theme.Coin, 52);
            if (chain == 1) Sfx.Good();
            else Sfx.Blip(900f + 140f * chain, 0.12f, 0.3f);

            clearTimer = 0f;
            phase = Phase.Clearing;
        }

        void FinishClear()
        {
            foreach (var g in clearing) Destroy(g.image.gameObject);
            clearing.Clear();

            for (var c = 0; c < Cols; c++)
            {
                var write = 0;
                for (var r = 0; r < Rows; r++)
                {
                    if (grid[c, r] == null) continue;
                    if (r != write)
                    {
                        grid[c, write] = grid[c, r];
                        grid[c, r] = null;
                        SetTarget(new Vector2Int(c, write), FallSpeed);
                    }
                    write++;
                }
                for (var r = write; r < Rows; r++)
                {
                    grid[c, r] = NewGem(Rand(0, Theme.Palette.Length), CellPos(c, Rows + r - write));
                    SetTarget(new Vector2Int(c, r), FallSpeed);
                }
            }
            phase = Phase.Falling;
        }

        bool HasMove()
        {
            for (var c = 0; c < Cols; c++)
            for (var r = 0; r < Rows; r++)
            {
                if (c + 1 < Cols && SwapMakesMatch(c, r, c + 1, r)) return true;
                if (r + 1 < Rows && SwapMakesMatch(c, r, c, r + 1)) return true;
            }
            return false;
        }

        bool SwapMakesMatch(int c1, int r1, int c2, int r2)
        {
            (grid[c1, r1], grid[c2, r2]) = (grid[c2, r2], grid[c1, r1]);
            var found = AnyMatch();
            (grid[c1, r1], grid[c2, r2]) = (grid[c2, r2], grid[c1, r1]);
            return found;
        }

        bool AnyMatch()
        {
            for (var c = 0; c < Cols; c++)
            for (var r = 0; r < Rows; r++)
            {
                var k = Kind(c, r);
                if (k < 0) continue;
                if (c + 2 < Cols && Kind(c + 1, r) == k && Kind(c + 2, r) == k) return true;
                if (r + 2 < Rows && Kind(c, r + 1) == k && Kind(c, r + 2) == k) return true;
            }
            return false;
        }

        // ----- board building -----

        /// <summary>Fills the board with no matches and at least one move; used at start and for reshuffles.</summary>
        void Rebuild()
        {
            do
            {
                foreach (var g in grid)
                    if (g != null) Destroy(g.image.gameObject);
                System.Array.Clear(grid, 0, grid.Length);
                for (var c = 0; c < Cols; c++)
                for (var r = 0; r < Rows; r++)
                {
                    int k;
                    do k = Rand(0, Theme.Palette.Length);
                    while ((c >= 2 && Kind(c - 1, r) == k && Kind(c - 2, r) == k) ||
                           (r >= 2 && Kind(c, r - 1) == k && Kind(c, r - 2) == k));
                    grid[c, r] = NewGem(k, CellPos(c, r));
                }
            } while (!HasMove());
        }

        Gem NewGem(int kind, Vector2 pos)
        {
            var color = Theme.Palette[kind];
            Image img;
            switch (kind)
            {
                case 0: img = Disc(pos, 118f, color); break;
                case 1: img = Box(pos, new Vector2(108f, 108f), color); break;
                case 2: img = Box(pos, new Vector2(88f, 88f), color); img.SetRot(45f); break;
                case 3: img = Box(pos, new Vector2(120f, 80f), color); break;
                case 4: img = Box(pos, new Vector2(80f, 120f), color); break;
                default:
                    img = Disc(pos, 118f, color);
                    Ui.Disc(img.transform, Theme.Panel, Vector2.zero, 48f);
                    break;
            }
            return new Gem { image = img, kind = kind, to = pos, speed = FallSpeed };
        }
    }
}
