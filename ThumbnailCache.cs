using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace LumaShot
{
    internal static class ThumbnailCache
    {
        sealed class Entry
        {
            public string Key;
            public BitmapSource Image;
            public long Bytes;
        }

        static readonly Dictionary<string, LinkedListNode<Entry>> entries =
            new Dictionary<string, LinkedListNode<Entry>>();
        static readonly LinkedList<Entry> order = new LinkedList<Entry>();
        static long bytes;
        const long Budget = 32 * 1024 * 1024;
        internal static long Bytes
        {
            get
            {
                lock (entries)
                    return bytes;
            }
        }

        public static BitmapSource Load(string path, int width, Func<BitmapSource> read)
        {
            var file = new FileInfo(path);
            string key =
                file.FullName + "|" + width + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks;
            lock (entries)
            {
                LinkedListNode<Entry> found;
                if (entries.TryGetValue(key, out found))
                {
                    order.Remove(found);
                    order.AddLast(found);
                    return found.Value.Image;
                }
                var image = read();
                long size = (long)image.PixelWidth * image.PixelHeight * 4;
                if (size > Budget)
                    return image;
                while (bytes + size > Budget && order.First != null)
                {
                    var old = order.First;
                    order.RemoveFirst();
                    entries.Remove(old.Value.Key);
                    bytes -= old.Value.Bytes;
                }
                var node = order.AddLast(
                    new Entry
                    {
                        Key = key,
                        Image = image,
                        Bytes = size,
                    }
                );
                entries[key] = node;
                bytes += size;
                return image;
            }
        }
    }
}
