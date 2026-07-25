namespace Arise.Application.Common.Exceptions;

/// <summary>
/// La quête visée appartient à un jour révolu : sa fenêtre de complétion — le jour de la quête
/// ou la veille (doc mécaniques, section 2) — est passée.
///
/// <para>Sans cette borne, un Chasseur revenu après dix jours d'absence compléterait les dix
/// quêtes laissées derrière lui d'affilée, et la progression cesserait de mesurer quoi que ce
/// soit.</para>
///
/// <para>Le message est un constat, jamais un reproche (règle n°5) : le Chasseur qui rouvre
/// l'app après une pause n'a rien fait de mal, et le Système lui pose simplement la quête du
/// jour.</para>
/// </summary>
public sealed class QuestExpiredException()
    : Exception("Cette quête appartient à un jour révolu. Celle du jour t'attend.");
