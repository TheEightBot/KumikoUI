namespace KumikoUI.Core.Rendering;

/// <summary>The outcome of <see cref="IKumikoImageResolver.Resolve"/> for one image source.</summary>
public enum KumikoImageStatus
{
    /// <summary>The image is ready: the resolver returned a handle that <see cref="IDrawingContext.DrawImage"/> accepts.</summary>
    Ready,

    /// <summary>The image is being loaded. Nothing is drawn yet; the resolver asks the host to redraw when it is ready.</summary>
    Pending,

    /// <summary>Loading the image failed (missing file, network error, data that is not an image). Nothing is drawn.</summary>
    Failed,

    /// <summary>The resolver does not know how to load this kind of value. Nothing is drawn.</summary>
    Unsupported,
}
