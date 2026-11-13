using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.BladeSpin
{
    /// <summary>Throw blades into a spinning disc without hitting the ones already stuck in it.</summary>
    [MiniGame("blade_spin", "Blade Spin", Kind = "Arcade", Colour = "B07CFF", Order = 5,
        MaxScorePerSecond = 6f, ScoreBase = 10f, MinDuration = 2f, MissionScore = 30)]
    public class BladeSpinGame : MiniGameBase
    {
        const float Radius = 210f;
        const float BladeLength = 190f;
        const float Buried = 80f;            // how far the tip sits inside the rim once stuck
        const float RestY = -640f;
        const float FlySpeed = 4200f;
        const float ImpactY = 330f - Radius - BladeLength * 0.5f; // centre y when the tip touches the rim
        // degrees between blade centres. A blade is about 7 degrees wide at the rim, so 9 reads as a fair touch
        // and still leaves room on late stages (up to 12 blades in the disc at once).
        const float MinGap = 9f;
        const float RearmDelay = 0.2f;
        static readonly Vector2 TargetPos = new Vector2(0f, 330f);

        enum Phase { Rearm, Idle, Flying, Bounce, Between }

        readonly List<float> angles = new List<float>();   // local angles of every stuck blade
        readonly List<Image> stuck = new List<Image>();
        readonly List<Image> markers = new List<Image>();
        RectTransform target;
        Image blade;
        Vector2 bounceVel;
        Phase phase = Phase.Rearm;
        int stage, quota, thrown, mode;
        float rot, spin, baseSpeed, dir, freq, shift, flipTimer, kick, timer;
        bool buffered;

        protected override string Hint => "Tap to throw blades";
        protected override bool AllowContinue => true;

        protected override void OnStart()
        {
            target = Ui.Node(Field, "Target", TargetPos, new Vector2(Radius * 2f, Radius * 2f));
            Ui.Disc(target, Theme.Primary, Vector2.zero, Radius * 2f);
            Ui.Disc(target, Theme.PanelLight, Vector2.zero, 340f);
            Ui.Disc(target, Theme.Primary.Shade(0.75f), Vector2.zero, 250f);
            Ui.Disc(target, Theme.Coin, new Vector2(0f, 110f), 56f);    // off-centre dots make the spin readable
            Ui.Disc(target, Theme.Good, new Vector2(-95f, -60f), 34f);
            rot = Rand(0f, 360f);
            StartStage(1);
        }

        void StartStage(int n)
        {
            stage = n;
            quota = Mathf.Min(10, 4 + n);
            thrown = 0;

            baseSpeed = n == 1 ? 110f : Mathf.Clamp(Mathf.Lerp(110f, 260f, (n - 1) / 8f) + Rand(-15f, 15f), 110f, 260f);
            dir = n > 1 && Rng.NextDouble() < 0.5 ? -1f : 1f;
            mode = n < 3 ? 0 : Rand(1, 3); // 0 steady, 1 sinusoidal, 2 periodic reversals
            freq = Rand(1.5f, 3f);
            shift = Rand(0f, 6.28f);
            flipTimer = Rand(1.2f, 2.2f);
            spin = dir * baseSpeed;

            if (n >= 2)
            {
                var count = Rand(1, Mathf.Min(quota >= 9 ? 2 : 3, n) + 1);
                for (var i = 0; i < count; i++)
                {
                    for (var tries = 0; tries < 30; tries++)
                    {
                        var a = Rand(-180f, 180f);
                        if (Clashes(a, MinGap * 2f)) continue;
                        Stick(a, Theme.Bad);
                        break;
                    }
                }
            }

            foreach (var m in markers) Destroy(m.gameObject);
            markers.Clear();
            for (var i = 0; i < quota; i++)
                markers.Add(Box(new Vector2(-460f, -700f + i * 52f), new Vector2(18f, 38f), Theme.Text));
            phase = Phase.Rearm;
            timer = 0.1f;
            buffered = false; // a tap made before the new obstacles were visible must not throw
        }

        bool Clashes(float angle, float gap)
        {
            foreach (var a in angles)
                if (Mathf.Abs(Mathf.DeltaAngle(a, angle)) < gap) return true;
            return false;
        }

        Image MakeBlade(Transform parent, Color body)
        {
            var img = Ui.Box(parent, body, Vector2.zero, new Vector2(26f, BladeLength));
            Ui.Box(img.transform, Theme.TextDim.Shade(0.5f), new Vector2(0f, -60f), new Vector2(30f, 70f));
            return img;
        }

        // Puts a blade into the target at a local angle (degrees, counter-clockwise from +x), pointing at the centre.
        void Stick(float angle, Color body)
        {
            var img = MakeBlade(target, body);
            var rad = angle * Mathf.Deg2Rad;
            img.SetPos(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (Radius + BladeLength * 0.5f - Buried));
            img.SetRot(angle + 90f); // the image's tip faces +y at rotation 0, which is angle 90
            angles.Add(angle);
            stuck.Add(img);
        }

        void ShowMarkers(bool inFlight)
        {
            var left = quota - thrown - (inFlight ? 1 : 0);
            for (var i = 0; i < markers.Count; i++) markers[i].Show(i < left);
        }

        protected override void OnTick(float dt)
        {
            if (PointerDown) buffered = true;
            Spin(dt);

            switch (phase)
            {
                case Phase.Rearm:
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        blade = MakeBlade(Field, Theme.Text);
                        blade.SetPos(0f, RestY);
                        ShowMarkers(false);
                        phase = Phase.Idle;
                    }
                    break;
                case Phase.Idle:
                    if (buffered)
                    {
                        buffered = false;
                        phase = Phase.Flying;
                        ShowMarkers(true);
                        Sfx.Blip(520f, 0.04f, 0.15f);
                    }
                    break;
                case Phase.Flying:
                    var y = blade.GetPos().y + FlySpeed * dt;
                    blade.SetPos(0f, y);
                    if (y >= ImpactY) Impact();
                    break;
                case Phase.Bounce:
                    timer -= dt;
                    blade.SetPos(blade.GetPos() + bounceVel * dt);
                    blade.SetRot(blade.transform.localEulerAngles.z + 900f * dt);
                    bounceVel.y -= 2500f * dt;
                    if (timer <= 0f) EndRound();
                    break;
                case Phase.Between:
                    timer -= dt;
                    if (timer <= 0f) StartStage(stage + 1);
                    break;
            }
        }

        void Spin(float dt)
        {
            var goal = dir * baseSpeed;
            if (mode == 1) goal *= 1f + 0.6f * Mathf.Sin(Elapsed * freq + shift);
            else if (mode == 2)
            {
                flipTimer -= dt;
                if (flipTimer <= 0f)
                {
                    dir = -dir;
                    flipTimer = Rand(1.2f, 2.2f);
                }
                goal = dir * baseSpeed;
            }
            spin = Mathf.MoveTowards(spin, goal, 900f * dt);
            rot = Mathf.Repeat(rot + spin * dt, 360f);
            target.SetRot(rot);

            kick = Mathf.MoveTowards(kick, 0f, 5f * dt);
            target.SetScale(1f + 0.08f * kick);
        }

        void Impact()
        {
            // The blade always arrives from straight below (world angle -90); undo the target's rotation.
            var local = Mathf.DeltaAngle(0f, -90f - rot);
            if (Clashes(local, MinGap))
            {
                Sfx.Bad();
                bounceVel = new Vector2(Rand(-700f, 700f), -900f);
                phase = Phase.Bounce;
                timer = 0.4f;
                buffered = false;
                return;
            }

            Destroy(blade.gameObject);
            blade = null;
            Stick(local, Theme.Text);
            thrown++;
            Score += 1;
            kick = 1f;
            Sfx.Good();
            ShowMarkers(false);

            if (thrown < quota)
            {
                phase = Phase.Rearm;
                timer = RearmDelay;
                return;
            }
            Score += 5;
            Pop("Stage " + stage, TargetPos + new Vector2(0f, 300f), Theme.Coin, 72);
            Sfx.Great();
            foreach (var s in stuck) Destroy(s.gameObject);
            stuck.Clear();
            angles.Clear();
            phase = Phase.Between;
            timer = 0.45f;
        }

        protected override void OnContinue()
        {
            if (blade != null) Destroy(blade.gameObject);
            blade = null;
            buffered = false;
            phase = Phase.Rearm;
            timer = 0.3f;
        }
    }
}
