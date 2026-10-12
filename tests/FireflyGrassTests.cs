using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using 撈金魚.ActionPerform;
using 撈金魚.Analyzer;

internal static class FireflyGrassTests
{
    private static int checks;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAILED: " + description);
        checks++;
    }

    private static void Throws<T>(Action action, string description) where T : Exception
    {
        try { action(); }
        catch (T) { checks++; return; }
        throw new Exception("FAILED: " + description);
    }

    private static int Main(string[] args)
    {
        try
        {
            var session = new FakeSession();
            var route = new FireflyGrassRoute(session);
            route.Run(1);
            Check(route.Completed == 1 && session.GrassClicks == 3, "Count confirmed rewards, not grass clicks");
            Check(session.Clicks[0] == "39,515" && session.Clicks[1] == "48,300" && session.Clicks[2] == "288,206 (content pixels)", "Use the user supplied content-pixel entrance coordinates");
            Check(session.Screen == FireflyGrassScreen.Waterfall, "Remain at waterfall after completion");

            session = new FakeSession { Screen = FireflyGrassScreen.Reward };
            route = new FireflyGrassRoute(session); route.Run(1);
            Check(route.Completed == 1 && session.GrassClicks == 3 && session.Clicks[0] == "477,360",
                "Dismiss an existing reward without counting it as this run's harvest");

            session = new FakeSession { MissedRewardClicks = 1 };
            route = new FireflyGrassRoute(session); route.Run(1);
            Check(route.Completed == 1 && session.RewardCloseClicks == 2, "Retry ignored confirmation without counting twice");

            session = new FakeSession { Screen = FireflyGrassScreen.Reward, MissedRewardClicks = 99 };
            Throws<InvalidOperationException>(() => new FireflyGrassRoute(session).Run(1), "Stop when reward cannot be dismissed");
            Check(session.RewardCloseClicks == 3 && session.GrassClicks == 0, "Bound confirmation retries and do not click grass behind dialog");

            session = new FakeSession(); route = new FireflyGrassRoute(session);
            route.Run(0);
            Check(route.Completed == 2 && session.GrassClicks == 7, "Blank input stops at daily limit without counting it");
            Check(session.Clicks[session.Clicks.Count - 1] == "477,350", "Acknowledge daily limit at its own button position");

            session = new FakeSession { Screen = FireflyGrassScreen.Waterfall, Rewards = 2 };
            route = new FireflyGrassRoute(session); route.Run(10);
            Check(route.Completed == 0 && session.GrassClicks == 1, "Already exhausted stops early");

            session = new FakeSession { Screen = FireflyGrassScreen.DailyLimit };
            route = new FireflyGrassRoute(session); route.Run(1);
            Check(route.Completed == 0 && session.GrassClicks == 0 && session.Clicks.Count == 1,
                "Existing daily-limit dialog is acknowledged without navigating or harvesting");

            session = new FakeSession { Screen = FireflyGrassScreen.DailyLimit, MissedLimitClicks = 1 };
            route = new FireflyGrassRoute(session); route.Run(1);
            Check(session.LimitCloseClicks == 2 && session.Screen == FireflyGrassScreen.Waterfall && route.Completed == 0,
                "Retry daily-limit confirmation and verify the dialog has closed");
            session = new FakeSession { Screen = FireflyGrassScreen.DailyLimit, MissedLimitClicks = 99 };
            Throws<InvalidOperationException>(() => new FireflyGrassRoute(session).Run(1), "Report a stuck daily-limit dialog");
            Check(session.LimitCloseClicks == 3 && session.GrassClicks == 0, "Bound daily-limit confirmation retries");

            session = new FakeSession { AlreadyHere = true, SlowDialogClose = true };
            route = new FireflyGrassRoute(session); route.Run(1);
            Check(session.Clicks.Contains("751,87") && route.Completed == 1, "Handle already-at-destination dialog and close map");

            session = new FakeSession { Screen = FireflyGrassScreen.BerryMap, MissedEntryClicks = 1 };
            route = new FireflyGrassRoute(session); route.Run(1);
            Check(session.EntryClicks == 2 && route.Completed == 1, "Retry an ignored entry click on the same map");
            Check(session.EntryClickTimes[0] >= 600 && session.EntryClickTimes[1] - session.EntryClickTimes[0] >= 2500, "Wait for animation and pace retries");

            session = new FakeSession { Screen = FireflyGrassScreen.BerryMap, MissedEntryClicks = 10 };
            Throws<InvalidOperationException>(() => new FireflyGrassRoute(session).Run(1), "Entry timeout after bounded retries");
            Check(session.EntryClicks == 3 && session.GrassClicks == 0, "Stop after three misses without harvesting elsewhere");

            session = new FakeSession { Screen = FireflyGrassScreen.BerryMap, EntryLoading = true };
            Throws<InvalidOperationException>(() => new FireflyGrassRoute(session).Run(1), "Wait for loading to complete");
            Check(session.EntryClicks == 1 && session.GrassClicks == 0, "Never retry entry on an unknown/loading frame");

            session = new FakeSession { Screen = FireflyGrassScreen.UnknownDialog };
            Throws<InvalidOperationException>(() => new FireflyGrassRoute(session).Run(1), "Do not act on initial unknown dialog");
            Check(session.Clicks.Count == 0, "Unknown dialog remains untouched");

            session = new FakeSession { Screen = FireflyGrassScreen.Waterfall, UnexpectedDialog = true };
            route = new FireflyGrassRoute(session);
            Throws<InvalidOperationException>(() => route.Run(1), "Stop on unknown harvesting result");
            Check(session.Clicks.Count == 1 && route.Completed == 0, "Never acknowledge unknown reward");

            session = new FakeSession { Screen = FireflyGrassScreen.Waterfall, Stuck = true };
            Throws<InvalidOperationException>(() => new FireflyGrassRoute(session).Run(1), "Harvest timeout");
            Check(session.ElapsedMilliseconds <= 61000 && session.GrassClicks < 30, "Harvest retries are bounded and paced");

            session = new FakeSession { Stuck = true };
            Throws<InvalidOperationException>(() => new FireflyGrassRoute(session).Run(1), "Map timeout");
            Check(session.Clicks.Count == 1, "No repeated navigation clicks on failed map");

            session = new FakeSession { Cancelled = true };
            Throws<OperationCanceledException>(() => new FireflyGrassRoute(session).Run(1), "Cancellation propagates");
            Throws<ArgumentOutOfRangeException>(() => new FireflyGrassRoute(new FakeSession()).Run(-1), "Reject negative count");
            Throws<ArgumentOutOfRangeException>(() => new FireflyGrassRoute(new FakeSession()).Run(1000), "Reject excessive count");
            using (var blank = new Bitmap(960, 560))
            {
                Check(AnalyzeFireflyGrass.Recognize(blank) == FireflyGrassScreen.Unknown, "Blank frame is unknown");
            }

            if (args.Length > 0) CheckRecording(args[0]);
            if (args.Length > 1)
                using (var actualLimit = new Bitmap(args[1]))
                    Check(AnalyzeFireflyGrass.Recognize(actualLimit) == FireflyGrassScreen.DailyLimit, "Recognize real failure capture as daily limit");
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--berry-map")
                {
                    using (var map = new Bitmap(args[++i]))
                    {
                        foreach (int width in new[] { 720, 960, 1440 })
                            using (var scaled = new Bitmap(map, width, width * 560 / 960))
                                Check(AnalyzeFireflyGrass.Recognize(scaled) == FireflyGrassScreen.BerryMap,
                                    "Recognize map opened from home at width " + width);
                        session = new FakeSession { BerryImage = map };
                        route = new FireflyGrassRoute(session); route.Run(1);
                        Check(route.Completed == 1 && session.EntryClicks == 1,
                            "Actual home-backed map allows the entrance click and harvest flow");
                    }
                    continue;
                }
                using (var actualReward = new Bitmap(args[i]))
                {
                    foreach (int width in new[] { 720, 960, 1440 })
                        using (var scaled = new Bitmap(actualReward, width, width * 560 / 960))
                            Check(AnalyzeFireflyGrass.Recognize(scaled) == FireflyGrassScreen.Reward,
                                "Actual four-grass reward at width " + width + ": " + AnalyzeFireflyGrass.Recognize(scaled));
                    using (var g = Graphics.FromImage(actualReward)) g.FillRectangle(Brushes.White, 591, 290, 13, 18);
                    Check(AnalyzeFireflyGrass.Recognize(actualReward) == FireflyGrassScreen.Reward,
                        "Recognition does not depend on reward quantity");
                    using (var g = Graphics.FromImage(actualReward)) g.FillRectangle(Brushes.White, 355, 307, 67, 17);
                    Check(AnalyzeFireflyGrass.Recognize(actualReward) == FireflyGrassScreen.UnknownDialog,
                        "Reward prefix alone must not acknowledge unrelated items");
                }
            }
            Console.WriteLine("PASS: " + checks + " checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void CheckRecording(string directory)
    {
        var frames = new Dictionary<int, FireflyGrassScreen>();
        foreach (int frame in new[] { 1, 8, 10, 14, 18, 22, 25, 29, 33, 37 }) frames.Add(frame, FireflyGrassScreen.Waterfall);
        frames.Add(3, FireflyGrassScreen.WorldMap);
        frames.Add(5, FireflyGrassScreen.BerryMap);
        frames.Add(6, FireflyGrassScreen.AlreadyHere);
        frames.Add(26, FireflyGrassScreen.Reward);
        frames.Add(27, FireflyGrassScreen.Reward);
        foreach (int frame in new[] { 34, 35, 36 }) frames.Add(frame, FireflyGrassScreen.DailyLimit);
        foreach (var pair in frames)
            using (var original = new Bitmap(Path.Combine(directory, "frame-" + pair.Key.ToString("00") + ".png")))
                foreach (int width in new[] { 720, 960, 1440 })
                    using (var scaled = new Bitmap(original, width, width * 560 / 960))
                    {
                        var actual = AnalyzeFireflyGrass.Recognize(scaled);
                        Check(actual == pair.Value, "Frame " + pair.Key + " @ " + width + ": " + actual + " expected " + pair.Value);
                    }
        using (var unknown = new Bitmap(Path.Combine(directory, "frame-26.png")))
        {
            using (var g = Graphics.FromImage(unknown)) g.FillRectangle(Brushes.White, 350, 285, 250, 50);
            Check(AnalyzeFireflyGrass.Recognize(unknown) == FireflyGrassScreen.UnknownDialog, "Unrecognized dialog text must not count as reward");
        }
    }

    private sealed class FakeSession : IFireflyGrassSession
    {
        internal Bitmap BerryImage;
        internal FireflyGrassScreen Screen = FireflyGrassScreen.Unknown;
        internal bool AlreadyHere, UnexpectedDialog, Stuck, Cancelled, SlowDialogClose, EntryLoading;
        internal int MissedEntryClicks, EntryClicks;
        internal int MissedRewardClicks, RewardCloseClicks;
        internal int MissedLimitClicks, LimitCloseClicks;
        internal readonly List<long> EntryClickTimes = new List<long>();
        private int dialogReadsRemaining;
        internal int Rewards, GrassClicks;
        internal readonly List<string> Clicks = new List<string>();
        public long ElapsedMilliseconds { get; private set; }
        public FireflyGrassScreen ReadScreen()
        {
            if (Cancelled) throw new OperationCanceledException();
            if (dialogReadsRemaining > 0 && --dialogReadsRemaining == 0) Screen = FireflyGrassScreen.BerryMap;
            return Screen;
        }
        public void Click(int x, int y)
        {
            Clicks.Add(x + "," + y);
            if (x == 80) GrassClicks++;
            if (Stuck) return;
            if (x == 39) Screen = FireflyGrassScreen.WorldMap;
            else if (x == 48) Screen = BerryImage == null ? FireflyGrassScreen.BerryMap : AnalyzeFireflyGrass.Recognize(BerryImage);
            else if (x == 751) Screen = FireflyGrassScreen.Waterfall;
            else if (x == 80)
            {
                if (UnexpectedDialog) Screen = FireflyGrassScreen.UnknownDialog;
                else if (Rewards >= 2) Screen = FireflyGrassScreen.DailyLimit;
                else if (GrassClicks % 3 == 0) { Rewards++; Screen = FireflyGrassScreen.Reward; }
            }
            else if (x == 477)
            {
                if (Screen == FireflyGrassScreen.DailyLimit)
                {
                    LimitCloseClicks++;
                    if (MissedLimitClicks-- > 0) return;
                }
                if (Screen == FireflyGrassScreen.Reward)
                {
                    RewardCloseClicks++;
                    if (MissedRewardClicks-- > 0) return;
                }
                if (Screen == FireflyGrassScreen.AlreadyHere && SlowDialogClose) dialogReadsRemaining = 3;
                else Screen = Screen == FireflyGrassScreen.AlreadyHere ? FireflyGrassScreen.BerryMap : FireflyGrassScreen.Waterfall;
            }
        }
        public void Delay(int milliseconds) { ElapsedMilliseconds += milliseconds; }
        public bool TryEnterWaterfall()
        {
            EntryClicks++;
            EntryClickTimes.Add(ElapsedMilliseconds);
            Clicks.Add("288,206 (content pixels)");
            if (MissedEntryClicks-- > 0) return true;
            Screen = EntryLoading ? FireflyGrassScreen.Unknown : AlreadyHere ? FireflyGrassScreen.AlreadyHere : FireflyGrassScreen.Waterfall;
            return true;
        }
        public void Report(string status, int completed) { }
    }
}
