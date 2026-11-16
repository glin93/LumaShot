using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace LumaShot
{
    public static class LocalOcr
    {
        static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        // Windows handles OCR in process; no PowerShell startup per capture.
        public static async Task<string> Read(Library lib, Shot item)
        {
            await gate.WaitAsync();
            try
            {
                return await Task.Run(async () =>
                {
                    var engine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (engine == null)
                        throw new IOException("请在 Windows 中安装所需的 OCR 语言。");
                    var file = await Await<StorageFile>(
                        StorageFile.GetFileFromPathAsync(lib.Full(item.File))
                    );
                    using (
                        var stream = await Await<Windows.Storage.Streams.IRandomAccessStream>(
                            file.OpenAsync(FileAccessMode.Read)
                        )
                    )
                    {
                        var decoder = await Await<BitmapDecoder>(BitmapDecoder.CreateAsync(stream));
                        double scale = Math.Min(
                            1,
                            Math.Min(2400, OcrEngine.MaxImageDimension)
                                / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight)
                        );
                        var transform = new BitmapTransform
                        {
                            ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)),
                            ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)),
                        };
                        using (
                            var bitmap = await Await<SoftwareBitmap>(
                                decoder.GetSoftwareBitmapAsync(
                                    BitmapPixelFormat.Bgra8,
                                    BitmapAlphaMode.Premultiplied,
                                    transform,
                                    ExifOrientationMode.IgnoreExifOrientation,
                                    ColorManagementMode.DoNotColorManage
                                )
                            )
                        )
                        {
                            var result = await Await<OcrResult>(engine.RecognizeAsync(bitmap));
                            return string.Join(
                                Environment.NewLine,
                                result.Lines.Select(x => x.Text)
                            );
                        }
                    }
                });
            }
            finally
            {
                gate.Release();
            }
        }

        static Task<T> Await<T>(object operation)
        {
            var method = typeof(System.WindowsRuntimeSystemExtensions)
                .GetMethods()
                .First(x =>
                    x.Name == "AsTask"
                    && x.IsGenericMethod
                    && x.GetParameters().Length == 1
                    && x.GetParameters()[0].ParameterType.Name == "IAsyncOperation`1"
                );
            return (Task<T>)method.MakeGenericMethod(typeof(T)).Invoke(null, new[] { operation });
        }
    }
}
