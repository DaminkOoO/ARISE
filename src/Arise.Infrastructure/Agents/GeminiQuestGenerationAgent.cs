using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Arise.Application.Features.Sport;
using Arise.Domain.Quests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arise.Infrastructure.Agents;

/// <summary>
/// Implémentation Gemini de <see cref="IQuestGenerationAgent"/>. Elle reprend la convention
/// posée par <see cref="GeminiOnboardingAgent"/> — validation en trois temps avant d'utiliser
/// quoi que ce soit venu du modèle — et y ajoute ce que la quête de sport exige en propre :
///
/// <list type="number">
/// <item><description><b>Parse</b> — l'enveloppe HTTP, puis le texte généré, sont-ils du JSON
/// exploitable ?</description></item>
/// <item><description><b>Forme</b> — les six champs du contrat (doc mécaniques, section 3)
/// sont-ils présents, et <c>type</c>, <c>stat_target</c>, <c>difficulty</c> dans les
/// énumérations attendues ?</description></item>
/// <item><description><b>Garde-fous produit</b> — la récompense tombe-t-elle dans la
/// fourchette de sa difficulté (<see cref="BaremeXpQuete"/>), et le texte respecte-t-il les
/// règles du sport : aucune prescription chiffrée, aucun diagnostic, aucun
/// reproche ?</description></item>
/// </list>
///
/// <para>Un échec de validation vaut <b>une seule</b> nouvelle tentative, avec le motif du
/// rejet rappelé au modèle. Une panne réseau ou un statut HTTP d'échec, eux, ne sont pas
/// réessayés : le Système est indisponible, pas incohérent, et faire patienter le Chasseur une
/// seconde fois ne changerait rien. Dans tous les cas, l'échec final se solde par une quête de
/// repli — jamais une exception qui remonte, jamais le texte brut du modèle.</para>
/// </summary>
internal sealed class GeminiQuestGenerationAgent(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiQuestGenerationAgent> logger)
    : IQuestGenerationAgent
{
    /// <summary>
    /// <c>UnsafeRelaxedJsonEscaping</c> : sans lui, chaque accent du prompt partirait en
    /// séquence <c>\uXXXX</c>. Le corps reste du JSON valide en UTF-8 — l'échappement strict
    /// protège des contextes HTML/JS, dont celui-ci n'est pas — et le prompt français reste
    /// lisible dans les journaux comme dans les tests.
    /// </summary>
    private static readonly JsonSerializerOptions OptionsJson = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Au-delà de ce nombre, un chiffre cesse d'être un défi de jeu : « 40 pompes » se juge à
    /// vue d'œil, « 500 pompes » est une prescription déguisée. Le contexte transmis au modèle
    /// ne porte ni condition physique ni historique de complétion — le plafond ne peut donc pas
    /// s'adapter au Chasseur, il doit rester bas.
    /// </summary>
    private const int MagnitudeMaximale = 100;

    /// <summary>Une quête du jour ne mobilise pas plus d'une heure du Chasseur.</summary>
    private const int MinutesMaximales = 60;

    private static readonly Regex Nombre = new(
        @"\d+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex DureeEnMinutes = new(
        @"(\d+)\s*(?:min\b|mins\b|minutes?\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex DureeEnHeures = new(
        @"\d+\s*(?:h\b|heures?\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Toute distance chiffrée, sans plafond : adjointe à une durée, elle vaut une allure
    /// (« 10 km en 40 minutes » = 4 min/km), et l'allure est déjà interdite.
    /// </summary>
    private static readonly Regex DistanceChiffree = new(
        @"\d+\s*(?:km\b|kilometres?\b|metres?\b|miles?\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// La quête servie quand le Système n'a rien rendu d'utilisable. Elle respecte les mêmes
    /// garde-fous que ce qu'on exige du modèle — un test le vérifie en la lui faisant repasser
    /// — et porte le renvoi vers un professionnel de santé : c'est le seul texte que le
    /// Chasseur lira ce jour-là, il ne peut pas être plus silencieux que le prompt.
    /// </summary>
    private static readonly QuestGenerationAgentResult Repli = new(
        "Éveil du Corps",
        "Bouge à ton rythme aujourd'hui, Chasseur : une marche, quelques étirements, ce qui te "
        + "fait du bien. Écoute ton corps et arrête-toi quand il te le demande ; un "
        + "professionnel de santé saura t'orienter si besoin.",
        QuestType.Quotidienne,
        QuestStat.Force,
        QuestDifficulty.Facile,
        10,
        EstRepli: true);

    public async Task<QuestGenerationAgentResult> ExecuteAsync(
        QuestGenerationAgentRequest request, CancellationToken cancellationToken)
    {
        var premiere = await Tenter(request, reproche: null, cancellationToken);

        if (premiere.Quete is not null)
        {
            return premiere.Quete;
        }

        // Pas de motif de rejet : le Système n'a pas répondu du tout. Réessayer ne changerait
        // rien et ferait attendre le Chasseur deux fois plus longtemps.
        if (premiere.Motif is null)
        {
            return Repli;
        }

        var seconde = await Tenter(request, premiere.Motif, cancellationToken);

        return seconde.Quete ?? Repli;
    }

    private async Task<Tentative> Tenter(
        QuestGenerationAgentRequest request, string? reproche, CancellationToken cancellationToken)
    {
        var config = options.Value;

        // La clé voyage en en-tête, jamais en query string : AddHttpClient enregistre par
        // défaut un LoggingHttpMessageHandler qui trace l'URI complète en niveau Information,
        // et un « ?key=... » finirait donc en clair dans les journaux de production.
        using var requeteHttp = new HttpRequestMessage(
            HttpMethod.Post, $"v1beta/models/{config.Model}:generateContent")
        {
            Content = ConstruireRequete(request, reproche),
        };
        requeteHttp.Headers.Add("x-goog-api-key", config.ApiKey);

        try
        {
            // Disposée : sans cela la connexion sous-jacente reste retenue jusqu'au passage du
            // GC, et l'agent est appelé sur un chemin de lecture utilisateur.
            using var reponseHttp = await httpClient.SendAsync(requeteHttp, cancellationToken);

            if (!reponseHttp.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Réponse HTTP {StatutCode} du Système (quête).", reponseHttp.StatusCode);
                return Tentative.Panne;
            }

            return Valider(await reponseHttp.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Panne réseau lors de l'appel au Système (quête).");
            return Tentative.Panne;
        }
        catch (Exception exception) when (
            exception is TaskCanceledException or OperationCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Le jeton de l'appelant n'est pas celui qui a expiré : c'est le délai interne du
            // HttpClient qui a tranché. Une annulation réellement demandée, elle, continue de
            // se propager telle quelle.
            logger.LogWarning(exception, "Délai dépassé lors de l'appel au Système (quête).");
            return Tentative.Panne;
        }
    }

    /// <summary>
    /// Les trois temps de la validation. Chaque rejet porte un motif en français, réutilisé
    /// tel quel dans le rappel adressé au modèle à la seconde tentative.
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
                    "Réponse du Système sans candidat exploitable (quête). Corps : {Corps}",
                    corpsEnveloppe);
                return Tentative.Rejet("ta réponse ne contenait aucun contenu exploitable");
            }

            texteGenere = texte;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception, "Enveloppe JSON du Système illisible (quête). Corps : {Corps}", corpsEnveloppe);
            return Tentative.Rejet("ta réponse n'était pas lisible");
        }

        QuetePayload? charge;
        try
        {
            charge = JsonSerializer.Deserialize<QuetePayload>(SansBarrieresMarkdown(texteGenere), OptionsJson);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception, "Contenu généré illisible en JSON (quête). Contenu : {Contenu}", texteGenere);
            return Tentative.Rejet("ta réponse n'était pas un JSON conforme au contrat");
        }

        if (charge is null)
        {
            return Tentative.Rejet("ta réponse n'était pas un JSON conforme au contrat");
        }

        if (string.IsNullOrWhiteSpace(charge.Title) || string.IsNullOrWhiteSpace(charge.Description))
        {
            logger.LogWarning("Quête générée sans titre ou sans description : rejetée.");
            return Tentative.Rejet("le titre et la description ne peuvent pas être vides");
        }

        var titre = charge.Title.Trim();
        var description = charge.Description.Trim();

        if (titre.Length > Quest.LongueurMaximaleTitre)
        {
            logger.LogWarning("Titre de quête hors bornes rejeté. Longueur : {Longueur}", titre.Length);
            return Tentative.Rejet(
                $"le titre ne doit pas dépasser {Quest.LongueurMaximaleTitre} caractères");
        }

        if (description.Length > Quest.LongueurMaximaleDescription)
        {
            logger.LogWarning(
                "Description de quête hors bornes rejetée. Longueur : {Longueur}", description.Length);
            return Tentative.Rejet(
                $"la description ne doit pas dépasser {Quest.LongueurMaximaleDescription} caractères");
        }

        if (TraduireType(charge.Type) is not { } type)
        {
            logger.LogWarning("Type de quête hors contrat rejeté : {Type}", charge.Type);
            return Tentative.Rejet("type doit valoir exactement daily ou penalty");
        }

        if (TraduireStat(charge.StatTarget) is not { } statCible)
        {
            logger.LogWarning("Statistique hors contrat rejetée : {Stat}", charge.StatTarget);
            return Tentative.Rejet("stat_target doit valoir FOR, VIT, INT, OR ou PER");
        }

        if (TraduireDifficulte(charge.Difficulty) is not { } difficulte)
        {
            logger.LogWarning("Difficulté hors contrat rejetée : {Difficulte}", charge.Difficulty);
            return Tentative.Rejet("difficulty doit valoir easy, medium ou hard");
        }

        // Garde-fou d'équilibrage : sans lui, le modèle fixerait seul la vitesse de
        // progression du Chasseur.
        if (!BaremeXpQuete.EstCoherent(type, difficulte, charge.XpReward))
        {
            var (minimum, maximum) = BaremeXpQuete.Fourchette(type, difficulte);
            logger.LogWarning(
                "Récompense incohérente rejetée : {Xp} XP pour une quête {Difficulte}.",
                charge.XpReward,
                difficulte);
            return Tentative.Rejet(
                $"xp_reward doit tenir entre {minimum} et {maximum} pour cette difficulté");
        }

        // Garde-fou produit, en C# et pas seulement dans le prompt : un JSON parfaitement
        // formé peut prescrire « 4×12 à 80 kg » ou diagnostiquer une tendinite.
        if (ViolationDesGardeFous(titre) is { } motifTitre)
        {
            logger.LogWarning("Titre de quête rejeté par les garde-fous du sport.");
            return Tentative.Rejet(motifTitre);
        }

        if (ViolationDesGardeFous(description) is { } motifDescription)
        {
            logger.LogWarning("Description de quête rejetée par les garde-fous du sport.");
            return Tentative.Rejet(motifDescription);
        }

        // En dernier, et sur la description seule : c'est le contrôle le plus grossier des
        // trois, celui qui rattrape ce que des lexiques français laissent forcément passer.
        if (!GardeFousTextuels.SembleEtreEnFrancais(description))
        {
            logger.LogWarning("Description de quête rejetée : elle ne semble pas être en français.");
            return Tentative.Rejet("la quête doit être écrite en français, au tutoiement");
        }

        return Tentative.Reussite(new QuestGenerationAgentResult(
            titre, description, type, statCible, difficulte, charge.XpReward, EstRepli: false));
    }

    /// <summary>
    /// Retire les barrières de bloc de code dont le modèle encadre volontiers sa réponse, même
    /// en mode JSON — son réflexe le plus tenace. Le contenu est parfaitement valide ; se replier
    /// pour trois caractères de décoration servirait au Chasseur un texte générique.
    ///
    /// <para>Décoration seulement : ce qui reste est désérialisé et repasse par la totalité des
    /// contrôles, aucune indulgence n'est accordée au contenu lui-même.</para>
    /// </summary>
    private static string SansBarrieresMarkdown(string texte)
    {
        var rogne = texte.Trim();

        if (!rogne.StartsWith("```", StringComparison.Ordinal))
        {
            return texte;
        }

        // La barrière ouvrante porte parfois le langage annoncé (```json) : tout ce qui suit
        // sur cette première ligne en fait partie.
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

    /// <summary>
    /// Le motif de rejet du texte, ou <see langword="null"/> s'il passe les garde-fous du
    /// sport (règle non négociable n°5).
    /// </summary>
    private static string? ViolationDesGardeFous(string texte)
    {
        if (GardeFousTextuels.EstVisiblementAnglais(texte))
        {
            return "la quête doit être écrite en français, au tutoiement";
        }

        var normalise = GardeFousTextuels.SansAccents(texte);

        if (GardeFousTextuels.PrescritDesChiffres(normalise))
        {
            return "une quête ne prescrit ni charge, ni allure, ni calories, ni dosage, ni "
                + "pourcentage d'effort";
        }

        // Propre au sport, et donc laissé ici : la magnitude tolérable dépend de ce qu'une
        // quête du jour est censée mobiliser, pas d'une règle de texte partagée.
        if (MagnitudeHorsBornes(normalise) is { } motifMagnitude)
        {
            return motifMagnitude;
        }

        if (GardeFousTextuels.InviteAPasserOutreLaDouleur(normalise))
        {
            return "une quête n'invite jamais à s'entraîner malgré une douleur : elle invite au "
                + "contraire à s'arrêter et à consulter un professionnel de santé";
        }

        if (GardeFousTextuels.Reproche(normalise))
        {
            return "une quête ne reproche jamais rien au Chasseur, même quand sa série est "
                + "tombée à zéro";
        }

        if (GardeFousTextuels.EmploieUnVocabulaireInterdit(normalise))
        {
            return "une quête ne pose aucun diagnostic, ne prescrit aucun traitement et ne "
                + "formule jamais de reproche";
        }

        return null;
    }

    /// <summary>
    /// Le motif de rejet d'une magnitude que le Chasseur n'a pas les moyens de discuter, ou
    /// <see langword="null"/>. Borner les unités sans borner les nombres laissait passer
    /// « Cours 10 km en 40 minutes », « 500 pompes » ou « 3 heures de gainage » : la frontière
    /// tenable est l'unité <b>et</b> la magnitude.
    /// </summary>
    private static string? MagnitudeHorsBornes(string normalise)
    {
        if (DistanceChiffree.IsMatch(normalise))
        {
            return "une quête ne fixe aucune distance chiffrée";
        }

        if (DureeEnHeures.IsMatch(normalise))
        {
            return $"une quête du jour ne dépasse pas {MinutesMaximales} minutes d'effort";
        }

        foreach (Match duree in DureeEnMinutes.Matches(normalise))
        {
            // Un nombre trop long pour un int est, à plus forte raison, hors bornes.
            if (!int.TryParse(duree.Groups[1].Value, out var minutes) || minutes > MinutesMaximales)
            {
                return $"une quête du jour ne dépasse pas {MinutesMaximales} minutes d'effort";
            }
        }

        foreach (Match nombre in Nombre.Matches(normalise))
        {
            if (!int.TryParse(nombre.Value, out var valeur) || valeur > MagnitudeMaximale)
            {
                return $"aucun chiffre d'une quête ne dépasse {MagnitudeMaximale}";
            }
        }

        return null;
    }

    private static QuestType? TraduireType(string? jeton) => jeton?.Trim().ToLowerInvariant() switch
    {
        "daily" => QuestType.Quotidienne,
        "penalty" => QuestType.Penalite,
        _ => null,
    };

    private static QuestStat? TraduireStat(string? jeton) => jeton?.Trim().ToLowerInvariant() switch
    {
        "for" => QuestStat.Force,
        "vit" => QuestStat.Vitesse,
        "int" => QuestStat.Intelligence,
        "or" => QuestStat.Or,
        "per" => QuestStat.Perception,
        _ => null,
    };

    private static QuestDifficulty? TraduireDifficulte(string? jeton) =>
        jeton?.Trim().ToLowerInvariant() switch
        {
            "easy" => QuestDifficulty.Facile,
            "medium" => QuestDifficulty.Moyenne,
            "hard" => QuestDifficulty.Difficile,
            _ => null,
        };

    private static StringContent ConstruireRequete(
        QuestGenerationAgentRequest request, string? reproche)
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
    /// (règle n°7). Il porte aussi les garde-fous, non parce qu'ils s'y appliqueraient
    /// vraiment (ils vivent dans <see cref="ViolationDesGardeFous"/>), mais parce qu'une
    /// réponse conforme du premier coup vaut mieux qu'un repli servi au Chasseur.
    /// </summary>
    private static string ConstruirePrompt(QuestGenerationAgentRequest request, string? reproche)
    {
        var (minFacile, maxFacile) = BaremeXpQuete.Fourchette(
            QuestType.Quotidienne, QuestDifficulty.Facile);
        var (minMoyenne, maxMoyenne) = BaremeXpQuete.Fourchette(
            QuestType.Quotidienne, QuestDifficulty.Moyenne);
        var (minDifficile, maxDifficile) = BaremeXpQuete.Fourchette(
            QuestType.Quotidienne, QuestDifficulty.Difficile);

        var prompt = new StringBuilder()
            .Append("Tu es « le Système » de l'application ARISE, qui gamifie la vie réelle ")
            .Append("façon Solo Leveling. Écris la quête de sport du jour d'un Chasseur de ")
            .Append(CultureInfo.InvariantCulture, $"niveau {request.Level}, de rang ")
            .Append(CultureInfo.InvariantCulture, $"{request.Rank}, qui tient une série de ")
            .Append(CultureInfo.InvariantCulture, $"{request.StreakCurrent} jours.")
            .Append("\n\nRègles de contenu, sans exception :")
            .Append("\n- En français, au tutoiement, dans la voix solennelle du Système. ")
            .Append("Court et concret.")
            .Append("\n- Un défi de jeu, jamais une consigne médicale : aucune charge en kilos, ")
            .Append("aucune allure, aucune dépense en calories, aucun pourcentage d'effort.")
            .Append(CultureInfo.InvariantCulture, $"\n- Aucune distance chiffrée, aucun effort de ")
            .Append(CultureInfo.InvariantCulture, $"plus de {MinutesMaximales} minutes, et aucun chiffre ")
            .Append(CultureInfo.InvariantCulture, $"supérieur à {MagnitudeMaximale} : le Chasseur juge ")
            .Append("seul de son intensité.")
            .Append("\n- Aucun diagnostic, aucune interprétation de symptôme, aucun conseil de ")
            .Append("traitement. Le Chasseur s'arrête quand il le décide.")
            .Append("\n- Termine la description en l'invitant à écouter son corps, à s'arrêter ")
            .Append("s'il ressent une douleur et à consulter un professionnel de santé si elle ")
            .Append("persiste. Ne lui demande jamais de poursuivre malgré une douleur.")
            .Append("\n- Jamais de reproche ni de culpabilisation, même si la série est à zéro.")
            .Append(CultureInfo.InvariantCulture, $"\n- Le titre tient en {Quest.LongueurMaximaleTitre} ")
            .Append(CultureInfo.InvariantCulture, $"caractères, la description en {Quest.LongueurMaximaleDescription}.")
            .Append("\n\nRéponds uniquement avec ce JSON, sans aucun texte autour : ")
            .Append("""{"title":"...","description":"...","type":"daily","stat_target":"FOR|VIT|INT|OR|PER","difficulty":"easy|medium|hard","xp_reward":<entier>}""")
            .Append("\n\nxp_reward doit tomber dans la fourchette de la difficulté annoncée : ")
            .Append(CultureInfo.InvariantCulture, $"easy {minFacile}-{maxFacile}, medium {minMoyenne}-{maxMoyenne}, ")
            .Append(CultureInfo.InvariantCulture, $"hard {minDifficile}-{maxDifficile}.");

        if (reproche is not null)
        {
            prompt
                .Append(CultureInfo.InvariantCulture, $"\n\nTa réponse précédente a été rejetée : {reproche}. ")
                .Append("Respecte cette fois le contrat à la lettre.");
        }

        return prompt.ToString();
    }

    /// <summary>
    /// L'issue d'un appel au Système. <see cref="Motif"/> non nul signale un rejet de
    /// validation — le seul cas qui vaut une nouvelle tentative, avec ce motif en rappel.
    /// </summary>
    private sealed record Tentative(QuestGenerationAgentResult? Quete, string? Motif)
    {
        public static readonly Tentative Panne = new(null, null);

        public static Tentative Reussite(QuestGenerationAgentResult quete) => new(quete, null);

        public static Tentative Rejet(string motif) => new(null, motif);
    }

    private sealed record GeminiReponse(GeminiCandidat[]? Candidates);

    private sealed record GeminiCandidat(GeminiContenu? Content);

    private sealed record GeminiContenu(GeminiPartie[]? Parts);

    private sealed record GeminiPartie(string? Text);

    private sealed record QuetePayload(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("stat_target")] string? StatTarget,
        [property: JsonPropertyName("difficulty")] string? Difficulty,
        [property: JsonPropertyName("xp_reward")] int XpReward);
}
