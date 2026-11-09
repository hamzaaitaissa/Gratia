namespace Gratia.Application.Exceptions
{
    // Thrown by services; the API's GlobalExceptionHandler turns each one into a ProblemDetails response.

    /// <summary>The requested resource does not exist (404).</summary>
    public sealed class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    /// <summary>The request clashes with existing data, e.g. a duplicate email (409).</summary>
    public sealed class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }

    /// <summary>The caller is authenticated but not allowed to do this (403).</summary>
    public sealed class ForbiddenException : Exception
    {
        public ForbiddenException(string message) : base(message) { }
    }

    /// <summary>The request breaks a business rule, e.g. sending points to yourself (400).</summary>
    public sealed class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message) : base(message) { }
    }

    /// <summary>
    /// An email couldn't be sent, e.g. the SMTP server is down (503, so the caller knows to try again). Thrown by
    /// IEmailService; a handler that saved something the email was about undoes it first (#20).
    /// </summary>
    public sealed class EmailDeliveryException : Exception
    {
        public EmailDeliveryException(string message, Exception? innerException = null) : base(message, innerException) { }
    }
}
