using FluentValidation;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Api.Common;

/// <summary>
/// Bridges FluentValidation validators (registered by the Application layer) with the
/// Domain's <see cref="ValidationDomainException"/> so controllers can validate incoming
/// requests and let <see cref="TourismHcm.Api.Middleware.ExceptionHandlingMiddleware"/> turn
/// failures into a consistent 400 ProblemDetails response.
/// </summary>
public static class ValidationExtensions
{
    public static async Task ValidateAndThrowDomainAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new ValidationDomainException(result.ToDictionary());
        }
    }
}
