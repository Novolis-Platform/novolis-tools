namespace Novolis.Tools.CanvasHtml;

/// <summary>A canvas source could not be transpiled or rendered.</summary>
public sealed class CanvasRenderException : Exception
{
    /// <summary>Creates a render failure.</summary>
    public CanvasRenderException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a render failure that wraps the engine or parser error.</summary>
    public CanvasRenderException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
