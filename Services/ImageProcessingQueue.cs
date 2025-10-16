using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Gif;

namespace Choosr.Web.Services;

public record ImageProcessRequest(Stream Source, string FileName, string OutputPath, Func<Image, (IImageEncoder encoder, Action<IImageProcessingContext> mutate)> Pipeline, TaskCompletionSource<(bool ok, int? w, int? h)> Tcs);

public interface IImageProcessingQueue
{
    Task<(bool ok, int? w, int? h)> EnqueueAsync(ImageProcessRequest req, CancellationToken ct = default);
}

public class BoundedImageProcessingQueue : BackgroundService, IImageProcessingQueue
{
    private readonly Channel<ImageProcessRequest> _channel;

    public BoundedImageProcessingQueue()
    {
        // Bounded capacity to protect the server under load
        var opts = new BoundedChannelOptions(capacity: 64)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<ImageProcessRequest>(opts);
    }

    public Task<(bool ok, int? w, int? h)> EnqueueAsync(ImageProcessRequest req, CancellationToken ct = default)
    {
        if(!_channel.Writer.TryWrite(req))
        {
            return Task.FromResult((false, (int?)null, (int?)null));
        }
        return req.Tcs.Task;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach(var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                job.Source.Position = 0;
                using var image = await Image.LoadAsync(job.Source, stoppingToken);
                var (encoder, mutate) = job.Pipeline(image);
                image.Mutate(mutate);
                Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);
                await using var fs = new FileStream(job.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await image.SaveAsync(fs, encoder, stoppingToken);
                job.Tcs.TrySetResult((true, image.Width, image.Height));
            }
            catch
            {
                job.Tcs.TrySetResult((false, null, null));
            }
            finally
            {
                await job.Source.DisposeAsync();
            }
        }
    }

    // Helper factories for pipelines
    public static (IImageEncoder encoder, Action<IImageProcessingContext> mutate) Cover16x9(string ext)
    {
        IImageEncoder encoder = ext switch
        {
            ".png" => new PngEncoder(),
            ".webp" => new WebpEncoder(),
            ".gif" => new GifEncoder(),
            _ => new JpegEncoder { Quality = 85 }
        };
        Action<IImageProcessingContext> mutate = ctx => { /* set by caller per image size */ };
        // mutate is set later with actual crop based on current image dimensions
        return (encoder, mutate);
    }
}
