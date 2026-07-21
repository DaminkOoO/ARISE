---
name: agent-gemini
description: Le pattern d'agent IA d'ARISE — IAgent<TRequest,TResult>, tests via faux HttpMessageHandler, et validation obligatoire de la réponse JSON de Gemini. Utilise cette skill dès qu'une tâche touche à un agent, à Gemini, au « Système », à la génération de quêtes, au rapport quotidien, à l'onboarding, au coach sportif ou à une recommandation RAG — en clair, dès qu'un appel à un LLM entre dans le code. Jamais d'appel Gemini réel dans la suite de tests.
---

# Agents Gemini sur ARISE

Gemini joue « le Système » : il génère les quêtes, le rapport quotidien, les recommandations.
C'est la seule partie du produit dont la sortie n'est pas déterministe — d'où deux exigences
qui ne se négocient pas : les tests ne touchent jamais l'API réelle, et aucune réponse n'est
utilisée sans avoir été validée.

## Le contrat

Chaque agent s'expose derrière `IAgent<TRequest,TResult>` et rend un type métier — jamais une
`string`, jamais un `JsonDocument`. Le parsing et la validation appartiennent à l'agent :
c'est sa frontière. Si un `JsonDocument` fuit vers un handler, la validation finira dupliquée
à trois endroits et oubliée au quatrième.

Pas de Semantic Kernel : un `HttpClient` injecté, appelé depuis une classe d'agent.

## Les tests n'appellent jamais Gemini

Un appel réel en test le rend lent, non déterministe, payant, et rouge quand le réseau
tousse. Injecte un faux `HttpMessageHandler` qui rend la réponse que tu veux éprouver :

```csharp
internal sealed class FakeHttpMessageHandler(HttpStatusCode code, string body)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(code)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
}
```

C'est le premier outil à mettre en place, avant tout agent concret : chaque agent suivant s'y
branche.

## Valider avant d'utiliser

Le mode schema de Gemini réduit les sorties malformées, il ne les élimine pas — et surtout il
ne dit rien de la **pertinence** du contenu. Un JSON parfaitement formé peut contenir une
quête qui prescrit « 4×12 à 80 kg », ce que les garde-fous produit interdisent. Valide donc
en trois temps, dans cet ordre :

1. **Parse** — le corps est-il du JSON exploitable ?
2. **Forme** — champs requis présents, types corrects, énumérations dans les valeurs attendues.
3. **Garde-fous produit** — le contenu respecte-t-il les règles du domaine ? (voir la skill
   `garde-fous` : pas de prescription chiffrée ni de diagnostic de blessure côté sport, pas de
   conseil investissement/dette/fiscal côté budget, aucune formulation culpabilisante.)

Un échec à l'étape 3 est un échec de validation au même titre qu'un JSON cassé : on ne
« corrige » pas la sortie du modèle, on la rejette.

## Dégrader proprement

Quand la validation échoue, l'utilisateur ne doit ni voir une erreur brute, ni perdre sa
journée : rends un contenu de repli neutre (quête générique, rapport minimal) et journalise
la réponse rejetée pour diagnostic. Ne renvoie jamais le texte brut du modèle à l'écran — il
n'a passé aucun contrôle.

## Les quatre tests minimum par agent

1. Réponse valide → le `TResult` attendu.
2. JSON malformé → repli, pas d'exception qui remonte.
3. JSON valide mais violant un garde-fou → rejeté, repli.
4. Erreur HTTP ou timeout → repli, pas d'exception qui remonte.

Le test 3 est celui qu'on oublie, et c'est celui qui protège l'utilisateur.

## Rappels

- Le prompt et la sortie sont en français — c'est l'utilisateur qui les lit.
- La clé d'API vient de la configuration, jamais du code ni d'un test.
- Un garde-fou écrit uniquement dans le prompt n'est pas un garde-fou : il se contourne à la
  première réponse inattendue. Il doit exister en C#, dans la validation.
