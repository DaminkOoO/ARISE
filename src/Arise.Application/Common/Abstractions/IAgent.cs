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
/// contenu qui viole un garde-fou produit — se solde par un repli, et la réponse rejetée
/// part au journal pour diagnostic.</para>
///
/// <para>Un contenu bien formé mais interdit se rejette exactement comme un JSON cassé : on
/// ne corrige pas la sortie du modèle, on la remplace. Les garde-fous vivent donc dans cette
/// validation, en C#, et pas seulement dans le prompt — un garde-fou écrit dans le prompt se
/// contourne à la première réponse inattendue.</para>
///
/// <para><b>Le repli est du texte utilisateur</b>, pas un code d'erreur déguisé : il est en
/// français (règle n°7), il ne culpabilise jamais (règle n°5), et il ne contient jamais le
/// texte brut du modèle — celui-ci n'a passé aucun contrôle.</para>
///
/// <para><b>« Repli neutre » n'est pas la bonne réponse partout, et c'est le piège de ce
/// contrat.</b> Un repli neutre convient à une quête ou à un rapport quotidien, où ne rien
/// proposer de personnalisé est sans conséquence. Il ne convient pas là où le silence est
/// lui-même un risque : sur le coach sportif, toute mention de douleur doit renvoyer vers un
/// professionnel de santé, <b>y compris et surtout quand le modèle est tombé ou a été
/// rejeté</b>. Servir une séance générique dans ce cas n'est pas une dégradation propre,
/// c'est une régression de sécurité. Chaque agent décide donc de ce que son repli doit dire
/// — la seule règle universelle est qu'il en existe un, et qu'il respecte les garde-fous du
/// domaine concerné.</para>
///
/// <para>Chaque implémentation doit quatre tests, tous contre un faux transport HTTP :
/// réponse valide, JSON malformé, contenu violant un garde-fou, panne réseau. Le troisième
/// est celui qu'on oublie, et c'est celui qui protège l'utilisateur. Ces quatre tests ne
/// sont imposés par aucun type : rien ici n'empêche une implémentation de lever, ni de
/// n'avoir aucun test. C'est une obligation de revue, pas de compilateur — le premier agent
/// concret devra la transformer en socle exécutable (suite de tests héritée) plutôt que de
/// la laisser en commentaire, faute de quoi ce contrat reproduit exactement le défaut qu'il
/// dénonce : une règle écrite là où rien ne la fait respecter.</para>
///
/// <para>Point ouvert, à trancher avant le premier agent : <typeparamref name="TResult"/> ne
/// permet pas à l'appelant de distinguer un résultat validé d'un repli. Le jour où un
/// handler devra ne pas attribuer d'XP, ne pas notifier, ou changer l'habillage « Système »
/// sur une réponse dégradée, l'information aura été perdue à cette frontière.</para>
/// </summary>
public interface IAgent<in TRequest, TResult>
{
    Task<TResult> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}
