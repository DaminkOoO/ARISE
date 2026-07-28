using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arise.Application.Features.Habits;
using Arise.Domain.Habits;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arise.Infrastructure.Agents;

/// <summary>
/// Implémentation Gemini de <see cref="IHabitSuggestionAgent"/>. Elle suit la convention posée
/// par <see cref="GeminiQuestGenerationAgent"/> — validation en trois temps avant d'utiliser quoi
/// que ce soit venu du modèle :
///
/// <list type="number">
/// <item><description><b>Parse</b> — l'enveloppe HTTP, puis le texte généré, sont-ils du JSON
/// exploitable ?</description></item>
/// <item><description><b>Forme</b> — chaque habitude porte-t-elle un nom dans les bornes de
/// <see cref="Habit.LongueurMaximaleNom"/> et un rythme de l'énumération attendue ?</description></item>
/// <item><description><b>Garde-fous produit</b> — chaque nom passe-t-il
/// <see cref="GardeFousDesHabitudes"/> : aucune prescription, aucun registre médical, aucun
/// reproche, du français ?</description></item>
/// </list>
///
/// <para>Une <b>seule</b> habitude fautive fait rejeter toute la réponse, plutôt que d'être
/// retirée en silence : servir une liste amputée reviendrait à publier du contenu partiellement
/// validé, et le Chasseur ne saurait ni ce qui manque ni pourquoi. La nouvelle tentative, elle,
/// vaut mieux qu'une liste tronquée.</para>
///
/// <para>Un échec de validation vaut une seule nouvelle tentative, avec le motif du rejet rappelé
/// au modèle. Une panne réseau ou un statut HTTP d'échec, eux, ne sont pas réessayés : le Système
/// est indisponible, pas incohérent. Dans tous les cas, l'échec final se solde par une liste de
/// repli — jamais une exception qui remonte, jamais le texte brut du modèle.</para>
/// </summary>
internal sealed class GeminiHabitSuggestionAgent(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiHabitSuggestionAgent> logger)
    : IHabitSuggestionAgent
{
    /// <inheritdoc cref="GeminiQuestGenerationAgent"/>
    private static readonly JsonSerializerOptions OptionsJson = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Assez pour donner le choix, pas assez pour noyer : au-delà, l'écran devient une liste à
    /// trier plutôt qu'une proposition à accepter.
    /// </summary>
    private const int SuggestionsMaximales = 5;

    /// <summary>
    /// Les habitudes servies quand le Système n'a rien rendu d'utilisable. Volontairement
    /// banales et sans chiffre : elles respectent les mêmes garde-fous que ce qu'on exige du
    /// modèle — un test le vérifie en les lui faisant repasser.
    /// </summary>
    private static readonly IReadOnlyList<HabitSuggestion> HabitudesDeRepli =
    [
        new("Boire un grand verre d'eau au réveil", HabitFrequency.Quotidienne),
        new("Lire quelques pages avant de dormir", HabitFrequency.Quotidienne),
        new("Marcher un peu après le déjeuner", HabitFrequency.Quotidienne),
        new("Ranger ton espace de travail", HabitFrequency.Hebdomadaire),
    ];

    private static readonly HabitSuggestionAgentResult Repli =
        new(HabitudesDeRepli, EstRepli: true);

    public async Task<HabitSuggestionAgentResult> ExecuteAsync(
        HabitSuggestionAgentRequest request, CancellationToken cancellationToken)
    {
        var premiere = await Tenter(request, reproche: null, cancellationToken);

        if (premiere.Suggestions is not null)
        {
            return new HabitSuggestionAgentResult(premiere.Suggestions, EstRepli: false);
        }

        // Pas de motif de rejet : le Système n'a pas répondu du tout. Réessayer ne changerait
        // rien et ferait attendre le Chasseur deux fois plus longtemps.
        if (premiere.Motif is null)
        {
            return Repli;
        }

        var seconde = await Tenter(request, premiere.Motif, cancellationToken);

        return seconde.Suggestions is null
            ? Repli
            : new HabitSuggestionAgentResult(seconde.Suggestions, EstRepli: false);
    }

    private async Task<Tentative> Tenter(
        HabitSuggestionAgentRequest request,
        string? reproche,
        CancellationToken cancellationToken)
    {
        var config = options.Value;

        // La clé voyage en en-tête, jamais en query string : AddHttpClient enregistre par défaut
        // un LoggingHttpMessageHandler qui trace l'URI complète en niveau Information, et un
        // « ?key=... » finirait donc en clair dans les journaux de production.
        using var requeteHttp = new HttpRequestMessage(
            HttpMethod.Post, $"v1beta/models/{config.Model}:generateContent")
        {
            Content = ConstruireRequete(request, reproche),
        };
        requeteHttp.Headers.Add("x-goog-api-key", config.ApiKey);

        try
        {
            using var reponseHttp = await httpClient.SendAsync(requeteHttp, cancellationToken);

            if (!reponseHttp.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Réponse HTTP {StatutCode} du Système (habitudes).", reponseHttp.StatusCode);
                return Tentative.Panne;
            }

            return Valider(await reponseHttp.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Panne réseau lors de l'appel au Système (habitudes).");
            return Tentative.Panne;
        }
        catch (Exception exception) when (
            exception is TaskCanceledException or OperationCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Le jeton de l'appelant n'est pas celui qui a expiré : c'est le délai interne du
            // HttpClient qui a tranché. Une annulation réellement demandée continue de se
            // propager telle quelle.
            logger.LogWarning(exception, "Délai dépassé lors de l'appel au Système (habitudes).");
            return Tentative.Panne;
        }
    }

    /// <summary>
    /// Les trois temps de la validation. Chaque rejet porte un motif en français, réutilisé tel
    /// quel dans le rappel adressé au modèle à la seconde tentative.
    /// </summary>
    private Tentative Valider(string corpsEnveloppe)
    {
        string texteGenere;
        try
        {
            var enveloppe = JsonSerializer.Deserialize<GeminiReponse>(corpsEnveloppe, OptionsJson);
            var texte = enveloppe?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(texte))
            {
                logger.LogWarning(
                    "Réponse du Système sans candidat exploitable (habitudes). Corps : {Corps}",
                    corpsEnveloppe);
                return Tentative.Rejet("ta réponse ne contenait aucun contenu exploitable");
            }

            texteGenere = texte;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Enveloppe JSON du Système illisible (habitudes). Corps : {Corps}",
                corpsEnveloppe);
            return Tentative.Rejet("ta réponse n'était pas lisible");
        }

        SuggestionsPayload? charge;
        try
        {
            charge = JsonSerializer.Deserialize<SuggestionsPayload>(
                SansBarrieresMarkdown(texteGenere), OptionsJson);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Contenu généré illisible en JSON (habitudes). Contenu : {Contenu}",
                texteGenere);
            return Tentative.Rejet("ta réponse n'était pas un JSON conforme au contrat");
        }

        if (charge?.Habits is not { Length: > 0 } proposees)
        {
            logger.LogWarning("Réponse du Système sans aucune habitude : rejetée.");
            return Tentative.Rejet("propose au moins une habitude");
        }

        var retenues = new List<HabitSuggestion>();

        foreach (var proposee in proposees)
        {
            if (string.IsNullOrWhiteSpace(proposee.Name))
            {
                logger.LogWarning("Habitude suggérée sans nom : rejetée.");
                return Tentative.Rejet("le nom d'une habitude ne peut pas être vide");
            }

            var nom = proposee.Name.Trim();

            if (nom.Length > Habit.LongueurMaximaleNom)
            {
                logger.LogWarning(
                    "Nom d'habitude hors bornes rejeté. Longueur : {Longueur}", nom.Length);
                return Tentative.Rejet(
                    $"le nom d'une habitude ne doit pas dépasser {Habit.LongueurMaximaleNom} caractères");
            }

            if (TraduireRythme(proposee.Frequency) is not { } rythme)
            {
                logger.LogWarning("Rythme d'habitude hors contrat rejeté : {Rythme}", proposee.Frequency);
                return Tentative.Rejet("frequency doit valoir exactement daily ou weekly");
            }

            // Garde-fou produit, en C# et pas seulement dans le prompt : un JSON parfaitement
            // formé peut proposer « Jeûner seize heures » ou « Arrêter d'être paresseux ».
            if (GardeFousDesHabitudes.Violation(nom) is { } motif)
            {
                logger.LogWarning("Habitude suggérée rejetée par les garde-fous.");
                return Tentative.Rejet(motif);
            }

            retenues.Add(new HabitSuggestion(nom, rythme));
        }

        // Tronqué et non rejeté : une liste trop longue est un défaut de mise en forme, pas de
        // contenu — chacune de ces habitudes a passé tous les contrôles.
        return Tentative.Reussite(retenues.Take(SuggestionsMaximales).ToList());
    }

    /// <inheritdoc cref="GeminiQuestGenerationAgent"/>
    private static string SansBarrieresMarkdown(string texte)
    {
        var rogne = texte.Trim();

        if (!rogne.StartsWith("```", StringComparison.Ordinal))
        {
            return texte;
        }

        var finDeLaPremiereLigne = rogne.IndexOf('\n');
        if (finDeLaPremiereLigne < 0)
        {
            return texte;
        }

        var interieur = rogne[(finDeLaPremiereLigne + 1)..].TrimEnd();

        return interieur.EndsWith("```", StringComparison.Ordinal)
            ? interieur[..^3]
            : texte;
    }

    private static HabitFrequency? TraduireRythme(string? jeton) =>
        jeton?.Trim().ToLowerInvariant() switch
        {
            "daily" => HabitFrequency.Quotidienne,
            "weekly" => HabitFrequency.Hebdomadaire,
            _ => null,
        };

    private static StringContent ConstruireRequete(
        HabitSuggestionAgentRequest request, string? reproche)
    {
        var corps = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = ConstruirePrompt(request, reproche) } } },
            },
            generationConfig = new { responseMimeType = "application/json" },
        };

        return new StringContent(
            JsonSerializer.Serialize(corps, OptionsJson), Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Le prompt est en français et au tutoiement — c'est le Chasseur qui lira la sortie
    /// (règle n°7). Il porte aussi les garde-fous, non parce qu'ils s'y appliqueraient vraiment
    /// (ils vivent dans <see cref="GardeFousDesHabitudes"/>), mais parce qu'une réponse conforme
    /// du premier coup vaut mieux qu'un repli servi au Chasseur.
    /// </summary>
    private static string ConstruirePrompt(HabitSuggestionAgentRequest request, string? reproche)
    {
        var prompt = new StringBuilder()
            .Append("Tu es « le Système » de l'application ARISE, qui gamifie la vie réelle ")
            .Append("façon Solo Leveling. Propose des habitudes à un Chasseur de ")
            .Append(CultureInfo.InvariantCulture, $"niveau {request.Level}, de rang {request.Rank}.")
            .Append("\n\nRègles de contenu, sans exception :")
            .Append("\n- En français, au tutoiement, dans la voix solennelle du Système. ")
            .Append("Chaque habitude est un libellé court et concret.")
            .Append("\n- Une intention que le Chasseur choisit de répéter, jamais une consigne ")
            .Append("médicale ou nutritionnelle : aucune charge en kilos, aucune allure, aucune ")
            .Append("distance chiffrée, aucune calorie, aucun jeûne, aucun dosage, aucun ")
            .Append("pourcentage.")
            .Append("\n- Aucun diagnostic, aucun régime, aucun traitement.")
            .Append("\n- Jamais de reproche : une habitude décrit ce que le Chasseur choisit de ")
            .Append("faire, jamais ce qu'il aurait manqué.")
            .Append(CultureInfo.InvariantCulture, $"\n- Chaque nom tient en {Habit.LongueurMaximaleNom} caractères.")
            .Append(CultureInfo.InvariantCulture, $"\n- Propose au plus {SuggestionsMaximales} habitudes.");

        if (request.HabitudesExistantes.Count > 0)
        {
            prompt
                .Append("\n\nLe Chasseur suit déjà ces habitudes — n'en propose aucune qui s'en ")
                .Append("rapproche : ")
                .Append(string.Join(", ", request.HabitudesExistantes));
        }

        prompt
            .Append("\n\nRéponds uniquement avec ce JSON, sans aucun texte autour : ")
            .Append("""{"habits":[{"name":"...","frequency":"daily|weekly"}]}""");

        if (reproche is not null)
        {
            prompt
                .Append(CultureInfo.InvariantCulture, $"\n\nTa réponse précédente a été rejetée : {reproche}. ")
                .Append("Respecte cette fois le contrat à la lettre.");
        }

        return prompt.ToString();
    }

    /// <summary>
    /// L'issue d'un appel au Système. <see cref="Motif"/> non nul signale un rejet de validation
    /// — le seul cas qui vaut une nouvelle tentative, avec ce motif en rappel.
    /// </summary>
    private sealed record Tentative(IReadOnlyList<HabitSuggestion>? Suggestions, string? Motif)
    {
        public static readonly Tentative Panne = new(null, null);

        public static Tentative Reussite(IReadOnlyList<HabitSuggestion> suggestions) =>
            new(suggestions, null);

        public static Tentative Rejet(string motif) => new(null, motif);
    }

    private sealed record GeminiReponse(GeminiCandidat[]? Candidates);

    private sealed record GeminiCandidat(GeminiContenu? Content);

    private sealed record GeminiContenu(GeminiPartie[]? Parts);

    private sealed record GeminiPartie(string? Text);

    private sealed record SuggestionsPayload(
        [property: JsonPropertyName("habits")] HabitudePayload[]? Habits);

    private sealed record HabitudePayload(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("frequency")] string? Frequency);
}
