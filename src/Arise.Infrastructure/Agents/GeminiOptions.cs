namespace Arise.Infrastructure.Agents;

/// <summary>
/// Paramètres d'appel de l'API Gemini, liés à la section <c>Gemini</c> de la configuration.
/// La clé est un secret : elle vient de l'environnement (ou d'un secret utilisateur), jamais
/// d'un <c>appsettings.json</c> versionné — même modèle que <see cref="Arise.Infrastructure.Auth.JwtOptions"/>.
/// </summary>
public sealed class GeminiOptions
{
    public const string Section = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Identifiant du modèle Gemini appelé (ex. <c>gemini-2.0-flash</c>).</summary>
    public string Model { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
}
