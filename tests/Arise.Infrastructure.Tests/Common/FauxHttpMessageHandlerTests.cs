using System.Net;
using System.Text;
using FluentAssertions;

namespace Arise.Infrastructure.Tests.Common;

/// <summary>
/// Le faux transport est l'outil sur lequel se branchera chaque test d'agent : aucun d'eux
/// ne joint l'API Gemini réelle (règle non négociable n°4). Il est éprouvé pour lui-même,
/// parce qu'un doublure qui n'enregistre rien en silence affaiblirait tous les tests bâtis
/// dessus sans jamais rougir.
/// </summary>
public class FauxHttpMessageHandlerTests
{
    private const string CorpsJson = """{"quête":"Marcher 20 minutes"}""";

    [Fact]
    public async Task Rend_le_code_de_statut_configure()
    {
        var transport = FauxHttpMessageHandler.Repond(CorpsJson, HttpStatusCode.ServiceUnavailable);

        var reponse = await transport.Client().GetAsync("/v1/generer");

        reponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Rend_succes_par_defaut()
    {
        var reponse = await FauxHttpMessageHandler.Repond(CorpsJson).Client().GetAsync("/v1/generer");

        reponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rend_le_corps_configure()
    {
        var reponse = await FauxHttpMessageHandler.Repond(CorpsJson).Client().GetAsync("/v1/generer");

        (await reponse.Content.ReadAsStringAsync()).Should().Be(CorpsJson);
    }

    // Un agent qui lit la réponse en JSON s'appuie dessus.
    [Fact]
    public async Task Annonce_du_JSON_encode_en_UTF8()
    {
        var reponse = await FauxHttpMessageHandler.Repond(CorpsJson).Client().GetAsync("/v1/generer");

        reponse.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        reponse.Content.Headers.ContentType.CharSet.Should().Be("utf-8");
    }

    // Les accents du prompt et de la réponse ne doivent pas se perdre en route : toute
    // l'interface est en français (règle n°7).
    [Fact]
    public async Task Preserve_les_accents_du_corps()
    {
        const string avecAccents = """{"quête":"Éveil du Système à l'aube"}""";

        var reponse = await FauxHttpMessageHandler.Repond(avecAccents).Client()
            .GetAsync("/v1/generer");

        (await reponse.Content.ReadAsStringAsync()).Should().Be(avecAccents);
    }

    [Fact]
    public async Task Capture_la_methode_et_l_adresse_appelees()
    {
        var transport = FauxHttpMessageHandler.Repond(CorpsJson);

        await transport.Client().PostAsync("/v1/generer", new StringContent(""));

        var requete = transport.Requetes.Should().ContainSingle().Which;
        requete.Methode.Should().Be(HttpMethod.Post);
        requete.Uri!.AbsolutePath.Should().Be("/v1/generer");
    }

    // C'est cette capture qui permettra d'éprouver le prompt envoyé au Système — sans elle,
    // un agent pourrait envoyer n'importe quoi sans qu'un test s'en aperçoive.
    [Fact]
    public async Task Capture_le_corps_envoye()
    {
        const string prompt = """{"prompt":"Génère une quête de sport pour aujourd'hui."}""";
        var transport = FauxHttpMessageHandler.Repond(CorpsJson);

        await transport.Client().PostAsync(
            "/v1/generer", new StringContent(prompt, Encoding.UTF8, "application/json"));

        transport.Requetes.Should().ContainSingle().Which.Corps.Should().Be(prompt);
    }

    [Fact]
    public async Task Capture_une_requete_sans_corps_comme_chaine_vide()
    {
        var transport = FauxHttpMessageHandler.Repond(CorpsJson);

        await transport.Client().GetAsync("/v1/generer");

        transport.Requetes.Should().ContainSingle().Which.Corps.Should().BeEmpty();
    }

    [Fact]
    public async Task Capture_chaque_appel_dans_l_ordre()
    {
        var transport = FauxHttpMessageHandler.Repond(CorpsJson);
        var client = transport.Client();

        await client.GetAsync("/premier");
        await client.GetAsync("/second");

        transport.Requetes.Select(requete => requete.Uri!.AbsolutePath)
            .Should().Equal("/premier", "/second");
    }

    [Fact]
    public async Task Rejoue_la_meme_reponse_a_chaque_appel()
    {
        var client = FauxHttpMessageHandler.Repond(CorpsJson).Client();

        await client.GetAsync("/premier");
        var seconde = await client.GetAsync("/second");

        (await seconde.Content.ReadAsStringAsync()).Should().Be(CorpsJson);
    }

    // Quatrième test minimum de tout agent : panne réseau → repli, pas d'exception qui
    // remonte. Encore faut-il pouvoir simuler la panne.
    [Fact]
    public async Task Leve_la_panne_configuree_au_lieu_de_repondre()
    {
        var transport = FauxHttpMessageHandler.Tombe(new HttpRequestException("réseau injoignable"));

        var acte = () => transport.Client().GetAsync("/v1/generer");

        (await acte.Should().ThrowAsync<HttpRequestException>())
            .Which.Message.Should().Be("réseau injoignable");
    }

    [Fact]
    public async Task Capture_aussi_la_requete_qui_declenche_une_panne()
    {
        var transport = FauxHttpMessageHandler.Tombe(new HttpRequestException("réseau injoignable"));

        var acte = () => transport.Client().GetAsync("/v1/generer");
        await acte.Should().ThrowAsync<HttpRequestException>();

        transport.Requetes.Should().ContainSingle();
    }

    // L'autre moitié du quatrième test : le délai dépassé, que HttpClient signale par une
    // annulation et non par une HttpRequestException.
    [Fact]
    public async Task Honore_une_annulation_deja_demandee()
    {
        var transport = FauxHttpMessageHandler.Repond(CorpsJson);
        using var annulation = new CancellationTokenSource();
        await annulation.CancelAsync();

        var acte = () => transport.Client().GetAsync("/v1/generer", annulation.Token);

        await acte.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Expose_un_client_dont_l_adresse_de_base_n_est_pas_routable()
    {
        // .invalid est réservé par la RFC 2606 : si un test fuit vers le vrai réseau, il
        // échoue à la résolution plutôt que d'atteindre un serveur au hasard.
        FauxHttpMessageHandler.Repond(CorpsJson).Client().BaseAddress!.Host
            .Should().EndWith(".invalid");
    }
}
