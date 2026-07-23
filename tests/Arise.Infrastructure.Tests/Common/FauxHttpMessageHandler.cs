using System.Net;
using System.Text;

namespace Arise.Infrastructure.Tests.Common;

/// <summary>
/// Transport HTTP factice : il rend la réponse qu'on lui a dictée et retient ce qu'on lui a
/// envoyé. C'est ce qui permet d'éprouver un agent sans jamais joindre l'API Gemini réelle
/// (règle non négociable n°4) — un appel réel rendrait la suite lente, non déterministe,
/// payante, et rouge au premier hoquet du réseau.
///
/// <para>Deux modes, choisis à la construction : répondre (<see cref="Repond"/>) ou tomber
/// en panne (<see cref="Tombe"/>). Le second couvre le quatrième test minimum de tout agent
/// — erreur réseau ou délai dépassé → repli, sans exception qui remonte à l'appelant.</para>
/// </summary>
internal sealed class FauxHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _code;
    private readonly string _corps;
    private readonly Func<Exception>? _panne;
    private readonly List<RequeteCapturee> _requetes = [];
    private readonly Lock _verrou = new();

    private FauxHttpMessageHandler(HttpStatusCode code, string corps, Func<Exception>? panne)
    {
        _code = code;
        _corps = corps;
        _panne = panne;
    }

    /// <summary>Répond ce corps, avec ce statut, à chaque appel.</summary>
    public static FauxHttpMessageHandler Repond(
        string corps,
        HttpStatusCode code = HttpStatusCode.OK) => new(code, corps, panne: null);

    /// <summary>
    /// Lève l'exception que rend cette fabrique au lieu de répondre —
    /// <see cref="HttpRequestException"/> pour un réseau injoignable,
    /// <see cref="TaskCanceledException"/> pour un délai dépassé.
    ///
    /// <para>Une fabrique et non une instance : relancer le même objet écrase sa trace de
    /// pile à chaque appel, et un agent qui réessaie verrait deux fois la même exception,
    /// le second échec effaçant le premier.</para>
    /// </summary>
    public static FauxHttpMessageHandler Tombe(Func<Exception> panne) =>
        new(HttpStatusCode.OK, string.Empty, panne);

    /// <summary>
    /// Ce qui a été envoyé, dans l'ordre des appels — y compris l'appel qui a déclenché une
    /// panne : un agent qui n'envoie pas ce qu'il faut doit être pris sur le fait même quand
    /// le réseau le trahit.
    /// </summary>
    public IReadOnlyList<RequeteCapturee> Requetes
    {
        get
        {
            lock (_verrou)
            {
                return _requetes.ToArray();
            }
        }
    }

    /// <summary>
    /// Un client déjà branché sur ce transport.
    ///
    /// <para>L'adresse de base est sous <c>.invalid</c>, réservé par la RFC 2606 : si un test
    /// venait à fuir vers le vrai réseau, il échoue à la résolution du nom plutôt que
    /// d'atteindre un serveur au hasard.</para>
    /// </summary>
    public HttpClient Client() =>
        new(this, disposeHandler: false) { BaseAddress = new Uri("https://gemini.invalid/") };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Le corps se lit maintenant : la requête est disposée dès le retour, et l'agent
        // aura pu y placer un flux qu'on ne pourrait plus relire ensuite.
        //
        // CancellationToken.None, délibérément : passer le jeton ferait lever cette lecture
        // AVANT l'enregistrement dès que le corps n'est pas tamponné (StreamContent), et la
        // requête serait perdue précisément dans le test de délai dépassé que ce transport
        // existe pour couvrir. L'annulation est honorée juste après, une fois la capture
        // faite.
        var corps = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(CancellationToken.None);

        lock (_verrou)
        {
            _requetes.Add(new RequeteCapturee(request.Method, request.RequestUri, corps));
        }

        // Après la capture : le vrai HttpClient part aussi en annulation une fois la requête
        // engagée, et un agent doit être éprouvé sur ce même ordre.
        cancellationToken.ThrowIfCancellationRequested();

        if (_panne is not null)
        {
            throw _panne();
        }

        return new HttpResponseMessage(_code)
        {
            // UTF-8 explicite : les accents du prompt comme de la réponse traversent, toute
            // l'interface étant en français (règle n°7).
            Content = new StringContent(_corps, Encoding.UTF8, "application/json"),
        };
    }
}

internal sealed record RequeteCapturee(HttpMethod Methode, Uri? Uri, string Corps);
