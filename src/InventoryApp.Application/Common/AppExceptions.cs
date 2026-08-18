namespace InventoryApp.Application.Common;

/// <summary>Base type for errors that are safe to surface to the client verbatim.</summary>
public abstract class AppException(string message) : Exception(message);

/// <summary>The request was well-formed but broke a business rule (400 / FailedPrecondition).</summary>
public sealed class ValidationException(string message) : AppException(message);

/// <summary>The requested entity does not exist (404 / NotFound).</summary>
public sealed class NotFoundException(string entity, int id)
    : AppException($"{entity} with id {id} was not found.");

/// <summary>The operation conflicts with current state, e.g. deleting a category in use (409 / FailedPrecondition).</summary>
public sealed class ConflictException(string message) : AppException(message);

/// <summary>Credentials rejected (401 / Unauthenticated).</summary>
public sealed class AuthenticationFailedException(string message) : AppException(message);
