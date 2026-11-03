namespace VibeCode.MdEditor.Core.Services;

internal static class ImageSizeReader
{
    public static (int Width, int Height)? TryRead(byte[] data)
    {
        try
        {
            if (data.Length >= 24 &&
                data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            {
                return (ReadInt32BigEndian(data, 16), ReadInt32BigEndian(data, 20));
            }

            if (data.Length >= 10 &&
                data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F')
            {
                return (ReadUInt16LittleEndian(data, 6), ReadUInt16LittleEndian(data, 8));
            }

            if (data.Length >= 26 && data[0] == (byte)'B' && data[1] == (byte)'M')
            {
                var width = Math.Abs(ReadInt32LittleEndian(data, 18));
                var height = Math.Abs(ReadInt32LittleEndian(data, 22));
                return width > 0 && height > 0 ? (width, height) : null;
            }

            if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
            {
                return ReadJpegSize(data);
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    private static (int Width, int Height)? ReadJpegSize(byte[] data)
    {
        var offset = 2;
        while (offset + 9 < data.Length)
        {
            if (data[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            var marker = data[offset + 1];
            if (marker is 0xD8 or 0xD9 or 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                offset += 2;
                continue;
            }

            if (marker == 0xDA || marker == 0x00)
            {
                break;
            }

            var length = (data[offset + 2] << 8) | data[offset + 3];
            if (length < 2 || offset + length + 1 >= data.Length)
            {
                break;
            }

            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or
                0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                var height = (data[offset + 5] << 8) | data[offset + 6];
                var width = (data[offset + 7] << 8) | data[offset + 8];
                return width > 0 && height > 0 ? (width, height) : null;
            }

            offset += 2 + length;
        }

        return null;
    }

    private static int ReadInt32BigEndian(byte[] data, int offset)
    {
        return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }

    private static int ReadInt32LittleEndian(byte[] data, int offset)
    {
        return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
    }

    private static int ReadUInt16LittleEndian(byte[] data, int offset)
    {
        return data[offset] | (data[offset + 1] << 8);
    }
}
