using NekoHub.Application.Abstractions.Metadata;
using System.Buffers.Binary;

namespace NekoHub.Infrastructure.Metadata;

public sealed class BasicAssetMetadataExtractor : IAssetMetadataExtractor
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] Gif87a = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89a = "GIF89a"u8.ToArray();
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();
    private static readonly byte[] Ihdr = "IHDR"u8.ToArray();
    private static readonly byte[] Vp8X = "VP8X"u8.ToArray();
    private static readonly byte[] Vp8 = "VP8 "u8.ToArray();
    private static readonly byte[] Vp8L = "VP8L"u8.ToArray();

    public Task<ExtractedAssetMetadata> ExtractAsync(
        Stream content,
        string? originalFileName,
        string? declaredContentType,
        CancellationToken cancellationToken = default)
    {
        long? size = content.CanSeek ? content.Length : null;
        string? extension = NormalizeExtension(originalFileName);
        var (width, height) = TryExtractImageDimensions(content, declaredContentType, extension);

        return Task.FromResult(new ExtractedAssetMetadata(
            ContentType: declaredContentType,
            Size: size,
            Width: width,
            Height: height,
            Extension: extension,
            ChecksumSha256: null));
    }

    private static string? NormalizeExtension(string? originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        return string.IsNullOrWhiteSpace(extension) ? null : extension.ToLowerInvariant();
    }

    private static (int? Width, int? Height) TryExtractImageDimensions(
        Stream content,
        string? declaredContentType,
        string? extension)
    {
        if (!IsImage(declaredContentType, extension) || !content.CanSeek)
        {
            return (null, null);
        }

        var originalPosition = content.Position;
        try
        {
            content.Position = 0;

            return TryReadPngDimensions(content, out var pngWidth, out var pngHeight)
                || TryReadJpegDimensions(content, out pngWidth, out pngHeight)
                || TryReadGifDimensions(content, out pngWidth, out pngHeight)
                || TryReadWebpDimensions(content, out pngWidth, out pngHeight)
                ? (pngWidth, pngHeight)
                : (null, null);
        }
        catch
        {
            return (null, null);
        }
        finally
        {
            content.Position = originalPosition;
        }
    }

    private static bool IsImage(string? declaredContentType, string? extension)
    {
        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            declaredContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";
    }

    private static bool TryReadPngDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        stream.Position = 0;

        Span<byte> header = stackalloc byte[24];
        if (!TryReadExactly(stream, header))
        {
            return false;
        }

        if (!header[..8].SequenceEqual(PngSignature))
        {
            return false;
        }

        if (!header[12..16].SequenceEqual(Ihdr))
        {
            return false;
        }

        width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        return width > 0 && height > 0;
    }

    private static bool TryReadGifDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        stream.Position = 0;

        Span<byte> header = stackalloc byte[10];
        if (!TryReadExactly(stream, header))
        {
            return false;
        }

        if (!header[..6].SequenceEqual(Gif87a) && !header[..6].SequenceEqual(Gif89a))
        {
            return false;
        }

        width = BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]);
        height = BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);
        return width > 0 && height > 0;
    }

    private static bool TryReadJpegDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        stream.Position = 0;

        Span<byte> start = stackalloc byte[2];
        if (!TryReadExactly(stream, start) || start[0] != 0xFF || start[1] != 0xD8)
        {
            return false;
        }

        Span<byte> markerLengthBuffer = stackalloc byte[2];
        while (stream.Position < stream.Length)
        {
            var markerPrefix = stream.ReadByte();
            if (markerPrefix < 0)
            {
                return false;
            }

            if (markerPrefix != 0xFF)
            {
                continue;
            }

            int marker;
            do
            {
                marker = stream.ReadByte();
                if (marker < 0)
                {
                    return false;
                }
            } while (marker == 0xFF);

            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                continue;
            }

            if (marker == 0xD9 || marker == 0xDA)
            {
                return false;
            }

            if (!TryReadExactly(stream, markerLengthBuffer))
            {
                return false;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(markerLengthBuffer);
            if (segmentLength < 2)
            {
                return false;
            }

            var segmentPayloadLength = segmentLength - 2;
            if (IsSofMarker(marker))
            {
                if (segmentPayloadLength < 5)
                {
                    return false;
                }

                Span<byte> sof = stackalloc byte[5];
                if (!TryReadExactly(stream, sof))
                {
                    return false;
                }

                height = BinaryPrimitives.ReadUInt16BigEndian(sof[1..3]);
                width = BinaryPrimitives.ReadUInt16BigEndian(sof[3..5]);
                return width > 0 && height > 0;
            }

            if (!SkipBytes(stream, segmentPayloadLength))
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsSofMarker(int marker)
    {
        return marker is >= 0xC0 and <= 0xC3
            or >= 0xC5 and <= 0xC7
            or >= 0xC9 and <= 0xCB
            or >= 0xCD and <= 0xCF;
    }

    private static bool TryReadWebpDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        stream.Position = 0;

        Span<byte> riffHeader = stackalloc byte[12];
        if (!TryReadExactly(stream, riffHeader))
        {
            return false;
        }

        if (!riffHeader[..4].SequenceEqual(Riff) || !riffHeader[8..12].SequenceEqual(Webp))
        {
            return false;
        }

        Span<byte> chunkHeader = stackalloc byte[8];
        while (stream.Position + 8 <= stream.Length)
        {
            if (!TryReadExactly(stream, chunkHeader))
            {
                return false;
            }

            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(chunkHeader[4..8]);
            if (chunkSize < 0)
            {
                return false;
            }

            var chunkType = chunkHeader[..4];

            if (chunkType.SequenceEqual(Vp8X))
            {
                Span<byte> vp8x = stackalloc byte[10];
                if (chunkSize < vp8x.Length || !TryReadExactly(stream, vp8x))
                {
                    return false;
                }

                width = 1 + vp8x[4] + (vp8x[5] << 8) + (vp8x[6] << 16);
                height = 1 + vp8x[7] + (vp8x[8] << 8) + (vp8x[9] << 16);
                return width > 0 && height > 0;
            }

            if (chunkType.SequenceEqual(Vp8))
            {
                Span<byte> vp8 = stackalloc byte[10];
                if (chunkSize < vp8.Length || !TryReadExactly(stream, vp8))
                {
                    return false;
                }

                width = BinaryPrimitives.ReadUInt16LittleEndian(vp8[6..8]) & 0x3FFF;
                height = BinaryPrimitives.ReadUInt16LittleEndian(vp8[8..10]) & 0x3FFF;
                return width > 0 && height > 0;
            }

            if (chunkType.SequenceEqual(Vp8L))
            {
                Span<byte> vp8l = stackalloc byte[5];
                if (chunkSize < vp8l.Length || !TryReadExactly(stream, vp8l))
                {
                    return false;
                }

                if (vp8l[0] != 0x2F)
                {
                    return false;
                }

                width = 1 + (vp8l[1] | ((vp8l[2] & 0x3F) << 8));
                height = 1 + (((vp8l[2] & 0xC0) >> 6) | (vp8l[3] << 2) | ((vp8l[4] & 0x0F) << 10));
                return width > 0 && height > 0;
            }

            if (!SkipBytes(stream, chunkSize))
            {
                return false;
            }

            // RIFF chunk 按 2-byte 对齐，奇数字节需要补齐一个 padding byte。
            if ((chunkSize & 1) == 1 && !SkipBytes(stream, 1))
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryReadExactly(Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);
            if (read <= 0)
            {
                return false;
            }

            totalRead += read;
        }

        return true;
    }

    private static bool SkipBytes(Stream stream, int length)
    {
        if (length < 0)
        {
            return false;
        }

        if (stream.CanSeek)
        {
            if (stream.Position + length > stream.Length)
            {
                return false;
            }

            stream.Position += length;
            return true;
        }

        Span<byte> skipBuffer = stackalloc byte[512];
        var remaining = length;
        while (remaining > 0)
        {
            var toRead = Math.Min(skipBuffer.Length, remaining);
            var read = stream.Read(skipBuffer[..toRead]);
            if (read <= 0)
            {
                return false;
            }

            remaining -= read;
        }

        return true;
    }
}
