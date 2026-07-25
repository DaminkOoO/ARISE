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
    /// Prescription chiffrée : charge, allure, calories, pourcentage d'effort. Interdites par
    /// la règle non négociable n°5.
    ///
    /// <para>Ce qui n'est <b>pas</b> visé, et volontairement : les répétitions et les minutes
    /// au poids du corps (« 40 pompes », « 5 minutes de gainage »). C'est l'exemple validé du
    /// document de référence, et un filtre qui le rejetterait servirait le repli tous les
    /// jours — une dégradation permanente au nom d'un garde-fou.</para>
    /// </summary>
    private static readonly Regex PrescriptionChiffree = new(
        @"\d+\s*(?:%|kg\b|kilos?\b|kilogrammes?\b|lbs?\b|livres?\b|kcal\b|calories?\b|rm\b"
        + @"|watts?\b|km\s*/\s*h\b|min\s*/\s*km\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Vocabulaire qu'une quête n'a aucune raison d'employer : diagnostic et interprétation de
    /// symptôme, registre médical, et reproche.
    ///
    /// <para>S'applique au texte débarrassé de ses accents, pour qu'un « echoue » sans accent
    /// ne passe pas au travers. Les faux positifs coûtent une nouvelle tentative puis une
    /// quête de repli sûre : jamais une quête douteuse affichée au Chasseur.</para>
    ///
    /// <para><b>« douleur » et « blessure » n'y figurent pas</b>, et c'est délibéré : la règle
    /// n°5 <i>exige</i> qu'une mention de douleur renvoie vers un professionnel de santé. Les
    /// bannir rejetterait « arrête-toi à la moindre douleur et consulte un professionnel de
    /// santé » — la plus sûre des réponses, et celle que le prompt réclame. Ce qui est interdit
    /// est l'injonction à passer outre : voir <see cref="InjonctionAPasserOutre"/>.</para>
    /// </summary>
    private static readonly Regex VocabulaireInterdit = new(
        @"\b(?:tendinite|entorse|claquage|dechirure|fracture"
        + @"|diagnostic|symptome|pathologie|inflammation|anti-?inflammatoire|ordonnance"
        + @"|posologie|dose|medicament|traitement|therapie|regime"
        + @"|honte|paresse|paresseux|paresseuse|faineant|mediocre|lamentable|indigne"
        + @"|decevant|decu|echec|echoue|echouee)s?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// L'injonction à s'entraîner malgré un signal du corps — le vrai danger de la règle n°5,
    /// là où le mot « douleur » seul n'en est pas un.
    /// </summary>
    private static readonly Regex InjonctionAPasserOutre = new(
        @"\b(?:malgre|surmonte|surmonter|surmontant|ignore|ignorer|ignorant|depasse|depasser"
        + @"|encaisse|encaisser|endure|endurer|passer?\s+outre|sans\s+ecouter"
        + @"|sans\s+tenir\s+compte(?:\s+de)?)"
        + @"\s+(?:l[ae]|ta|ton|tes|ce|cette|toute|la\s+moindre)?\s*"
        + @"(?:douleurs?|souffrances?|blessures?|genes?|inconforts?|signaux?|signal)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Mots-outils français, cherchés dans la description générée. Tous les autres garde-fous
    /// de ce fichier sont des lexiques français : une réponse en anglais les franchit
    /// intégralement — « You failed yesterday, hunter. Push through the pain. » est à la fois
    /// une culpabilisation et une injonction à ignorer la douleur — et s'afficherait telle
    /// quelle au Chasseur, en violation de la règle n°7 par-dessus le marché.
    ///
    /// <para>Une description d'une à deux phrases en porte forcément au moins un ; c'est ce qui
    /// rend l'heuristique fiable là où elle ne le serait pas sur un titre nominal
    /// (« Éveil du Corps », « Ascension »).</para>
    /// </summary>
    private static readonly Regex MotsOutilsFrancais = new(
        @"\b(?:le|la|les|un|une|des|du|de|ton|ta|tes|et|ce|cette|qui|que|pour|avec|sans|dans"
        + @"|sur|ne|pas|plus|tu|toi|chaque|jusqu)\b|à",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Le pendant du contrôle précédent pour le titre, trop court pour porter un mot-outil :
    /// ici on ne cherche pas du français, on refuse ce qui est visiblement anglais.
    /// </summary>
    private static readonly Regex MarqueurAnglais = new(
        @"\b(?:you|your|the|and|with|through|push|keep|today|yesterday|dont|don't|reps"
        + @"|workout|hunter|no\s+excuses)\b",
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
        var adresse = $"v1beta/models/{config.Model}:generateContent?key={config.ApiKey}";

        HttpResponseMessage reponseHttp;
        try
        {
            reponseHttp = await httpClient.PostAsync(
                adresse, ConstruireRequete(request, reproche), cancellationToken);
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

        if (!reponseHttp.IsSuccessStatusCode)
        {
            logger.LogWarning("Réponse HTTP {StatutCode} du Système (quête).", reponseHttp.StatusCode);
            return Tentative.Panne;
        }

        var corpsEnveloppe = await reponseHttp.Content.ReadAsStringAsync(cancellationToken);

        return Valider(corpsEnveloppe);
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
            charge = JsonSerializer.Deserialize<QuetePayload>(texteGenere, OptionsJson);
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
        if (!MotsOutilsFrancais.IsMatch(description))
        {
            logger.LogWarning("Description de quête rejetée : elle ne semble pas être en français.");
            return Tentative.Rejet("la quête doit être écrite en français, au tutoiement");
        }

        return Tentative.Reussite(new QuestGenerationAgentResult(
            titre, description, type, statCible, difficulte, charge.XpReward, EstRepli: false));
    }

    /// <summary>
    /// Le motif de rejet du texte, ou <see langword="null"/> s'il passe les garde-fous du
    /// sport (règle non négociable n°5).
    /// </summary>
    private static string? ViolationDesGardeFous(string texte)
    {
        if (MarqueurAnglais.IsMatch(texte))
        {
            return "la quête doit être écrite en français, au tutoiement";
        }

        if (PrescriptionChiffree.IsMatch(texte))
        {
            return "une quête ne prescrit ni charge, ni allure, ni calories, ni pourcentage d'effort";
        }

        var normalise = SansAccents(texte);

        if (InjonctionAPasserOutre.IsMatch(normalise))
        {
            return "une quête n'invite jamais à s'entraîner malgré une douleur : elle invite au "
                + "contraire à s'arrêter et à consulter un professionnel de santé";
        }

        if (VocabulaireInterdit.IsMatch(normalise))
        {
            return "une quête ne pose aucun diagnostic, ne prescrit aucun traitement et ne "
                + "formule jamais de reproche";
        }

        return null;
    }

    /// <summary>
    /// Débarrasse le texte de ses accents pour que la recherche de vocabulaire interdit ne se
    /// laisse pas contourner par un mot mal accentué.
    /// </summary>
    private static string SansAccents(string texte) =>
        new(texte
            .Normalize(NormalizationForm.FormD)
            .Where(caractere =>
                CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            .ToArray());

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
