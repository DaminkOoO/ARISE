namespace Arise.Infrastructure.Auth;

/// <summary>
/// Paramètres de signature des jetons, liés à la section <c>Jwt</c> de la configuration. La
/// clé est un secret : elle vient de l'environnement (ou d'un secret utilisateur), jamais d'un
/// <c>appsettings.json</c> versionné.
/// </summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int DureeMinutes { get; set; }
}
