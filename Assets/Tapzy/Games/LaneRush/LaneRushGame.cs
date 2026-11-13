using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.LaneRush
{
    /// <summary>Swipe between three lanes to dodge barriers as the speed rises. Score is distance.</summary>
    [MiniGame("lane_rush", "Lane Rush", Kind = "Arcade", Colour = "FF5C7A", Order = 1,
        MaxScorePerSecond = 60f, MinDuration = 1.5f, MissionScore = 500)]
    public class LaneRushGame : MiniGameBase
    {
        const float LaneWidth = 300f;
        const float PlayerY = -560f;
        const float SpawnY = 1100f;
        const float StartSpeed = 900f;
        const float MaxSpeed = 2600f;
        const float Acceleration = 35f;
        const float UnitsPerPoint = 50f;

        class Barrier
        {
            public Image image;
            public float y;
        }

        readonly List<Barrier> barriers = new List<Barrier>();
        readonly List<Image> dashes = new List<Image>();
        Image player;
        int lane = 1, lastFreeLane = 1;
        float speed, distance, untilNextRow, dashOffset;

        protected override bool AllowContinue => true;
        protected override string Hint => "Swipe to change lane";

        static float LaneX(int index) => (index - 1) * LaneWidth;

        protected override void OnStart()
        {
            Box(Vector2.zero, new Vector2(LaneWidth * 3f, Ui.H), Theme.Panel, false);
            for (var side = -1; side <= 1; side += 2)
            for (var i = 0; i < 9; i++)
                dashes.Add(Box(new Vector2(side * LaneWidth * 0.5f, 0f), new Vector2(14f, 120f), Theme.PanelLight));

            player = Box(new Vector2(0f, PlayerY), new Vector2(170f, 170f), Theme.Coin);
            speed = StartSpeed;
            untilNextRow = 900f;
        }

        protected override void OnTick(float dt)
        {
            // steer: swipe sideways, or tap the left / right half
            var move = Swipe.x;
            if (move == 0 && Tapped) move = PointerPos.x < 0f ? -1 : 1;
            if (move != 0)
            {
                var target = Mathf.Clamp(lane + move, 0, 2);
                if (target != lane)
                {
                    lane = target;
                    Sfx.Blip(660f, 0.04f, 0.15f);
                }
            }
            var pos = player.GetPos();
            pos.x = Mathf.MoveTowards(pos.x, LaneX(lane), 3600f * dt);
            player.SetPos(pos);

            // advance the world
            speed = Mathf.Min(MaxSpeed, speed + Acceleration * dt);
            var step = speed * dt;
            distance += step;
            Score = Mathf.FloorToInt(distance / UnitsPerPoint);

            dashOffset = (dashOffset + step) % 240f;
            for (var i = 0; i < dashes.Count; i++)
            {
                var x = dashes[i].GetPos().x;
                dashes[i].SetPos(x, 1080f - (i % 9) * 240f - dashOffset);
            }

            untilNextRow -= step;
            if (untilNextRow <= 0f)
            {
                SpawnRow();
                untilNextRow = Mathf.Max(640f, speed * 0.55f); // always about half a second to react
            }

            for (var i = barriers.Count - 1; i >= 0; i--)
            {
                var b = barriers[i];
                b.y -= step;
                var bp = b.image.GetPos();
                b.image.SetPos(bp.x, b.y);
                if (b.y < -1100f)
                {
                    Destroy(b.image.gameObject);
                    barriers.RemoveAt(i);
                    continue;
                }
                if (Mathf.Abs(b.y - PlayerY) < 130f && Mathf.Abs(bp.x - pos.x) < 200f)
                {
                    Sfx.Bad();
                    b.image.color = Theme.Text;
                    EndRound();
                    return;
                }
            }
        }

        void SpawnRow()
        {
            // one lane always stays open, and never more than one lane away from the last opening
            var free = Mathf.Clamp(lastFreeLane + Rand(-1, 2), 0, 2);
            var blockTwo = Rng.NextDouble() < Mathf.Lerp(0.25f, 0.7f, Mathf.InverseLerp(StartSpeed, MaxSpeed, speed));
            var keepOpen = blockTwo ? -1 : PickOther(free);
            for (var i = 0; i < 3; i++)
            {
                if (i == free || i == keepOpen) continue;
                var img = Box(new Vector2(LaneX(i), SpawnY), new Vector2(250f, 110f), Theme.Bad);
                barriers.Add(new Barrier { image = img, y = SpawnY });
            }
            lastFreeLane = free;
            player.transform.SetAsLastSibling();
        }

        int PickOther(int free)
        {
            int other;
            do other = Rand(0, 3);
            while (other == free);
            return other;
        }

        protected override void OnContinue()
        {
            foreach (var b in barriers) Destroy(b.image.gameObject);
            barriers.Clear();
            speed = Mathf.Max(StartSpeed, speed * 0.75f);
            untilNextRow = 900f;
        }
    }
}
