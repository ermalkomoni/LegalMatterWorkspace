namespace Amanet.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;

    public bool HasValidSigningKey()
    {
        try
        {
            return Convert.FromBase64String(SigningKey).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
