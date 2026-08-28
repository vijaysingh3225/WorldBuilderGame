param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [int] $OutputSize = 4096,
    [int] $Overlap = 160
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing.Common
$drawingAssemblies = @(
    [System.Drawing.Bitmap].Assembly.Location,
    [System.Drawing.Rectangle].Assembly.Location,
    (Join-Path (Split-Path -Parent ([System.Drawing.Bitmap].Assembly.Location)) "System.Private.Windows.GdiPlus.dll"),
    (Join-Path (Split-Path -Parent ([System.Drawing.Bitmap].Assembly.Location)) "System.Private.Windows.Core.dll")
)
Add-Type -ReferencedAssemblies $drawingAssemblies -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ColumnBladeTextureMasterBuilder
{
    private sealed class Tile : IDisposable
    {
        public readonly Bitmap Bitmap;
        public readonly byte[] Pixels;
        public readonly int Stride;
        public readonly double[] Mean;

        public Tile(string path)
        {
            Bitmap = new Bitmap(path);
            Rectangle bounds = new Rectangle(0, 0, Bitmap.Width, Bitmap.Height);
            BitmapData data = Bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                Stride = data.Stride;
                Pixels = new byte[Stride * Bitmap.Height];
                Marshal.Copy(data.Scan0, Pixels, 0, Pixels.Length);
            }
            finally
            {
                Bitmap.UnlockBits(data);
            }

            Mean = new double[3];
            long samples = 0;
            for (int y = 0; y < Bitmap.Height; y += 8)
            {
                for (int x = 0; x < Bitmap.Width; x += 8)
                {
                    int offset = y * Stride + x * 4;
                    Mean[0] += Pixels[offset];
                    Mean[1] += Pixels[offset + 1];
                    Mean[2] += Pixels[offset + 2];
                    samples++;
                }
            }
            for (int channel = 0; channel < 3; channel++)
                Mean[channel] /= samples;
        }

        public void Dispose() => Bitmap.Dispose();
    }

    public static void Build(string[] paths, string outputPath, int outputSize, int overlap)
    {
        if (paths == null || paths.Length != 4)
            throw new ArgumentException("Exactly four source quadrants are required.");

        Tile[] tiles = new Tile[4];
        try
        {
            for (int index = 0; index < 4; index++)
                tiles[index] = new Tile(paths[index]);

            int tileSize = tiles[0].Bitmap.Width;
            if (tiles[0].Bitmap.Height != tileSize)
                throw new InvalidDataException("Source quadrants must be square.");
            for (int index = 1; index < 4; index++)
            {
                if (tiles[index].Bitmap.Width != tileSize || tiles[index].Bitmap.Height != tileSize)
                    throw new InvalidDataException("All source quadrants must have identical dimensions.");
            }
            if (overlap <= 0 || overlap >= tileSize / 2)
                throw new ArgumentOutOfRangeException("overlap");

            double[] targetMean = new double[3];
            for (int channel = 0; channel < 3; channel++)
            {
                for (int index = 0; index < 4; index++)
                    targetMean[channel] += tiles[index].Mean[channel];
                targetMean[channel] /= 4.0;
            }

            int step = tileSize - overlap;
            int rawSize = tileSize * 2 - overlap;
            byte[] output = new byte[rawSize * rawSize * 4];
            for (int y = 0; y < rawSize; y++)
            {
                float bottomWeight = BlendWeight(y, step, tileSize);
                float topWeight = 1f - bottomWeight;
                int topY = Math.Min(y, tileSize - 1);
                int bottomY = Math.Max(0, y - step);

                for (int x = 0; x < rawSize; x++)
                {
                    float rightWeight = BlendWeight(x, step, tileSize);
                    float leftWeight = 1f - rightWeight;
                    int leftX = Math.Min(x, tileSize - 1);
                    int rightX = Math.Max(0, x - step);
                    float[] weights =
                    {
                        leftWeight * topWeight,
                        rightWeight * topWeight,
                        leftWeight * bottomWeight,
                        rightWeight * bottomWeight
                    };
                    int[] sampleX = { leftX, rightX, leftX, rightX };
                    int[] sampleY = { topY, topY, bottomY, bottomY };
                    int destination = (y * rawSize + x) * 4;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        double value = 0.0;
                        for (int index = 0; index < 4; index++)
                        {
                            int source = sampleY[index] * tiles[index].Stride + sampleX[index] * 4 + channel;
                            double normalized = tiles[index].Pixels[source] +
                                (targetMean[channel] - tiles[index].Mean[channel]);
                            value += weights[index] * Math.Max(0.0, Math.Min(255.0, normalized));
                        }
                        output[destination + channel] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
                    }
                    output[destination + 3] = 255;
                }
            }

            using (Bitmap raw = new Bitmap(rawSize, rawSize, PixelFormat.Format32bppArgb))
            {
                Rectangle bounds = new Rectangle(0, 0, rawSize, rawSize);
                BitmapData data = raw.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    Marshal.Copy(output, 0, data.Scan0, output.Length);
                }
                finally
                {
                    raw.UnlockBits(data);
                }

                using (Bitmap final = new Bitmap(outputSize, outputSize, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(final))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.DrawImage(raw, new Rectangle(0, 0, outputSize, outputSize));
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    final.Save(outputPath, ImageFormat.Png);
                }
            }
        }
        finally
        {
            foreach (Tile tile in tiles)
                if (tile != null)
                    tile.Dispose();
        }
    }

    private static float BlendWeight(int coordinate, int step, int tileSize)
    {
        if (coordinate < step)
            return 0f;
        if (coordinate >= tileSize)
            return 1f;
        float t = (coordinate - step) / (float)(tileSize - step);
        return t * t * (3f - 2f * t);
    }
}
'@

$materials = @("Stone", "Wood", "Obsidian")
foreach ($material in $materials) {
    $sources = 1..4 | ForEach-Object {
        Join-Path $SourceDirectory "$material-$_.png"
    }
    foreach ($source in $sources) {
        if (-not (Test-Path -LiteralPath $source)) {
            throw "Missing source quadrant: $source"
        }
    }
    $destination = Join-Path $OutputDirectory "ColumnBlade$material.png"
    [ColumnBladeTextureMasterBuilder]::Build(
        $sources,
        $destination,
        $OutputSize,
        $Overlap)
    Write-Output $destination
}
