using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace LumaShot
{
    public static class StorageMaintenance
    {
        public static bool EqualFiles(string a, string b)
        {
            if (
                !File.Exists(a)
                || !File.Exists(b)
                || new FileInfo(a).Length != new FileInfo(b).Length
            )
                return false;
            using (var sha = SHA256.Create())
            using (var first = File.OpenRead(a))
            using (var second = File.OpenRead(b))
                return sha.ComputeHash(first).SequenceEqual(sha.ComputeHash(second));
        }

        public static void RemoveImportedInboxFile(
            Library library,
            string inbox,
            string source,
            Shot shot
        )
        {
            string path = Path.GetFullPath(source),
                directory = Path.GetFullPath(inbox);
            if (
                !string.Equals(
                    Path.GetDirectoryName(path),
                    directory,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                return;
            try
            {
                // Only delete this application's staging file after verifying its
                // durable original. Manual imports and unknown files stay intact.
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    return;
                if (EqualFiles(path, library.Full(shot.Original)))
                    File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static long CleanInbox(Library library, string inbox)
        {
            long bytes = 0;
            foreach (var shot in library.Items)
            {
                string key = shot.SourceKey;
                if (string.IsNullOrEmpty(key))
                    continue;
                int separator = key.IndexOf('|');
                if (separator < 1)
                    continue;
                string source = key.Substring(0, separator);
                if (!File.Exists(source))
                    continue;
                long size = new FileInfo(source).Length;
                RemoveImportedInboxFile(library, inbox, source, shot);
                if (!File.Exists(source))
                    bytes += size;
            }
            return bytes;
        }

        public static long CleanRedundantFiles(Library library, string inbox)
        {
            long bytes = CleanInbox(library, inbox);
            var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<IEnumerable<Shot>> protect = items =>
            {
                foreach (var shot in items)
                foreach (string relative in new[] { shot.File, shot.Original, shot.Thumb })
                    if (!string.IsNullOrEmpty(relative))
                        references.Add(library.Full(relative));
            };
            protect(library.Items);
            string backup = Path.Combine(library.Root, "library.json.bak");
            if (File.Exists(backup))
            {
                // A recoverable index must keep its referenced media too.
                try
                {
                    using (var stream = File.OpenRead(backup))
                        protect(
                            (List<Shot>)
                                new DataContractJsonSerializer(typeof(List<Shot>)).ReadObject(
                                    stream
                                )
                        );
                }
                catch
                {
                    return bytes;
                }
            }
            foreach (var shot in library.Items)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(shot.Id ?? "", "^[a-f0-9]{32}$"))
                    continue;
                var candidates = new[]
                {
                    "captures/" + shot.Id + Path.GetExtension(shot.File),
                    "captures/" + shot.Id + ".png",
                    "thumbs/" + shot.Id + ".png",
                };
                foreach (string relative in candidates.Distinct())
                {
                    string file = library.Full(relative);
                    if (
                        references.Contains(file)
                        || !File.Exists(file)
                        || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0
                    )
                        continue;
                    bool duplicate =
                        EqualFiles(file, library.Full(shot.File))
                        || EqualFiles(file, library.Full(shot.Original))
                        || EqualFiles(file, library.Full(shot.Thumb));
                    // Legacy video/GIF import also saved a full-size poster under
                    // this exact record ID. Compare decoded thumbnail pixels before removing it.
                    if (
                        !duplicate
                        && (shot.Kind == "video" || shot.Kind == "gif")
                        && relative == "captures/" + shot.Id + ".png"
                    )
                    {
                        try
                        {
                            duplicate = SamePixels(
                                Images.Thumbnail(Images.Load(file), 480),
                                Images.Load(library.Full(shot.Thumb))
                            );
                        }
                        catch { }
                    }
                    if (duplicate)
                    {
                        long size = new FileInfo(file).Length;
                        File.Delete(file);
                        bytes += size;
                    }
                }
            }
            return bytes;
        }

        static bool SamePixels(
            System.Windows.Media.Imaging.BitmapSource first,
            System.Windows.Media.Imaging.BitmapSource second
        )
        {
            if (first.PixelWidth != second.PixelWidth || first.PixelHeight != second.PixelHeight)
                return false;
            var a = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                first,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                0
            );
            var b = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                second,
                System.Windows.Media.PixelFormats.Bgra32,
                null,
                0
            );
            int stride = a.PixelWidth * 4;
            var x = new byte[stride];
            var y = new byte[stride];
            for (int row = 0; row < a.PixelHeight; row++)
            {
                var rect = new System.Windows.Int32Rect(0, row, a.PixelWidth, 1);
                a.CopyPixels(rect, x, stride, 0);
                b.CopyPixels(rect, y, stride, 0);
                if (!x.SequenceEqual(y))
                    return false;
            }
            return true;
        }
    }
}
