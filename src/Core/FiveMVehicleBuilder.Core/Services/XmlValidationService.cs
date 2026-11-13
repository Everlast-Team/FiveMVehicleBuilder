using System.Text.RegularExpressions;
using System.Xml;

namespace FiveMVehicleBuilder.Core.Services;

public partial class XmlValidationService : IXmlValidationService
{
    public XmlValidationResult ValidateXml(string content, string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return XmlValidationResult.Failure("XML-Inhalt ist leer", filePath: filePath);
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            };

            using var stringReader = new StringReader(content);
            using var xmlReader = XmlReader.Create(stringReader, settings);

            while (xmlReader.Read())
            {
                // Alle Nodes durchlaufen um Validierung zu erzwingen
            }

            return XmlValidationResult.Success(filePath);
        }
        catch (XmlException ex)
        {
            return XmlValidationResult.Failure(
                ex.Message,
                ex.LineNumber,
                ex.LinePosition,
                filePath);
        }
    }

    public async Task<XmlValidationResult> ValidateXmlFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return XmlValidationResult.Failure($"Datei nicht gefunden: {filePath}", filePath: filePath);
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            return ValidateXml(content, filePath);
        }
        catch (IOException ex)
        {
            return XmlValidationResult.Failure($"Fehler beim Lesen der Datei: {ex.Message}", filePath: filePath);
        }
    }

    public string? TryRepairXml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var repaired = content;
        var hasChanges = false;

        // Problem 1: Gemischte Syntax wie <tag>content"" />
        // Pattern: <tagname>content"" />  oder  <tagname>content" />
        // Sollte sein: <tagname>content</tagname>
        var mixedSyntaxPattern = MixedSyntaxRegex();
        if (mixedSyntaxPattern.IsMatch(repaired))
        {
            repaired = mixedSyntaxPattern.Replace(repaired, match =>
            {
                var tagName = match.Groups[1].Value;
                var content = match.Groups[2].Value;
                return $"<{tagName}>{content}</{tagName}>";
            });
            hasChanges = true;
        }

        // Problem 2: Unclosed Tags wie <tag>value (ohne schließendes Tag)
        // Das ist schwieriger zu fixen, da wir den Kontext brauchen

        // Problem 3: Doppelte Anführungszeichen in Attributen
        var doubleQuoteAttrPattern = DoubleQuoteAttrRegex();
        if (doubleQuoteAttrPattern.IsMatch(repaired))
        {
            repaired = doubleQuoteAttrPattern.Replace(repaired, "$1=\"$2\"");
            hasChanges = true;
        }

        // Problem 4: Self-closing Tags mit Inhalt wie <tag>value />
        var selfClosingWithContentPattern = SelfClosingWithContentRegex();
        if (selfClosingWithContentPattern.IsMatch(repaired))
        {
            repaired = selfClosingWithContentPattern.Replace(repaired, match =>
            {
                var tagName = match.Groups[1].Value;
                var content = match.Groups[2].Value.Trim();
                return $"<{tagName}>{content}</{tagName}>";
            });
            hasChanges = true;
        }

        if (!hasChanges)
            return null;

        // Prüfen ob Reparatur erfolgreich war
        var validation = ValidateXml(repaired);
        return validation.IsValid ? repaired : null;
    }

    // Regex für gemischte Syntax: <tagname>content"" /> oder <tagname>content" />
    [GeneratedRegex(@"<(\w+)>([^<]+)""+\s*/>", RegexOptions.Compiled)]
    private static partial Regex MixedSyntaxRegex();

    // Regex für doppelte Anführungszeichen in Attributen
    [GeneratedRegex(@"(\w+)=""([^""]+)""""", RegexOptions.Compiled)]
    private static partial Regex DoubleQuoteAttrRegex();

    // Regex für self-closing mit Content: <tag>value />
    [GeneratedRegex(@"<(\w+)>([^<]+?)\s*/>", RegexOptions.Compiled)]
    private static partial Regex SelfClosingWithContentRegex();
}
