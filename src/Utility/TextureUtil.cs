using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using static VintageCanvas.src.Entities.BlockEntityEasel;
using Scrtwpns.Mixbox;

namespace VintageCanvas.src.Utility
{
    public static class TextureUtil
    {
        //items which trigger ee.PaintPixels() when right-clicked
        static public string[] paintingTools = [
                "game:charcoal",
                "game:stone-limestone",
                "game:stone-chalk",
                "vintagecanvas:brush-small",
                "vintagecanvas:brush-medium",
                "vintagecanvas:brush-large"
            ];

        static public Dictionary<string, Vec2i[]> brushPatterns = new Dictionary<string, Vec2i[]> {
            {  "small", new Vec2i[] { new Vec2i(0,0) } },
            {  "medium", new Vec2i[] { new Vec2i(-1,-1), new Vec2i(0,-1), new Vec2i(1, -1), 
                new Vec2i(-1, 0), new Vec2i(0, 0), new Vec2i(1, 0), 
                new Vec2i(-1, 1), new Vec2i(0, 1), new Vec2i(1, 1) } },          
            {  "large", new Vec2i[] { new Vec2i(-1, -2), new Vec2i(0, -2), new Vec2i(1, -2), 
                new Vec2i(-2, -1), new Vec2i(-1, -1), new Vec2i(0, -1), new Vec2i(1, -1), new Vec2i(2, -1),
                new Vec2i(-2, 0), new Vec2i(-1, 0), new Vec2i(0, 0), new Vec2i(1, 0), new Vec2i(2, 0),
                new Vec2i(-2, 1), new Vec2i(-1, 1), new Vec2i(0, 1), new Vec2i(1, 1), new Vec2i(2, 1),
                new Vec2i(-1, 2), new Vec2i(0, 2), new Vec2i(1, 2) } }
            };

        static public Dictionary<String, int> PaintColors = new Dictionary<String, int>{
                    {"vermillion", -3669996},
                    {"ultramarine", -15466296 },
                    {"redochre", -6927317 },
                    {"woad", -13225332 },
                    {"leadtin", -199595 },
                    {"yellowochre", -5339583 },
                    {"organicgreen", -10582992 },
                    {"malachite", -14241530 },
                    {"carbonblack", -16119286 },
                    {"chalkwhite", -4080464 },
                    {"leadwhite", -1184275 }
                };
        public static int BlendWithMixing(int basepixel, int overlaypixel, float alpha)
        {
            int sr = (basepixel >> 16) & 0xFF, sg = (basepixel >> 8) & 0xFF, sb = basepixel & 0xFF;
            int dr = (overlaypixel >> 16) & 0xFF, dg = (overlaypixel >> 8) & 0xFF, db = overlaypixel & 0xFF;

            Color color1 = Color.FromArgb(sr, sg, sb);
            Color color2 = Color.FromArgb(dr, dg, db);
            Color colorMix = Color.FromArgb(Mixbox.Lerp(color1.ToArgb(), color2.ToArgb(), 1f - alpha));

            return (255 << 24) | (colorMix.R << 16) | (colorMix.G << 8) | colorMix.B;
        }

        public static int BlendColor(int basepixel, int overlaypixel, float alpha)
        {
            if (VintageCanvasModSystem.config.MixBoxBlending)
            {
                return BlendWithMixing(basepixel, overlaypixel, alpha);
            }
            else
            {
                int sr = (basepixel >> 16) & 0xFF, sg = (basepixel >> 8) & 0xFF, sb = basepixel & 0xFF;
                int dr = (overlaypixel >> 16) & 0xFF, dg = (overlaypixel >> 8) & 0xFF, db = overlaypixel & 0xFF;
                int r = (int)(sr * alpha + dr * (1 - alpha));
                int g = (int)(sg * alpha + dg * (1 - alpha));
                int b = (int)(sb * alpha + db * (1 - alpha));
                return (255 << 24) | (r << 16) | (g << 8) | b;
            }
        }

        public static float[] ARGBtoRGBA(int argb)
        {
            float r = (float)(((argb >> 16) & 0xFF) / 255f);
            float g = (float)(((argb >> 8) & 0xFF) / 255f);
            float b = (float)((argb & 0xFF) / 255f);
            float a = (float)(((argb >> 24) & 0xFF) / 255f);
            float[] rgba = { r, g, b, a };
            return rgba;
        }

        public static TextureAtlasPosition SwapPaintingTexture(int[] pixeldata, int textureSize, AssetLocation texLoc, ICoreClientAPI capi)
        {
            TextureAtlasPosition TexPos = new TextureAtlasPosition();
            ClientMain main = capi.World as ClientMain;
            ClientPlatformAbstract platform = main.Platform;

            bool textureCreated = false;
            capi.BlockTextureAtlas.GetOrInsertTexture(
                        texLoc,
                        out int _,
                        out TexPos,
                        () =>
                        {
                            textureCreated = true;
                            return platform.CreateBitmapFromPixels(pixeldata, textureSize, textureSize);
                        }, 0.005f
                    );

            if (!textureCreated)
            {
                capi.Event.EnqueueMainThreadTask(() =>
                {
                    LoadedTexture loadedTex = new LoadedTexture(capi);
                    loadedTex.Width = textureSize;
                    loadedTex.Height = textureSize;
                    capi.Render.LoadTexture(platform.CreateBitmapFromPixels(pixeldata, textureSize, textureSize), ref loadedTex);

                    capi.BlockTextureAtlas.RenderTextureIntoAtlas(
                        capi.BlockTextureAtlas.AtlasTextures[TexPos.atlasNumber].TextureId,
                        loadedTex,
                        0, 0, textureSize, textureSize,
                        TexPos.x1 * capi.BlockTextureAtlas.Size.Width,
                        TexPos.y1 * capi.BlockTextureAtlas.Size.Height,
                        -1f
                        );
                    loadedTex.Dispose();
                }, "canvasatlasupdater");
            }

            return TexPos;
        }

        public static MeshData SwapPaintingTextureMesh(int[] pixeldata, int textureSize, AssetLocation texLoc, ITexPositionSource defaultSrc, AssetLocation code, CompositeShape shape, ICoreClientAPI capi)
        {
            TextureAtlasPosition TexPos = new TextureAtlasPosition();
            ClientMain main = capi.World as ClientMain;
            ClientPlatformAbstract platform = main.Platform;

            bool textureCreated = false;
            capi.BlockTextureAtlas.GetOrInsertTexture(
                        texLoc,
                        out int _,
                        out TexPos,
                        () =>
                        {
                            textureCreated = true;
                            return platform.CreateBitmapFromPixels(pixeldata, textureSize, textureSize);
                        }, 0.005f
                    );

            if (!textureCreated)
            {
                capi.Event.EnqueueMainThreadTask(() =>
                {
                    LoadedTexture loadedTex = new LoadedTexture(capi);
                    loadedTex.Width = textureSize;
                    loadedTex.Height = textureSize;
                    capi.Render.LoadTexture(platform.CreateBitmapFromPixels(pixeldata, textureSize, textureSize), ref loadedTex);

                    capi.BlockTextureAtlas.RenderTextureIntoAtlas(
                        capi.BlockTextureAtlas.AtlasTextures[TexPos.atlasNumber].TextureId,
                        loadedTex,
                        0, 0, textureSize, textureSize,
                        TexPos.x1 * capi.BlockTextureAtlas.Size.Width,
                        TexPos.y1 * capi.BlockTextureAtlas.Size.Height,
                        -1f
                        );
                    loadedTex.Dispose();
                }, "canvasatlasupdater");
            }

            //ITexPositionSource defaultSrc = capi.Tesselator.GetTextureSource(itemstack.Item);
            var paintingSrc = new PaintingTexSource(defaultSrc, TexPos, capi.BlockTextureAtlas.Size);
            string shapestring = texLoc.ToString();

            capi.Tesselator.TesselateShape(
                    shapestring,
                    code,
                    shape,
                    out MeshData mesh,
                    paintingSrc
                );

            return mesh;
        }
        public class PaintingTexSource : ITexPositionSource
        {
            private readonly ITexPositionSource defaultSrc;
            private readonly TextureAtlasPosition paintingPos;
            private readonly Size2i atlasSize;

            public PaintingTexSource(
                ITexPositionSource defaultSrc,
                TextureAtlasPosition paintingPos,
                Size2i atlasSize)
            {
                this.defaultSrc = defaultSrc;
                this.paintingPos = paintingPos;
                this.atlasSize = atlasSize;
            }

            public TextureAtlasPosition this[string textureCode] =>
                textureCode == "painting" ? paintingPos : defaultSrc[textureCode];

            public Size2i AtlasSize => atlasSize;
        }
        public static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
                gzip.Write(data, 0, data.Length);
            return output.ToArray();
        }
        public static byte[] Decompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            try
            {
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    gzip.CopyTo(output);
            }
            catch
            {
                return data;
            }
            return output.ToArray();
        }

        //Handles legacy cases where old paintings were stored uncompressed
        public static int[] ReadCompressedPixelData(byte[] pixelbytes)
        {
            int[] pixeldata;
            try
            {
                pixeldata = SerializerUtil.Deserialize<int[]>(TextureUtil.Decompress(
                    pixelbytes));
            }
            catch
            {
                pixeldata = SerializerUtil.Deserialize<int[]>(
                    pixelbytes);
            }

            return pixeldata;
        }

        public static byte[] WriteCompressedPixelData(int[] pixeldata)
        {
            return TextureUtil.Compress(SerializerUtil.Serialize(pixeldata));
        }

        public static int GetByteHashCode(byte[] key)
        {
            if (key == null) return 0;
            unchecked
            {
                int result = 0;
                foreach (byte b in key)
                    result = (result * 31) ^ b;
                return result;
            }
        }
    }
}
