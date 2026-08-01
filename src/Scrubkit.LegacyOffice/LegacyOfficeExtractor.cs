// Copyright © 2026 jjopensoftworks-blip

namespace Scrubkit;

/// <summary>
/// Reads the pre-2007 <b>binary</b> Microsoft Office formats — Word (<c>.doc</c>),
/// Excel (<c>.xls</c>), and PowerPoint (<c>.ppt</c>). The body text becomes
/// <see cref="ExtractedContent.Text"/> and the Title / Author / Subject document properties
/// become metadata.
///
/// All three formats are OLE2 compound files; this reads the relevant streams directly with
/// the BCL — no dependency beyond <c>Scrubkit.Abstractions</c>. Each format's text extraction
/// sits behind its own internal reader, so the parsing for one format can be improved (or
/// swapped for a library) without touching this type or the package's public surface.
/// Best-effort and fully offline; register it via <see cref="ReadOptions.Extractors"/>. The
/// modern OOXML formats (<c>.docx</c>/<c>.xlsx</c>/<c>.pptx</c>) are handled by the core.
/// </summary>
public sealed class LegacyOfficeExtractor : IFileExtractor
{
    /// <inheritdoc/>
    public bool CanHandle(string extension) => extension is ".doc" or ".xls" or ".ppt";

    /// <inheritdoc/>
    public ExtractedContent Extract(string path)
    {
        var cf = new CompoundFile(File.ReadAllBytes(path));
        var meta = SummaryInformation.Read(cf);
        var text = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".doc" => DocText.Read(cf),
            ".xls" => XlsText.Read(cf),
            ".ppt" => PptText.Read(cf),
            _ => "",
        };
        return new ExtractedContent(meta, text);
    }
}
