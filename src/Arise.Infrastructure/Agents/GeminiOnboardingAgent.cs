using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arise.Application.Features.Hunters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arise.Infrastructure.Agents;

/// <summary>
/// Implémentation Gemini de <see cref="IOnboardingAgent"/> — le tout premier agent concret du
/// dépôt, qui pose la convention reprise par les suivants (quêtes, rapport quotidien) :
/// validation en trois temps avant d'utiliser quoi que ce soit venu du modèle.
///
/// <list type="number">
/// <item><description><b>Parse</b> — l'enveloppe HTTP, puis le texte généré, sont-ils du JSON
/// exploitable ?</description></item>
/// <item><description><b>Forme</b> — <c>awakening_narrative</c> est-il une chaîne
/// présente ?</description></item>
/// <item><description><b>Garde-fous produit</b> — non vide, non démesurée, et sans aucun
/// chiffre : niveau, rang et XP de départ sont des constantes fixées par
/// <see cref="Arise.Domain.Hunters.HunterProfile.Create"/>, jamais par le modèle, donc toute
/// narration qui en mentionne un est nécessairement mensongère dès le premier Éveil. Ce
/// contrôle vit en C#, pas seulement dans le prompt — un garde-fou écrit uniquement dans le
/// prompt se contourne à la première réponse inattendue.</description></item>
/// </list>
///
/// <para>Un échec à n'importe quelle étape — y compris réseau ou délai dépassé — se solde par
/// un repli neutre, jamais par une exception qui remonterait à l'appelant, et jamais par le
/// texte brut du modèle : celui-ci n'a passé aucun contrôle.</para>
/// </summary>
internal sealed class GeminiOnboardingAgent(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiOnboardingAgent> logger)
    : IOnboardingAgent
{
    /// <summary>
    /// Borne haute de la narration générée. Le contrat de sortie promet 1 à 3 phrases : au-delà
    /// de quelques centaines de caractères, ce n'est plus une narration d'Éveil mais une dérive
    /// du modèle, à rejeter comme n'importe quel autre contenu hors garde-fou.
    /// </summary>
    private const int LongueurMaximaleNarration = 480;

    private static readonly JsonSerializerOptions OptionsJson =
        new(JsonSerializerDefaults.Web);

    private static readonly OnboardingAgentResult Repli = new(
        "Le Système t'a reconnu, Chasseur. Ton Éveil a commencé — chaque quête accomplie te "
        + "rapproche du Chasseur que tu deviens.",
        EstRepli: true);

    public async Task<OnboardingAgentResult> ExecuteAsync(
        OnboardingAgentRequest request, CancellationToken cancellationToken)
    {
        var config = options.Value;

        // La clé voyage en en-tête, jamais en query string : AddHttpClient enregistre par
        // défaut un LoggingHttpMessageHandler qui trace l'URI complète en niveau Information,
        // et un « ?key=... » finirait donc en clair dans les journaux de production.
        using var requeteHttp = new HttpRequestMessage(
            HttpMethod.Post, $"v1beta/models/{config.Model}:generateContent")
        {
            Content = ConstruireRequete(request),
        };
        requeteHttp.Headers.Add("x-goog-api-key", config.ApiKey);

        string corpsEnveloppe;
        try
        {
            // Disposée : sans cela la connexion sous-jacente reste retenue jusqu'au passage du
            // GC, et l'agent est appelé sur un chemin de requête utilisateur.
            using var reponseHttp = await httpClient.SendAsync(requeteHttp, cancellationToken);

            if (!reponseHttp.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Réponse HTTP {StatutCode} du Système (onboarding).", reponseHttp.StatusCode);
                return Repli;
            }

            corpsEnveloppe = await reponseHttp.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Panne réseau lors de l'appel au Système (onboarding).");
            return Repli;
        }
        catch (Exception exception) when (
            exception is TaskCanceledException or OperationCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Le jeton de l'appelant n'est pas celui qui a expiré : c'est le délai interne du
            // HttpClient qui a tranché, pas une annulation demandée par l'appelant — celle-là,
            // à l'inverse, doit continuer de se propager telle quelle.
            logger.LogWarning(exception, "Délai dépassé lors de l'appel au Système (onboarding).");
            return Repli;
        }

        string texteGenere;
        try
        {
            var enveloppe = JsonSerializer.Deserialize<GeminiReponse>(corpsEnveloppe, OptionsJson);
            var texte = enveloppe?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrEmpty(texte))
            {
                logger.LogWarning(
                    "Réponse du Système sans candidat exploitable (onboarding). Corps : {Corps}",
                    corpsEnveloppe);
                return Repli;
            }

            texteGenere = texte;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Enveloppe JSON du Système illisible (onboarding). Corps : {Corps}",
                corpsEnveloppe);
            return Repli;
        }

        OnboardingPayload? charge;
        try
        {
            charge = JsonSerializer.Deserialize<OnboardingPayload>(texteGenere, OptionsJson);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Contenu généré illisible en JSON (onboarding). Contenu : {Contenu}",
                texteGenere);
            return Repli;
        }

        var narration = charge?.AwakeningNarrative;

        // Garde-fou produit : une narration vide ou démesurée est rejetée comme n'importe quel
        // autre contenu qui ne respecte pas le contrat, jamais « corrigée » ni tronquée.
        if (string.IsNullOrWhiteSpace(narration))
        {
            logger.LogWarning("Narration vide ou absente rejetée (onboarding).");
            return Repli;
        }

        var narrationRognee = narration.Trim();

        if (narrationRognee.Length > LongueurMaximaleNarration)
        {
            logger.LogWarning(
                "Narration hors bornes rejetée (onboarding). Longueur : {Longueur}",
                narrationRognee.Length);
            return Repli;
        }

        // Garde-fou produit, en C# et pas seulement dans le prompt : niveau, rang et XP de
        // départ sont des constantes fixées par HunterProfile.Create(), jamais par le modèle.
        // Un chiffre dans la narration (« Rang C », « niveau 12 »...) est donc nécessairement
        // mensonger dès le premier Éveil — rejeté comme n'importe quel autre contenu hors
        // contrat, jamais « corrigé » en le retirant du texte.
        if (narrationRognee.Any(char.IsDigit))
        {
            logger.LogWarning("Narration contenant un chiffre rejetée (onboarding).");
            return Repli;
        }

        return new OnboardingAgentResult(narrationRognee, EstRepli: false);
    }

    private static StringContent ConstruireRequete(OnboardingAgentRequest request)
    {
        var corps = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = ConstruirePrompt(request) } } },
            },
            generationConfig = new { responseMimeType = "application/json" },
        };

        return new StringContent(
            JsonSerializer.Serialize(corps, OptionsJson), Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Le prompt reste en français — c'est l'utilisateur qui lit la sortie (règle n°7) — et ne
    /// demande jamais de chiffre : les stats et le niveau de départ sont des constantes fixées
    /// par <see cref="Arise.Domain.Hunters.HunterProfile.Create"/>, jamais par le modèle.
    /// </summary>
    private static string ConstruirePrompt(OnboardingAgentRequest request)
    {
        var objectifs = string.Join(", ", request.Objectifs.Select(LibelleFrancais));

        return "Tu es « le Système » de l'application ARISE, qui gamifie la vie réelle façon "
            + "Solo Leveling. Un nouveau Chasseur vient de s'Éveiller. Il a déclaré vouloir "
            + $"progresser sur : {objectifs}. Écris le message d'Éveil qu'il découvre à "
            + "l'écran : 1 à 3 phrases, en français, au tutoiement, dans la voix solennelle du "
            + "Système, personnalisées selon ses objectifs déclarés. Ne mentionne aucun "
            + "chiffre de statistique, de niveau ni de rang — ce ne sont jamais les tiens à "
            + "fixer. Réponds uniquement avec ce JSON, sans aucun texte autour : "
            + """{"awakening_narrative": "..."}""";
    }

    private static string LibelleFrancais(HunterGoal objectif) => objectif switch
    {
        HunterGoal.Sport => "le Sport",
        HunterGoal.Budget => "le Budget",
        HunterGoal.Habitudes => "les Habitudes et Tâches",
        HunterGoal.Calendrier => "le Calendrier",
        HunterGoal.Tout => "tous les domaines",
        _ => throw new ArgumentOutOfRangeException(nameof(objectif)),
    };

    private sealed record GeminiReponse(GeminiCandidat[]? Candidates);

    private sealed record GeminiCandidat(GeminiContenu? Content);

    private sealed record GeminiContenu(GeminiPartie[]? Parts);

    private sealed record GeminiPartie(string? Text);

    private sealed record OnboardingPayload(
        [property: JsonPropertyName("awakening_narrative")] string? AwakeningNarrative);
}
