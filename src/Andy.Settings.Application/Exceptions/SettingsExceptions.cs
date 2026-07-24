namespace Andy.Settings.Application.Exceptions;

/// <summary>
/// Base for conditions the API maps to a specific status code.
/// </summary>
/// <remarks>
/// Controllers used to pick status codes by substring-matching exception
/// messages — <c>ex.Message.Contains("etag")</c> for 409, and
/// <c>Contains("already exists") || Contains("duplicate")</c> for 409 on
/// definitions. Nothing linked the thrower to the catcher, so rewording an
/// error message silently turned a 409 into a 400, and the two call sites had
/// already drifted apart on case sensitivity
/// (rivoli-ai/andy-settings#143).
/// </remarks>
public abstract class SettingsException : Exception
{
    protected SettingsException(string message) : base(message) { }
    protected SettingsException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>An optimistic-concurrency check failed. Maps to 409.</summary>
public sealed class ConcurrencyConflictException : SettingsException
{
    public ConcurrencyConflictException(string message) : base(message) { }
}

/// <summary>A uniquely-keyed entity already exists. Maps to 409.</summary>
public sealed class DuplicateKeyException : SettingsException
{
    public DuplicateKeyException(string message) : base(message) { }
}

/// <summary>
/// The request is well-formed but violates a domain rule — an invalid scope, a
/// value that fails its definition's validation, a plaintext write to a
/// secret-backed key. Maps to 400.
/// </summary>
public sealed class DomainRuleViolationException : SettingsException
{
    public DomainRuleViolationException(string message) : base(message) { }
    public DomainRuleViolationException(string message, Exception inner) : base(message, inner) { }
}
