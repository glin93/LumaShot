using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using F = System.Windows.Forms;

namespace LumaShot
{
    internal static class DisplayPlacement
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Point
        {
            public int X,
                Y;
        }

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromPoint(Point point, uint flags);

        [DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr monitor, int kind, out uint x, out uint y);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(
            IntPtr window,
            IntPtr after,
            int x,
            int y,
            int width,
            int height,
            uint flags
        );

        public static double Scale(F.Screen screen)
        {
            try
            {
                uint x,
                    y;
                var p = new Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 };
                if (GetDpiForMonitor(MonitorFromPoint(p, 2), 0, out x, out y) == 0 && x > 0)
                    return x / 96.0;
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
            return 1;
        }

        internal static Rect LogicalArea(System.Drawing.Rectangle area, double scale)
        {
            return new Rect(
                area.Left / scale,
                area.Top / scale,
                area.Width / scale,
                area.Height / scale
            );
        }

        public static Rect WorkArea(F.Screen screen)
        {
            return LogicalArea(screen.WorkingArea, Scale(screen));
        }

        public static void Position(
            Window window,
            F.Screen screen,
            double left,
            double top,
            double width,
            double height
        )
        {
            double scale = Scale(screen);
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
                SetWindowPos(
                    handle,
                    IntPtr.Zero,
                    (int)Math.Round(left * scale),
                    (int)Math.Round(top * scale),
                    (int)Math.Round(width * scale),
                    (int)Math.Round(height * scale),
                    0x14
                );
        }
    }
}
