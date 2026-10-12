using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Template = 撈金魚.Analyzer.AnalyzeFireflyGrass.Template;

namespace 撈金魚.Analyzer
{
    internal enum ForestHarvestScreen { Unknown, WorldMap, Bridge, ForestMap, Gate, SeedReward, MushroomReward, FlytrapReward, Limit, UnknownDialog }

    internal static class AnalyzeForestHarvest
    {
        private static readonly Template world = new Template("WorldMap", 410, 212);
        private static readonly Template bridge = new Template("ForestBridge", 80, 142);
        private static readonly Template map = new Template("ForestMap", 424, 84);
        private static readonly Template gate = new Template("ForestGate", 198, 104);
        private static readonly Template dialog = new Template("Dialog", 330, 180);
        private static readonly Template profile = new Template("ForestProfile", 306, 46);
        private static readonly Template seed = new Template("ForestSeed", 385, 294, true);
        private static readonly Template mushroom = new Template("ForestMushroom", 356, 294, true);
        private static readonly Template seedLive = new Template("ForestSeedLive", 384, 291, true);
        private static readonly Template mushroomLive = new Template("ForestMushroomLive", 356, 291, true);
        private static readonly Template seedName = new Template("ForestSeedName", 477, 292, true);
        private static readonly Template flytrapName = new Template("ForestFlytrapName", 477, 292, true);
        private static readonly Template flytrap = new Template("ForestFlytrapLive", 384, 291, true);
        private static readonly Template limitLive = new Template("ForestLimitLive", 384, 291, true);
        private static readonly Template limit = new Template("ForestLimit", 385, 294, true);

        internal static ForestHarvestScreen Recognize(Bitmap image)
        {
            using (var normalized = new Bitmap(960, 560, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(normalized))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(image, new Rectangle(0, 0, 960, 560));
                }
                int[] pixels = AnalyzeFireflyGrass.ReadPixels(normalized);
                if (profile.Matches(pixels)) return ForestHarvestScreen.UnknownDialog;
                if (dialog.Matches(pixels))
                {
                    if (limit.Matches(pixels) || limitLive.Matches(pixels)) return ForestHarvestScreen.Limit;
                    if (seed.Matches(pixels) || (seedLive.Matches(pixels) && seedName.Matches(pixels))) return ForestHarvestScreen.SeedReward;
                    if (mushroom.Matches(pixels) || mushroomLive.Matches(pixels)) return ForestHarvestScreen.MushroomReward;
                    if (flytrap.Matches(pixels) && flytrapName.Matches(pixels)) return ForestHarvestScreen.FlytrapReward;
                    return ForestHarvestScreen.UnknownDialog;
                }
                if (map.Matches(pixels)) return ForestHarvestScreen.ForestMap;
                if (world.Matches(pixels)) return ForestHarvestScreen.WorldMap;
                if (bridge.Matches(pixels)) return ForestHarvestScreen.Bridge;
                if (gate.Matches(pixels)) return ForestHarvestScreen.Gate;
                return ForestHarvestScreen.Unknown;
            }
        }
    }
}





