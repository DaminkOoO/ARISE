using System.Net;
using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Hunters;
using Arise.Infrastructure.Agents;
using Arise.Infrastructure.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arise.Infrastructure.Tests.Agents;

/// <summary>
/// Premier agent Gemini concret du dépôt : il pose la convention de validation en trois temps
/// (parse → forme → garde-fous produit) que les agents suivants (quêtes, rapport quotidien)
/// reprendront. Aucun test ici ne joint l'API Gemini réelle — tout passe par
/// <see cref="FauxHttpMessageHandler"/> (règle non négociable n°4).
/// </summary>
public class GeminiOnboardingAgentTests
{
    private static readonly OnboardingAgentRequest Requete = new([HunterGoal.Sport]);

    private static GeminiOptions GeminiOptionsDeTest() => new()
    {
        ApiKey = "clef-de-test",
        Model = "gemini-2.0-flash",
    };

    private static GeminiOnboardingAgent Agent(FauxHttpMessageHandler transport) =>
        new(transport.Client(), Options.Create(GeminiOptionsDeTest()), NullLogger<GeminiOnboardingAgent>.Instance);

    private static string EnveloppeGemini(string texteGenere)
    {
        // Forme réelle de la réponse Gemini : le JSON qu'on valide (awakening_narrative) est
        // lui-même encodé en texte à l'intérieur de candidates[0].content.parts[0].text.
        // Pas d'interpolation de chaîne brute ici : la charge utile porte trop d'accolades
        // consécutives ("}}]}") pour que $$"""...""" les distingue d'un délimiteur.
        var texteEchappe = texteGenere.Replace("\"", "\\\"");
        return """{"candidates":[{"content":{"parts":[{"text":"MARQUEUR"}]}}]}"""
            .Replace("MARQUEUR", texteEchappe);
    }

    // 1. Réponse valide → le TResult attendu.
    [Fact]
    public async Task Rend_la_narration_quand_la_reponse_est_valide()
    {
        const string narration = "Le Système t'a repéré, Chasseur. Ta voie vers le Sport commence maintenant.";
        var transport = FauxHttpMessageHandler.Repond(
            EnveloppeGemini($$"""{"awakening_narrative": "{{narration}}"}"""));

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.AwakeningNarrative.Should().Be(narration);
    }

    [Fact]
    public async Task N_est_pas_marque_comme_repli_quand_la_reponse_est_valide()
    {
        var transport = FauxHttpMessageHandler.Repond(
            EnveloppeGemini("""{"awakening_narrative": "Le Système t'a repéré, Chasseur."}"""));

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeFalse();
    }

    // 2. JSON malformé → repli, pas d'exception qui remonte.
    [Fact]
    public async Task Se_replie_quand_l_enveloppe_n_est_pas_du_JSON()
    {
        var transport = FauxHttpMessageHandler.Repond("ceci n'est pas du JSON");

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_le_texte_genere_n_est_pas_du_JSON()
    {
        var transport = FauxHttpMessageHandler.Repond(EnveloppeGemini("ceci n'est pas du JSON non plus"));

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_l_enveloppe_ne_porte_aucun_candidat()
    {
        var transport = FauxHttpMessageHandler.Repond("""{"candidates":[]}""");

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    // Le repli ne renvoie jamais le texte brut du modèle : celui-ci n'a passé aucun contrôle.
    [Fact]
    public async Task Le_repli_ne_contient_pas_le_texte_brut_rejete()
    {
        var transport = FauxHttpMessageHandler.Repond("ceci n'est pas du JSON");

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.AwakeningNarrative.Should().NotContain("ceci n'est pas du JSON");
    }

    [Fact]
    public async Task Le_repli_est_un_texte_non_vide_en_francais()
    {
        var transport = FauxHttpMessageHandler.Repond("ceci n'est pas du JSON");

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.AwakeningNarrative.Should().NotBeNullOrWhiteSpace();
    }

    // 3. JSON valide mais violant un garde-fou (ici : narration vide ou démesurée) → rejeté, repli.
    [Fact]
    public async Task Se_replie_quand_la_narration_generee_est_vide()
    {
        var transport = FauxHttpMessageHandler.Repond(
            EnveloppeGemini("""{"awakening_narrative": ""}"""));

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_la_narration_generee_ne_contient_que_des_espaces()
    {
        var transport = FauxHttpMessageHandler.Repond(
            EnveloppeGemini("""{"awakening_narrative": "   "}"""));

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_quand_la_narration_generee_depasse_la_longueur_raisonnable()
    {
        var narrationDemesuree = new string('a', 481);
        var transport = FauxHttpMessageHandler.Repond(
            EnveloppeGemini($$"""{"awakening_narrative": "{{narrationDemesuree}}"}"""));

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    // 4. Erreur HTTP ou délai dépassé → repli, pas d'exception qui remonte.
    [Fact]
    public async Task Se_replie_sur_une_panne_reseau()
    {
        var transport = FauxHttpMessageHandler.Tombe(() => new HttpRequestException("réseau injoignable"));

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_delai_depasse()
    {
        var transport = FauxHttpMessageHandler.Tombe(() => new TaskCanceledException("délai dépassé"));

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Se_replie_sur_un_code_de_statut_HTTP_d_echec()
    {
        var transport = FauxHttpMessageHandler.Repond(
            EnveloppeGemini("""{"awakening_narrative": "Peu importe, ce corps n'est pas lu."}"""),
            HttpStatusCode.ServiceUnavailable);

        var resultat = await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        resultat.EstRepli.Should().BeTrue();
    }

    [Fact]
    public async Task Ne_leve_aucune_exception_sur_une_panne_reseau()
    {
        var transport = FauxHttpMessageHandler.Tombe(() => new HttpRequestException("réseau injoignable"));

        var acte = () => Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        await acte.Should().NotThrowAsync();
    }

    // La demande envoyée au Système doit refléter les objectifs déclarés — sans quoi la
    // narration ne peut pas être personnalisée comme le contrat de sortie l'exige.
    [Fact]
    public async Task Envoie_une_requete_qui_mentionne_les_objectifs_declares()
    {
        var transport = FauxHttpMessageHandler.Repond(
            EnveloppeGemini("""{"awakening_narrative": "Le Système t'a repéré."}"""));

        await Agent(transport).ExecuteAsync(new OnboardingAgentRequest([HunterGoal.Budget]), CancellationToken.None);

        transport.Requetes.Should().ContainSingle().Which.Corps.Should().Contain("Budget");
    }

    [Fact]
    public async Task Appelle_le_modele_et_la_clef_configures()
    {
        var transport = FauxHttpMessageHandler.Repond(
            EnveloppeGemini("""{"awakening_narrative": "Le Système t'a repéré."}"""));

        await Agent(transport).ExecuteAsync(Requete, CancellationToken.None);

        var requete = transport.Requetes.Should().ContainSingle().Which;
        requete.Uri!.AbsolutePath.Should().Contain("gemini-2.0-flash");
        requete.Uri.Query.Should().Contain("clef-de-test");
    }
}
