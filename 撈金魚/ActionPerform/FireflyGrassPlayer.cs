using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows;
using 撈金魚.ActionPerform.Common;
using 撈金魚.Analyzer;
using 撈金魚.structures;
using 撈金魚.UserInterface;

namespace 撈金魚.ActionPerform
{
    internal sealed class FireflyGrassPlayer : GamePlayer, IFireflyGrassSession
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private volatile bool cancelled;
        private int completed;
        private string lastClick = "尚未點擊";
        private FireflyGrassScreen lastScreen;
        private string diagnosticPath;

        internal FireflyGrassPlayer(WindowPack.WindowSource window, int times)
            : base(window, times, "彩虹瀑布 · 採收螢火草")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                counter.pause_button.Visibility = Visibility.Collapsed;
                counter.show_button.Visibility = Visibility.Collapsed;
                counter.stop_button.Click += (sender, args) => cancelled = true;
                counter.Closed += (sender, args) => cancelled = true;
                counter.counter.Text = "準備中";
            });
        }

        public override void Start()
        {
            try
            {
                ShowCounterFrame();
                string result = new FireflyGrassRoute(this).Run(total_times);
                Message.ShowMessageToUser(result + "\n本次已確認 " + completed + " 回採收獎勵。", "採收螢火草");
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                SaveDiagnostic();
                Message.ShowMessageToUser("採收已停止：" + error.Message +
                    (diagnosticPath == null ? "" : "\n診斷資料：" + diagnosticPath), "採收螢火草");
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() => { if (counter.IsVisible) counter.Close(); });
            }
        }

        public FireflyGrassScreen ReadScreen()
        {
            CheckCancelled();
            if (window.Process.HasExited) throw new InvalidOperationException("遊戲視窗已關閉。");
            using (var image = ScreenAction.GetContentShot(window))
            {
                var screen = AnalyzeFireflyGrass.Recognize(image.BaseBitmap);
                lastScreen = screen;
                return screen;
            }
        }

        public void Click(int x, int y)
        {
            CheckCancelled();
            // Match the detector's independent X/Y scaling at non-default game sizes.
            ClickCaptured((int)Math.Round(x * window.WindowRect.Width / 960.0),
                (int)Math.Round(y * window.WindowRect.Height / 560.0));
        }

        private void ClickCaptured(int x, int y)
        {
            CheckCancelled();
            if (x < 0 || y < 0 || x >= window.WindowRect.Width || y >= window.WindowRect.Height)
                throw new InvalidOperationException("入口座標超出遊戲畫面，請重新抓取座標。");
            lastClick = "Content=(" + x + "," + y + "), Client=(" + (window.WindowRect.left + x) + "," + (window.WindowRect.top + y) + ")";
            MouseInput.MoveForCapturedContent(window, x, y);
            Delay(150);
            CheckCancelled();
            MouseInput.ClickForCapturedContent(window, x, y);
        }

        public bool TryEnterWaterfall()
        {
            // User-supplied coordinates refer to captured content pixels, not the 960x560 model.
            // Keep this separate from Click(), which scales the prerecorded route coordinates.
            Report("點擊彩虹瀑布入口 X:288 Y:206", completed);
            ClickCaptured(288, 206);
            return true;
        }
        private void SaveDiagnostic()
        {
            try
            {
                string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics");
                Directory.CreateDirectory(directory);
                string stem = Path.Combine(directory, "firefly-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + window.Process.Id);
                using (var image = ScreenAction.GetContentShot(window)) image.BaseBitmap.Save(stem + ".png", ImageFormat.Png);
                File.WriteAllText(stem + ".txt", "Last click: " + lastClick + "\r\nScreen: " + lastScreen + "\r\nContent: " + window.WindowRect.Width + "x" + window.WindowRect.Height);
                diagnosticPath = stem + ".png";
            }
            catch { /* Diagnostic I/O must not hide the original failure. */ }
        }

        public void Delay(int milliseconds)
        {
            for (int remaining = milliseconds; remaining > 0; remaining -= 50)
            {
                CheckCancelled();
                Thread.Sleep(Math.Min(50, remaining));
            }
        }

        public long ElapsedMilliseconds => clock.ElapsedMilliseconds;

        public void Report(string status, int count)
        {
            CheckCancelled();
            completed = count;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (cancelled) return;
                counter.Title = "螢火草 · " + status;
                counter.counter.Text = total_times == 0 ? count + " 回（每日上限）" : count + " / " + total_times + " 回";
            });
        }

        private void CheckCancelled() { if (cancelled) throw new OperationCanceledException(); }
        protected override void StartGame() { }
        protected override bool PlayGame() => false;
        protected override void StopGame() { }
        protected override void GoToGameRegion() { }
        protected override void LeaveGameRegion() { }
    }
}
