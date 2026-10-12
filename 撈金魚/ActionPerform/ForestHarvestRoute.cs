using System;
using 撈金魚.Analyzer;

namespace 撈金魚.ActionPerform
{
    internal interface IForestHarvestSession
    {
        ForestHarvestScreen ReadScreen();
        void Click(int x, int y);
        void Delay(int milliseconds);
        long ElapsedMilliseconds { get; }
        void Report(string status, int count);
    }

    internal sealed class ForestHarvestRoute
    {
        private readonly IForestHarvestSession session;
        internal int Seeds { get; private set; }
        internal int Mushrooms { get; private set; }
        internal int Flytraps { get; private set; }
        internal ForestHarvestRoute(IForestHarvestSession session) { this.session = session; }

        internal string Run(int requested)
        {
            if (requested < 0 || requested > 999) throw new ArgumentOutOfRangeException(nameof(requested));
            var initial = session.ReadScreen();
            if (initial == ForestHarvestScreen.SeedReward || initial == ForestHarvestScreen.MushroomReward || initial == ForestHarvestScreen.FlytrapReward || initial == ForestHarvestScreen.Limit)
                Dismiss(initial); // A previous run's dialog is not a new reward or this run's limit.
            Navigate();
            Harvest(ForestHarvestScreen.SeedReward, "毛毛豆種子", 80, 430, requested);
            Harvest(ForestHarvestScreen.MushroomReward, "拉姆菌", 638, 305, requested);
            Harvest(ForestHarvestScreen.FlytrapReward, "捕蠅草種子", 850, 425, requested);
            return "摩界之門採集完成：毛毛豆種子 " + Seeds + " 回、拉姆菌 " + Mushrooms + " 回、捕蠅草種子 " + Flytraps + " 回。" +
                (requested == 0 ? "\n三項均已出現採集上限提示。" : "\n各項達指定回數或採集上限即停止。");
        }

        private void Navigate()
        {
            var screen = session.ReadScreen();
            Reject(screen);
            if (screen == ForestHarvestScreen.Gate) return;
            if (screen != ForestHarvestScreen.Bridge && screen != ForestHarvestScreen.ForestMap)
            {
                if (screen != ForestHarvestScreen.WorldMap)
                    Transition(39, 515, screen, ForestHarvestScreen.WorldMap, "開啟主世界地圖");
                Transition(643, 137, ForestHarvestScreen.WorldMap, ForestHarvestScreen.Bridge, "前往黑森林／雲霧謎橋");
            }
            if (session.ReadScreen() != ForestHarvestScreen.ForestMap)
                Transition(39, 515, ForestHarvestScreen.Bridge, ForestHarvestScreen.ForestMap, "開啟黑森林地圖");
            Transition(391, 155, ForestHarvestScreen.ForestMap, ForestHarvestScreen.Gate, "進入摩界之門");
        }

        private void Transition(int x, int y, ForestHarvestScreen source, ForestHarvestScreen target, string status)
        {
            session.Report(status, Seeds + Mushrooms + Flytraps);
            session.Delay(600);
            long started = session.ElapsedMilliseconds, next = started;
            int attempts = 0;
            while (session.ElapsedMilliseconds - started < 15000)
            {
                var screen = session.ReadScreen();
                if (screen == target) return;
                Reject(screen);
                // Only retry on the source scene, never on a loading screen.
                if (screen == source && attempts < 3 && session.ElapsedMilliseconds >= next)
                {
                    session.Click(x, y);
                    attempts++;
                    next = session.ElapsedMilliseconds + 2500;
                }
                session.Delay(200);
            }
            throw new InvalidOperationException(status + "逾時，已停止。");
        }

        private void Harvest(ForestHarvestScreen reward, string name, int x, int y, int requested)
        {
            long started = session.ElapsedMilliseconds;
            int count = 0;
            while (requested == 0 || count < requested)
            {
                if (session.ElapsedMilliseconds - started >= 300000)
                    throw new InvalidOperationException(name + "採集超過 5 分鐘，已停止。");
                session.Report("採集" + name, Seeds + Mushrooms + Flytraps);
                if (reward == ForestHarvestScreen.MushroomReward)
                {
                    // The avatar walks onto the mushroom and intercepts the next click.
                    // Move to open ground before each collection, including resumed runs.
                    if (session.ReadScreen() != ForestHarvestScreen.Gate)
                        throw new InvalidOperationException("採集前未確認摩界之門畫面，已停止。");
                    session.Click(550, 400);
                    session.Delay(2500);
                }
                var result = HarvestOnce(x, y, reward);
                if (result == ForestHarvestScreen.Limit) { Dismiss(result); return; }
                count++;
                if (reward == ForestHarvestScreen.SeedReward) Seeds++;
                else if (reward == ForestHarvestScreen.MushroomReward) Mushrooms++;
                else Flytraps++;
                session.Report("已取得" + name, Seeds + Mushrooms + Flytraps);
                Dismiss(reward);
            }
        }

        private ForestHarvestScreen HarvestOnce(int x, int y, ForestHarvestScreen expected)
        {
            long started = session.ElapsedMilliseconds, next = started;
            while (session.ElapsedMilliseconds - started < 60000)
            {
                var screen = session.ReadScreen();
                if (screen == expected || screen == ForestHarvestScreen.Limit) return screen;
                Reject(screen);
                if (screen != ForestHarvestScreen.Gate && screen != ForestHarvestScreen.Unknown)
                    throw new InvalidOperationException("採集結果或所在地與預期不符，已停止並保留畫面。");
                if (screen == ForestHarvestScreen.Gate && session.ElapsedMilliseconds >= next)
                {
                    session.Click(x, y);
                    next = session.ElapsedMilliseconds + 5000;
                }
                session.Delay(200);
            }
            throw new InvalidOperationException("60 秒內未確認採集結果，已停止。");
        }

        private void Dismiss(ForestHarvestScreen expected)
        {
            long started = session.ElapsedMilliseconds, next = started;
            int attempts = 0;
            while (session.ElapsedMilliseconds - started < 6000)
            {
                var screen = session.ReadScreen();
                if (screen == ForestHarvestScreen.Gate) return;
                Reject(screen);
                if (screen == expected && attempts < 3 && session.ElapsedMilliseconds >= next)
                {
                    session.Click(477, 362);
                    attempts++;
                    next = session.ElapsedMilliseconds + 1000;
                }
                session.Delay(200);
            }
            throw new InvalidOperationException("採集提示未關閉，已停止。");
        }

        private static void Reject(ForestHarvestScreen screen)
        {
            if (screen == ForestHarvestScreen.UnknownDialog)
                throw new InvalidOperationException("出現未支援的提示，已停止並保留畫面。");
        }
    }
}

