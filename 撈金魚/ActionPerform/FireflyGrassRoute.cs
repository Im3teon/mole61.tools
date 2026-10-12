using System;
using 撈金魚.Analyzer;

namespace 撈金魚.ActionPerform
{
    internal interface IFireflyGrassSession
    {
        FireflyGrassScreen ReadScreen();
        void Click(int x, int y);
        void Delay(int milliseconds);
        long ElapsedMilliseconds { get; }
        void Report(string status, int completed);
        bool TryEnterWaterfall();
    }

    internal sealed class FireflyGrassRoute
    {
        private readonly IFireflyGrassSession session;
        internal int Completed { get; private set; }
        internal FireflyGrassRoute(IFireflyGrassSession session) { this.session = session; }

        // Zero means harvest until the in-game daily limit, with an overall timeout.
        internal string Run(int requested)
        {
            if (requested < 0 || requested > 999) throw new ArgumentOutOfRangeException(nameof(requested));
            long started = session.ElapsedMilliseconds;
            // A reward left open by an earlier run must not block this run or count twice.
            if (session.ReadScreen() == FireflyGrassScreen.Reward) DismissReward();
            GoToWaterfall();
            while (requested == 0 || Completed < requested)
            {
                if (session.ElapsedMilliseconds - started > 600000)
                    throw new InvalidOperationException("已執行 10 分鐘，仍未確認每日上限，已停止。請檢查遊戲畫面。");
                session.Report("採收螢火草，等待獎勵提示", Completed);
                if (HarvestOnce() == FireflyGrassScreen.DailyLimit)
                {
                    DismissReward(FireflyGrassScreen.DailyLimit);
                    return "已達今日採收上限，明天再來。";
                }
                Completed++;
                session.Report("已完成採收", Completed);
                DismissReward();
            }
            return "已完成指定的採收回數。";
        }

        private void GoToWaterfall()
        {
            session.Report("前往漿果叢林 → 彩虹瀑布", Completed);
            var screen = session.ReadScreen();
            if (screen == FireflyGrassScreen.Waterfall || screen == FireflyGrassScreen.DailyLimit) return;
            if (screen == FireflyGrassScreen.UnknownDialog || screen == FireflyGrassScreen.Reward ||
                screen == FireflyGrassScreen.DailyLimit || screen == FireflyGrassScreen.AlreadyHere)
                throw new InvalidOperationException("請先關閉遊戲中的提示視窗，再開始採收。");
            if (screen != FireflyGrassScreen.BerryMap)
            {
                if (screen != FireflyGrassScreen.WorldMap)
                {
                    session.Report("開啟主世界地圖", Completed);
                    session.Click(39, 515);
                    WaitFor(FireflyGrassScreen.WorldMap, 10000, "無法開啟主世界地圖。請回到可開啟地圖的畫面。");
                }
                session.Report("開啟漿果叢林地圖", Completed);
                session.Click(48, 300);
                WaitFor(FireflyGrassScreen.BerryMap, 10000, "未能開啟漿果叢林地圖，已停止。");
            }
            session.Report("準備點擊彩虹瀑布 X:288 Y:206", Completed);
            // Give the map's opening animation time to settle, then confirm it before every attempt.
            session.Delay(600);
            long started = session.ElapsedMilliseconds;
            long nextAttempt = started;
            int attempts = 0;
            while (session.ElapsedMilliseconds - started < 15000)
            {
                screen = session.ReadScreen();
                if (screen == FireflyGrassScreen.Waterfall || screen == FireflyGrassScreen.DailyLimit) return;
                if (screen == FireflyGrassScreen.BerryMap && attempts < 3 && session.ElapsedMilliseconds >= nextAttempt)
                {
                    session.TryEnterWaterfall();
                    attempts++;
                    nextAttempt = session.ElapsedMilliseconds + 2500;
                    session.Delay(200);
                    continue;
                }
                if (screen == FireflyGrassScreen.AlreadyHere)
                {
                    session.Click(477, 362);
                    WaitFor(FireflyGrassScreen.BerryMap, 5000, "無法關閉已在此地的提示，已停止。");
                    session.Click(751, 87);
                    WaitFor(FireflyGrassScreen.Waterfall, 5000, "無法關閉漿果叢林地圖，已停止。");
                    return;
                }
                RejectUnexpectedDialog(screen);
                session.Delay(200);
            }
            throw new InvalidOperationException("未能確認已抵達彩虹瀑布，已停止。入口已點擊 " + attempts + " 次。");
        }

        private void DismissReward(FireflyGrassScreen dialog = FireflyGrassScreen.Reward)
        {
            long started = session.ElapsedMilliseconds;
            long nextClick = started;
            int attempts = 0;
            while (session.ElapsedMilliseconds - started < 6000)
            {
                var screen = session.ReadScreen();
                if (screen == FireflyGrassScreen.Waterfall) return;
                RejectUnexpectedDialog(screen);
                if (screen == dialog && attempts < 3 && session.ElapsedMilliseconds >= nextClick)
                {
                    session.Click(477, dialog == FireflyGrassScreen.DailyLimit ? 350 : 360);
                    attempts++;
                    nextClick = session.ElapsedMilliseconds + 1000;
                }
                session.Delay(200);
            }
            throw new InvalidOperationException("採收提示跳窗未關閉，已停止。請查看「知道了」按鈕是否被遮住。");
        }

        private FireflyGrassScreen HarvestOnce()
        {
            long started = session.ElapsedMilliseconds;
            long nextClick = started;
            while (session.ElapsedMilliseconds - started < 60000)
            {
                var screen = session.ReadScreen();
                if (screen == FireflyGrassScreen.DailyLimit || screen == FireflyGrassScreen.Reward) return screen;
                RejectUnexpectedDialog(screen);
                if (screen == FireflyGrassScreen.WorldMap || screen == FireflyGrassScreen.BerryMap)
                    throw new InvalidOperationException("採收期間開啟了地圖，已停止。請回到彩虹瀑布再試。");
                if (screen == FireflyGrassScreen.Waterfall && session.ElapsedMilliseconds >= nextClick)
                {
                    session.Click(80, 210);
                    nextClick = session.ElapsedMilliseconds + 2200;
                }
                session.Delay(200);
            }
            throw new InvalidOperationException("60 秒內未辨識到採收結果，已停止。請確認畫面及螢火草位置。");
        }

        private void WaitFor(FireflyGrassScreen expected, int timeout, string error)
        {
            long started = session.ElapsedMilliseconds;
            while (session.ElapsedMilliseconds - started < timeout)
            {
                var screen = session.ReadScreen();
                if (screen == expected) return;
                // Acknowledging a dialog is asynchronous; allow it to remain until the map returns.
                if (screen != FireflyGrassScreen.AlreadyHere || expected != FireflyGrassScreen.BerryMap)
                    RejectUnexpectedDialog(screen);
                session.Delay(200);
            }
            throw new InvalidOperationException(error);
        }

        private static void RejectUnexpectedDialog(FireflyGrassScreen screen)
        {
            if (screen == FireflyGrassScreen.UnknownDialog || screen == FireflyGrassScreen.AlreadyHere)
                throw new InvalidOperationException("出現尚未支援的遊戲提示，已停止並保留畫面。請截圖以便確認。");
        }
    }
}
