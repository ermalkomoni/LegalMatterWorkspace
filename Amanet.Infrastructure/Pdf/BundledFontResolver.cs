using PdfSharp.Fonts;

namespace Amanet.Infrastructure.Pdf;

public sealed class BundledFontResolver : IFontResolver
{
    public const string FamilyName = "Lato";

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (!string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase))
            return null;

        return new FontResolverInfo(bold ? "Lato-Bold" : "Lato-Regular", false, italic);
    }

    public byte[] GetFont(string faceName)
    {
        if (faceName is not ("Lato-Regular" or "Lato-Bold"))
            throw new ArgumentException("Unknown bundled font.", nameof(faceName));

        using var resource = typeof(BundledFontResolver).Assembly
            .GetManifestResourceStream($"Amanet.Infrastructure.Pdf.Fonts.{faceName}.ttf")
            ?? throw new InvalidOperationException("The bundled PDF font is missing.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }
}
