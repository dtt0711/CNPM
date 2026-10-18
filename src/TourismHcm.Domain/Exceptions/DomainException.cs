namespace TourismHcm.Domain.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public DomainException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string resource, object key)
        : base("not_found", $"{resource} '{key}' was not found.", 404)
    {
    }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message)
        : base("conflict", message, 409)
    {
    }
}

public sealed class UnauthorizedDomainException : DomainException
{
    public UnauthorizedDomainException(string message = "Unauthorized.")
        : base("unauthorized", message, 401)
    {
    }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "Forbidden.")
        : base("forbidden", message, 403)
    {
    }
}

public sealed class ProviderUnavailableException : DomainException
{
    public ProviderUnavailableException(string provider, string message)
        : base("provider_unavailable", $"{provider}: {message}", 503)
    {
    }
}

public sealed class ValidationDomainException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationDomainException(IDictionary<string, string[]> errors)
        : base("validation_failed", "One or more validation errors occurred.", 400)
    {
        Errors = errors;
    }
}
