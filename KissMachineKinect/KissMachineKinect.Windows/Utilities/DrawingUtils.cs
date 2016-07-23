using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace KissMachineKinect.Utilities
{
    public static class DrawingUtils
    {

        public static void ApplyMarchingAntsAnimation(this Shape shape, TimeSpan duration)
        {
            var storyboard = new Storyboard();
            var doubleAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = -shape.StrokeDashArray.Sum(),
                Duration = new Duration(duration),
                AutoReverse = false,
                RepeatBehavior = RepeatBehavior.Forever,
                EnableDependentAnimation = true
            };
            storyboard.Children.Add(doubleAnimation);
            Storyboard.SetTarget(doubleAnimation, shape);
            Storyboard.SetTargetProperty(doubleAnimation, "StrokeDashOffset");
            storyboard.Begin();
        }
    }
}
