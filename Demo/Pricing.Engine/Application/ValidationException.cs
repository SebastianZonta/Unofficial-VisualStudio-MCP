namespace Pricing.Engine.Application;

public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
