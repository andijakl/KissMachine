using WindowsPreview.Kinect;

namespace KissMachineKinect.Models
{
    public class KissPositionModel
    {
        public float DistanceInM { get; set; } = float.MaxValue;
        public ColorSpacePoint Player1Pos { get; set; }
        public ColorSpacePoint Player2Pos { get; set; }
        public int Player1BodyNum { get; set; }
        public int Player2BodyNum { get; set; }
        public ulong Player1TrackingId { get; set; }
        public ulong Player2TrackingId { get; set; }
    }
}