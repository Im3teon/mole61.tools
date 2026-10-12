using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using 撈金魚.ActionPerform;
using 撈金魚.Analyzer;

internal static class ForestHarvestTests
{
    private static int checks;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    private static void Fails(Action action) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception("Expected failure"); }
    private static int Main(string[] args)
    {
        try
        {
            var s = new Session(); var r = new ForestHarvestRoute(s); r.Run(0);
            Check(r.Seeds == 2 && r.Mushrooms == 2 && r.Flytraps == 2 && s.State == ForestHarvestScreen.Gate, "Both resources collected to limit, dialogs closed");
            Check(string.Join(";", s.Clicks).StartsWith("39,515;643,137;39,515;391,155"), "Full navigation route");
            s = new Session { State = ForestHarvestScreen.Gate }; r = new ForestHarvestRoute(s); r.Run(1);
            Check(r.Seeds == 1 && r.Mushrooms == 1 && r.Flytraps == 1, "Requested count applies to each item");
            s = new Session { State = ForestHarvestScreen.SeedReward }; r = new ForestHarvestRoute(s); r.Run(1);
            Check(r.Seeds == 1 && r.Mushrooms == 1 && s.Clicks[0] == "477,362", "Previous reward not counted");
            s = new Session { State = ForestHarvestScreen.FlytrapReward }; r = new ForestHarvestRoute(s); r.Run(1);
            Check(r.Flytraps == 1 && s.Clicks[0] == "477,362", "Previous flytrap reward not counted");
            s = new Session { SeedCount = 2, MushroomCount = 2 }; r = new ForestHarvestRoute(s); r.Run(0);
            Check(r.Seeds == 0 && r.Mushrooms == 0 && r.Flytraps == 2 && s.State == ForestHarvestScreen.Gate,
                "First two limits still collect flytraps and close final limit");
            s = new Session { SeedCount = 2 }; r = new ForestHarvestRoute(s); r.Run(0);
            Check(r.Seeds == 0 && r.Mushrooms == 2, "Seed cap still permits mushroom collection");
            s = new Session { MissedConfirm = 1 }; r = new ForestHarvestRoute(s); r.Run(1);
            Check(s.ConfirmClicks == 4 && r.Seeds == 1, "Ignored confirmation retried without double counting");
            s = new Session { State = ForestHarvestScreen.Limit, MissedConfirm = 99 };
            Fails(() => new ForestHarvestRoute(s).Run(1)); Check(s.ConfirmClicks == 3, "Bounded dialog retries");
            s = new Session { State = ForestHarvestScreen.UnknownDialog }; Fails(() => new ForestHarvestRoute(s).Run(1));
            Check(s.Clicks.Count == 0, "Unknown popup untouched");
            s = new Session { State = ForestHarvestScreen.Gate, WrongReward = true }; Fails(() => new ForestHarvestRoute(s).Run(1));
            Check(s.ConfirmClicks == 0, "Wrong item popup not acknowledged");
            s = new Session { StuckMap = true }; Fails(() => new ForestHarvestRoute(s).Run(1));
            Check(s.Clicks.Count == 3, "Map retries bounded");
            s = new Session { Loading = true }; Fails(() => new ForestHarvestRoute(s).Run(1));
            Check(s.Clicks.Count == 2, "Loading frame does not trigger repeated destination clicks");
            s = new Session { State = ForestHarvestScreen.Gate, StuckHarvest = true }; Fails(() => new ForestHarvestRoute(s).Run(1));
            Check(s.ElapsedMilliseconds <= 61000 && s.Clicks.Count <= 24, "Harvest timeout bounded");
            s = new Session { Cancel = true };
            try { new ForestHarvestRoute(s).Run(1); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { checks++; }
            if (args.Length > 0)
            {
                var cases = new Dictionary<int, ForestHarvestScreen> {
                    {1,ForestHarvestScreen.Unknown},{3,ForestHarvestScreen.WorldMap},{5,ForestHarvestScreen.Bridge},
                    {7,ForestHarvestScreen.ForestMap},{10,ForestHarvestScreen.Gate},{12,ForestHarvestScreen.SeedReward},
                    {17,ForestHarvestScreen.SeedReward},{32,ForestHarvestScreen.Limit},{35,ForestHarvestScreen.MushroomReward},
                    {50,ForestHarvestScreen.MushroomReward},{64,ForestHarvestScreen.Gate}};
                foreach (var pair in cases)
                    using (var original = new Bitmap(Path.Combine(args[0], "frame-" + pair.Key.ToString("00") + ".png")))
                        foreach (int width in new[] {720,960,1440})
                            using (var scaled = new Bitmap(original,width,width*560/960))
                                Check(AnalyzeForestHarvest.Recognize(scaled) == pair.Value, "Frame " + pair.Key + " @ " + width + ": " + AnalyzeForestHarvest.Recognize(scaled));
                using (var unknown = new Bitmap(Path.Combine(args[0], "frame-12.png")))
                {
                    using (var g = Graphics.FromImage(unknown)) g.FillRectangle(Brushes.White,350,285,255,50);
                    Check(AnalyzeForestHarvest.Recognize(unknown) == ForestHarvestScreen.UnknownDialog, "Unknown text is not a reward");
                }
            }
            for (int i = 1; i + 1 < args.Length; i += 2)
                using (var live = new Bitmap(args[i + 1]))
                    Check(AnalyzeForestHarvest.Recognize(live).ToString() == args[i], "Live capture: " + AnalyzeForestHarvest.Recognize(live) + " expected " + args[i]);
            Console.WriteLine("PASS: " + checks + " checks"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private sealed class Session : IForestHarvestSession
    {
        internal ForestHarvestScreen State = ForestHarvestScreen.Unknown;
        internal int SeedCount, MushroomCount, FlytrapCount, MissedConfirm, ConfirmClicks;
        internal bool WrongReward, StuckMap, Loading, StuckHarvest, Cancel;
        internal List<string> Clicks = new List<string>();
        public long ElapsedMilliseconds { get; private set; }
        public ForestHarvestScreen ReadScreen() { if (Cancel) throw new OperationCanceledException(); return State; }
        public void Click(int x, int y)
        {
            Clicks.Add(x + "," + y);
            if (x == 39) { if (!StuckMap) State = State == ForestHarvestScreen.Bridge ? ForestHarvestScreen.ForestMap : ForestHarvestScreen.WorldMap; }
            else if (x == 643) State = Loading ? ForestHarvestScreen.Unknown : ForestHarvestScreen.Bridge;
            else if (x == 391) State = ForestHarvestScreen.Gate;
            else if (x == 80 && !StuckHarvest) State = WrongReward ? ForestHarvestScreen.MushroomReward : SeedCount++ >= 2 ? ForestHarvestScreen.Limit : ForestHarvestScreen.SeedReward;
            else if (x == 638 && !StuckHarvest) State = MushroomCount++ >= 2 ? ForestHarvestScreen.Limit : ForestHarvestScreen.MushroomReward;
            else if (x == 850 && !StuckHarvest) State = FlytrapCount++ >= 2 ? ForestHarvestScreen.Limit : ForestHarvestScreen.FlytrapReward;
            else if (x == 477) { ConfirmClicks++; if (MissedConfirm-- <= 0) State = ForestHarvestScreen.Gate; }
        }
        public void Delay(int ms) { if (Cancel) throw new OperationCanceledException(); ElapsedMilliseconds += ms; }
        public void Report(string status, int count) { }
    }
}



