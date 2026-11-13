using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.JuiceSlash
{
    /// <summary>Slice the fruit as it flies up, avoid the bombs, and lose the round after three misses.</summary>
    [MiniGame("juice_slash", "Juice Slash", Kind = "Arcade", Colour = "FF9447", Order = 3,
        MaxScorePerSecond = 12f, ScoreBase = 10f, MinDuration = 3f, MissionScore = 60)]
    public class JuiceSlashGame : MiniGameBase
    {
        const float Gravity = 2200f;
        const float LaunchY = -1050f;
        const float Radius = 85f;
        const float SliceReach = Radius + 20f; // a little forgiving: a near miss with a thumb should still cut
        const float MaxX = 450f;
        const int MaxLives = 3;
        const float TrailLife = 0.15f;
        const float PieceLife = 0.6f;

        class Thing
        {
            public Image body;
            public Vector2 pos, vel;
            public float spin, angle;
            public bool bomb;
            public Color colour;
        }

        class Fade
        {
            public Image image;
            public Vector2 vel;
            public float age, life;
            public Color colour;
        }

        readonly List<Thing> things = new List<Thing>();
        readonly List<Fade> pieces = new List<Fade>();
        readonly List<Fade> trails = new List<Fade>();
        readonly List<Image> hearts = new List<Image>();
        Image flash;
        int misses, combo;
        float untilWave;
        bool hasLast;
        Vector2 lastPointer;

        protected override bool AllowContinue => true;
        protected override string Hint => "Swipe to slice fruit";

        protected override void OnStart()
        {
            flash = Box(Vector2.zero, new Vector2(Ui.W, Ui.H), Theme.Bad.Alpha(0f), false);
            for (var i = 0; i < MaxLives; i++)
                hearts.Add(Disc(new Vector2(-440f + i * 70f, 700f), 50f, Theme.Bad));
            untilWave = 0.6f;
        }

        protected override void OnTick(float dt)
        {
            untilWave -= dt;
            if (untilWave <= 0f)
            {
                SpawnWave();
                untilWave = Mathf.Lerp(1.6f, 0.9f, Mathf.Clamp01(Elapsed / 90f));
            }

            HandleSlicing();
            MoveThings(dt);
            TickFades(pieces, dt, true);
            TickFades(trails, dt, false);

            var a = flash.color.a;
            if (a > 0f) flash.color = Theme.Bad.Alpha(Mathf.Max(0f, a - dt * 1.5f));
        }

        // ----- spawning -----

        void SpawnWave()
        {
            var maxCount = Mathf.Clamp(1 + (int)(Elapsed / 15f), 1, 4);
            var count = Rand(1, maxCount + 1);
            var bombChance = Elapsed < 8f ? 0f : Mathf.Lerp(0.1f, 0.2f, Mathf.Clamp01((Elapsed - 8f) / 60f));
            var slot = 2f * (MaxX - 90f) / count; // one slot per object so a wave never overlaps itself
            for (var i = 0; i < count; i++)
            {
                var x0 = -(MaxX - 90f) + (i + Rand(0.15f, 0.85f)) * slot;
                var apex = Rand(100f, 650f);
                var vy = Mathf.Sqrt(2f * Gravity * (apex - LaunchY));
                var flight = 2f * vy / Gravity;
                var endX = Mathf.Clamp(x0 + Rand(-250f, 250f), -MaxX, MaxX);
                Spawn(new Vector2(x0, LaunchY), new Vector2((endX - x0) / flight, vy), Rng.NextDouble() < bombChance);
            }
        }

        void Spawn(Vector2 pos, Vector2 vel, bool bomb)
        {
            var t = new Thing { pos = pos, vel = vel, bomb = bomb, spin = Rand(-120f, 120f) };
            if (bomb)
            {
                t.colour = Theme.Bad;
                t.body = Disc(pos, Radius * 2f, Theme.Bad);
                Ui.Disc(t.body.transform, Theme.Dark, Vector2.zero, 142f);
                Ui.Disc(t.body.transform, Theme.Bad, Vector2.zero, 44f);
            }
            else
            {
                t.colour = Theme.Palette[Rand(0, Theme.Palette.Length)];
                t.body = Disc(pos, Radius * 2f, t.colour);
                Ui.Disc(t.body.transform, Color.Lerp(t.colour, Color.white, 0.55f), new Vector2(-24f, 24f), 62f);
            }
            things.Add(t);
        }

        // ----- slicing -----

        void HandleSlicing()
        {
            if (!PointerHeld)
            {
                hasLast = false;
                return;
            }
            if (PointerDown || !hasLast)
            {
                hasLast = true;
                lastPointer = PointerPos;
                combo = 0;
                return;
            }
            var from = lastPointer;
            var to = PointerPos;
            if ((to - from).sqrMagnitude < 16f) return;
            lastPointer = to;
            AddTrail(from, to);

            for (var i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (SegmentDistance(t.pos, from, to) > (t.bomb ? Radius : SliceReach)) continue;
                things.RemoveAt(i);
                if (t.bomb) HitBomb(t);
                else SliceFruit(t, to - from);
                if (misses >= MaxLives) return;
            }
        }

        void SliceFruit(Thing t, Vector2 dir)
        {
            combo++;
            Score += combo >= 3 ? 2 : 1;
            Sfx.Good();
            Pop(combo >= 3 ? "Combo x" + combo : "+1", t.pos, combo >= 3 ? Theme.Coin : Theme.Text);

            // the halves fly apart sideways to the swipe
            var side = new Vector2(-dir.y, dir.x).normalized;
            for (var s = -1; s <= 1; s += 2)
                pieces.Add(new Fade
                {
                    image = Disc(t.pos + side * s * 25f, Radius, t.colour),
                    vel = t.vel * 0.3f + side * s * 380f + Vector2.up * 150f,
                    life = PieceLife,
                    colour = t.colour
                });
            Destroy(t.body.gameObject);
        }

        void HitBomb(Thing t)
        {
            combo = 0;
            flash.color = Theme.Bad.Alpha(0.45f);
            Pop("Boom!", t.pos, Theme.Bad, 72);
            Destroy(t.body.gameObject);
            Miss();
        }

        void AddTrail(Vector2 from, Vector2 to)
        {
            var d = to - from;
            var img = Box((from + to) * 0.5f, new Vector2(d.magnitude + 10f, 14f), Color.white, false);
            img.SetRot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            trails.Add(new Fade { image = img, life = TrailLife, colour = Color.white });
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // ----- motion -----

        void MoveThings(float dt)
        {
            for (var i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                t.vel.y -= Gravity * dt;
                t.pos += t.vel * dt;
                t.angle += t.spin * dt;
                t.body.SetPos(t.pos);
                t.body.SetRot(t.angle);
                if (t.vel.y >= 0f || t.pos.y > LaunchY) continue;

                things.RemoveAt(i);
                Destroy(t.body.gameObject);
                if (t.bomb) continue;
                Pop("Miss", new Vector2(t.pos.x, -700f), Theme.Bad);
                Miss();
                if (misses >= MaxLives) return;
            }
        }

        void TickFades(List<Fade> list, float dt, bool falling)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var f = list[i];
                f.age += dt;
                if (f.age >= f.life)
                {
                    Destroy(f.image.gameObject);
                    list.RemoveAt(i);
                    continue;
                }
                if (falling)
                {
                    f.vel.y -= Gravity * dt;
                    f.image.SetPos(f.image.GetPos() + f.vel * dt);
                }
                f.image.color = f.colour.Alpha(1f - f.age / f.life);
            }
        }

        // ----- lives -----

        void Miss()
        {
            if (misses >= MaxLives) return;
            misses++;
            Sfx.Bad();
            RefreshHearts();
            if (misses >= MaxLives) EndRound();
        }

        void RefreshHearts()
        {
            for (var i = 0; i < hearts.Count; i++)
                hearts[i].color = i < MaxLives - misses ? Theme.Bad : Theme.PanelLight;
        }

        protected override void OnContinue()
        {
            foreach (var t in things) Destroy(t.body.gameObject);
            things.Clear();
            misses = MaxLives - 1;
            RefreshHearts();
            hasLast = false;
            untilWave = 1.2f;
        }
    }
}
