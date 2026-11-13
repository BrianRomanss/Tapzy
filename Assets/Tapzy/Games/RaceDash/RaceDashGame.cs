using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.RaceDash
{
    /// <summary>
    /// A 200 metre sprint in four lanes against three computer runners. A marker sweeps across
    /// the stride bar: tap while it is in the middle to speed up, tap badly and you stumble.
    /// The faster you run, the faster the marker sweeps.
    ///
    /// Score: 10 per perfect tap, 5 per good tap, then 400 / 200 / 100 / 0 for finishing
    /// first / second / third / fourth.
    /// </summary>
    [MiniGame("race_dash", "Race Dash", Kind = "Sports", Colour = "FF9A1F", Order = 12,
        MaxScorePerSecond = 25f, ScoreBase = 500f, MinDuration = 12f, MissionScore = 350)]
    public class RaceDashGame : MiniGameBase
    {
        const int Runners = 4;
        const int MyLane = 1;
        const float Distance = 200f;       // metres
        const float MaxSpeed = 10f;        // metres per second
        const float PerfectZone = 0.22f;   // marker runs from -1 to 1
        const float GoodZone = 0.55f;
        const float PerfectBoost = 2.2f, GoodBoost = 1.3f;
        const float CountdownSeconds = 2.4f;

        // the track is drawn inside its own clipped panel; these are positions inside that panel
        const float TrackY = 20f, TrackW = 840f, TrackH = 1160f;
        const float LaneW = 200f;
        const float PixelsPerMetre = 60f;
        const float RunnerY = -220f;       // where the player's runner sits; the track scrolls past it
        const float RunnerSize = 110f;

        const float BarY = -720f, BarHalf = 400f;
        const float StripY = 690f, StripHalf = 440f;

        static readonly int[] PlacePoints = { 400, 200, 100, 0 };
        static readonly string[] PlaceNames = { "1st", "2nd", "3rd", "4th" };

        enum Phase { Countdown, Racing, Over }

        class Runner
        {
            public string name;
            public Color colour;
            public Bot bot;              // null for the player
            public float dist, speed, cruise, nextStumble, wobble;
            public int place;            // 0 while still running
            public Image image, stripDot;
            public Text tag;
        }

        readonly Runner[] runners = new Runner[Runners];
        readonly Image[] lines = new Image[4];
        readonly Text[] lineLabels = new Text[4];

        Phase phase;
        float timer, raceTime, sweep;
        int finished, lastCount = -1;
        bool won;

        RectTransform track;
        Image marker;
        Text placeText, leftText;

        protected override string Hint => "Tap when the marker's centred.";

        Runner Me => runners[MyLane];

        // ---------- setup ----------

        protected override void OnStart()
        {
            var bots = Bots.Pick(Rng, Runners - 1);
            var botColours = new[] { Theme.Palette[0], Theme.Palette[2], Theme.Palette[3] };

            track = Ui.Node(Field, "Track", new Vector2(0f, TrackY), new Vector2(TrackW, TrackH));
            track.gameObject.AddComponent<RectMask2D>();
            Ui.Box(track, Theme.Panel, Vector2.zero, new Vector2(TrackW, TrackH), null, false);
            Ui.Box(track, Theme.PanelLight.Alpha(0.5f), new Vector2(LaneX(MyLane), 0f), new Vector2(LaneW, TrackH), null, false);
            for (var i = 1; i < Runners; i++)
                Ui.Box(track, Color.white.Alpha(0.25f), new Vector2(LaneX(i) - LaneW * 0.5f, 0f), new Vector2(6f, TrackH), null, false);

            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = Ui.Box(track, Color.white, Vector2.zero, new Vector2(TrackW, 8f), null, false);
                lineLabels[i] = Ui.Label(track, "", 34, Color.white, Vector2.zero, new Vector2(400f, 50f), TextAnchor.MiddleLeft, null, true);
            }

            // race overview strip: one dot per runner, start on the left, finish on the right
            Box(new Vector2(0f, StripY), new Vector2(StripHalf * 2f + 40f, 20f), Theme.PanelLight);
            Box(new Vector2(StripHalf, StripY), new Vector2(10f, 60f), Color.white, false);

            var nextBot = 0;
            for (var i = 0; i < Runners; i++)
            {
                var r = new Runner();
                if (i == MyLane)
                {
                    r.name = "YOU";
                    r.colour = Theme.Palette[1];
                }
                else
                {
                    r.bot = bots[nextBot];
                    r.name = r.bot.Label;
                    r.colour = botColours[nextBot++];
                    r.cruise = Mathf.Lerp(5.8f, 9f, r.bot.Skill);
                    r.wobble = Rand(0f, 6f);
                    r.nextStumble = StumbleGap(r.bot);
                }
                r.image = Ui.Disc(track, r.colour, new Vector2(LaneX(i), RunnerY), RunnerSize);
                Ui.Disc(r.image.transform, r.colour.Shade(0.7f), Vector2.zero, RunnerSize * 0.45f);
                r.tag = Ui.Label(track, r.name, i == MyLane ? 36 : 26, Theme.Text, Vector2.zero, new Vector2(LaneW, 44f), bold: true);
                runners[i] = r;
            }
            // dots last so they sit on top of the strip; the player's on top of everyone's
            for (var i = 0; i < Runners; i++)
                if (i != MyLane)
                    runners[i].stripDot = Disc(new Vector2(-StripHalf, StripY), 40f, runners[i].colour);
            Me.stripDot = Disc(new Vector2(-StripHalf, StripY), 54f, Me.colour);

            placeText = Ui.Label(Field, "", 70, Theme.Text, new Vector2(-330f, -610f), new Vector2(300f, 90f), TextAnchor.MiddleLeft, null, true);
            leftText = Ui.Label(Field, "", 50, Theme.TextDim, new Vector2(300f, -610f), new Vector2(360f, 80f), TextAnchor.MiddleRight, null, true);

            // stride bar: wide "good" zone, narrow "perfect" zone, sweeping marker
            Box(new Vector2(0f, BarY), new Vector2(BarHalf * 2f + 40f, 90f), Theme.Dark);
            Box(new Vector2(0f, BarY), new Vector2(BarHalf * 2f * GoodZone, 60f), Theme.Coin, false);
            Box(new Vector2(0f, BarY), new Vector2(BarHalf * 2f * PerfectZone, 60f), Theme.Good, false);
            marker = Box(new Vector2(-BarHalf, BarY), new Vector2(20f, 120f), Color.white);
            Ui.Label(Field, "Tap anywhere when the marker is in the green", 30, Theme.TextDim,
                new Vector2(0f, BarY - 110f), new Vector2(1000f, 44f));

            phase = Phase.Countdown;
            timer = CountdownSeconds;
            Draw();
        }

        static float LaneX(int lane) => (lane - (Runners - 1) * 0.5f) * LaneW;

        /// <summary>Seconds until a bot's next stumble: better bots stumble less often.</summary>
        float StumbleGap(Bot bot) => Mathf.Lerp(3.5f, 9f, bot.Skill) * Rand(0.6f, 1.4f);

        // ---------- main loop ----------

        protected override void OnTick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Countdown:
                    var count = Mathf.CeilToInt(timer / (CountdownSeconds / 3f));
                    if (count != lastCount && count > 0)
                    {
                        lastCount = count;
                        Pop(count.ToString(), new Vector2(0f, 200f), Theme.Text, 200);
                        Sfx.Blip(660f, 0.08f, 0.3f);
                    }
                    if (timer <= 0f)
                    {
                        phase = Phase.Racing;
                        Pop("GO!", new Vector2(0f, 200f), Theme.Good, 220);
                        Sfx.Blip(1175f, 0.2f, 0.35f);
                    }
                    break;

                case Phase.Racing:
                    raceTime += dt;
                    TickMarker(dt);
                    if (PointerDown) Stride();
                    TickRunners(dt);
                    break;

                case Phase.Over:
                    TickRunners(dt); // everyone jogs on past the line behind the banner
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

        /// <summary>The marker sweeps back and forth, faster the faster the player is running.</summary>
        void TickMarker(float dt)
        {
            var passSeconds = Mathf.Lerp(1f, 0.42f, Me.speed / MaxSpeed);
            sweep += dt * 2f / passSeconds;
        }

        float MarkerPos => Mathf.PingPong(sweep, 2f) - 1f;

        /// <summary>The player tapped: judge it by how close the marker is to the middle.</summary>
        void Stride()
        {
            var off = Mathf.Abs(MarkerPos);
            var at = new Vector2(MarkerPos * BarHalf, BarY + 120f);
            if (off <= PerfectZone)
            {
                Me.speed = Mathf.Min(MaxSpeed, Me.speed + PerfectBoost);
                Score += 10;
                Pop("PERFECT", at, Theme.Good, 64);
                Sfx.Blip(880f + 40f * Me.speed, 0.06f, 0.3f);
            }
            else if (off <= GoodZone)
            {
                Me.speed = Mathf.Min(MaxSpeed, Me.speed + GoodBoost);
                Score += 5;
                Pop("Good", at, Theme.Coin, 52);
                Sfx.Blip(660f, 0.05f, 0.22f);
            }
            else
            {
                Me.speed *= 0.75f;
                Pop("Stumble!", at, Theme.Bad, 60);
                Shake(10f, 0.2f);
                Sfx.Bad();
            }
        }

        void TickRunners(float dt)
        {
            foreach (var r in runners)
            {
                if (r.bot == null)
                {
                    // the player slows down on their own; only good taps keep the speed up
                    r.speed = Mathf.Max(0f, r.speed - (0.6f + 0.15f * r.speed) * dt);
                }
                else
                {
                    var target = r.cruise + 0.6f * Mathf.Sin(raceTime * 0.7f + r.wobble);
                    r.speed = Mathf.MoveTowards(r.speed, target, 3.5f * dt);
                    if (r.place == 0 && raceTime >= r.nextStumble)
                    {
                        r.speed *= 0.45f;
                        r.nextStumble = raceTime + StumbleGap(r.bot);
                    }
                }
                r.dist += r.speed * dt;
                if (r.place == 0 && r.dist >= Distance) r.place = ++finished;
            }

            if (phase != Phase.Racing) return;
            if (Me.place > 0) GameOver(Me.place);
            else if (finished == Runners - 1) GameOver(Runners); // everyone else is home
        }

        void GameOver(int place)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = place == 1;
            var points = PlacePoints[place - 1];
            Score += points;

            Box(new Vector2(0f, 60f), new Vector2(940f, 300f), Theme.Dark);
            Label(won ? "YOU WIN!" : PlaceNames[place - 1] + " place", new Vector2(0f, points > 0 ? 100f : 60f), 96,
                won ? Theme.Coin : Theme.Text, 900f);
            if (points > 0) Label("+" + points, new Vector2(0f, 0f), 64, Theme.Good, 900f);
            if (won)
            {
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else if (place < Runners) Sfx.Good();
            else Sfx.Bad();
        }

        // ---------- drawing ----------

        void Draw()
        {
            var my = Me.dist;
            for (var i = 0; i < Runners; i++)
            {
                var r = runners[i];
                var y = RunnerY + (r.dist - my) * PixelsPerMetre;
                r.image.SetPos(LaneX(i), y);
                r.image.SetScale(1f + 0.08f * Mathf.Sin(r.dist * 2.4f)); // bobs with every stride
                r.tag.SetPos(LaneX(i), y - RunnerSize * 0.5f - 28f);
                r.stripDot.SetPos(-StripHalf + StripHalf * 2f * Mathf.Clamp01(r.dist / Distance), StripY);
            }

            // distance lines every 10 metres; the last one is the finish
            var first = Mathf.FloorToInt(my / 10f) - 1;
            for (var i = 0; i < lines.Length; i++)
            {
                var metres = (first + i) * 10;
                var show = metres >= 0 && metres <= Distance;
                lines[i].Show(show);
                lineLabels[i].Show(show);
                if (!show) continue;
                var finish = metres >= Distance;
                var y = RunnerY + (metres - my) * PixelsPerMetre;
                lines[i].SetPos(0f, y);
                lines[i].SetSize(TrackW, finish ? 36f : 8f);
                lines[i].color = finish ? Color.white : Color.white.Alpha(0.35f);
                lineLabels[i].SetPos(-TrackW * 0.5f + 215f, y + 34f);
                lineLabels[i].text = finish ? "FINISH" : metres == 0 ? "START" : metres + " m";
            }

            marker.SetPos(MarkerPos * BarHalf, BarY);

            var place = Me.place;
            if (place == 0)
            {
                place = 1;
                foreach (var r in runners)
                    if (r != Me && (r.place > 0 || r.dist > my))
                        place++;
            }
            placeText.text = PlaceNames[place - 1];
            placeText.color = place == 1 ? Theme.Coin : Theme.Text;
            leftText.text = Mathf.CeilToInt(Mathf.Max(0f, Distance - my)) + " m";
        }
    }
}
