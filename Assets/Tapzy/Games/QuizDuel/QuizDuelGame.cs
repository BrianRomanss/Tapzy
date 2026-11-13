using System.Collections.Generic;
using Tapzy.GameKit;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tapzy.Games.QuizDuel
{
    /// <summary>
    /// A seven-question general knowledge duel against a computer opponent. Both sides answer
    /// the same question at the same time and neither sees the other's answer until the reveal.
    /// A right answer is worth 50 points plus up to 30 for speed; a wrong one is worth nothing.
    /// The higher total after seven questions wins.
    ///
    /// Score: your quiz points, plus 300 for winning.
    /// </summary>
    [MiniGame("quiz_duel", "Quiz Duel", Kind = "Trivia", Colour = "8A6BFF", Order = 20,
        MaxScorePerSecond = 15f, ScoreBase = 1000f, MinDuration = 8f, MissionScore = 300)]
    public class QuizDuelGame : MiniGameBase
    {
        const int QuestionCount = 7, Choices = 4;
        const float AnswerSeconds = 8f, RevealSeconds = 1.9f;
        const int IdleLimit = 3;            // questions the player may sleep through before the duel is forfeited
        const float BarWidth = 900f;
        const float ButtonW = 960f, ButtonH = 150f, ButtonTop = 150f, ButtonGap = 172f;

        enum Phase { Ask, Reveal, Over }

        // Each row: the question, the right answer, then three wrong ones. Answers are shuffled when shown.
        static readonly string[][] Bank =
        {
            new[] { "Which planet is known as the Red Planet?", "Mars", "Venus", "Jupiter", "Mercury" },
            new[] { "How many sides does a hexagon have?", "6", "5", "7", "8" },
            new[] { "Which is the largest ocean on Earth?", "Pacific", "Atlantic", "Indian", "Arctic" },
            new[] { "Which gas do plants take in from the air to make food?", "Carbon dioxide", "Oxygen", "Nitrogen", "Hydrogen" },
            new[] { "How many players does a cricket team have on the field?", "11", "9", "10", "12" },
            new[] { "What is the capital of Japan?", "Tokyo", "Kyoto", "Osaka", "Seoul" },
            new[] { "Which is the largest planet in our solar system?", "Jupiter", "Saturn", "Neptune", "Earth" },
            new[] { "What is 9 x 8?", "72", "64", "81", "78" },
            new[] { "Which is the tallest animal on land?", "Giraffe", "Elephant", "Ostrich", "Camel" },
            new[] { "How many continents are there?", "7", "5", "6", "8" },
            new[] { "What is the chemical formula for water?", "H2O", "CO2", "O2", "NaCl" },
            new[] { "Which instrument has 88 keys?", "Piano", "Guitar", "Violin", "Flute" },
            new[] { "What is the hardest natural substance?", "Diamond", "Gold", "Iron", "Quartz" },
            new[] { "How many days are there in a leap year?", "366", "364", "365", "367" },
            new[] { "Kangaroos are native to which country?", "Australia", "South Africa", "Brazil", "India" },
            new[] { "At what temperature does water freeze, in Celsius?", "0", "32", "100", "-10" },
            new[] { "Which shape has three sides?", "Triangle", "Square", "Pentagon", "Circle" },
            new[] { "Who wrote \"Romeo and Juliet\"?", "William Shakespeare", "Charles Dickens", "Mark Twain", "Leo Tolstoy" },
            new[] { "What is the smallest prime number?", "2", "0", "1", "3" },
            new[] { "Which organ pumps blood around the body?", "Heart", "Lungs", "Liver", "Kidneys" },
            new[] { "How many colours are there in a rainbow?", "7", "5", "6", "8" },
            new[] { "Which is the largest hot desert in the world?", "Sahara", "Gobi", "Kalahari", "Thar" },
            new[] { "What is the capital of France?", "Paris", "Lyon", "Rome", "Madrid" },
            new[] { "In which of these sports is a score of zero called \"love\"?", "Tennis", "Golf", "Hockey", "Chess" },
            new[] { "How many minutes are there in two hours?", "120", "100", "90", "140" },
            new[] { "Which metal is liquid at room temperature?", "Mercury", "Iron", "Silver", "Aluminium" },
            new[] { "Which of these birds cannot fly?", "Penguin", "Eagle", "Parrot", "Sparrow" },
            new[] { "What is the square root of 144?", "12", "11", "14", "16" },
            new[] { "Which star is nearest to Earth?", "The Sun", "Sirius", "Polaris", "Alpha Centauri" },
            new[] { "How many strings does a standard guitar have?", "6", "4", "5", "7" },
            new[] { "Which is the smallest continent?", "Australia", "Europe", "Antarctica", "South America" },
            new[] { "What do bees make?", "Honey", "Silk", "Milk", "Wool" },
            new[] { "How many zeros are there in one million?", "6", "5", "7", "9" },
            new[] { "Which planet is famous for its bright rings?", "Saturn", "Mars", "Venus", "Mercury" },
            new[] { "Which is the highest mountain above sea level?", "Mount Everest", "K2", "Kilimanjaro", "Mont Blanc" },
            new[] { "How many legs does a spider have?", "8", "6", "10", "12" },
            new[] { "Which colour do you get by mixing blue and yellow?", "Green", "Purple", "Orange", "Brown" },
            new[] { "Which is the fastest land animal?", "Cheetah", "Lion", "Horse", "Greyhound" },
            new[] { "How many bones are there in an adult human body?", "206", "106", "186", "306" },
            new[] { "Which country gave the Statue of Liberty to the United States?", "France", "Britain", "Spain", "Italy" },
            new[] { "What is 15% of 200?", "30", "15", "20", "45" },
            new[] { "Which is the largest animal alive today?", "Blue whale", "African elephant", "Giraffe", "Hippopotamus" },
            new[] { "How many rings are there on the Olympic flag?", "5", "4", "6", "7" },
            new[] { "At what temperature does water boil at sea level, in Celsius?", "100", "90", "120", "212" },
            new[] { "Which vitamin does sunlight help your body make?", "Vitamin D", "Vitamin A", "Vitamin C", "Vitamin B12" },
            new[] { "How many hours are there in a week?", "168", "144", "156", "172" },
            new[] { "Which chess piece moves only diagonally?", "Bishop", "Rook", "Knight", "King" },
            new[] { "Which gas makes up most of the air we breathe?", "Nitrogen", "Oxygen", "Carbon dioxide", "Argon" },
            new[] { "On which continent is the Sahara?", "Africa", "Asia", "Australia", "South America" },
            new[] { "What is the capital of Australia?", "Canberra", "Sydney", "Melbourne", "Perth" },
            new[] { "How many sides does an octagon have?", "8", "6", "7", "10" },
            new[] { "Which planet is closest to the Sun?", "Mercury", "Venus", "Earth", "Mars" },
            new[] { "What number does the Roman numeral X stand for?", "10", "5", "50", "100" },
            new[] { "Which sport is played with a shuttlecock?", "Badminton", "Tennis", "Squash", "Table tennis" },
            new[] { "Which is the largest country by area?", "Russia", "Canada", "China", "United States" },
            new[] { "How many teeth does a full adult set have?", "32", "28", "30", "36" },
            new[] { "Which is the longest bone in the human body?", "Thigh bone", "Shin bone", "Upper arm bone", "Collarbone" },
            new[] { "What is 7 squared?", "49", "42", "56", "64" },
            new[] { "Which animal is called the ship of the desert?", "Camel", "Horse", "Donkey", "Elephant" },
            new[] { "What is the currency of Japan?", "Yen", "Won", "Yuan", "Baht" },
            new[] { "How many degrees are there in a right angle?", "90", "45", "180", "360" },
            new[] { "Which ocean lies between Africa and Australia?", "Indian Ocean", "Pacific Ocean", "Atlantic Ocean", "Arctic Ocean" },
            new[] { "Which part of a plant makes most of its food from sunlight?", "Leaves", "Roots", "Stem", "Flowers" },
            new[] { "How many sides does a pentagon have?", "5", "4", "6", "7" }
        };

        readonly List<int> order = new List<int>();
        readonly Image[] buttons = new Image[Choices];
        readonly Text[] buttonLabels = new Text[Choices];
        readonly int[] shown = new int[Choices];   // which answer of the row each button holds; 1 is the right one
        Bot bot;

        Phase phase;
        int questionNumber, correctIndex, myChoice, botChoice, idleStreak;
        int myPoints, botPoints;
        float timer, myTime, botAt;
        bool won;

        Text question, tally, progress, status, botTag;
        Image timeBar;

        protected override string Hint => "Answer fast. Answer right.";

        // ---------- setup ----------

        protected override void OnStart()
        {
            bot = Bots.Pick(Rng, 1)[0];

            // seven different questions, drawn without repeats
            var pool = new List<int>();
            for (var i = 0; i < Bank.Length; i++) pool.Add(i);
            for (var i = 0; i < QuestionCount; i++)
            {
                var pick = Rng.Next(pool.Count);
                order.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            tally = Label("", new Vector2(0f, 725f), 46, Theme.Text, 1040f);
            progress = Label("", new Vector2(0f, 665f), 34, Theme.TextDim, 1040f);
            Box(new Vector2(0f, 440f), new Vector2(1000f, 340f), Theme.Panel);
            question = Ui.Label(Field, "", 50, Theme.Text, new Vector2(0f, 440f), new Vector2(920f, 300f));
            question.horizontalOverflow = HorizontalWrapMode.Wrap;
            question.verticalOverflow = VerticalWrapMode.Overflow;

            for (var i = 0; i < Choices; i++)
            {
                buttons[i] = Box(ButtonPos(i), new Vector2(ButtonW, ButtonH), Theme.PanelLight);
                buttonLabels[i] = Ui.Label(Field, "", 48, Theme.Text, ButtonPos(i), new Vector2(ButtonW - 60f, ButtonH));
            }
            botTag = Ui.Label(Field, "CPU", 30, Theme.Palette[0], Vector2.zero, new Vector2(120f, 44f), bold: true);
            botTag.Show(false);

            Box(new Vector2(0f, -470f), new Vector2(BarWidth, 16f), Theme.PanelLight);
            timeBar = Box(new Vector2(0f, -470f), new Vector2(BarWidth, 16f), Theme.Coin);
            status = Label("", new Vector2(0f, -560f), 46, Theme.Text, 1040f);
            Ui.Label(Field, "Right answers score 50, plus up to 30 for speed", 30, Theme.TextDim, new Vector2(0f, -660f), new Vector2(1000f, 44f));

            NextQuestion();
        }

        static Vector2 ButtonPos(int i) => new Vector2(0f, ButtonTop - i * ButtonGap);

        void ShowTally() => tally.text = "YOU  " + myPoints + "  -  " + botPoints + "  " + bot.Label;

        // ---------- flow ----------

        void NextQuestion()
        {
            var row = Bank[order[questionNumber]];
            questionNumber++;
            question.text = row[0];
            progress.text = "Question " + questionNumber + " of " + QuestionCount;

            // shuffle the four answers onto the buttons
            for (var i = 0; i < Choices; i++) shown[i] = i + 1;
            for (var i = Choices - 1; i > 0; i--)
            {
                var j = Rng.Next(i + 1);
                (shown[i], shown[j]) = (shown[j], shown[i]);
            }
            for (var i = 0; i < Choices; i++)
            {
                buttonLabels[i].text = row[shown[i]];
                buttons[i].color = Theme.PanelLight;
                if (shown[i] == 1) correctIndex = i;
            }

            // the bot decides now, but its answer stays hidden until the reveal
            var knows = Rng.NextDouble() < Mathf.Lerp(0.45f, 0.85f, bot.Skill);
            botChoice = correctIndex;
            if (!knows)
                while (botChoice == correctIndex)
                    botChoice = Rng.Next(Choices);
            botAt = Rand(1.5f, 6.5f) - bot.Skill;

            myChoice = -1;
            botTag.Show(false);
            phase = Phase.Ask;
            timer = AnswerSeconds;
            status.text = "";
            status.color = Theme.TextDim;
            ShowTally();
            Sfx.Blip(660f, 0.06f, 0.25f);
        }

        protected override void OnTick(float dt)
        {
            timer -= dt;
            switch (phase)
            {
                case Phase.Ask:
                    if (PointerDown && myChoice < 0)
                    {
                        for (var i = 0; i < Choices; i++)
                        {
                            var d = PointerPos - ButtonPos(i);
                            if (Mathf.Abs(d.x) > ButtonW * 0.5f || Mathf.Abs(d.y) > ButtonH * 0.5f) continue;
                            Answer(i);
                            break;
                        }
                    }
                    var botDone = AnswerSeconds - timer >= botAt;
                    if (myChoice < 0 && botDone) status.text = bot.Name + " has answered";
                    // no need to sit out the clock once both sides are in
                    if (timer <= 0f || (myChoice >= 0 && botDone)) Reveal();
                    else if (myChoice >= 0) status.text = "Waiting for " + bot.Name;
                    break;

                case Phase.Reveal:
                    if (timer <= 0f)
                    {
                        if (idleStreak >= IdleLimit) GameOver(false, "Timed out");
                        else if (questionNumber >= QuestionCount)
                        {
                            if (myPoints > botPoints) GameOver(true, "YOU WIN!");
                            else GameOver(false, myPoints == botPoints ? "Draw" : bot.Name + " wins");
                        }
                        else NextQuestion();
                    }
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

            var asking = phase == Phase.Ask;
            timeBar.Show(asking);
            if (asking) timeBar.SetSize(Mathf.Max(16f, BarWidth * Mathf.Clamp01(timer / AnswerSeconds)), 16f);
        }

        void Answer(int choice)
        {
            if (phase != Phase.Ask || myChoice >= 0) return;
            myChoice = choice;
            myTime = Mathf.Max(0f, timer);
            buttons[choice].color = Theme.Palette[1]; // locked in; right or wrong is not shown yet
            Sfx.Blip(520f, 0.05f, 0.25f);
        }

        static int Points(float secondsLeft) => 50 + Mathf.RoundToInt(30f * Mathf.Clamp01(secondsLeft / AnswerSeconds));

        void Reveal()
        {
            phase = Phase.Reveal;
            timer = RevealSeconds;
            idleStreak = myChoice < 0 ? idleStreak + 1 : 0;

            buttons[correctIndex].color = Theme.Good.Shade(0.75f);
            if (myChoice >= 0 && myChoice != correctIndex) buttons[myChoice].color = Theme.Bad.Shade(0.75f);

            // the bot only counts if it answered inside the time allowed
            var botAnswered = botAt < AnswerSeconds;
            botTag.Show(botAnswered);
            if (botAnswered)
            {
                botTag.SetPos(ButtonPos(botChoice) + new Vector2(ButtonW * 0.5f - 80f, ButtonH * 0.5f - 30f));
                if (botChoice == correctIndex) botPoints += Points(AnswerSeconds - botAt);
            }

            if (myChoice == correctIndex)
            {
                var points = Points(myTime);
                myPoints += points;
                Score += points;
                status.text = "Correct!  +" + points;
                status.color = Theme.Good;
                Sfx.Great();
            }
            else
            {
                status.text = myChoice < 0 ? "Out of time" : "Wrong";
                status.color = Theme.Bad;
                Sfx.Bad();
            }
            ShowTally();
        }

        void GameOver(bool win, string message)
        {
            phase = Phase.Over;
            timer = 2.4f;
            won = win;
            status.text = "";
            Box(new Vector2(0f, 100f), new Vector2(980f, 320f), Theme.Dark);
            Label(message, new Vector2(0f, 150f), 96, won ? Theme.Coin : Theme.Text, 900f);
            Label(won ? "+300" : myPoints + " - " + botPoints, new Vector2(0f, 30f), 64, won ? Theme.Good : Theme.TextDim, 900f);
            if (won)
            {
                Score += 300;
                Flash(Theme.Good, 0.3f);
                Sfx.Great();
            }
            else Sfx.Bad();
        }
    }
}
