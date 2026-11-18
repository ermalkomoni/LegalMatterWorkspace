namespace Amanet.Application.Exceptions;

public sealed class InvalidDocumentException(string message, bool tooLarge = false) : Exception(message)
{
    public bool TooLarge { get; } = tooLarge;
}
