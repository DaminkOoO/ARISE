using System.Net;
using Arise.Application.Features.Habits;
using Arise.Domain.Habits;
using Arise.Domain.Hunters;
using Arise.Infrastructure.Agents;
using Arise.Infrastructure.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arise.Infrastructure.Tests.Agents;

/// <summary>
/// L'agent qui propose des habitudes au Chasseur. Aucun test ici ne joint l'API Gemini réelle —
/// tout passe par <see cref="FauxHttpMessageHandler"/> (règle non négociable n°4).
///
/// <para>Le troisième test minimum — une réponse bien formée mais qui viole un garde-fou produit
/// — est ici le plus fourni, et c'est voulu : une habitude suggérée est quelque chose que le
/// Chasseur va répéter tous les jours. « Jeûner seize heures » ou « Courir dix kilomètres » sont
/// des prescriptions, pas des intentions de jeu.</para>
/// </summary>
public class GeminiHabitSuggestionAgentTests
{
    private static readonly HabitSuggestionAgentRequest Requete = new(1, HunterRank.E, []);

    private static GeminiOptions GeminiOptionsDeTest() => new()
    {
        ApiKey = "clef-de-test",
        Model = "gemini-2.0-flash",
    };

    private static GeminiHabitSuggestionAgent Agent(FauxHttpMessageHandler transport) =>
        new(
            transport.Client(),
            Options.Create(GeminiOptionsDeTest()),
            NullLogger<GeminiHabitSuggestionAgent>.Instance);

    private static string EnveloppeGemini(string texteGenere)
    {
        var texteEchappe = texteGenere
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return """{"candidates":[{"content":{"parts":[{"text":"MARQUEUR"}]}}]}"""
            .Replace("MARQUEUR", texteEchappe);
    }

    private static string ChargeJson(params string[] habitudesJson) =>
        $$"""{"habits":[{{string.Join(",", habitudesJson)}}]}""";

    private static string Habitude(
        string nom = "Boire un grand verre d'eau au réveil", string rythme = "daily") =>
        $$"""{"name":"{{nom}}","frequency":"{{rythme}}"}""";

    private static string Charge(params string[] habitudesJson) =>
        EnveloppeGemini(ChargeJson(habitudesJson.Length == 0 ? [Habitude()] : habitudesJson));

    private static Task<HabitSuggestionAgentResult> Suggerer(FauxHttpMessageHandler transport) =>
        Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

    private static async Task<HabitSuggestionAgentResult> SuggererDepuis(string texteGenere) =>
        await Suggerer(FauxHttpMessageHandler.Repond(EnveloppeGemini(texteGenere)));

    // ---------------------------------------------------------------------------------------
    // 1. Réponse valide → le TResult attendu.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Rend_le_nom_suggere_quand_la_reponse_est_valide()
    {
        var resultat = await Suggerer(FauxHttpMessageHandler.Repond(Charge()));

        resultat.Suggestions.Should().ContainSingle()
            .Which.Name.Should().Be("Boire un grand verre d'eau au réveil");
    }

    [Theory]
    [InlineData("daily", HabitFrequency.Quotidienne)]
    [InlineData("weekly", HabitFrequency.Hebdomadaire)]
    public async Task Traduit_le_rythme_du_contrat_JSON(string jeton, HabitFrequency attendu)
    {
        var resultat = await Suggerer(
            FauxHttpMessageHandler.Repond(Charge(Habitude(rythme: jeton))));

        resultat.Suggestions.Should().ContainSingle().Which.Frequency.Should().Be(attendu);
    }

    [Fact]
    public async Task Rend_toutes_les_habitudes_suggerees()
    {
        var resultat = await Suggerer(FauxHttpMessageHandler.Repond(Charge(
            Habitude(nom: "Lire vingt minutes"),
            Habitude(nom: "Ranger le bureau", rythme: "weekly"))));

        resultat.Suggestions.Should().HaveCount(2);
    }

    [Fact]
    public async Task N_annonce_pas_un_repli_quand_la_reponse_est_valide()
    {
        (await Suggerer(FauxHttpMessageHandler.Repond(Charge()))).EstRepli.Should().BeFalse();
    }

    [Fact]
    public async Task Rogne_les_espaces_de_bordure_du_nom_suggere()
    {
        var resultat = await Suggerer(
            FauxHttpMessageHandler.Repond(Charge(Habitude(nom: "  Lire vingt minutes  "))));

        resultat.Suggestions.Should().ContainSingle()
            .Which.Name.Should().Be("Lire vingt minutes");
    }

    // La clé voyage en en-tête, jamais en query string : AddHttpClient trace l'URI complète en
    // niveau Information, et un « ?key=... » finirait en clair dans les journaux.
    [Fact]
    public async Task Envoie_la_clef_d_API_en_en_tete()
    {
        var transport = FauxHttpMessageHandler.Repond(Charge());

        await Suggerer(transport);

        transport.Requetes.Should().ContainSingle()
            .Which.EnTete("x-goog-api-key").Should().Be("clef-de-test");
    }

    // Sans cela le Système reproposerait ce que le Chasseur suit déjà.
    [Fact]
    public async Task Transmet_au_Systeme_les_habitudes_deja_suivies()
    {
        var transport = FauxHttpMessageHandler.Repond(Charge());

        await Agent(transport).ExecuteAsync(
            new HabitSuggestionAgentRequest(1, HunterRank.E, ["Courir le matin"]),
            CancellationToken.None);

        transport.Requetes.Should().ContainSingle()
            .Which.Corps.Should().Contain("Courir le matin");
    }

    // ---------------------------------------------------------------------------------------
    // 2. JSON malformé → repli, pas d'exception qui remonte.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Se_replie_sur_une_enveloppe_illisible()
    {
        var resultat = await Suggerer(FauxHttpMessageHandler.Repond("{ pas du JSON"));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_contenu_genere_illisible()
    {
        (await SuggererDepuis("{ ceci n'est pas du JSON")).EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_une_reponse_sans_candidat()
    {
        var resultat = await Suggerer(FauxHttpMessageHandler.Repond("""{"candidates":[]}"""));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_le_Systeme_ne_propose_aucune_habitude()
    {
        (await SuggererDepuis("""{"habits":[]}""")).EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_rythme_hors_contrat()
    {
        var resultat = await SuggererDepuis(ChargeJson(Habitude(rythme: "monthly")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_nom_vide()
    {
        (await SuggererDepuis(ChargeJson(Habitude(nom: "   ")))).EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_nom_plus_long_que_la_colonne()
    {
        var tropLong = new string('a', Habit.LongueurMaximaleNom + 1);

        (await SuggererDepuis(ChargeJson(Habitude(nom: tropLong)))).EstRepli.Should().BeTrue();
    }

    // Le modèle encadre volontiers sa réponse de barrières de code, même en mode JSON. Le
    // contenu est parfaitement valide : se replier pour trois caractères de décoration servirait
    // au Chasseur une liste générique.
    [Fact]
    public async Task Accepte_une_reponse_encadree_de_barrieres_markdown()
    {
        var resultat = await SuggererDepuis($"```json\n{ChargeJson(Habitude())}\n```");

        resultat.EstRepli.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // 3. JSON valide mais violant un garde-fou → rejeté, repli.
    // ---------------------------------------------------------------------------------------

    // Une habitude est répétée tous les jours : une prescription y est plus dangereuse encore
    // que dans une quête, qui ne vaut qu'un jour.
    [Theory]
    [InlineData("Courir dix kilomètres")]
    [InlineData("Soulever 80 kg au développé couché")]
    [InlineData("Jeûner seize heures par jour")]
    [InlineData("Rester sous 1800 calories")]
    [InlineData("Prendre 30 g de protéines au réveil")]
    [InlineData("Courir à 5:00 au kilomètre")]
    public async Task Se_replie_sur_une_habitude_qui_prescrit(string nom)
    {
        var resultat = await SuggererDepuis(ChargeJson(Habitude(nom: nom)));

        resultat.EstRepli.Should().BeTrue();
    }

    [Theory]
    [InlineData("Soigner ta tendinite chaque soir")]
    [InlineData("Prendre ton traitement anti-inflammatoire")]
    [InlineData("Suivre ton régime")]
    public async Task Se_replie_sur_une_habitude_a_registre_medical(string nom)
    {
        (await SuggererDepuis(ChargeJson(Habitude(nom: nom)))).EstRepli.Should().BeTrue();
    }

    [Theory]
    [InlineData("Arrêter d'être paresseux")]
    [InlineData("Ne plus abandonner comme d'habitude")]
    public async Task Se_replie_sur_une_habitude_culpabilisante(string nom)
    {
        (await SuggererDepuis(ChargeJson(Habitude(nom: nom)))).EstRepli.Should().BeTrue();
    }

    // Règle n°7 : tout ce que le Chasseur lit est en français.
    [Fact]
    public async Task Se_replie_sur_une_habitude_en_anglais()
    {
        var resultat = await SuggererDepuis(ChargeJson(Habitude(nom: "Drink your water today")));

        resultat.EstRepli.Should().BeTrue();
    }

    // Une seule habitude fautive suffit à faire rejeter la réponse : garder les autres
    // reviendrait à publier une liste partiellement validée, et le Chasseur ne saurait pas
    // laquelle a été retirée ni pourquoi.
    [Fact]
    public async Task Se_replie_des_qu_une_seule_habitude_de_la_liste_est_fautive()
    {
        var resultat = await SuggererDepuis(ChargeJson(
            Habitude(nom: "Lire vingt minutes"),
            Habitude(nom: "Courir dix kilomètres")));

        resultat.EstRepli.Should().BeTrue();
    }

    // Le repli est du texte utilisateur, pas un code d'erreur déguisé : il doit lui-même
    // repasser les garde-fous qu'on exige du modèle.
    [Fact]
    public async Task Le_repli_propose_des_habitudes_qui_passent_les_garde_fous()
    {
        var repli = await Suggerer(FauxHttpMessageHandler.Repond("{ pas du JSON"));

        repli.Suggestions.Should().NotBeEmpty();

        foreach (var suggestion in repli.Suggestions)
        {
            suggestion.Name.Length.Should().BeLessThanOrEqualTo(Habit.LongueurMaximaleNom);
            GardeFousDesHabitudes.Violation(suggestion.Name).Should().BeNull();
        }
    }

    // ---------------------------------------------------------------------------------------
    // 4. Erreur HTTP ou timeout → repli, pas d'exception qui remonte.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Se_replie_sur_un_statut_HTTP_d_echec()
    {
        var resultat = await Suggerer(
            FauxHttpMessageHandler.Repond("{}", HttpStatusCode.ServiceUnavailable));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_une_panne_reseau()
    {
        var resultat = await Suggerer(
            FauxHttpMessageHandler.Tombe(() => new HttpRequestException("réseau injoignable")));

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_delai_depasse()
    {
        var resultat = await Suggerer(
            FauxHttpMessageHandler.Tombe(() => new TaskCanceledException("délai dépassé")));

        resultat.EstRepli.Should().BeTrue();
    }

    // Une annulation réellement demandée par l'appelant n'est pas une panne du Système : elle
    // doit se propager, sans quoi un abandon de requête serait maquillé en repli.
    [Fact]
    public async Task Propage_une_annulation_demandee_par_l_appelant()
    {
        using var annulation = new CancellationTokenSource();
        await annulation.CancelAsync();

        var acte = async () => await Agent(FauxHttpMessageHandler.Repond(Charge()))
            .ExecuteAsync(Requete, annulation.Token);

        await acte.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---------------------------------------------------------------------------------------
    // Nouvelle tentative : une seule, et seulement sur un rejet de validation.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Reessaie_une_fois_apres_un_rejet_de_validation()
    {
        var transport = FauxHttpMessageHandler.RepondSuccessivement(
            EnveloppeGemini(ChargeJson(Habitude(nom: "Courir dix kilomètres"))),
            Charge());

        var resultat = await Suggerer(transport);

        resultat.EstRepli.Should().BeFalse();
        transport.Requetes.Should().HaveCount(2);
    }

    // Le Système est indisponible, pas incohérent : faire attendre le Chasseur une seconde fois
    // ne changerait rien.
    [Fact]
    public async Task Ne_reessaie_pas_apres_une_panne_reseau()
    {
        var transport = FauxHttpMessageHandler.Tombe(
            () => new HttpRequestException("réseau injoignable"));

        await Suggerer(transport);

        transport.Requetes.Should().ContainSingle();
    }
}
