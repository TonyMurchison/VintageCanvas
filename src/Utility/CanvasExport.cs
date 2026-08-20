using System;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using VintageCanvas.src.Blocks;
using VintageCanvas.src.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.Util;
using System.Runtime.CompilerServices;
using Cairo.Freetype;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using VintageCanvas.src.Items;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;

namespace VintageCanvas.src.Utility
{
    internal class CanvasExport : ModSystem
    {
        
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            api.ChatCommands.Create("vcexport")
                .WithDescription("Exports the currently highlighted painting to the screenshot folder")
                .RequiresPlayer()
                .HandleWith((args) =>
                {
                    var byEntity = args.Caller.Entity;
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    BlockSelection target = byPlayer.CurrentBlockSelection;

                    if(target.Block == null)
                    {
                        return TextCommandResult.Error("Not currently looking at a painting");
                    }

                    if (target.Block is Blocks.BlockCanvas || target.Block is BlockMultiblock || target.Block is BlockEasel)
                    {
                        int[] pixeldata = GetPixelsFromSelection(target, api);
                        BlockEntityCanvas bec = api.World.BlockAccessor.GetBlockEntity<BlockEntityCanvas>(target.Position);

                        int[] textureSizes = GetCanvasSizes(target, api);
                        if (textureSizes == null) return TextCommandResult.Error("Export failed"); ;
 
                        SavePainting(api as ICoreClientAPI, pixeldata, textureSizes[0], textureSizes[1]);
                    }

                    else
                    {
                        TextCommandResult.Error("Not currently looking at a painting");
                    }

                    return TextCommandResult.Success("Painting exported to screenshot folder");
                });
        }
        public void SavePainting(ICoreClientAPI capi, int[] pixels, int textureSizeX, int textureSizeY)
        {
            if (pixels == null) return;
            int[] trimmedPixels = TrimPixels(pixels, textureSizeX, textureSizeY);
            if (trimmedPixels == null) return;
            
            System.Drawing.Bitmap paintingImage = new System.Drawing.Bitmap(textureSizeX, textureSizeY);
            using Graphics g = Graphics.FromImage(paintingImage);
            g.Clear(Color.White);

            for (int i = 0; i < trimmedPixels.Length; i++) {
                Color color = Color.FromArgb(trimmedPixels[i]);
                paintingImage.SetPixel(i % textureSizeX, i / textureSizeX, color);
            }

            string folder = GamePaths.Screenshots;
            string timestamp = DateTime.Now.ToFileTime().ToString();
            string filename = System.IO.Path.Combine(folder, "VC_Painting_" + timestamp + ".png");
            paintingImage.Save(filename, System.Drawing.Imaging.ImageFormat.Png);        
        }

        private int[] TrimPixels(int[] pixels, int textureSizeX, int textureSizeY)
        {
            //cut off all data that's not part of the visible canvas for truncated ratios
            if (textureSizeX == textureSizeY)
            {
                return pixels;
            }

            int xlim = 0; int ylim = 0;

            int maxsize = Math.Max(textureSizeY, textureSizeX);
            if (textureSizeX < textureSizeY) xlim = maxsize / 8;
            if (textureSizeX > textureSizeY) ylim = maxsize / 8;

            List<int> trimmedpixels = new List<int>();


            for (int i = 0; i < pixels.Length; i++)
            {
                int x = i % maxsize; int y = i / maxsize;
                if (x >= xlim && x < maxsize - xlim
                    && y >= ylim && y < maxsize - ylim)
                {
                    trimmedpixels.Add(pixels[i]);
                }
            }                   

            return trimmedpixels.ToArray();

        }

        private int[] GetPixelsFromSelection(BlockSelection blockSel, ICoreClientAPI api)
        {
            BlockPos bp = blockSel.Position;

            if (blockSel.Block is BlockMultiblock)
            {
                BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                bp = blockSel.Clone().Position.Add(bm.OffsetInv);
            }

            if (api.World.BlockAccessor.GetBlock(bp) is Blocks.BlockCanvas) {
                BlockEntityCanvas bec = api.World.BlockAccessor.GetBlockEntity(bp) as BlockEntityCanvas;
                if (bec != null) return bec.pixeldata;
            }

            if (api.World.BlockAccessor.GetBlock(bp) is BlockEasel)
            {
                BlockEntityEasel bee = api.World.BlockAccessor.GetBlockEntity(bp) as BlockEntityEasel;
                if (bee != null) return bee.pixeldata;
            }

            return null;
        }

        private int[] GetCanvasSizes(BlockSelection blockSel, ICoreClientAPI api)
        {
            BlockPos bp = blockSel.Position;
            int canvasSize = 32;

            if (blockSel.Block is BlockMultiblock)
            {
                BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                bp = blockSel.Clone().Position.Add(bm.OffsetInv);
            }

            if (api.World.BlockAccessor.GetBlock(bp) is Blocks.BlockCanvas
                || api.World.BlockAccessor.GetBlock(bp) is BlockEasel)
            {
                Block baseblock = api.World.BlockAccessor.GetBlock(bp);
                int basesize = baseblock.Attributes["canvassize"].AsInt();
                switch (baseblock.Variant["ratio"]) {
                    case "landscape":
                        return [basesize, (int)(basesize * 0.75)];
                    case "portrait":
                        return [(int)(basesize * 0.75), basesize];
                    case "square":
                        return [basesize, basesize];
                    }
                switch (baseblock.Variant["canvas"])
                {
                    case "landscape":
                        return [basesize, (int)(basesize * 0.75)];
                    case "portrait":
                        return [(int)(basesize * 0.75), basesize];
                    case "square":
                        return [basesize, basesize];
                }
            }

            return null;

        }
    }
}
