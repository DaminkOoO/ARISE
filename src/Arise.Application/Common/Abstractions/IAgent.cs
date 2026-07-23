namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Un agent du « Système » : il pose une question à Gemini et en rend un résultat métier.
///
/// <para><typeparamref name="TResult"/> est un type du domaine — jamais une
/// <see cref="string"/>, jamais un <c>JsonDocument</c>. Le parsing et la validation
/// appartiennent à l'agent, c'est sa frontière : une réponse brute qui fuirait vers un
/// handler ferait dupliquer la validation à trois endroits, et oublier au quatrième.</para>
///
/// <para><b>Un agent ne lève pas.</b> La sortie d'un modèle n'est pas déterministe, et
/// l'utilisateur ne doit ni voir une erreur brute, ni perdre sa journée sur une réponse
/// malformée. Toute panne — JSON illisible, champ manquant, erreur HTTP, délai dépassé,
/// contenu qui viole un garde-fou produit — se solde par un repli neutre, et la réponse
/// rejetée part au journal pour diagnostic.</para>
///
/// <para>Un contenu bien formé mais interdit se rejette exactement comme un JSON cassé : on
/// ne corrige pas la sortie du modèle, on la remplace. Les garde-fous vivent donc dans cette
/// validation, en C#, et pas seulement dans le prompt — un garde-fou écrit dans le prompt se
/// contourne à la première réponse inattendue.</para>
///
/// <para>Chaque implémentation doit quatre tests, tous contre un faux transport HTTP :
/// réponse valide, JSON malformé, contenu violant un garde-fou, panne réseau. Le troisième
/// est celui qu'on oublie, et c'est celui qui protège l'utilisateur.</para>
/// </summary>
public interface IAgent<in TRequest, TResult>
{
    Task<TResult> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}
