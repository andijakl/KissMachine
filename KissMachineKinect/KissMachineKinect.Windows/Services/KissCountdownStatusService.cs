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
            switch (countdownVal)
            {
                case (int)SpecialKissTexts.GiveAKiss:
                    return "Gebt euch ein Bussi!";
                case (int)SpecialKissTexts.PhotoTaken:
                    return "Dankeschön! Ich hoffe, das Foto gefällt euch!";
                case (int)SpecialKissTexts.AnotherPhoto:
                    return "Ah, ihr wollt also noch ein Foto machen? Dann bleibt bitte noch kurz stehen...";
            }
            return countdownVal > 0 ? countdownVal.ToString() : "Bussi!";
        }
    }
}
