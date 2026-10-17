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

        // Greyscale stroke silhouettes reconstructed from two real HUD samples.
        // The test stores only tiny ASCII glyph masks, not full screenshots.
        static readonly string[] ZeroOnDark =
        {
            ".XXXXXX.", ".XX..XXX", "XX....XX", "XX....XX",
            "XX....XX", "XX....XX", "XX....XX", "XX....XX",
            "XX....XX", ".XX...XX", ".XXXXXX.", "..XXXX.."
        };

        static readonly string[] OtherZeroOnDark =
        {
            ".XXXXX..", "XX...XX.", "XX....X.", "XX....XX",
            "XX....XX", "XX....XX", "XX....XX", "XX....XX",
            "XX....XX", "XX...XX.", ".XXXXXX.", "..XXXX.."
        };

        static readonly string[] ZeroOnTerrain =
        {
            "..XXXX..", "..X.....", "..XXXX..", ".XXXXXXX",
            ".XXXXXXX", "XXX...XX", "XXX...XX", "XXX...XX",
            ".XX...XX", ".XX...XX", ".XX...XX", ".XX...XX",
            ".XX..XXX", "XXXXXXXX", "..XXXXX.", "...XX..."
        };

        static readonly string[] TwoOnTerrain =
        {
            "...XXX..", ".XXXXXX.", ".XX.XXXX", "XXX...XX",
            "XX....XX", ".....XXX", ".....XX.", "....XXX.",
            "...XXX..", "..XXX.XX", ".XXX....", ".XXXXXXX",
            "XXXXXXXX", ".XXXXXXX"
        };

        static void DrawGlyph(Bitmap bitmap, string[] rows, int x, int y, int foreground)
        {
            var ink = Color.FromArgb(foreground, foreground, foreground);
            for (int row = 0; row < rows.Length; row++)
                for (int col = 0; col < rows[row].Length; col++)
                    if (rows[row][col] == 'X')
                        bitmap.SetPixel(x + col, y + row, ink);
        }

        static Bitmap Hud()
        {
            var image = new Bitmap(69, 36);
            using (var g = Graphics.FromImage(image))
                g.Clear(Color.FromArgb(20, 20, 20));
            return image;
        }

        [TestMethod, TestCategory("force-feedback")]
        public void Reads_two_grey_zeroes_from_real_hud_stroke_shapes()
        {
            using (var hud = Hud())
            {
                DrawGlyph(hud, ZeroOnDark, 39, 13, 150);
                DrawGlyph(hud, OtherZeroOnDark, 49, 13, 150);
                int value;
                Assert.IsTrue(WardogsSpeedDigits.TryRead(hud, out value));
                Assert.AreEqual(0, value);
            }
        }

        [TestMethod, TestCategory("force-feedback")]
        public void Reads_zero_two_from_real_hud_stroke_shapes()
        {
            using (var hud = Hud())
            {
                DrawGlyph(hud, ZeroOnTerrain, 39, 10, 150);
                DrawGlyph(hud, TwoOnTerrain, 48, 12, 150);
                int value;
                Assert.IsTrue(WardogsSpeedDigits.TryRead(hud, out value));
                Assert.AreEqual(2, value);
            }
        }
    }
}
