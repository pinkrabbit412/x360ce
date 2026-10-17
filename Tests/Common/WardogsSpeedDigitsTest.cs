// @under-test: App.v4/Common/DInput/WardogsHudSpeedReader.cs
// @area: force-feedback  @layer: unit
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using x360ce.App.DInput;

namespace x360ce.Tests
{
    [TestClass]
    public class WardogsSpeedDigitsTest
    {
        [TestMethod, TestCategory("force-feedback")]
        public void Empty_hud_area_is_not_vehicle_speed()
        {
            using (var image = new Bitmap(80, 36))
            using (var g = Graphics.FromImage(image))
            {
                g.Clear(Color.Black);
                int kmh;
                Assert.IsFalse(WardogsSpeedDigits.TryRead(image, out kmh));
            }
        }

        [TestMethod, TestCategory("force-feedback")]
        public void Bright_featureless_background_is_not_vehicle_speed()
        {
            using (var image = new Bitmap(80, 36))
            using (var g = Graphics.FromImage(image))
            {
                g.Clear(Color.WhiteSmoke);
                int kmh;
                Assert.IsFalse(WardogsSpeedDigits.TryRead(image, out kmh));
            }
        }

        [TestMethod, TestCategory("force-feedback")]
        public void Empty_or_undersized_images_are_rejected()
        {
            int kmh;
            Assert.IsFalse(WardogsSpeedDigits.TryRead(null, out kmh));
            using (var small = new Bitmap(15, 12))
                Assert.IsFalse(WardogsSpeedDigits.TryRead(small, out kmh));
        }
    }
}
