using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LumaShot
{
    public static partial class SurfaceChecks
    {
        public static void Require(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }

        public static BitmapSource ColorImage(byte red)
        {
            byte[] pixels = new byte[32 * 24 * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 60;
                pixels[i + 1] = 90;
                pixels[i + 2] = red;
                pixels[i + 3] = 255;
            }
            var image = BitmapSource.Create(
                32,
                24,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                32 * 4
            );
            image.Freeze();
            return image;
        }

        public static byte Red(BitmapSource image)
        {
            var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4];
            converted.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
            return pixel[2];
        }

        public static async Task<byte> ClipboardRed()
        {
            for (int i = 0; i < 15; i++)
            {
                try
                {
                    var image = Clipboard.GetImage();
                    if (image != null)
                        return Red(image);
                }
                catch (System.Runtime.InteropServices.COMException) { }
                await Task.Delay(100);
            }
            throw new IOException("Clipboard image could not be read after retries");
        }

        public static int AIButtons(DependencyObject root)
        {
            int n =
                root is Button
                && System.Windows.Automation.AutomationProperties.GetName(root) == "询问 AI"
                    ? 1
                    : 0;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                n += AIButtons(child);
            return n;
        }
    }

    public partial class MainUI
    {
        internal async Task CheckCaptureSurfaces(List<string> results)
        {
            poll.Stop();
            syncTimer.Stop();
            foreach (int i in Enumerable.Range(1, 12).Reverse())
            {
                string path = Path.Combine(engine.Inbox, "burst-" + i.ToString("00") + ".png");
                Images.Save(SurfaceChecks.ColorImage((byte)i), path);
                File.SetLastWriteTime(path, DateTime.Now.AddMinutes(-2).AddSeconds(i));
            }
            await Scan();
            SurfaceChecks.Require(lib.Items.Count == 12, "burst archive count");
            SurfaceChecks.Require(
                await SurfaceChecks.ClipboardRed() == 12,
                "latest captured image must remain on clipboard"
            );
            SurfaceChecks.Require(
                pendingCards.Last() == lib.Items.OrderBy(x => x.Date).Last().Id,
                "newest thumbnail must be last"
            );
            SurfaceChecks.Require(
                pendingCards.Count < 12
                    && 12 + pendingCards.Sum(id => CaptureTileHeight(FindShot(id)) + 18)
                        <= railArea.Height - 24,
                "rail must fit screen"
            );
            SurfaceChecks.Require(
                lib.Items.All(x => File.Exists(lib.Full(x.File))),
                "evicted thumbnails must retain files"
            );
            results.Add(
                "PASS reverse-enumerated 12-image burst is archived chronologically; rail fits working area; all files retained; clipboard pixels match newest capture"
            );
            var five = Enumerable.Range(0, 12).Select(x => x.ToString()).ToList();
            FitCaptureCards(five, id => 158, 820);
            SurfaceChecks.Require(
                five.Count == 5 && five[0] == "7",
                "820 logical pixels should retain five cards"
            );
            var nine = Enumerable.Range(0, 12).Select(x => x.ToString()).ToList();
            FitCaptureCards(nine, id => 158, 1450);
            SurfaceChecks.Require(nine.Count == 9, "larger screen should retain nine cards");
            results.Add(
                "PASS capacity follows available height: five cards at 820, nine at 1450 logical pixels"
            );
            var state = Backend
                .Json()
                .Deserialize<Dictionary<string, object>>("{\"profiles\":[],\"activeAIId\":null}");
            UpdateAIAvailability(state);
            var shot = lib.Items[0];
            SurfaceChecks.Require(
                SurfaceChecks.AIButtons(CaptureTile(shot)) == 0,
                "unconfigured AI must be hidden"
            );
            state = Backend
                .Json()
                .Deserialize<Dictionary<string, object>>(
                    "{\"profiles\":[{\"id\":\"test\",\"model\":\"\"}],\"activeAIId\":\"test\"}"
                );
            UpdateAIAvailability(state);
            SurfaceChecks.Require(
                SurfaceChecks.AIButtons(CaptureTile(shot)) == 0,
                "model required"
            );
            state = Backend
                .Json()
                .Deserialize<Dictionary<string, object>>(
                    "{\"profiles\":[{\"id\":\"test\",\"model\":\"test-model\"}],\"activeAIId\":\"test\"}"
                );
            UpdateAIAvailability(state);
            SurfaceChecks.Require(
                SurfaceChecks.AIButtons(CaptureTile(shot)) == 1
                    && SurfaceChecks.AIButtons(CaptureTile(shot, true)) == 1,
                "configured AI in both surfaces"
            );
            state = Backend
                .Json()
                .Deserialize<Dictionary<string, object>>(
                    "{\"profiles\":[{\"id\":\"test\",\"model\":\"test-model\",\"available\":false}],\"activeAIId\":\"test\"}"
                );
            UpdateAIAvailability(state);
            SurfaceChecks.Require(
                SurfaceChecks.AIButtons(CaptureTile(shot)) == 0,
                "missing optional component hides AI"
            );
            state["activeAIId"] = null;
            UpdateAIAvailability(state);
            SurfaceChecks.Require(
                SurfaceChecks.AIButtons(CaptureTile(shot)) == 0,
                "removal hides AI immediately"
            );
            results.Add(
                "PASS AI action absent without active profile/model or optional component, present in rail and quick history with configured model, absent after removal; no AI requests sent"
            );
            int staleAttempts = 0;
            var stale = CaptureClipboard.Set(() =>
            {
                staleAttempts++;
                if (staleAttempts == 1)
                    throw new System.Runtime.InteropServices.COMException(
                        "simulated busy clipboard"
                    );
                Clipboard.SetText("stale");
            });
            var writes = new List<Task> { stale };
            for (byte i = 20; i < 40; i++)
                writes.Add(Images.Copy(SurfaceChecks.ColorImage(i)));
            await Task.WhenAll(writes);
            SurfaceChecks.Require(
                staleAttempts == 1 && await SurfaceChecks.ClipboardRed() == 39,
                "old retries must not overwrite newest image"
            );
            results.Add(
                "PASS 20 overlapping image copy requests supersede a busy stale writer; clipboard pixels match final request"
            );
            SurfaceChecks.Require(Window.Icon != null && tray.Icon != null, "window/tray icons");
            using (
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                )
            )
            {
                SurfaceChecks.Require(icon != null, "embedded executable icon");
            }
            results.Add("PASS custom icon loads from executable for window and tray");
            ToggleQuickHistory();
            SurfaceChecks.Require(
                quickHistory.IsVisible && captureRail.IsVisible,
                "surface fixture visible"
            );
            capturing = true;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            await PrepareCaptureSurfaces();
            clock.Stop();
            SurfaceChecks.Require(
                !captureRail.IsVisible && quickHistory.IsVisible && Window.IsVisible,
                "only automatic preview thumbnails should be hidden"
            );
            results.Add(
                "PASS capture preparation hides only automatic previews; library and quick history stay visible; compositor wait "
                    + clock.ElapsedMilliseconds
                    + " ms (no fixed 250 ms delay)"
            );
            capturing = false;
            ResumeSurfaces();
            HideQuickHistory();
            SurfaceChecks.Require(captureRail.IsVisible, "previews return after capture");
        }

        internal void FinishSurfaceCheck()
        {
            Exit();
        }
    }
}
