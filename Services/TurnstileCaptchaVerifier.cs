using System.Net.Http.Json;

namespace Choosr.Web.Services;

public class TurnstileCaptchaVerifier(IConfiguration config, IHttpClientFactory httpFactory) : ICaptchaVerifier
{
    private readonly string? _secret = config["Turnstile:SecretKey"] ?? config["Cloudflare:Turnstile:SecretKey"];

    public async Task<bool> VerifyAsync(string token, string ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_secret)) return true; // if not configured, allow (dev mode)
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var http = httpFactory.CreateClient();
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _secret!,
                ["response"] = token,
                ["remoteip"] = ipAddress
            });
            var resp = await http.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", form, ct);
            if (!resp.IsSuccessStatusCode) return false;
            var obj = await resp.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: ct);
            return obj?.success == true;
        }
        catch { return false; }
    }

    private sealed class TurnstileResponse
    {
        public bool success { get; set; }
        public string? hostname { get; set; }
        public string[]? error_codes { get; set; }
        public string? action { get; set; }
        public int? cdata { get; set; }
    }
}
