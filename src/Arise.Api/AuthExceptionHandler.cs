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
