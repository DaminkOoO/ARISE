namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Le profil du Chasseur a été écrit par ailleurs entre sa lecture et sa sauvegarde : le jeton
/// de concurrence vient de le trancher au moment de l'écriture.
///
/// <para>Deux gains d'XP simultanés — la quête de la veille et celle du jour, et dès la Phase 2
/// le Sport et les Habitudes — lisent tous deux le même total, ajoutent chacun leur montant et
/// écrivent le leur : sans ce refus, l'un des deux gains disparaîtrait sans un mot.</para>
///
/// <para>Signal interne au chemin d'écriture, qui rejoue son attribution par-dessus l'état
/// gagnant que le repository lui a rafraîchi. Le message reste en français (règle n°7) : rien
/// ne garantit qu'aucun chemin ne le laissera jamais remonter.</para>
/// </summary>
public sealed class ConcurrentHunterProfileUpdateException()
    : Exception("Ta progression vient d'être mise à jour par ailleurs.");
