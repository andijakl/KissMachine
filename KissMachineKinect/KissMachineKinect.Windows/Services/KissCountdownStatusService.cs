using System;
using System.Diagnostics;
using Windows.ApplicationModel.Resources;

namespace KissMachineKinect.Services
{
    public class KissCountdownStatusService
    {
        public enum SpecialKissTexts
        {
            Invisible = -1,
            Kiss = 0,
            GiveAKiss = 99,
            PhotoTaken = 98,
            AnotherPhoto = 97
        }

        private static readonly Random Rnd = new Random();
        private const int NumGiveAKiss = 6;
        private const int NumKiss = 4;
        private const int NumKissThankYou = 7;
        private const int NumAnotherPhoto = 4;

        public static string ConvertCodeToText(int countdownVal)
        {
            var resourceLoader = ResourceLoader.GetForCurrentView();
            switch (countdownVal)
            {
                case (int)SpecialKissTexts.GiveAKiss:
                    return resourceLoader.GetString(string.Format("GiveAKiss_{0:00}", Rnd.Next(0, NumGiveAKiss)));
                case (int)SpecialKissTexts.PhotoTaken:
                    return resourceLoader.GetString(string.Format("KissThankYou_{0:00}", Rnd.Next(0, NumKissThankYou)));
                case (int)SpecialKissTexts.AnotherPhoto:
                    return resourceLoader.GetString(string.Format("AnotherPhoto_{0:00}", Rnd.Next(0, NumAnotherPhoto)));
            }
            return countdownVal > 0 ? countdownVal.ToString() : resourceLoader.GetString(string.Format("Kiss_{0:00}", Rnd.Next(0, NumKiss))); ;
        }


    }
}
