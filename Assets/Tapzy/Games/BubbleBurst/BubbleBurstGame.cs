using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.BubbleBurst
{
    /// <summary>Aim and fire bubbles; match three or more to pop them, drop whatever hangs loose.</summary>
    [MiniGame("bubble_burst", "Bubble Burst", Kind = "Puzzle", Colour = "3DDC97", Order = 4,
        MaxScorePerSecond = 200f, ScoreBase = 1500f, MinDuration = 5f, MissionScore = 600)]
    public class BubbleBurstGame : MiniGameBase
    {
        const int Cols = 10;               // even rows; odd (shifted) rows hold Cols - 1
        const int StartRows = 6;
        const int DangerRow = 14;          // a bubble this low crosses the danger line
        const float D = 100f, R = 50f;
        const float RowH = 87f;
        const float CeilY = 740f;
        const float DangerY = -560f;
        const float ShooterY = -780f;
        const float AimMinY = -700f;
        const float Speed = 2400f;
        const float WallX = 540f - R;
        const float PushEvery = 12f;
        const int MissLimit = 5;
        const int AimDots = 30;

        class Bubble
        {
            public Image img;
            public int color;
        }

        class Fx
        {
            public Image img;
            public Vector2 vel;
            public float age, life;
            public bool burst;
        }

        readonly List<Bubble[]> rows = new List<Bubble[]>(); // rows[0] is the top row
        readonly List<Fx> fx = new List<Fx>();
        readonly List<Image> dots = new List<Image>();
        readonly Image[] pips = new Image[MissLimit];
        readonly int[] colours = new int[5];                 // indices into Theme.Palette used this round

        int parity;                  // flips every time a row is pushed in, keeps the zig-zag stable
        int misses, current, next;
        float pushTimer, slide, reload = 1f;
        Image shooter, preview, timerBar;

        bool flying;
        Image flightImg;
        Vector2 flightPos, flightDir;
        int flightColor;

        protected override float TimeLimit => 90f;
        protected override string Hint => "Drag to aim, release";

        // ----- grid maths -----

        bool Shifted(int r) => ((r + parity) & 1) == 1;
        int ColsIn(int r) => Shifted(r) ? Cols - 1 : Cols;

        Vector2 CellPos(int r, int c) => new Vector2((Shifted(r) ? -400f : -450f) + c * D, CeilY - R - r * RowH);

        Bubble Get(int r, int c) => r < 0 || r >= rows.Count || c < 0 || c >= ColsIn(r) ? null : rows[r][c];

        // Row r shifted right sits between columns c and c + 1 of its neighbours; otherwise between c - 1 and c.
        Vector2Int[] Neighbours(int r, int c)
        {
            var a = Shifted(r) ? 0 : -1;
            return new[]
            {
                new Vector2Int(r, c - 1), new Vector2Int(r, c + 1),
                new Vector2Int(r - 1, c + a), new Vector2Int(r - 1, c + a + 1),
                new Vector2Int(r + 1, c + a), new Vector2Int(r + 1, c + a + 1)
            };
        }

        bool Attached(int r, int c)
        {
            if (r == 0) return true;
            foreach (var n in Neighbours(r, c))
                if (Get(n.x, n.y) != null) return true;
            return false;
        }

        void EnsureRow(int r)
        {
            while (rows.Count <= r) rows.Add(new Bubble[Cols]);
        }

        void TrimRows()
        {
            while (rows.Count > 0 && System.Array.TrueForAll(rows[rows.Count - 1], b => b == null))
                rows.RemoveAt(rows.Count - 1);
        }

        void Refresh()
        {
            for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < ColsIn(r); c++)
                if (rows[r][c] != null) rows[r][c].img.SetPos(CellPos(r, c) + new Vector2(0f, slide));
        }

        // ----- setup -----

        protected override void OnStart()
        {
            Box(new Vector2(0f, (CeilY + DangerY) * 0.5f - 20f), new Vector2(1080f, CeilY - DangerY + 40f), Theme.Panel.Alpha(0.5f), false);
            Box(new Vector2(0f, DangerY), new Vector2(1080f, 5f), Theme.Bad.Alpha(0.35f), false);
            timerBar = Box(new Vector2(0f, DangerY - 22f), new Vector2(10f, 8f), Theme.TextDim.Alpha(0.5f), false);

            // pick 5 of the 6 palette colours
            var skip = Rand(0, Theme.Palette.Length);
            for (int i = 0, k = 0; i < Theme.Palette.Length; i++)
                if (i != skip) colours[k++] = i;

            for (var i = 0; i < AimDots; i++)
            {
                var dot = Disc(Vector2.zero, 16f, Color.white.Alpha(0.7f));
                dot.Show(false);
                dots.Add(dot);
            }
            for (var i = 0; i < MissLimit; i++)
                pips[i] = Disc(new Vector2(250f + i * 36f, ShooterY - 10f), 22f, Theme.PanelLight);

            SpawnRows(StartRows);

            Disc(new Vector2(0f, ShooterY), 150f, Theme.Panel);
            shooter = Disc(new Vector2(0f, ShooterY), D, Color.white);
            preview = Disc(new Vector2(-170f, ShooterY - 10f), 64f, Color.white);
            current = PickColour();
            next = PickColour();
            ShowShooter();
        }

        Bubble[] NewRow(int r)
        {
            var row = new Bubble[Cols];
            for (var c = 0; c < ColsIn(r); c++) row[c] = MakeBubble(colours[Rand(0, colours.Length)], Vector2.zero);
            return row;
        }

        Bubble MakeBubble(int colour, Vector2 pos) => new Bubble { color = colour, img = Disc(pos, D - 4f, Theme.Palette[colour]) };

        void SpawnRows(int count)
        {
            // rows are inserted at the top, so the final layout is parity-correct after each insert
            for (var i = 0; i < count; i++)
            {
                parity ^= 1;
                rows.Insert(0, NewRow(0));
            }
            Refresh();
        }

        int PickColour()
        {
            var present = new List<int>();
            foreach (var row in rows)
                foreach (var b in row)
                    if (b != null && !present.Contains(b.color)) present.Add(b.color);
            return present.Count > 0 ? present[Rand(0, present.Count)] : colours[Rand(0, colours.Length)];
        }

        bool InGrid(int colour)
        {
            foreach (var row in rows)
                foreach (var b in row)
                    if (b != null && b.color == colour) return true;
            return false;
        }

        void ShowShooter()
        {
            shooter.color = Theme.Palette[current];
            preview.color = Theme.Palette[next];
        }

        // ----- tick -----

        protected override void OnTick(float dt)
        {
            pushTimer += dt;
            if (pushTimer >= PushEvery)
            {
                Push();
                if (CheckDanger()) return;
            }
            timerBar.SetSize(Mathf.Max(10f, 1000f * pushTimer / PushEvery), 8f);

            if (slide > 0f)
            {
                slide = Mathf.Max(0f, slide - RowH * 6f * dt);
                Refresh();
            }
            reload = Mathf.Min(1f, reload + dt * 6f);
            shooter.SetScale(Mathf.Lerp(0.4f, 1f, reload));
            TickFx(dt);

            if (flying)
            {
                HideAim();
                StepFlight(dt);
                return;
            }

            var aiming = PointerHeld && PointerPos.y > AimMinY;
            if (aiming) DrawAim(AimDir(PointerPos));
            else HideAim();
            if (PointerUp && PointerPos.y > AimMinY) Fire(AimDir(PointerPos));
        }

        static Vector2 AimDir(Vector2 pointer)
        {
            var v = pointer - new Vector2(0f, ShooterY);
            var angle = Mathf.Clamp(Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg, 12f, 168f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        void DrawAim(Vector2 dir)
        {
            var p = new Vector2(0f, ShooterY);
            var stopped = false;
            for (var i = 0; i < AimDots; i++)
            {
                if (!stopped)
                {
                    p += dir * 50f;
                    if (p.x > WallX) { p.x = 2f * WallX - p.x; dir.x = -dir.x; }
                    else if (p.x < -WallX) { p.x = -2f * WallX - p.x; dir.x = -dir.x; }
                    if (p.y >= CeilY - R || Touches(p)) stopped = true;
                }
                dots[i].Show(!stopped);
                dots[i].SetPos(p);
            }
        }

        void HideAim()
        {
            foreach (var d in dots) d.Show(false);
        }

        // ----- shooting -----

        void Fire(Vector2 dir)
        {
            flying = true;
            flightColor = current;
            flightDir = dir;
            flightPos = new Vector2(0f, ShooterY);
            flightImg = Disc(flightPos, D - 4f, Theme.Palette[flightColor]);
            Sfx.Blip(520f, 0.04f, 0.15f);

            current = next;
            next = PickColour();
            reload = 0f;
            ShowShooter();
        }

        void StepFlight(float dt)
        {
            var remaining = Speed * dt;
            while (remaining > 0f && flying)
            {
                var s = Mathf.Min(remaining, 20f); // small steps so it can never tunnel through a bubble
                remaining -= s;
                flightPos += flightDir * s;
                if (flightPos.x > WallX) { flightPos.x = 2f * WallX - flightPos.x; flightDir.x = -flightDir.x; }
                else if (flightPos.x < -WallX) { flightPos.x = -2f * WallX - flightPos.x; flightDir.x = -flightDir.x; }
                if (flightPos.y >= CeilY - R || Touches(flightPos)) Land();
            }
            if (flying) flightImg.SetPos(flightPos);
        }

        bool Touches(Vector2 p)
        {
            var limit = D * 0.9f * D * 0.9f;
            foreach (var row in rows)
                foreach (var b in row)
                    if (b != null && (b.img.GetPos() - p).sqrMagnitude < limit) return true;
            return false;
        }

        void Land()
        {
            flying = false;
            // while a pushed row is still sliding in, bubbles are drawn `slide` above their cells
            var landed = flightPos - new Vector2(0f, slide);
            var r0 = Mathf.RoundToInt((CeilY - R - landed.y) / RowH);
            var best = new Vector2Int(-1, -1);
            var bestD = float.MaxValue;
            for (var r = Mathf.Max(0, r0 - 2); r <= r0 + 2; r++)
            for (var c = 0; c < ColsIn(r); c++)
            {
                if (Get(r, c) != null || !Attached(r, c)) continue;
                var d = (CellPos(r, c) - landed).sqrMagnitude;
                if (d < bestD) { bestD = d; best = new Vector2Int(r, c); }
            }

            if (best.x < 0) // no free cell nearby; cannot normally happen
            {
                Destroy(flightImg.gameObject);
                Settle(false, 0, flightPos);
                return;
            }

            EnsureRow(best.x);
            rows[best.x][best.y] = new Bubble { img = flightImg, color = flightColor };
            flightImg.SetPos(CellPos(best.x, best.y) + new Vector2(0f, slide));
            Resolve(best.x, best.y);
        }

        void Resolve(int r, int c)
        {
            var group = Group(r, c);
            var popped = group.Count >= 3;
            var gained = 0;
            var dropped = false;
            var at = CellPos(r, c);

            if (popped)
            {
                foreach (var g in group) Remove(g, true);
                gained += group.Count * 10;
                var loose = Loose();
                foreach (var g in loose) Remove(g, false);
                gained += loose.Count * 20;
                dropped = loose.Count > 0;
                TrimRows();
            }
            Settle(popped, gained, at, dropped);
        }

        void Settle(bool popped, int gained, Vector2 at, bool dropped = false)
        {
            if (popped)
            {
                Score += gained;
                Pop("+" + gained, new Vector2(Mathf.Clamp(at.x, -380f, 380f), at.y + 40f), Theme.Coin);
                if (dropped) Sfx.Great(); else Sfx.Good();
            }
            else
            {
                Sfx.Blip(300f, 0.05f, 0.15f);
                misses++;
            }

            if (rows.Count == 0)
            {
                Score += 200;
                Pop("CLEARED +200", new Vector2(0f, 300f), Theme.Good, 72);
                Sfx.Great();
                parity = 0;
                SpawnRows(5);
            }
            else if (misses >= MissLimit)
            {
                Push();
            }

            for (var i = 0; i < MissLimit; i++) pips[i].color = i < misses ? Theme.Bad : Theme.PanelLight;
            if (CheckDanger()) return;
            if (!InGrid(current)) current = PickColour();
            if (!InGrid(next)) next = PickColour();
            ShowShooter();
        }

        List<Vector2Int> Group(int r, int c)
        {
            var colour = rows[r][c].color;
            var seen = new HashSet<Vector2Int> { new Vector2Int(r, c) };
            var list = new List<Vector2Int> { new Vector2Int(r, c) };
            for (var i = 0; i < list.Count; i++)
                foreach (var n in Neighbours(list[i].x, list[i].y))
                {
                    var b = Get(n.x, n.y);
                    if (b != null && b.color == colour && seen.Add(n)) list.Add(n);
                }
            return list;
        }

        // Everything not reachable from the ceiling row.
        List<Vector2Int> Loose()
        {
            var seen = new HashSet<Vector2Int>();
            var list = new List<Vector2Int>();
            if (rows.Count > 0)
                for (var c = 0; c < ColsIn(0); c++)
                    if (rows[0][c] != null && seen.Add(new Vector2Int(0, c))) list.Add(new Vector2Int(0, c));
            for (var i = 0; i < list.Count; i++)
                foreach (var n in Neighbours(list[i].x, list[i].y))
                    if (Get(n.x, n.y) != null && seen.Add(n)) list.Add(n);

            var loose = new List<Vector2Int>();
            for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < ColsIn(r); c++)
                if (rows[r][c] != null && !seen.Contains(new Vector2Int(r, c))) loose.Add(new Vector2Int(r, c));
            return loose;
        }

        void Remove(Vector2Int cell, bool burst)
        {
            var b = rows[cell.x][cell.y];
            rows[cell.x][cell.y] = null;
            fx.Add(new Fx
            {
                img = b.img, burst = burst, life = burst ? 0.2f : 0.9f,
                vel = burst ? Vector2.zero : new Vector2(Rand(-120f, 120f), Rand(0f, 200f))
            });
        }

        void TickFx(float dt)
        {
            for (var i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.age += dt;
                if (f.age >= f.life)
                {
                    Destroy(f.img.gameObject);
                    fx.RemoveAt(i);
                    continue;
                }
                var t = f.age / f.life;
                if (f.burst) f.img.SetScale(1f + 0.5f * t);
                else
                {
                    f.vel.y -= 3200f * dt;
                    f.img.SetPos(f.img.GetPos() + f.vel * dt);
                }
                f.img.color = f.img.color.Alpha(1f - t);
            }
        }

        // ----- pressure -----

        void Push()
        {
            pushTimer = 0f;
            misses = 0;
            for (var i = 0; i < MissLimit; i++) pips[i].color = Theme.PanelLight;
            parity ^= 1; // old rows move down one row and must keep their horizontal shift
            rows.Insert(0, NewRow(0));
            slide = RowH;
            Refresh();
            Sfx.Blip(200f, 0.1f, 0.25f);
        }

        bool CheckDanger()
        {
            for (var r = DangerRow; r < rows.Count; r++)
                foreach (var b in rows[r])
                    if (b != null)
                    {
                        Sfx.Bad();
                        EndRound();
                        return true;
                    }
            return false;
        }
    }
}
