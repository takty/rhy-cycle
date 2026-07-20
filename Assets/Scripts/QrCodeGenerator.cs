using UnityEngine;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

public static class QrCodeGenerator
{
    public static Texture2D Generate(string text, int size = 256)
    {
        var writer = new QRCodeWriter();

        BitMatrix matrix = writer.encode(
            text,
            BarcodeFormat.QR_CODE,
            size,
            size,
            new System.Collections.Generic.Dictionary<EncodeHintType, object>
            {
                { EncodeHintType.MARGIN, 1 },
            }
        );

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = (size - 1 - y) * size + x;

                pixels[index] = matrix[x, y]
                    ? new Color32(0, 0, 0, 255)
                    : new Color32(255, 255, 255, 255);
            }
        }

        var texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false
        );

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();

        return texture;
    }
}
