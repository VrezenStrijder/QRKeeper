using QRCoder;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZXing;

namespace QRKeeper.Infrastructure.Services;

public sealed class QRCodeService : IQRCodeService
{
    private const int MinimumComponentSize = 2;
    private const int MinimumComponentArea = 6;
    private const int MinimumStylizedComponentCount = 45;
    private const int MaximumStylizedCandidateRegions = 12;
    private const int ColoredNv21ModuleLumaLimit = 252;
    private const int MaximumQrDimension = 177;

    public async Task<string?> DecodeAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<QRCodeDecodeResult> results = await DecodeAllAsync(imageStream, cancellationToken);
        return results.FirstOrDefault()?.Text;
    }

    public async Task<IReadOnlyList<QRCodeDecodeResult>> DecodeAllAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        if (imageStream.CanSeek)
        {
            imageStream.Position = 0;
        }

        using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        BarcodeReaderGeneric reader = CreateQrReader();

        byte[] pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);

        Result[]? results = reader.DecodeMultiple(pixels, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        if (results is { Length: > 0 })
        {
            return results
                .Where(result => !string.IsNullOrWhiteSpace(result.Text))
                .Select(ToDecodeResult)
                .ToArray();
        }

        Result? result = reader.Decode(pixels, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        if (result is not null && !string.IsNullOrWhiteSpace(result.Text))
        {
            return [ToDecodeResult(result)];
        }

        QRCodeDecodeResult? stylizedResult = TryDecodeStylizedDotQrImage(image, reader);
        return stylizedResult is null ? [] : [stylizedResult];
    }

    public byte[] GeneratePng(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AppException("二维码内容不能为空。");
        }

        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using PngByteQRCode code = new(data);
        byte[] rawPng = code.GetGraphic(20);

        using Image image = Image.Load(rawPng);
        image.Mutate(context => context.Resize(AppConstants.QrImageSize, AppConstants.QrImageSize));

        using MemoryStream stream = new();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    public static QRCodeDecodeResult? TryDecodeStylizedDotQr(
        byte[] mask,
        int width,
        int height,
        BarcodeReaderGeneric? reader = null)
    {
        BarcodeReaderGeneric activeReader = reader ?? CreateQrReader();
        return TryDecodeStylizedDotQrCore(mask, width, height, activeReader, true, 0, 0);
    }

    /// <summary>
    /// Decodes a stylized QR code from an Android NV21 camera preview frame.
    /// </summary>
    public static QRCodeDecodeResult? TryDecodeStylizedDotQrFromNv21(
        byte[] frame,
        int width,
        int height,
        int cropLeft,
        int cropTop,
        int cropWidth,
        int cropHeight,
        IReadOnlyList<int> lumaThresholds,
        int chromaThreshold,
        BarcodeReaderGeneric? reader = null)
    {
        int pixelCount = width * height;
        if (width <= 0 ||
            height <= 0 ||
            cropWidth <= 0 ||
            cropHeight <= 0 ||
            frame.Length < pixelCount)
        {
            return null;
        }

        int left = Math.Clamp(cropLeft, 0, width - 1);
        int top = Math.Clamp(cropTop, 0, height - 1);
        int right = Math.Clamp(cropLeft + cropWidth, left + 1, width);
        int bottom = Math.Clamp(cropTop + cropHeight, top + 1, height);
        int boundedWidth = right - left;
        int boundedHeight = bottom - top;
        byte[] mask = new byte[boundedWidth * boundedHeight];
        BarcodeReaderGeneric activeReader = reader ?? CreateQrReader();

        foreach (int threshold in lumaThresholds)
        {
            FillNv21StylizedMask(
                frame,
                width,
                height,
                left,
                top,
                boundedWidth,
                boundedHeight,
                threshold,
                chromaThreshold,
                mask);

            QRCodeDecodeResult? result = TryDecodeStylizedDotQrCore(mask, boundedWidth, boundedHeight, activeReader, true, left, top);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static QRCodeDecodeResult? TryDecodeStylizedDotQrCore(
        byte[] mask,
        int width,
        int height,
        BarcodeReaderGeneric reader,
        bool searchRegions,
        int offsetX,
        int offsetY)
    {
        IReadOnlyList<Component> components = FindCandidateComponents(mask, width, height);
        if (components.Count < MinimumStylizedComponentCount)
        {
            return null;
        }

        if (!searchRegions || ShouldTryFullStylizedComponents(width, height, components.Count))
        {
            QRCodeDecodeResult? fullResult = TryDecodeStylizedComponents(
                mask,
                width,
                height,
                components,
                reader,
                offsetX,
                offsetY);
            if (fullResult is not null || !searchRegions)
            {
                return fullResult;
            }
        }

        foreach (PixelRect region in FindStylizedCandidateRegions(components, width, height))
        {
            byte[] regionMask = CropMask(mask, width, region);
            QRCodeDecodeResult? regionResult = TryDecodeStylizedDotQrCore(
                regionMask,
                region.Width,
                region.Height,
                reader,
                false,
                offsetX + region.Left,
                offsetY + region.Top);
            if (regionResult is not null)
            {
                return regionResult;
            }
        }

        return null;
    }

    private static void FillNv21StylizedMask(
        byte[] frame,
        int width,
        int height,
        int cropLeft,
        int cropTop,
        int cropWidth,
        int cropHeight,
        int lumaThreshold,
        int chromaThreshold,
        byte[] mask)
    {
        int pixelCount = width * height;
        bool hasChroma = frame.Length >= pixelCount + pixelCount / 2;

        for (int y = 0; y < cropHeight; y++)
        {
            int sourceY = cropTop + y;
            int targetOffset = y * cropWidth;
            int sourceOffset = sourceY * width + cropLeft;
            for (int x = 0; x < cropWidth; x++)
            {
                int sourceX = cropLeft + x;
                int luma = frame[sourceOffset + x] & 0xFF;
                bool isColored = hasChroma && IsNv21ChromaColored(
                    frame,
                    pixelCount,
                    width,
                    height,
                    sourceX,
                    sourceY,
                    chromaThreshold);

                mask[targetOffset + x] = luma < lumaThreshold || (isColored && luma < ColoredNv21ModuleLumaLimit)
                    ? (byte)1
                    : (byte)0;
            }
        }
    }

    private static bool IsNv21ChromaColored(
        byte[] frame,
        int pixelCount,
        int width,
        int height,
        int x,
        int y,
        int chromaThreshold)
    {
        int uvX = x & ~1;
        int uvY = Math.Min(y / 2, Math.Max(1, height / 2) - 1);
        int uvIndex = pixelCount + uvY * width + uvX;
        if (uvIndex + 1 >= frame.Length)
        {
            return false;
        }

        int v = frame[uvIndex] & 0xFF;
        int u = frame[uvIndex + 1] & 0xFF;
        return Math.Abs(u - 128) + Math.Abs(v - 128) >= chromaThreshold;
    }

    private static QRCodeDecodeResult? TryDecodeStylizedComponents(
        byte[] mask,
        int width,
        int height,
        IReadOnlyList<Component> components,
        BarcodeReaderGeneric activeReader,
        int offsetX,
        int offsetY)
    {
        double tolerance = Math.Max(3, Math.Min(width, height) / 90.0);
        double[] xClusters = ClusterCenters(components.Select(component => component.CenterX), tolerance);
        double[] yClusters = ClusterCenters(components.Select(component => component.CenterY), tolerance);
        int[] integral = CreateIntegralMask(mask, width, height);

        foreach (int dimension in GetCandidateQrDimensions(xClusters, yClusters))
        {
            double[] gridX = GetGridCenters(xClusters, dimension);
            double[] gridY = GetGridCenters(yClusters, dimension);
            if (gridX.Length != dimension || gridY.Length != dimension)
            {
                continue;
            }

            bool[,] sourceModules = BuildModuleMatrix(integral, width, height, gridX, gridY);
            for (int rotation = 0; rotation < 4; rotation++)
            {
                bool[,] modules = RotateModules(sourceModules, rotation);
                ApplyStandardQrPatterns(modules);
                byte[] renderedPixels = RenderModuleMatrix(modules, 8, out int renderedWidth, out int renderedHeight);
                Result? decoded = activeReader.Decode(
                    renderedPixels,
                    renderedWidth,
                    renderedHeight,
                    RGBLuminanceSource.BitmapFormat.RGBA32);
                if (decoded is null || string.IsNullOrWhiteSpace(decoded.Text))
                {
                    continue;
                }

                float left = (float)Math.Max(0, gridX.First() - GetAveragePitch(gridX) / 2);
                float top = (float)Math.Max(0, gridY.First() - GetAveragePitch(gridY) / 2);
                float right = (float)Math.Min(width, gridX.Last() + GetAveragePitch(gridX) / 2);
                float bottom = (float)Math.Min(height, gridY.Last() + GetAveragePitch(gridY) / 2);
                return new QRCodeDecodeResult(
                    decoded.Text,
                    offsetX + left,
                    offsetY + top,
                    Math.Max(1, right - left),
                    Math.Max(1, bottom - top));
            }
        }

        return null;
    }

    private static bool ShouldTryFullStylizedComponents(int width, int height, int componentCount)
    {
        int minSide = Math.Min(width, height);
        return minSide <= 640 || componentCount <= 180;
    }

    private static QRCodeDecodeResult ToDecodeResult(Result result)
    {
        if (result.ResultPoints is null || result.ResultPoints.Length == 0)
        {
            return new QRCodeDecodeResult(result.Text, 0, 0, 0, 0);
        }

        float minX = result.ResultPoints.Min(point => point.X);
        float minY = result.ResultPoints.Min(point => point.Y);
        float maxX = result.ResultPoints.Max(point => point.X);
        float maxY = result.ResultPoints.Max(point => point.Y);
        return new QRCodeDecodeResult(result.Text, minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }

    private static BarcodeReaderGeneric CreateQrReader()
    {
        return new BarcodeReaderGeneric
        {
            Options =
            {
                TryHarder = true,
                PossibleFormats = [BarcodeFormat.QR_CODE]
            }
        };
    }

    private static QRCodeDecodeResult? TryDecodeStylizedDotQrImage(Image<Rgba32> image, BarcodeReaderGeneric reader)
    {
        byte[] mask = CreateColoredModuleMask(image);
        return TryDecodeStylizedDotQr(mask, image.Width, image.Height, reader);
    }

    private static byte[] CreateColoredModuleMask(Image<Rgba32> image)
    {
        byte[] mask = new byte[image.Width * image.Height];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                int offset = y * image.Width;
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = row[x];
                    int max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                    int min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                    bool coloredOrDark = max < 250 && (max - min > 20 || max < 180);
                    mask[offset + x] = coloredOrDark ? (byte)1 : (byte)0;
                }
            }
        });

        return mask;
    }

    private static IReadOnlyList<Component> FindCandidateComponents(byte[] mask, int width, int height)
    {
        bool[] visited = new bool[mask.Length];
        List<Component> components = new();
        Queue<int> queue = new();
        int maximumComponentSize = Math.Max(16, (int)(Math.Min(width, height) * 0.06));

        for (int index = 0; index < mask.Length; index++)
        {
            if (mask[index] == 0 || visited[index])
            {
                continue;
            }

            Component component = FloodFillComponent(mask, visited, queue, width, height, index);
            if (component.Width >= MinimumComponentSize &&
                component.Height >= MinimumComponentSize &&
                component.Width <= maximumComponentSize &&
                component.Height <= maximumComponentSize &&
                component.Area >= MinimumComponentArea)
            {
                components.Add(component);
            }
        }

        return components;
    }

    private static Component FloodFillComponent(
        byte[] mask,
        bool[] visited,
        Queue<int> queue,
        int width,
        int height,
        int startIndex)
    {
        queue.Clear();
        queue.Enqueue(startIndex);
        visited[startIndex] = true;

        int area = 0;
        int minX = width;
        int minY = height;
        int maxX = 0;
        int maxY = 0;
        double sumX = 0;
        double sumY = 0;

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;
            area++;
            sumX += x;
            sumY += y;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                int nextY = y + offsetY;
                if (nextY < 0 || nextY >= height)
                {
                    continue;
                }

                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    int nextX = x + offsetX;
                    if (nextX < 0 || nextX >= width)
                    {
                        continue;
                    }

                    int nextIndex = nextY * width + nextX;
                    if (mask[nextIndex] == 0 || visited[nextIndex])
                    {
                        continue;
                    }

                    visited[nextIndex] = true;
                    queue.Enqueue(nextIndex);
                }
            }
        }

        return new Component(
            sumX / area,
            sumY / area,
            area,
            maxX - minX + 1,
            maxY - minY + 1,
            minX,
            minY,
            maxX,
            maxY);
    }

    private static IReadOnlyList<PixelRect> FindStylizedCandidateRegions(
        IReadOnlyList<Component> components,
        int width,
        int height)
    {
        List<ScoredRegion> regions = new();
        foreach (int windowSize in GetCandidateWindowSizes(width, height))
        {
            foreach (Component component in components)
            {
                PixelRect window = CreateCenteredWindow(component.CenterX, component.CenterY, windowSize, width, height);
                List<Component> group = components
                    .Where(candidate => IsComponentCenterInside(candidate, window))
                    .ToList();
                if (group.Count < MinimumStylizedComponentCount)
                {
                    continue;
                }

                PixelRect? region = CreateCandidateRegion(group, width, height);
                if (region is null)
                {
                    continue;
                }

                double density = group.Count / (double)Math.Max(1, region.Width * region.Height);
                regions.Add(new ScoredRegion(region, group.Count, density));
            }
        }

        List<ScoredRegion> selectedRegions = new();
        foreach (ScoredRegion region in regions
            .OrderByDescending(region => region.Density * region.ComponentCount)
            .ThenByDescending(region => region.ComponentCount))
        {
            if (selectedRegions.Any(selected => GetOverlapRatio(selected.Region, region.Region) > 0.68))
            {
                continue;
            }

            selectedRegions.Add(region);
            if (selectedRegions.Count >= MaximumStylizedCandidateRegions)
            {
                break;
            }
        }

        return selectedRegions
            .Select(region => region.Region)
            .ToArray();
    }

    private static int[] GetCandidateWindowSizes(int width, int height)
    {
        int minSide = Math.Min(width, height);
        int[] fixedSizes = [128, 160, 208, 260, 320, 420, 560];
        double[] relativeSizes = [0.18, 0.24, 0.32, 0.42, 0.56, 0.72, 0.9];
        return fixedSizes
            .Concat(relativeSizes.Select(size => (int)Math.Round(minSide * size)))
            .Select(size => Math.Clamp(size, 48, minSide))
            .Distinct()
            .Order()
            .ToArray();
    }

    private static PixelRect CreateCenteredWindow(
        double centerX,
        double centerY,
        int size,
        int width,
        int height)
    {
        int boundedSize = Math.Min(size, Math.Min(width, height));
        int left = Math.Clamp((int)Math.Round(centerX - boundedSize / 2.0), 0, Math.Max(0, width - boundedSize));
        int top = Math.Clamp((int)Math.Round(centerY - boundedSize / 2.0), 0, Math.Max(0, height - boundedSize));
        return new PixelRect(left, top, boundedSize, boundedSize);
    }

    private static bool IsComponentCenterInside(Component component, PixelRect region)
    {
        return component.CenterX >= region.Left &&
            component.CenterX <= region.Left + region.Width &&
            component.CenterY >= region.Top &&
            component.CenterY <= region.Top + region.Height;
    }

    private static double GetOverlapRatio(PixelRect first, PixelRect second)
    {
        int left = Math.Max(first.Left, second.Left);
        int top = Math.Max(first.Top, second.Top);
        int right = Math.Min(first.Left + first.Width, second.Left + second.Width);
        int bottom = Math.Min(first.Top + first.Height, second.Top + second.Height);
        int intersectionWidth = Math.Max(0, right - left);
        int intersectionHeight = Math.Max(0, bottom - top);
        int intersectionArea = intersectionWidth * intersectionHeight;
        int firstArea = first.Width * first.Height;
        int secondArea = second.Width * second.Height;
        int unionArea = firstArea + secondArea - intersectionArea;
        return intersectionArea / (double)Math.Max(1, unionArea);
    }

    private static PixelRect? CreateCandidateRegion(
        IReadOnlyList<Component> group,
        int width,
        int height)
    {
        int left = group.Min(component => component.MinX);
        int top = group.Min(component => component.MinY);
        int right = group.Max(component => component.MaxX);
        int bottom = group.Max(component => component.MaxY);
        int regionWidth = right - left + 1;
        int regionHeight = bottom - top + 1;
        double ratio = regionWidth / (double)Math.Max(1, regionHeight);
        if (ratio is < 0.62 or > 1.62)
        {
            return null;
        }

        int span = Math.Max(regionWidth, regionHeight);
        int padding = Math.Max(16, (int)Math.Ceiling(span * 0.24));
        int size = Math.Min(Math.Max(24, span + padding * 2), Math.Max(width, height));
        int centerX = left + regionWidth / 2;
        int centerY = top + regionHeight / 2;
        int candidateLeft = Math.Clamp(centerX - size / 2, 0, Math.Max(0, width - size));
        int candidateTop = Math.Clamp(centerY - size / 2, 0, Math.Max(0, height - size));
        int candidateWidth = Math.Min(size, width - candidateLeft);
        int candidateHeight = Math.Min(size, height - candidateTop);
        return new PixelRect(candidateLeft, candidateTop, candidateWidth, candidateHeight);
    }

    private static byte[] CropMask(byte[] mask, int sourceWidth, PixelRect region)
    {
        byte[] cropped = new byte[region.Width * region.Height];
        for (int y = 0; y < region.Height; y++)
        {
            int sourceOffset = (region.Top + y) * sourceWidth + region.Left;
            int targetOffset = y * region.Width;
            Array.Copy(mask, sourceOffset, cropped, targetOffset, region.Width);
        }

        return cropped;
    }

    private static double[] ClusterCenters(IEnumerable<double> centers, double tolerance)
    {
        List<double> sortedCenters = centers.OrderBy(center => center).ToList();
        List<double> clusters = new();
        List<double> current = new();

        foreach (double center in sortedCenters)
        {
            if (current.Count == 0 || Math.Abs(center - current[^1]) <= tolerance)
            {
                current.Add(center);
                continue;
            }

            clusters.Add(current.Average());
            current.Clear();
            current.Add(center);
        }

        if (current.Count > 0)
        {
            clusters.Add(current.Average());
        }

        return clusters.ToArray();
    }

    private static int GetQrDimension(int xCount, int yCount)
    {
        if (xCount == yCount && IsQrDimension(xCount))
        {
            return xCount;
        }

        int average = (xCount + yCount) / 2;
        for (int dimension = 21; dimension <= MaximumQrDimension; dimension += 4)
        {
            if (Math.Abs(dimension - average) <= 1)
            {
                return dimension;
            }
        }

        return 0;
    }

    private static IReadOnlyList<int> GetCandidateQrDimensions(double[] xClusters, double[] yClusters)
    {
        List<int> candidates = new();
        AddCandidateDimension(candidates, GetQrDimension(xClusters.Length, yClusters.Length));
        AddCandidateDimension(candidates, EstimateDimensionFromPitch(xClusters));
        AddCandidateDimension(candidates, EstimateDimensionFromPitch(yClusters));

        int averageCount = (xClusters.Length + yClusters.Length) / 2;
        return Enumerable.Range(0, (MaximumQrDimension - 21) / 4 + 1)
            .Select(index => 21 + index * 4)
            .OrderBy(dimension => candidates.Contains(dimension) ? 0 : 1)
            .ThenBy(dimension => Math.Abs(dimension - averageCount))
            .ToArray();
    }

    private static void AddCandidateDimension(List<int> candidates, int dimension)
    {
        if (IsQrDimension(dimension) && !candidates.Contains(dimension))
        {
            candidates.Add(dimension);
        }
    }

    private static int EstimateDimensionFromPitch(double[] clusters)
    {
        if (clusters.Length < 8)
        {
            return 0;
        }

        double[] gaps = clusters
            .Zip(clusters.Skip(1), (left, right) => right - left)
            .Where(gap => gap > 1)
            .Order()
            .ToArray();
        if (gaps.Length == 0)
        {
            return 0;
        }

        double pitch = gaps[gaps.Length / 2];
        int estimated = (int)Math.Round((clusters.Last() - clusters.First()) / pitch) + 1;
        int nearest = 0;
        int nearestDistance = int.MaxValue;
        for (int dimension = 21; dimension <= MaximumQrDimension; dimension += 4)
        {
            int distance = Math.Abs(dimension - estimated);
            if (distance < nearestDistance)
            {
                nearest = dimension;
                nearestDistance = distance;
            }
        }

        return nearestDistance <= 3 ? nearest : 0;
    }

    private static bool IsQrDimension(int dimension)
    {
        return dimension >= 21 &&
            dimension <= MaximumQrDimension &&
            (dimension - 21) % 4 == 0;
    }

    private static double[] GetGridCenters(double[] clusters, int dimension)
    {
        if (clusters.Length == dimension)
        {
            return clusters;
        }

        if (clusters.Length < 2)
        {
            return [];
        }

        double first = clusters.First();
        double pitch = (clusters.Last() - first) / (dimension - 1);
        return Enumerable.Range(0, dimension)
            .Select(index => first + pitch * index)
            .ToArray();
    }

    private static int[] CreateIntegralMask(byte[] mask, int width, int height)
    {
        int[] integral = new int[(width + 1) * (height + 1)];
        for (int y = 0; y < height; y++)
        {
            int rowSum = 0;
            for (int x = 0; x < width; x++)
            {
                rowSum += mask[y * width + x];
                int integralIndex = (y + 1) * (width + 1) + x + 1;
                integral[integralIndex] = integral[integralIndex - width - 1] + rowSum;
            }
        }

        return integral;
    }

    private static bool[,] BuildModuleMatrix(
        int[] integral,
        int width,
        int height,
        double[] gridX,
        double[] gridY)
    {
        int dimension = gridX.Length;
        bool[,] modules = new bool[dimension, dimension];
        int radius = Math.Max(3, (int)Math.Round(Math.Min(GetAveragePitch(gridX), GetAveragePitch(gridY)) * 0.4));
        int threshold = Math.Max(8, radius * radius / 3);

        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                int centerX = (int)Math.Round(gridX[x]);
                int centerY = (int)Math.Round(gridY[y]);
                int count = CountMaskPixels(integral, width, height, centerX - radius, centerY - radius, centerX + radius, centerY + radius);
                modules[x, y] = count >= threshold;
            }
        }

        return modules;
    }

    private static int CountMaskPixels(
        int[] integral,
        int width,
        int height,
        int left,
        int top,
        int right,
        int bottom)
    {
        int x0 = Math.Clamp(left, 0, width - 1);
        int y0 = Math.Clamp(top, 0, height - 1);
        int x1 = Math.Clamp(right, 0, width - 1);
        int y1 = Math.Clamp(bottom, 0, height - 1);
        int stride = width + 1;
        return integral[(y1 + 1) * stride + x1 + 1] -
            integral[y0 * stride + x1 + 1] -
            integral[(y1 + 1) * stride + x0] +
            integral[y0 * stride + x0];
    }

    private static void ApplyStandardQrPatterns(bool[,] modules)
    {
        int dimension = modules.GetLength(0);
        ApplyFinderPattern(modules, 0, 0);
        ApplyFinderPattern(modules, dimension - 7, 0);
        ApplyFinderPattern(modules, 0, dimension - 7);
        ApplySeparators(modules);

        for (int index = 8; index < dimension - 8; index++)
        {
            bool value = index % 2 == 0;
            modules[index, 6] = value;
            modules[6, index] = value;
        }
    }

    private static void ApplyFinderPattern(bool[,] modules, int left, int top)
    {
        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                modules[left + x, top + y] =
                    x == 0 || x == 6 || y == 0 || y == 6 || (x is >= 2 and <= 4 && y is >= 2 and <= 4);
            }
        }
    }

    private static void ApplySeparators(bool[,] modules)
    {
        int dimension = modules.GetLength(0);
        for (int index = 0; index < 8; index++)
        {
            modules[7, index] = false;
            modules[index, 7] = false;
            modules[dimension - 8, index] = false;
            modules[dimension - 1 - index, 7] = false;
            modules[index, dimension - 8] = false;
            modules[7, dimension - 1 - index] = false;
        }
    }

    private static bool[,] RotateModules(bool[,] source, int clockwiseTurns)
    {
        int dimension = source.GetLength(0);
        bool[,] target = new bool[dimension, dimension];
        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                switch (clockwiseTurns)
                {
                    case 0:
                        target[x, y] = source[x, y];
                        break;
                    case 1:
                        target[dimension - 1 - y, x] = source[x, y];
                        break;
                    case 2:
                        target[dimension - 1 - x, dimension - 1 - y] = source[x, y];
                        break;
                    default:
                        target[y, dimension - 1 - x] = source[x, y];
                        break;
                }
            }
        }

        return target;
    }

    private static byte[] RenderModuleMatrix(bool[,] modules, int scale, out int width, out int height)
    {
        const int quietZone = 4;
        int dimension = modules.GetLength(0);
        width = (dimension + quietZone * 2) * scale;
        height = width;
        byte[] pixels = new byte[width * height * 4];
        Array.Fill<byte>(pixels, 255);

        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                if (!modules[x, y])
                {
                    continue;
                }

                int left = (x + quietZone) * scale;
                int top = (y + quietZone) * scale;
                for (int py = top; py < top + scale; py++)
                {
                    int offset = (py * width + left) * 4;
                    for (int px = 0; px < scale; px++)
                    {
                        pixels[offset] = 0;
                        pixels[offset + 1] = 0;
                        pixels[offset + 2] = 0;
                        pixels[offset + 3] = 255;
                        offset += 4;
                    }
                }
            }
        }

        return pixels;
    }

    private static double GetAveragePitch(double[] centers)
    {
        return centers.Length < 2
            ? 1
            : (centers.Last() - centers.First()) / (centers.Length - 1);
    }

    private sealed record Component(
        double CenterX,
        double CenterY,
        int Area,
        int Width,
        int Height,
        int MinX,
        int MinY,
        int MaxX,
        int MaxY);

    private sealed record ScoredRegion(PixelRect Region, int ComponentCount, double Density);

    private sealed record PixelRect(int Left, int Top, int Width, int Height);
}
