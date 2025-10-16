namespace Choosr.Web.Services;

public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string token, string ipAddress, CancellationToken ct = default);
}
