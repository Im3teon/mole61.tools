using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace 撈金魚.Analyzer
{
    internal enum FireflyGrassScreen
    {
        Unknown, WorldMap, BerryMap, Waterfall, Reward, DailyLimit, AlreadyHere, UnknownDialog
    }

    // Reference coordinates are normalized to the game content, excluding window chrome.
    internal static class AnalyzeFireflyGrass
    {
        private static readonly Template world = new Template("WorldMap", 410, 212);
        // Both patches are wholly inside the opaque map. Its upper mountain edge contains
        // the scene behind the map, which differs when opened from home or the waterfall.
        private static readonly Template berryLotus = new Template("BerryLotus", 510, 205);
        private static readonly Template berryIsland = new Template("BerryIsland", 400, 430);
        private static readonly Template waterfall = new Template("Waterfall", 293, 82);
        private static readonly Template dialog = new Template("Dialog", 330, 180);
        private static readonly Template reward = new Template("Reward", 353, 291, true);
        // Match the fixed reward prefix and item name, excluding the variable quantity.
        private static readonly Template rewardPrefix = new Template("RewardPrefix", 385, 290, true, 145);
        private static readonly Template rewardItem = new Template("RewardItem", 355, 307, true, 145);
        private static readonly Template limit = new Template("DailyLimit", 350, 288, true);
        private static readonly Template limitIcon = new Template("DailyLimitIcon", 445, 205);
        private static readonly Template already = new Template("AlreadyHere", 353, 291, true);

        internal static FireflyGrassScreen Recognize(Bitmap image)
        {
            using (var normalized = new Bitmap(960, 560, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(normalized))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    graphics.DrawImage(image, new Rectangle(0, 0, 960, 560));
                }
                int[] pixels = ReadPixels(normalized);
                // Dialogs must take priority: the waterfall remains visible behind them.
                if (dialog.Matches(pixels))
                {
                    if (limit.Matches(pixels) || limitIcon.Matches(pixels)) return FireflyGrassScreen.DailyLimit;
                    if (reward.Matches(pixels) || (rewardPrefix.Matches(pixels) && rewardItem.Matches(pixels)))
                        return FireflyGrassScreen.Reward;
                    if (already.Matches(pixels)) return FireflyGrassScreen.AlreadyHere;
                    return FireflyGrassScreen.UnknownDialog;
                }
                if (world.Matches(pixels)) return FireflyGrassScreen.WorldMap;
                if (berryLotus.Matches(pixels) && berryIsland.Matches(pixels)) return FireflyGrassScreen.BerryMap;
                if (waterfall.Matches(pixels)) return FireflyGrassScreen.Waterfall;
                return FireflyGrassScreen.Unknown;
            }
        }

        internal static int[] ReadPixels(Bitmap bitmap)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] pixels = new int[bitmap.Width * bitmap.Height];
                for (int y = 0; y < bitmap.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * bitmap.Width, bitmap.Width);
                return pixels;
            }
            finally { bitmap.UnlockBits(data); }
        }

        internal sealed class Template
        {
            private readonly List<Sample> samples = new List<Sample>();
            private readonly int tolerance;
            internal Template(string name, int left, int top, bool textOnly = false, int tolerance = 55)
            {
                this.tolerance = tolerance;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FireflyGrass." + name + ".png"))
                {
                    if (stream == null) throw new InvalidOperationException("Missing firefly grass template: " + name);
                    using (var bitmap = new Bitmap(stream))
                    {
                        int step = textOnly ? 1 : 3;
                        for (int y = 0; y < bitmap.Height; y += step)
                            for (int x = 0; x < bitmap.Width; x += step)
                            {
                                Color color = bitmap.GetPixel(x, y);
                                // White space must not make unrelated dialog text appear to match.
                                int inkLimit = tolerance == 145 ? 80 : 180;
                                if (textOnly && (color.R > inkLimit || color.G > inkLimit || color.B > inkLimit)) continue;
                                samples.Add(new Sample { X = left + x, Y = top + y, Color = color });
                            }
                    }
                }
            }

            internal bool Matches(int[] pixels)
            {
                if (samples.Count == 0) return false;
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int matched = 0;
                        foreach (var sample in samples)
                        {
                            bool found = false;
                            // Small glyph strokes move by a pixel after capture resizing/antialiasing.
                            int radius = tolerance == 145 ? 1 : 0;
                            for (int sy = -radius; sy <= radius && !found; sy++)
                                for (int sx = -radius; sx <= radius && !found; sx++)
                                {
                                    int pixel = pixels[(sample.Y + dy + sy) * 960 + sample.X + dx + sx];
                                    found = Math.Abs(((pixel >> 16) & 255) - sample.Color.R) <= tolerance &&
                                        Math.Abs(((pixel >> 8) & 255) - sample.Color.G) <= tolerance &&
                                        Math.Abs((pixel & 255) - sample.Color.B) <= tolerance;
                                }
                            if (found) matched++;
                        }
                        if (matched >= samples.Count * 0.78) return true;
                    }
                return false;
            }

            private struct Sample { internal int X, Y; internal Color Color; }
        }
    }
}
