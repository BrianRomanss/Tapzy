using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.BlockFit
{
    /// <summary>Drag shapes onto an 8x8 grid and complete rows or columns. Ends when nothing fits.</summary>
    [MiniGame("block_fit", "Block Fit", Kind = "Puzzle", Colour = "FFC83D", Order = 6,
        MaxScorePerSecond = 40f, ScoreBase = 400f, MinDuration = 5f, MissionScore = 250)]
    public class BlockFitGame : MiniGameBase
    {
        const int N = 8;
        const float Pitch = 120f;
        const float CellSize = 112f;
        const float TrayScale = 0.5f;
        const float TrayY = -640f;
        const float PickRadius = 170f;
        const float DragLift = 220f;
        static readonly Vector2 GridCentre = new Vector2(0f, 180f);
        static readonly float[] SlotX = { -340f, 0f, 340f };

        // Rows top to bottom, 'X' = filled. Every orientation is listed explicitly.
        static readonly string[] Library =
        {
            "X", "XX", "X|X", "XXX", "X|X|X", "XXXX", "X|X|X|X", "XXXXX", "X|X|X|X|X",
            "XX|XX", "XXX|XXX|XXX",
            "X.|XX", "XX|X.", "XX|.X", ".X|XX",
            "X..|X..|XXX", "XXX|X..|X..", "XXX|..X|..X", "..X|..X|XXX",
            "XXX|.X.", ".X.|XXX", "X.|XX|X.", ".X|XX|.X",
            ".XX|XX.", "X.|XX|.X", "XX.|.XX", ".X|XX|X."
        };

        static readonly Vector2Int[][] Shapes = BuildShapes();

        class Piece
        {
            public Vector2Int[] cells;
            public int w, h;
            public Color color;
            public RectTransform node;
            public readonly List<Image> images = new List<Image>();
        }

        class Fx { public Image image; public float t; }

        readonly Image[,] cellImages = new Image[N, N];
        readonly bool[,] filled = new bool[N, N];
        readonly Piece[] tray = new Piece[3];
        readonly List<Image> ghost = new List<Image>();
        readonly List<Fx> fxs = new List<Fx>();
        int dragging = -1;
        bool gameOver;
        float endTimer;

        protected override string Hint => "Drag shapes. Fill lines.";

        static Vector2Int[][] BuildShapes()
        {
            var result = new Vector2Int[Library.Length][];
            for (var i = 0; i < Library.Length; i++)
            {
                var rows = Library[i].Split('|');
                var list = new List<Vector2Int>();
                for (var r = 0; r < rows.Length; r++)
                for (var c = 0; c < rows[r].Length; c++)
                    if (rows[r][c] == 'X') list.Add(new Vector2Int(c, rows.Length - 1 - r));
                result[i] = list.ToArray();
            }
            return result;
        }

        // Centre of a cell. Row 0 is the bottom row.
        static Vector2 CellPos(int x, int y) => GridCentre + new Vector2((x - (N - 1) * 0.5f) * Pitch, (y - (N - 1) * 0.5f) * Pitch);

        static Vector2Int CellAt(Vector2 pos)
        {
            var o = (pos - GridCentre) / Pitch + new Vector2((N - 1) * 0.5f, (N - 1) * 0.5f);
            return new Vector2Int(Mathf.RoundToInt(o.x), Mathf.RoundToInt(o.y));
        }

        protected override void OnStart()
        {
            Box(GridCentre, new Vector2(N * Pitch + 30f, N * Pitch + 30f), Theme.Dark);
            for (var x = 0; x < N; x++)
            for (var y = 0; y < N; y++)
                cellImages[x, y] = Box(CellPos(x, y), new Vector2(CellSize, CellSize), Theme.Panel);
            for (var i = 0; i < 9; i++)
            {
                var g = Box(Vector2.zero, new Vector2(CellSize, CellSize), Color.white);
                g.Show(false);
                ghost.Add(g);
            }
            Deal();
            RefreshTray();
        }

        protected override void OnTick(float dt)
        {
            TickFx(dt);
            if (gameOver)
            {
                endTimer -= dt;
                if (endTimer <= 0f) EndRound();
                return;
            }

            if (PointerDown) PickUp();
            var target = Vector2.zero;
            var origin = Vector2Int.zero;
            var fits = false;
            if (dragging >= 0)
            {
                var p = tray[dragging];
                target = new Vector2(PointerPos.x, Mathf.Min(PointerPos.y + DragLift, 700f));
                origin = CellAt(OriginPos(p, target));
                fits = Fits(p.cells, origin.x, origin.y);
                ShowGhost(p, origin, fits);
                if (PointerUp)
                {
                    var index = dragging;
                    dragging = -1;
                    ShowGhost(p, origin, false);
                    if (fits) Place(index, origin);
                    else Sfx.Bad();
                }
                else if (!PointerHeld)
                {
                    dragging = -1;
                    ShowGhost(p, origin, false);
                }
            }

            // everything in the tray glides to where it belongs (dragged piece follows the finger)
            var k = 1f - Mathf.Exp(-22f * dt);
            for (var i = 0; i < tray.Length; i++)
            {
                var p = tray[i];
                if (p == null) continue;
                var drag = i == dragging;
                var goal = drag ? target : new Vector2(SlotX[i], TrayY);
                var scale = drag ? 1f : TrayScale;
                p.node.SetPos(Vector2.Lerp(p.node.GetPos(), goal, k));
                p.node.SetScale(Mathf.Lerp(p.node.localScale.x, scale, k));
            }
        }

        void PickUp()
        {
            var best = -1;
            var bestDist = PickRadius;
            for (var i = 0; i < tray.Length; i++)
            {
                if (tray[i] == null) continue;
                var d = Vector2.Distance(PointerDownPos, new Vector2(SlotX[i], TrayY));
                if (d > bestDist) continue;
                best = i;
                bestDist = d;
            }
            if (best < 0) return;
            dragging = best;
            tray[best].node.SetAsLastSibling();
            Sfx.Blip(440f, 0.04f, 0.12f);
        }

        // Where the shape's (0,0) cell sits when its bounding box is centred on 'centre'.
        static Vector2 OriginPos(Piece p, Vector2 centre) =>
            centre - new Vector2((p.w - 1) * 0.5f * Pitch, (p.h - 1) * 0.5f * Pitch);

        bool Fits(Vector2Int[] cells, int ox, int oy)
        {
            foreach (var c in cells)
            {
                int x = ox + c.x, y = oy + c.y;
                if (x < 0 || y < 0 || x >= N || y >= N || filled[x, y]) return false;
            }
            return true;
        }

        bool FitsAnywhere(Vector2Int[] cells)
        {
            for (var x = 0; x < N; x++)
            for (var y = 0; y < N; y++)
                if (Fits(cells, x, y)) return true;
            return false;
        }

        void ShowGhost(Piece p, Vector2Int origin, bool on)
        {
            for (var i = 0; i < ghost.Count; i++)
            {
                var visible = on && i < p.cells.Length;
                ghost[i].Show(visible);
                if (!visible) continue;
                var c = p.cells[i];
                ghost[i].SetPos(CellPos(origin.x + c.x, origin.y + c.y));
                ghost[i].color = p.color.Alpha(0.55f);
            }
        }

        void Place(int index, Vector2Int origin)
        {
            var p = tray[index];
            var mid = Vector2.zero;
            foreach (var c in p.cells)
            {
                int x = origin.x + c.x, y = origin.y + c.y;
                filled[x, y] = true;
                cellImages[x, y].color = p.color;
                mid += CellPos(x, y) / p.cells.Length;
            }
            Score += p.cells.Length;
            Sfx.Blip(520f, 0.06f, 0.25f);
            Pop("+" + p.cells.Length, mid, p.color, 48);

            Destroy(p.node.gameObject);
            tray[index] = null;

            ClearLines();
            if (tray[0] == null && tray[1] == null && tray[2] == null) Deal();
            RefreshTray();

            foreach (var t in tray)
                if (t != null && FitsAnywhere(t.cells)) return;
            gameOver = true;
            endTimer = 0.8f;
            Sfx.Bad();
        }

        void ClearLines()
        {
            var clear = new bool[N, N];
            var lines = 0;
            for (var i = 0; i < N; i++)
            {
                bool row = true, col = true;
                for (var j = 0; j < N; j++)
                {
                    row &= filled[j, i];
                    col &= filled[i, j];
                }
                for (var j = 0; j < N; j++)
                {
                    if (row) clear[j, i] = true;
                    if (col) clear[i, j] = true;
                }
                if (row) lines++;
                if (col) lines++;
            }
            if (lines == 0) return;

            for (var x = 0; x < N; x++)
            for (var y = 0; y < N; y++)
            {
                if (!clear[x, y]) continue;
                var img = Box(CellPos(x, y), new Vector2(CellSize, CellSize), cellImages[x, y].color);
                fxs.Add(new Fx { image = img });
                filled[x, y] = false;
                cellImages[x, y].color = Theme.Panel;
            }
            var points = 10 * lines * lines;
            Score += points;
            Pop("+" + points, GridCentre, Theme.Text, 96);
            Sfx.Great();
        }

        void TickFx(float dt)
        {
            for (var i = fxs.Count - 1; i >= 0; i--)
            {
                var f = fxs[i];
                f.t += dt / 0.3f;
                if (f.t >= 1f)
                {
                    Destroy(f.image.gameObject);
                    fxs.RemoveAt(i);
                    continue;
                }
                f.image.SetScale(1f + 0.5f * f.t);
                f.image.color = f.image.color.Alpha(1f - f.t);
            }
        }

        void Deal()
        {
            var pick = new int[3];
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var ok = false;
                for (var i = 0; i < 3; i++)
                {
                    pick[i] = PickShape();
                    ok |= FitsAnywhere(Shapes[pick[i]]);
                }
                if (ok) break;
            }
            for (var i = 0; i < 3; i++) tray[i] = MakePiece(Shapes[pick[i]], new Vector2(SlotX[i], TrayY));
        }

        // Big shapes (5 cells or more) come up half as often, so the board does not jam in the first minute.
        int PickShape()
        {
            var index = Rand(0, Shapes.Length);
            if (Shapes[index].Length >= 5 && Rng.NextDouble() < 0.5) index = Rand(0, Shapes.Length);
            return index;
        }

        Piece MakePiece(Vector2Int[] cells, Vector2 pos)
        {
            var p = new Piece { cells = cells, color = Theme.Palette[Rand(0, Theme.Palette.Length)] };
            foreach (var c in cells)
            {
                p.w = Mathf.Max(p.w, c.x + 1);
                p.h = Mathf.Max(p.h, c.y + 1);
            }
            p.node = Ui.Node(Field, "Piece", pos, new Vector2(p.w * Pitch, p.h * Pitch));
            p.node.SetScale(0f); // grows into its slot
            foreach (var c in cells)
            {
                var local = new Vector2((c.x - (p.w - 1) * 0.5f) * Pitch, (c.y - (p.h - 1) * 0.5f) * Pitch);
                p.images.Add(Ui.Box(p.node, p.color, local, new Vector2(CellSize, CellSize)));
            }
            return p;
        }

        // Shapes that fit nowhere are drawn dimmed.
        void RefreshTray()
        {
            foreach (var p in tray)
            {
                if (p == null) continue;
                var c = FitsAnywhere(p.cells) ? p.color : p.color.Alpha(0.3f);
                foreach (var img in p.images) img.color = c;
            }
        }
    }
}
