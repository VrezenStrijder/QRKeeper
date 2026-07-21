using System.Text.RegularExpressions;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

public sealed class ContentTypeDetector : IContentTypeDetector
{
    private static readonly Regex PhoneRegex = new(@"^(tel:)?\+?[0-9][0-9\-\s()]{5,}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public QRContentType Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return QRContentType.Unknown;
        }

        string value = content.Trim();

        if (value.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase))
        {
            return QRContentType.WiFi;
        }

        if (value.StartsWith("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("MECARD:", StringComparison.OrdinalIgnoreCase))
        {
            return QRContentType.Contact;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return uri.Scheme.ToLowerInvariant() switch
            {
                "http" or "https" => QRContentType.Url,
                "mailto" => QRContentType.Email,
                "tel" => QRContentType.Phone,
                "sms" or "smsto" => QRContentType.Sms,
                "geo" => QRContentType.GeoLocation,
                _ => QRContentType.Text
            };
        }

        if (value.StartsWith("MATMSG:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("SMTP:", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return QRContentType.Email;
        }

        if (PhoneRegex.IsMatch(value))
        {
            return QRContentType.Phone;
        }

        return QRContentType.Text;
    }
}
