using Hazdryx.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using 撈金魚.ActionPerform.Common;
using 撈金魚.structures;

namespace 撈金魚.ActionPerform
{
    internal class TmpPlayer : GamePlayer
    {
        public TmpPlayer(WindowPack.WindowSource window, int times) : base(window, times)
        {
        }

        protected override void GoToGameRegion()
        { }

        protected override void LeaveGameRegion()
        { }

        protected override bool PlayGame()
        {
            MouseInput.MouseClickForMole(window, 480, 280); //招集令
            MouseInput.MouseClickForMole(window, 610, 160); //月餅
            Wait.WaitForNormalConfirmDialog(window);
            FastBitmap shot = ScreenAction.GetContentShot(window);
            FastBitmap shot2 = new FastBitmap(245, 67);
            shot.CopyTo(shot2, 355, 277, 245, 67);
            shot2.Save(counter.FinishCount + ".jpg");
            Click.ClickNormalConfirmButton(window);
            return true;
        }

        protected override void StartGame()
        { }

        protected override void StopGame()
        { }
    }
}
