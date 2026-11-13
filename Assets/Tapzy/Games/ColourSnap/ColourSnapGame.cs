using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.ColourSnap
{
    /// <summary>Tap only the shapes that match the target colour. Wrong taps cost time.</summary>
    [MiniGame("colour_snap", "Colour Snap", Kind = "Arcade", Colour = "4DA3FF", Order = 2,
        MaxScorePerSecond = 45f, MinDuration = 5f, MissionScore = 250)]
    public class ColourSnapGame : MiniGameBase
    {
        const float ShapeSize = 170f;
        const float HitRadius = 100f;
        const float MinGap = 200f;
        const float TargetY = 620f;
        const float RoundTime = 45f;

        class Shape
        {
            public Image image;
            public int colour;
            public float age, life;
        }

        readonly List<Shape> shapes = new List<Shape>();
        Image swatch;
        Text swatchName;
        int target = -1, previousTarget = -1;
        float untilSpawn = 0.3f, untilTargetChange, flash;

        protected override float TimeLimit => RoundTime;
        protected override string Hint => "Tap the target colour";

        protected override void OnStart()
        {
            swatch = Box(new Vector2(0f, TargetY), new Vector2(620f, 150f), Theme.Palette[0]);
            swatchName = Label("", new Vector2(0f, TargetY), 76, Theme.Dark, 600f);
            ChangeTarget(false);
        }

        protected override void OnTick(float dt)
        {
            var progress = Mathf.Clamp01(Elapsed / RoundTime);

            untilTargetChange -= dt;
            if (untilTargetChange <= 0f) ChangeTarget(true);
            AnimateTarget(dt);

            untilSpawn -= dt;
            if (untilSpawn <= 0f)
                untilSpawn = TrySpawn(progress) ? Mathf.Lerp(0.55f, 0.3f, progress) : 0.1f;

            if (PointerDown) HandleTap(PointerDownPos);

            for (var i = shapes.Count - 1; i >= 0; i--)
            {
                var s = shapes[i];
                s.age += dt;
                if (s.age >= s.life)
                {
                    Destroy(s.image.gameObject);
                    shapes.RemoveAt(i);
                    continue;
                }
                var t = s.age / s.life;
                var c = s.image.color;
                c.a = Mathf.Lerp(1f, 0.3f, t * t);
                s.image.color = c;
                var pop = Mathf.Clamp01(s.age / 0.12f); // quick pop-in
                s.image.SetScale(Mathf.Lerp(0.4f, 1f, pop) * Mathf.Lerp(1f, 0.8f, t));
            }
        }

        void ChangeTarget(bool announce)
        {
            int next;
            do next = Rand(0, Theme.Palette.Length);
            while (next == target);
            previousTarget = target;
            target = next;
            untilTargetChange = Rand(7f, 9f);
            swatch.color = Theme.Palette[target];
            swatchName.text = Theme.PaletteNames[target];
            var c = Theme.Palette[target];
            swatchName.color = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f > 0.55f ? Theme.Dark : Theme.Text;
            if (!announce) return;
            flash = 0.35f;
            Sfx.Blip(880f, 0.08f, 0.2f);
        }

        void AnimateTarget(float dt)
        {
            if (flash <= 0f) return;
            flash = Mathf.Max(0f, flash - dt);
            swatch.SetScale(1f + 0.25f * Mathf.Sin(flash / 0.35f * Mathf.PI));
        }

        bool TrySpawn(float progress)
        {
            // keep about 40% of spawns on target, and never leave the board without one
            var hasTarget = shapes.Exists(s => s.colour == target);
            var colour = !hasTarget || Rng.NextDouble() < 0.4 ? target : RandomOther();

            for (var attempt = 0; attempt < 12; attempt++)
            {
                var pos = new Vector2(Rand(-430f, 430f), Rand(-820f, 440f));
                if (shapes.Exists(s => (s.image.GetPos() - pos).magnitude < MinGap)) continue;

                var img = Rng.NextDouble() < 0.5
                    ? Disc(pos, ShapeSize, Theme.Palette[colour])
                    : Box(pos, new Vector2(ShapeSize, ShapeSize), Theme.Palette[colour]);
                shapes.Add(new Shape { image = img, colour = colour, life = Mathf.Lerp(1.8f, 1.0f, progress) });
                return true;
            }
            return false;
        }

        int RandomOther()
        {
            int c;
            do c = Rand(0, Theme.Palette.Length);
            while (c == target);
            return c;
        }

        void HandleTap(Vector2 p)
        {
            for (var i = shapes.Count - 1; i >= 0; i--) // newest first
            {
                var s = shapes[i];
                var pos = s.image.GetPos();
                if ((pos - p).magnitude > HitRadius) continue;

                if (s.colour == target)
                {
                    var points = 10 + Mathf.RoundToInt(5f * (1f - s.age / s.life));
                    Score += points;
                    Sfx.Good();
                    Pop("+" + points, pos, Theme.Good);
                }
                else if (s.colour == previousTarget && flash > 0f)
                {
                    // the target changed a moment ago: too late to score, too soon to punish
                    Pop("Changed!", pos, Theme.TextDim);
                }
                else
                {
                    TimeLeft -= 3f;
                    Sfx.Bad();
                    Pop("-3s", pos, Theme.Bad);
                }
                Destroy(s.image.gameObject);
                shapes.RemoveAt(i);
                return;
            }
        }
    }
}
