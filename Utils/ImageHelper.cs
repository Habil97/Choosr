using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Choosr.Web.Utils;

public static class ImageHelper
{
    // 16:9 crop + 1280x720 resize, jpg kalite 85
    public static async Task<string> SaveCover16x9Async(IFormFile file, string webRootPath, string subFolder = "uploads")
    {
        Directory.CreateDirectory(Path.Combine(webRootPath, subFolder));
        var fileName = $"{Guid.NewGuid():N}.jpg";
        var savePath = Path.Combine(webRootPath, subFolder, fileName);
        using var stream = file.OpenReadStream();
        using var image = await Image.LoadAsync(stream);

        // hedef en-boy oranı 16:9
        var targetRatio = 16d / 9d;
        var srcRatio = (double)image.Width / image.Height;

        if (srcRatio > targetRatio)
        {
            // fazla geniş → yataydan kes
            var newWidth = (int)(image.Height * targetRatio);
            var x = (image.Width - newWidth) / 2;
            image.Mutate(ctx => ctx.Crop(new Rectangle(x, 0, newWidth, image.Height)));
        }
        else if (srcRatio < targetRatio)
        {
            // fazla yüksek → dikeyden kes
            var newHeight = (int)(image.Width / targetRatio);
            var y = (image.Height - newHeight) / 2;
            image.Mutate(ctx => ctx.Crop(new Rectangle(0, y, image.Width, newHeight)));
        }

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(1280, 720)
        }));

        await image.SaveAsJpegAsync(savePath, new JpegEncoder { Quality = 85 });

        return $"/{subFolder}/{fileName}".Replace('\\', '/');
    }
}