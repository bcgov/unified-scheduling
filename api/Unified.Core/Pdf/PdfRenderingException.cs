namespace Unified.Core.Pdf;

public sealed class PdfRenderingException : Exception
{
    public bool IsServiceUnavailable { get; }

    public PdfRenderingException(string message)
        : base(message) { }

    public PdfRenderingException(string message, Exception innerException)
        : base(message, innerException) { }

    public PdfRenderingException(string message, Exception innerException, bool isServiceUnavailable)
        : base(message, innerException)
    {
        IsServiceUnavailable = isServiceUnavailable;
    }
}
