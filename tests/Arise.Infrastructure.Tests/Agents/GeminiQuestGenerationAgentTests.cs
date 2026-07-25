using System.Net;
using Arise.Application.Features.Sport;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using Arise.Infrastructure.Agents;
using Arise.Infrastructure.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arise.Infrastructure.Tests.Agents;

/// <summary>
/// L'agent qui écrit la quête de sport du jour. Aucun test ici ne joint l'API Gemini réelle —
/// tout passe par <see cref="FauxHttpMessageHandler"/> (règle non négociable n°4).
///
/// <para>Le troisième test minimum — une réponse bien formée mais qui viole un garde-fou
/// produit — est ici le plus fourni, et c'est voulu : c'est celui qui protège le Chasseur.
/// Une quête qui prescrit une charge, qui diagnostique une blessure ou qui culpabilise est
/// rejetée exactement comme un JSON cassé.</para>
/// </summary>
public class GeminiQuestGenerationAgentTests
{
    private static readonly QuestGenerationAgentRequest Requete = new(1, HunterRank.E, 0);

    private const string DescriptionSaine = "Bouge à ton rythme : marche, gainage, étirements.";

    private static GeminiOptions GeminiOptionsDeTest() => new()
    {
        ApiKey = "clef-de-test",
        Model = "gemini-2.0-flash",
    };

    private static GeminiQuestGenerationAgent Agent(FauxHttpMessageHandler transport) =>
        new(
            transport.Client(),
            Options.Create(GeminiOptionsDeTest()),
            NullLogger<GeminiQuestGenerationAgent>.Instance);

    private static string EnveloppeGemini(string texteGenere)
    {
        // Forme réelle de la réponse Gemini : le JSON qu'on valide est lui-même encodé en
        // texte à l'intérieur de candidates[0].content.parts[0].text.
        // Les retours à la ligne comptent : un saut brut dans une chaîne JSON la rend
        // invalide, et l'agent se replierait pour une raison qui n'est pas celle qu'on éprouve.
        var texteEchappe = texteGenere
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return """{"candidates":[{"content":{"parts":[{"text":"MARQUEUR"}]}}]}"""
            .Replace("MARQUEUR", texteEchappe);
    }

    private static string Charge(
        string titre = "L'Épreuve du Guerrier",
        string description = DescriptionSaine,
        string type = "daily",
        string statCible = "FOR",
        string difficulte = "medium",
        int xp = 20) =>
        EnveloppeGemini(
            $$"""
            {"title":"{{titre}}","description":"{{description}}","type":"{{type}}",
             "stat_target":"{{statCible}}","difficulty":"{{difficulte}}","xp_reward":{{xp}}}
            """);

    private static Task<QuestGenerationAgentResult> Generer(FauxHttpMessageHandler transport) =>
        Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

    // ---------------------------------------------------------------------------------------
    // 1. Réponse valide → le TResult attendu.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Rend_le_titre_genere_quand_la_reponse_est_valide()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge()));

        resultat.Title.Should().Be("L'Épreuve du Guerrier");
    }

    [Fact]
    public async Task Rend_la_description_generee_quand_la_reponse_est_valide()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge()));

        resultat.Description.Should().Be(DescriptionSaine);
    }

    [Theory]
    [InlineData("daily", QuestType.Quotidienne)]
    [InlineData("penalty", QuestType.Penalite)]
    public async Task Traduit_le_type_du_contrat_JSON(string jeton, QuestType attendu)
    {
        // Une quête de pénalité vaut 10 XP fixes : le montant suit le type, sans quoi le
        // barème rejetterait la réponse pour une autre raison que celle qu'on éprouve.
        var xp = jeton == "penalty" ? 10 : 20;

        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(type: jeton, xp: xp)));

        resultat.Type.Should().Be(attendu);
    }

    [Theory]
    [InlineData("FOR", QuestStat.Force)]
    [InlineData("VIT", QuestStat.Vitesse)]
    [InlineData("INT", QuestStat.Intelligence)]
    [InlineData("OR", QuestStat.Or)]
    [InlineData("PER", QuestStat.Perception)]
    public async Task Traduit_la_statistique_visee_du_contrat_JSON(string jeton, QuestStat attendue)
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(statCible: jeton)));

        resultat.StatTarget.Should().Be(attendue);
    }

    [Theory]
    [InlineData("easy", QuestDifficulty.Facile, 12)]
    [InlineData("medium", QuestDifficulty.Moyenne, 20)]
    [InlineData("hard", QuestDifficulty.Difficile, 30)]
    public async Task Traduit_la_difficulte_du_contrat_JSON(
        string jeton, QuestDifficulty attendue, int xp)
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(Charge(difficulte: jeton, xp: xp)));

        resultat.Difficulty.Should().Be(attendue);
    }

    [Fact]
    public async Task Rend_la_recompense_annoncee_quand_elle_est_coherente()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(xp: 25)));

        resultat.XpReward.Should().Be(25);
    }

    [Fact]
    public async Task N_est_pas_marque_comme_repli_quand_la_reponse_est_valide()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge()));

        resultat.EstRepli.Should().BeFalse();
    }

    // Les jetons du contrat sont écrits en minuscules et en majuscules dans la documentation
    // de Gemini ; un modèle qui renvoie « DAILY » ne dit rien de faux.
    [Fact]
    public async Task Accepte_les_jetons_du_contrat_quelle_qu_en_soit_la_casse()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(Charge(type: "DAILY", statCible: "for", difficulte: "Medium")));

        resultat.EstRepli.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // 2. JSON malformé → repli, pas d'exception qui remonte.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Se_replie_quand_l_enveloppe_n_est_pas_du_JSON()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond("ceci n'est pas du JSON"));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_le_texte_genere_n_est_pas_du_JSON()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(EnveloppeGemini("ceci n'est pas du JSON non plus")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_l_enveloppe_ne_porte_aucun_candidat()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond("""{"candidates":[]}"""));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_un_champ_du_contrat_manque()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(
            EnveloppeGemini("""{"title":"L'Épreuve du Guerrier","xp_reward":20}""")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_la_recompense_n_est_pas_un_entier()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(
            EnveloppeGemini(
                """
                {"title":"L'Épreuve","description":"Marche à ton rythme.","type":"daily",
                 "stat_target":"FOR","difficulty":"medium","xp_reward":"beaucoup"}
                """)));

        resultat.EstRepli.Should().BeTrue();
    }

    [Theory]
    [InlineData("weekly")]
    [InlineData("boss")]
    public async Task Se_replie_sur_un_type_hors_du_contrat(string type)
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(type: type)));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_une_statistique_hors_du_contrat()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(statCible: "CHANCE")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_une_difficulte_hors_du_contrat()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(difficulte: "extreme")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_titre_vide()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(titre: "   ")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_une_description_vide()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(description: "")));

        resultat.EstRepli.Should().BeTrue();
    }

    // Les bornes sont celles des colonnes : une quête plus longue serait acceptée ici puis
    // refusée par PostgreSQL, loin du site fautif.
    [Fact]
    public async Task Se_replie_sur_un_titre_plus_long_que_sa_colonne()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(Charge(titre: new string('a', Quest.LongueurMaximaleTitre + 1))));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_une_description_plus_longue_que_sa_colonne()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(
            Charge(description: new string('a', Quest.LongueurMaximaleDescription + 1))));

        resultat.EstRepli.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------
    // 3. JSON valide mais violant un garde-fou → rejeté, repli.
    // ---------------------------------------------------------------------------------------

    // Le barème d'XP est un garde-fou d'équilibrage : sans lui, le modèle décide seul de la
    // vitesse de progression du Chasseur.
    [Theory]
    [InlineData("easy", 40)]
    [InlineData("easy", 5)]
    [InlineData("medium", 60)]
    [InlineData("hard", 100)]
    public async Task Se_replie_quand_la_recompense_sort_de_la_fourchette_de_sa_difficulte(
        string difficulte, int xp)
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(Charge(difficulte: difficulte, xp: xp)));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_une_quete_de_penalite_ne_vaut_pas_dix_XP()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(Charge(type: "penalty", difficulte: "easy", xp: 15)));

        resultat.EstRepli.Should().BeTrue();
    }

    // Règle non négociable n°5 — aucune prescription numérique : ni charge, ni allure, ni
    // calories. Le Système propose un défi de jeu, jamais un dosage.
    [Theory]
    [InlineData("4 séries de 12 répétitions à 80 kg au développé couché.")]
    [InlineData("Squats à 60 kilos, trois séries.")]
    [InlineData("Cours 10 km à 4 min/km sans ralentir.")]
    [InlineData("Brûle 500 kcal aujourd'hui.")]
    [InlineData("Vise 800 calories sur ta séance.")]
    [InlineData("Travaille à 85% de ta fréquence cardiaque maximale.")]
    [InlineData("Trois séries à 90% de ton 1RM.")]
    public async Task Se_replie_sur_une_prescription_chiffree(string description)
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(description: description)));

        resultat.EstRepli.Should().BeTrue();
    }

    // Règle n°5 — aucun diagnostic de blessure, aucune interprétation de symptôme.
    [Theory]
    [InlineData("Ta gêne au genou est une tendinite : étire-la.")]
    [InlineData("Cette douleur au dos vient d'une déchirure musculaire.")]
    [InlineData("Ton entorse est guérie, reprends la course.")]
    [InlineData("Prends une dose d'anti-inflammatoire avant l'effort.")]
    public async Task Se_replie_sur_un_diagnostic_ou_une_consigne_medicale(string description)
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(description: description)));

        resultat.EstRepli.Should().BeTrue();
    }

    // Règle n°5 — ce qui est interdit, c'est l'injonction à passer outre, pas le mot
    // « douleur » lui-même.
    [Theory]
    [InlineData("Poursuis la séance malgré la douleur, Chasseur.")]
    [InlineData("Ignore la douleur et termine ta série.")]
    [InlineData("Surmonte ta douleur, Chasseur.")]
    [InlineData("Passe outre la douleur jusqu'au bout.")]
    [InlineData("Termine ta séance sans écouter la douleur.")]
    [InlineData("Avance malgré ta blessure.")]
    public async Task Se_replie_sur_une_injonction_a_passer_outre_la_douleur(string description)
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(description: description)));

        resultat.EstRepli.Should().BeTrue();
    }

    // Le pendant du test précédent, et le plus important des deux : la mention protectrice est
    // exactement ce que la règle n°5 exige et ce que le prompt réclame. Un garde-fou qui vise
    // le mot plutôt que l'injonction rejetterait la plus sûre des réponses et servirait le
    // repli tous les jours.
    [Fact]
    public async Task Accepte_une_mention_protectrice_de_la_douleur()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(
            description: "Écoute ton corps : arrête-toi à la moindre douleur et consulte un "
                + "professionnel de santé.")));

        resultat.EstRepli.Should().BeFalse();
    }

    // Règle n°7, et bien plus qu'elle : les deux lexiques de garde-fous sont français. Une
    // réponse en anglais les franchit tous — « Push through the pain » est à la fois une
    // culpabilisation et une injonction à ignorer la douleur, et elle s'afficherait telle
    // quelle au Chasseur. La langue ne peut donc pas n'être garantie que par le prompt.
    [Theory]
    [InlineData("You failed yesterday, hunter. Push through the pain.")]
    [InlineData("Do 40 push-ups and hold a plank. No excuses.")]
    public async Task Se_replie_sur_une_description_qui_n_est_pas_en_francais(string description)
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(description: description)));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_titre_en_anglais()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(Charge(titre: "Push Through The Pain")));

        resultat.EstRepli.Should().BeTrue();
    }

    // Règle n°5 — aucune quête n'est présentée de façon culpabilisante.
    [Theory]
    [InlineData("Tu as échoué hier : rattrape-toi ou reste médiocre.")]
    [InlineData("Quelle honte d'avoir sauté ta séance.")]
    [InlineData("Arrête d'être paresseux, Chasseur.")]
    public async Task Se_replie_sur_une_formulation_culpabilisante(string description)
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(Charge(description: description)));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_titre_qui_viole_un_garde_fou()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(Charge(titre: "Développé couché à 100 kg")));

        resultat.EstRepli.Should().BeTrue();
    }

    // Le garde-fou ne doit pas déborder : un défi au poids du corps chiffré en répétitions et
    // en minutes reste une quête de jeu, et c'est l'exemple validé du document de référence.
    // Un filtre qui le rejetterait renverrait le repli tous les jours.
    [Fact]
    public async Task Accepte_un_defi_au_poids_du_corps_chiffre_en_repetitions()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(
            Charge(description: "40 pompes, 3 séries de squats, 5 minutes de gainage.")));

        resultat.EstRepli.Should().BeFalse();
    }

    [Fact]
    public async Task Accepte_une_marche_chiffree_en_minutes()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(
            Charge(description: "Marche 30 minutes à ton rythme, Chasseur.")));

        resultat.EstRepli.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // 4. Erreur HTTP ou délai dépassé → repli, pas d'exception qui remonte.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Se_replie_sur_une_panne_reseau()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Tombe(() => new HttpRequestException("réseau injoignable")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_delai_depasse()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Tombe(() => new TaskCanceledException("délai dépassé")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_code_de_statut_HTTP_d_echec()
    {
        var resultat = await Generer(
            FauxHttpMessageHandler.Repond(Charge(), HttpStatusCode.ServiceUnavailable));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Ne_leve_aucune_exception_sur_une_panne_reseau()
    {
        var transport = FauxHttpMessageHandler.Tombe(() => new HttpRequestException("réseau injoignable"));

        var acte = () => Generer(transport);

        await acte.Should().NotThrowAsync();
    }

    // Le Système est en panne, pas incohérent : réessayer ne changerait rien et ferait
    // attendre le Chasseur deux fois plus longtemps.
    [Fact]
    public async Task Ne_reessaie_pas_apres_une_panne_reseau()
    {
        var transport = FauxHttpMessageHandler.Tombe(() => new HttpRequestException("réseau injoignable"));

        await Generer(transport);

        transport.Requetes.Should().ContainSingle();
    }

    // ---------------------------------------------------------------------------------------
    // Une seule nouvelle tentative, puis repli.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Reessaie_une_fois_quand_la_premiere_reponse_est_rejetee()
    {
        var transport = FauxHttpMessageHandler.RepondSuccessivement(
            Charge(difficulte: "easy", xp: 40), Charge());

        var resultat = await Generer(transport);

        resultat.Title.Should().Be("L'Épreuve du Guerrier");
        resultat.EstRepli.Should().BeFalse();
    }

    [Fact]
    public async Task Rappelle_le_contrat_au_Systeme_lors_de_la_nouvelle_tentative()
    {
        var transport = FauxHttpMessageHandler.RepondSuccessivement(
            Charge(difficulte: "easy", xp: 40), Charge());

        await Generer(transport);

        transport.Requetes.Should().HaveCount(2);
        transport.Requetes[1].Corps.Should().Contain("précédente");
    }

    [Fact]
    public async Task Ne_reessaie_qu_une_seule_fois_avant_de_se_replier()
    {
        var transport = FauxHttpMessageHandler.Repond(Charge(difficulte: "easy", xp: 40));

        var resultat = await Generer(transport);

        transport.Requetes.Should().HaveCount(2);
        resultat.EstRepli.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------
    // Le repli : du texte utilisateur, pas un code d'erreur déguisé.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Le_repli_ne_contient_pas_le_texte_brut_rejete()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond(
            Charge(description: "4 séries de 12 répétitions à 80 kg au développé couché.")));

        resultat.Description.Should().NotContain("80 kg");
    }

    [Fact]
    public async Task Le_repli_propose_une_quete_dont_le_texte_n_est_pas_vide()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond("ceci n'est pas du JSON"));

        resultat.Title.Should().NotBeNullOrWhiteSpace();
        resultat.Description.Should().NotBeNullOrWhiteSpace();
    }

    // Règle n°5 : toute mention de douleur renvoie vers un professionnel de santé. Le repli
    // est le seul texte que le Chasseur lira ce jour-là ; c'est donc lui qui doit porter ce
    // renvoi, pas le prompt d'un modèle qu'on n'a pas su joindre.
    [Fact]
    public async Task Le_repli_renvoie_vers_un_professionnel_de_sante()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond("ceci n'est pas du JSON"));

        resultat.Description.Should().Contain("professionnel de santé");
    }

    [Fact]
    public async Task Le_repli_est_une_quete_facile_a_dix_XP()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond("ceci n'est pas du JSON"));

        resultat.Difficulty.Should().Be(QuestDifficulty.Facile);
        resultat.XpReward.Should().Be(10);
    }

    [Fact]
    public async Task Le_repli_reste_une_quete_quotidienne()
    {
        var resultat = await Generer(FauxHttpMessageHandler.Repond("ceci n'est pas du JSON"));

        resultat.Type.Should().Be(QuestType.Quotidienne);
    }

    // Le repli doit passer les contrôles qu'il applique aux autres : s'il était lui-même
    // rejetable, c'est que le filtre ou le texte de secours serait à revoir.
    [Fact]
    public async Task Le_repli_franchirait_lui_meme_les_garde_fous_du_sport()
    {
        var repli = await Generer(FauxHttpMessageHandler.Repond("ceci n'est pas du JSON"));

        var relu = await Generer(FauxHttpMessageHandler.Repond(
            Charge(titre: repli.Title, description: repli.Description, difficulte: "easy", xp: 10)));

        relu.EstRepli.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // Ce qui part vers le Système.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Transmet_le_contexte_du_Chasseur_au_Systeme()
    {
        var transport = FauxHttpMessageHandler.Repond(Charge());

        await Agent(transport).ExecuteAsync(
            new QuestGenerationAgentRequest(7, HunterRank.D, 4), CancellationToken.None);

        var corps = transport.Requetes.Should().ContainSingle().Which.Corps;
        corps.Should().Contain("niveau 7").And.Contain("rang D").And.Contain("série de 4 jours");
    }

    // Le garde-fou vit en C#, mais le prompt doit le porter aussi : mieux vaut une réponse
    // conforme du premier coup qu'un repli servi au Chasseur.
    [Fact]
    public async Task Interdit_explicitement_les_prescriptions_chiffrees_dans_le_prompt()
    {
        var transport = FauxHttpMessageHandler.Repond(Charge());

        await Generer(transport);

        var corps = transport.Requetes.Should().ContainSingle().Which.Corps;
        corps.Should().Contain("charge").And.Contain("diagnostic");
    }

    // Le renvoi vers un professionnel de santé ne peut pas n'exister que dans le repli : il n'y
    // vivrait alors que les jours de panne. Le prompt doit le réclamer, et le garde-fou doit
    // le laisser passer.
    [Fact]
    public async Task Demande_au_Systeme_de_renvoyer_vers_un_professionnel_de_sante()
    {
        var transport = FauxHttpMessageHandler.Repond(Charge());

        await Generer(transport);

        transport.Requetes.Should().ContainSingle().Which.Corps
            .Should().Contain("professionnel de santé");
    }

    [Fact]
    public async Task Demande_une_quete_en_francais_au_tutoiement()
    {
        var transport = FauxHttpMessageHandler.Repond(Charge());

        await Generer(transport);

        transport.Requetes.Should().ContainSingle().Which.Corps
            .Should().Contain("français").And.Contain("tutoiement");
    }

    [Fact]
    public async Task Appelle_le_modele_et_la_clef_configures()
    {
        var transport = FauxHttpMessageHandler.Repond(Charge());

        await Generer(transport);

        var requete = transport.Requetes.Should().ContainSingle().Which;
        requete.Uri!.AbsolutePath.Should().Contain("gemini-2.0-flash");
        requete.Uri.Query.Should().Contain("clef-de-test");
    }
}
