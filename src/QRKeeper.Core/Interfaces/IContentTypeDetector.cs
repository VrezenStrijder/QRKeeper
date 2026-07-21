using QRKeeper.Core.Models;

namespace QRKeeper.Core.Interfaces;

public interface IContentTypeDetector
{
    QRContentType Detect(string content);
}
