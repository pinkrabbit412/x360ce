using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
    /// <summary>
    /// Optional, local-only WARDOGS speed source. Reads the game's on-screen km/h indicator
    /// rather than opening its process or reading game memory. No extra executable or OCR
    /// runtime is required. Disabled unless the Wheel page opts in.
    /// </summary>
    public sealed class WardogsHudSpeedReader : IDisposable
    {
        // Measured from a 2048x1152 WARDOGS vehicle HUD screenshot. Proportional coordinates
        // scale to 2560x1440; UI scaling / different vehicle HUDs may need new calibration.
        public const double DigitsLeft = 0.395;
        public const double DigitsTop = 0.855;
        public const double DigitsWidth = 0.034;
        public const double DigitsHeight = 0.032;

        const int IntervalMs = 100;
        const int StaleMs = 800;
        const int MaximumKmh = 260;
        readonly object deviceLock = new object();
        readonly Thread worker;
        volatile bool stopped;
        volatile string status = "Waiting for WARDOGS.";
        UserDevice target;
        int lastValidAt;
        int acceptedSpeed = -1;
        double filteredSpeed;
        int pendingSpeed = -1;
        int pendingCount;

        public string Status { get { return status; } }

        public WardogsHudSpeedReader()
        {
            worker = new Thread(Loop) { IsBackground = true, Name = "WARDOGS HUD speed OCR" };
            worker.Start();
        }

        /// <summary>Called from the UI thread only when the selected wheel changes.</summary>
        public void SetDevice(UserDevice device)
        {
            lock (deviceLock)
            {
                if (ReferenceEquals(target, device))
                    return;
                if (target != null)
                    target.ForceSpringSpeedOverride = null;
                target = device;
                if (target != null)
                    target.ForceSpringSpeedOverride = null;
            }
        }

        void Publish(int? kmh)
        {
            lock (deviceLock)
            {
                if (target != null)
                    target.ForceSpringSpeedOverride = kmh;
            }
        }

        void Loop()
        {
            while (!stopped)
            {
                try
                {
                    Rectangle game;
                    if (!TryGetForegroundGameRect(out game))
                    {
                        Publish(null);
                        status = "WARDOGS is not the foreground window (manual Speed used).";
                        ResetAfterLoss();
                    }
                    else
                    {
                        var bounds = new Rectangle(
                            game.Left + (int)(game.Width * DigitsLeft),
                            game.Top + (int)(game.Height * DigitsTop),
                            Math.Max(28, (int)(game.Width * DigitsWidth)),
                            Math.Max(20, (int)(game.Height * DigitsHeight)));
                        using (var hud = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
                        using (var g = Graphics.FromImage(hud))
                        {
                            // CopyFromScreen works with windowed/borderless games. Some exclusive
                            // fullscreen / protected surfaces cannot be captured through GDI.
                            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                            int read;
                            if (WardogsSpeedDigits.TryRead(hud, out read) && read <= MaximumKmh)
                                Accept(read);
                            else
                                Missing();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Publish(null);
                    status = "HUD capture failed: " + ex.GetType().Name + " (manual Speed used).";
                    ResetAfterLoss();
                }
                Thread.Sleep(IntervalMs);
            }
            Publish(null);
        }

        void Accept(int read)
        {
            int now = Environment.TickCount;
            if (acceptedSpeed < 0)
            {
                if (pendingSpeed >= 0 && Math.Abs(pendingSpeed - read) <= 2)
                    pendingCount++;
                else
                    pendingCount = 1;
                pendingSpeed = read;
                if (pendingCount < 2)
                {
                    status = "Validating first HUD speed reading...";
                    return;
                }
                filteredSpeed = read;
            }
            else if (Math.Abs(read - acceptedSpeed) > 18)
            {
                // A single 08 -> 80 misread must not jerk the wheel.
                if (pendingSpeed >= 0 && Math.Abs(pendingSpeed - read) <= 2)
                    pendingCount++;
                else
                    pendingCount = 1;
                pendingSpeed = read;
                if (pendingCount < 3)
                {
                    if (unchecked(now - lastValidAt) >= StaleMs)
                    {
                        Publish(null);
                        status = "Speed change not confirmed; manual Speed used until OCR stabilizes.";
                    }
                    else
                        status = "Confirming large speed change before changing wheel force...";
                    return;
                }
                filteredSpeed += (read - filteredSpeed) * 0.4;
            }
            else
            {
                filteredSpeed += (read - filteredSpeed) * 0.4;
            }

            pendingCount = 0;
            pendingSpeed = -1;
            acceptedSpeed = read;
            lastValidAt = now;
            int smoothed = (int)Math.Round(filteredSpeed);
            Publish(smoothed);
            status = "WARDOGS: " + read + " km/h (spring uses " + smoothed + " km/h).";
        }

        void Missing()
        {
            if (acceptedSpeed >= 0 && unchecked(Environment.TickCount - lastValidAt) < StaleMs)
            {
                status = "Speed OCR uncertain; waiting briefly for another HUD frame.";
                return;
            }
            Publish(null);
            status = "No reliable speed HUD reading (manual Speed used).";
            ResetAfterLoss();
        }

        void ResetAfterLoss()
        {
            acceptedSpeed = -1;
            pendingCount = 0;
            pendingSpeed = -1;
        }

        public void Dispose()
        {
            stopped = true;
            // The thread never holds deviceLock during capture or OCR.
            lock (deviceLock)
            {
                if (target != null)
                    target.ForceSpringSpeedOverride = null;
                target = null;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr handle, StringBuilder text, int length);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);
        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr handle, out NativeRect rect);
        [StructLayout(LayoutKind.Sequential)]
        struct NativePoint
        {
            public int X, Y;
        }

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr handle, ref NativePoint point);

        static bool TryGetForegroundGameRect(out Rectangle area)
        {
            area = Rectangle.Empty;
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return false;
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            bool wardogs = title.ToString().IndexOf("WARDOGS", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!wardogs)
            {
                uint pid;
                GetWindowThreadProcessId(window, out pid);
                if (pid == 0)
                    return false;
                try
                {
                    using (var process = Process.GetProcessById((int)pid))
                        wardogs = process.ProcessName.IndexOf("wardogs", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch (ArgumentException) { return false; }
                catch (System.ComponentModel.Win32Exception) { return false; }
                catch (InvalidOperationException) { return false; }
            }
            if (!wardogs)
                return false;
            NativeRect rect;
            if (!GetClientRect(window, out rect))
                return false;
            var origin = new NativePoint();
            if (!ClientToScreen(window, ref origin))
                return false;
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width < 1000 || height < 600)
                return false;
            area = new Rectangle(origin.X, origin.Y, width, height);
            return true;
        }
    }

    /// <summary>
    /// Small font-template OCR tuned for the WARDOGS white outlined speed digits.
    /// All recognition is local, and it is never run from the DirectInput poll thread.
    /// </summary>
    public static class WardogsSpeedDigits
    {
        const int GridW = 10;
        const int GridH = 16;
        sealed class Template
        {
            public int Digit;
            public bool[] Pixels;
        }
        static readonly Template[] templates = MakeTemplates();

        public static bool TryRead(Bitmap image, out int speed)
        {
            speed = 0;
            if (image == null || image.Width < 25 || image.Height < 18)
                return false;

            int w = image.Width, h = image.Height;
            var lum = new byte[w * h];
            var mask = new bool[w * h];

            // The speed glyphs are white with a black outline. Looking for a dark
            // neighbour suppresses most bright terrain/sky behind the HUD.
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = image.GetPixel(x, y);
                    lum[y * w + x] = (byte)((c.R * 30 + c.G * 59 + c.B * 11) / 100);
                }
            // HUD speed glyphs in real gameplay are often grey (roughly 70-130
            // luma), despite looking white on-screen. The old >=170 threshold
            // split each outlined '0' into disconnected vertical fragments.
            // Ignore the caption and horizontal border above the speed row.
            for (int y = Math.Max(2, h * 29 / 100); y < h - 2; y++)
                for (int x = 2; x < w - 2; x++)
                {
                    var n = y * w + x;
                    if (lum[n] < 70)
                        continue;
                    int min = 255;
                    for (int dy = -2; dy <= 2; dy += 2)
                        for (int dx = -2; dx <= 2; dx += 2)
                            min = Math.Min(min, lum[(y + dy) * w + x + dx]);
                    // Insist on a dark local outline, so bright terrain or
                    // sunlight alone cannot become a speed digit.
                    mask[n] = min < 45;
                }

            var components = Components(mask, w, h);
            var ranked = new List<Candidate>();
            foreach (var c in components)
            {
                if (c.Width < 3 || c.Height < h * 0.28 || c.Height > h * 0.75 ||
                    c.Width > c.Height * 1.4 || c.Width < c.Height * 0.22)
                    continue;
                var bits = Normalize(mask, w, c);
                int bestDigit = -1;
                double best = 0;
                foreach (var t in templates)
                {
                    double match = Similarity(bits, t.Pixels);
                    if (match > best)
                    {
                        best = match;
                        bestDigit = t.Digit;
                    }
                }
                if (best < 0.56)
                    continue;
                ranked.Add(new Candidate { Bounds = c, Digit = bestDigit, Score = best });
            }
            ranked.Sort((a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
            double bestScore = 0;
            int bestSpeed = -1;

            // Two or three digits form one row. Recognising an isolated bright object
            // must never turn into a plausible vehicle speed.
            for (int i = 0; i < ranked.Count; i++)
                for (int length = 2; length <= 3 && i + length <= ranked.Count; length++)
                {
                    int value = 0;
                    double score = 0;
                    bool aligned = true;
                    for (int j = 0; j < length; j++)
                    {
                        var c = ranked[i + j];
                        value = value * 10 + c.Digit;
                        score += c.Score;
                        if (j > 0)
                        {
                            var prev = ranked[i + j - 1].Bounds;
                            var cur = c.Bounds;
                            int gap = cur.Left - prev.Right;
                            if (gap < 0 || gap > Math.Max(8, prev.Height * 0.7) ||
                                Math.Abs(cur.Bottom - prev.Bottom) > Math.Max(3, prev.Height * 0.25))
                            {
                                aligned = false;
                                break;
                            }
                        }
                    }
                    if (!aligned || value > 260)
                        continue;
                    score /= length;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSpeed = value;
                    }
                }
            if (bestSpeed < 0 || bestScore < 0.62)
                return false;
            speed = bestSpeed;
            return true;
        }

        sealed class Candidate
        {
            public Rectangle Bounds;
            public int Digit;
            public double Score;
        }

        static List<Rectangle> Components(bool[] mask, int w, int h)
        {
            var found = new List<Rectangle>();
            var marked = new bool[mask.Length];
            var queue = new int[mask.Length];
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || marked[start])
                    continue;
                int head = 0, tail = 1;
                marked[start] = true;
                queue[0] = start;
                int minX = start % w, maxX = minX, minY = start / w, maxY = minY;
                while (head < tail)
                {
                    int at = queue[head++];
                    int x = at % w, y = at / w;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if ((dx == 0 && dy == 0) || nx < 0 || nx >= w || ny < 0 || ny >= h)
                                continue;
                            int n = ny * w + nx;
                            if (mask[n] && !marked[n])
                            {
                                marked[n] = true;
                                queue[tail++] = n;
                            }
                        }
                }
                found.Add(Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1));
            }
            return found;
        }

        static bool[] Normalize(bool[] source, int width, Rectangle r)
        {
            var result = new bool[GridW * GridH];
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                {
                    // Check the cell's centre and two nearby samples to survive thin glyphs.
                    int sx = r.Left + Math.Min(r.Width - 1, (x * r.Width + r.Width / 2) / GridW);
                    int sy = r.Top + Math.Min(r.Height - 1, (y * r.Height + r.Height / 2) / GridH);
                    bool on = source[sy * width + sx];
                    if (!on && sx + 1 < r.Right)
                        on = source[sy * width + sx + 1];
                    result[y * GridW + x] = on;
                }
            return result;
        }

        static double Similarity(bool[] a, bool[] b)
        {
            int overlap = 0, union = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] && b[i])
                    overlap++;
                if (a[i] || b[i])
                    union++;
            }
            return union == 0 ? 0 : overlap / (double)union;
        }

        static Template[] MakeTemplates()
        {
            var result = new List<Template>();
            var fonts = new[] { "Arial", "Segoe UI", "Tahoma", "Bahnschrift", "Consolas", "Arial Narrow" };
            foreach (var fontName in fonts)
                foreach (var fontStyle in new[] { FontStyle.Regular, FontStyle.Bold })
                    for (int digit = 0; digit < 10; digit++)
                    {
                        try
                        {
                            using (var bitmap = new Bitmap(48, 60))
                            using (var g = Graphics.FromImage(bitmap))
                            using (var font = new Font(fontName, 36, fontStyle, GraphicsUnit.Pixel))
                            {
                                g.Clear(Color.Black);
                                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                                g.DrawString(digit.ToString(), font, Brushes.White, 2f, 2f,
                                    StringFormat.GenericTypographic);
                                var pixels = new bool[bitmap.Width * bitmap.Height];
                                int minX = bitmap.Width, minY = bitmap.Height, maxX = -1, maxY = -1;
                                for (int y = 0; y < bitmap.Height; y++)
                                    for (int x = 0; x < bitmap.Width; x++)
                                    {
                                        if (bitmap.GetPixel(x, y).R < 130)
                                            continue;
                                        pixels[y * bitmap.Width + x] = true;
                                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                                    }
                                if (maxX >= minX && maxY >= minY)
                                    result.Add(new Template
                                    {
                                        Digit = digit,
                                        Pixels = Normalize(pixels, bitmap.Width,
                                            Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1))
                                    });
                            }
                        }
                        catch (ArgumentException) { }
                    }
            return result.ToArray();
        }
    }
}
