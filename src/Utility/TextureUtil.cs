using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using static VintageCanvas.src.Entities.BlockEntityEasel;

namespace VintageCanvas.src.Utility
{
    public static class TextureUtil
    {
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
        public static int BlendColor(int src, int dst, float alpha)
        {
            int sr = (src >> 16) & 0xFF, sg = (src >> 8) & 0xFF, sb = src & 0xFF;
            int dr = (dst >> 16) & 0xFF, dg = (dst >> 8) & 0xFF, db = dst & 0xFF;
            int r = (int)(sr * alpha + dr * (1 - alpha));
            int g = (int)(sg * alpha + dg * (1 - alpha));
            int b = (int)(sb * alpha + db * (1 - alpha));
            return (255 << 24) | (r << 16) | (g << 8) | b;
        }

        public static MeshData SwapPaintingTexture(int[] pixeldata, int textureSize, AssetLocation texLoc, ITexPositionSource defaultSrc, AssetLocation code, CompositeShape shape, ICoreClientAPI capi)
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
    }
}
