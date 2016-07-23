using WindowsPreview.Kinect;

namespace KissMachineKinect.Models
{
    public class KissPositionModel
    {
        public float DistanceInM { get; set; } = float.MaxValue;
        public ColorSpacePoint Player1Pos { get; set; }
        public ColorSpacePoint Player2Pos { get; set; }
    }
}