using System.Globalization;
using System.Security.Cryptography;
using Amanet.Application.Exceptions;
using Amanet.Core.Entities;
using Amanet.Infrastructure.Storage;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;

namespace Amanet.Infrastructure.Pdf;

public sealed class PdfSigningGenerator(PrivateDocumentStorage storage)
{
    internal async Task<StoredPdf> GenerateAsync(DocumentVersion source, SignedDocument audit, CancellationToken cancellationToken)
    {
        await using var storedSource = storage.OpenRead(source.StorageKey, source.ByteLength);
        using var input = new MemoryStream();
        await storedSource.CopyToAsync(input, cancellationToken);
        input.Position = 0;
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, cancellationToken));
        if (!string.Equals(hash, source.Sha256, StringComparison.Ordinal))
            throw new InvalidDocumentException("The source PDF does not match its recorded hash.");
        input.Position = 0;
        using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var page = pdf.AddPage();
        page.Size = PageSize.A4;
        using (var graphics = XGraphics.FromPdfPage(page))
        {
            var heading = new XFont(BundledFontResolver.FamilyName, 22, XFontStyleEx.Bold);
            var font = new XFont(BundledFontResolver.FamilyName, 11);
            graphics.DrawString("Signature record", heading, XBrushes.Black, new XPoint(48, 70));
            var y = 110d;
            Draw(graphics, font, $"Signer: {audit.SignerName}", ref y);
            Draw(graphics, font, $"Email: {audit.SignerEmail}", ref y);
            Draw(graphics, font, $"Signer type: {(audit.SignerKind == SignerKind.Admin ? "Admin" : "External client")}", ref y);
            Draw(graphics, font, $"Signed at (UTC): {audit.SignedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}", ref y);
            Draw(graphics, font, $"Request reference: {audit.RequestId}", ref y);
            Draw(graphics, font, $"Source SHA-256: {audit.SourceSha256}", ref y);
            y += 16;
            Draw(graphics, font, "Explicit consent:", ref y);
            Draw(graphics, font, audit.ConsentText, ref y);
        }
        using var output = new MemoryStream();
        pdf.Save(output, closeStream: false);
        output.Position = 0;
        return await storage.StageGeneratedAsync(output, cancellationToken);
    }

    // Wrap long names/emails/hashes too, including strings without spaces.
    private static void Draw(XGraphics graphics, XFont font, string text, ref double y)
    {
        var line = string.Empty;
        foreach (var rune in text.EnumerateRunes())
        {
            if (GlyphHelper.GlyphIndexFromCodePoint(rune.Value, font) == 0)
                throw new InvalidDocumentException("A signing detail contains characters unsupported by the bundled PDF font.");
            var character = rune.ToString();
            if (graphics.MeasureString(line + character, font).Width > 495 && line.Length > 0)
            {
                graphics.DrawString(line, font, XBrushes.Black, new XPoint(48, y));
                y += 17;
                line = string.Empty;
            }
            line += character;
        }
        if (line.Length > 0)
            graphics.DrawString(line, font, XBrushes.Black, new XPoint(48, y));
        y += 25;
    }
}
