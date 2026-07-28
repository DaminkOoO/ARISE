using Arise.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Arise.Api;

/// <summary>
/// Traduit les exceptions métier en réponses <c>ProblemDetails</c> françaises, avec le bon
/// code HTTP.
///
/// <para>Les messages exposés sont ceux, déjà rédigés pour l'écran, des exceptions d'auth :
/// le conflit de nom est explicite, l'échec de connexion reste volontairement muet sur
/// <b>lequel</b> des deux champs est faux (anti-énumération). Une exception <b>inconnue</b>
/// n'est pas traduite : on rend la main au 500 générique, qui ne divulgue aucun détail.</para>
/// </summary>
internal sealed class AuthExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statut, detail) = exception switch
        {
            // Les messages de validation sont en français (culture fr posée dans Application)
            // et remontent tels quels jusqu'à l'écran.
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                string.Join(" ", validation.Errors.Select(erreur => erreur.ErrorMessage))),
            UsernameAlreadyTakenException => (StatusCodes.Status409Conflict, exception.Message),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, exception.Message),

            // 404 et non 403 : la ressource visée n'appartient pas au Chasseur, et les handlers
            // lèvent délibérément la même exception que pour un identifiant inconnu — distinguer
            // les deux révélerait l'existence de la ressource d'autrui.
            HabitNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            TaskNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            HunterProfileNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            UserNotFoundException => (StatusCodes.Status404NotFound, exception.Message),

            // 409 : l'état de la ressource s'oppose au geste, sans que rien soit mal formé.
            HabitNameAlreadyTakenException => (StatusCodes.Status409Conflict, exception.Message),
            HabitArchivedException => (StatusCodes.Status409Conflict, exception.Message),
            HunterAlreadyAwakenedException => (StatusCodes.Status409Conflict, exception.Message),

            // 403 : le jeton est valide, mais le compte n'a pas encore de profil à viser. Ni un
            // 401 — le Chasseur est bien authentifié — ni un 404, la ressource n'étant pas en
            // cause : c'est l'éveil qui manque, et le message le dit.
            HunterNotAwakenedException => (StatusCodes.Status403Forbidden, exception.Message),

            _ => (0, string.Empty),
        };

        if (statut == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = statut;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statut,
                Detail = detail,
            },
        });
    }
}
