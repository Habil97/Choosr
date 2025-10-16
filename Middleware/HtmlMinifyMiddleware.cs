using System.Text;
using System.Text.RegularExpressions;

namespace Choosr.Web.Middleware;

public class HtmlMinifyMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Regex WsBetweenTags = new Regex(
        @">\s+<",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public HtmlMinifyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only text/html responses
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context);
            buffer.Position = 0;
            var contentType = context.Response.ContentType ?? string.Empty;
            if(context.Response.StatusCode == 200 && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var html = await reader.ReadToEndAsync();
                var min = WsBetweenTags.Replace(html, "><");
                var bytes = Encoding.UTF8.GetBytes(min);
                context.Response.ContentLength = bytes.Length;
                await originalBody.WriteAsync(bytes, 0, bytes.Length, context.RequestAborted);
            }
            else
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody, context.RequestAborted);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}
