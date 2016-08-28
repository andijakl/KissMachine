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

        public static string ConvertCodeToText(int countdownVal)
        {
            var resourceLoader = ResourceLoader.GetForCurrentView();
            switch (countdownVal)
            {
                case (int)SpecialKissTexts.GiveAKiss:
                    return resourceLoader.GetString("GiveAKiss");
                case (int)SpecialKissTexts.PhotoTaken:
                    return resourceLoader.GetString("KissThankYou");
                case (int)SpecialKissTexts.AnotherPhoto:
                    return resourceLoader.GetString("AnotherPhoto");
            }
            return countdownVal > 0 ? countdownVal.ToString() : resourceLoader.GetString("Kiss"); ;
        }
    }
}
