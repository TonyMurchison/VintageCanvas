using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VintageCanvas.src.Blocks;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Items
{
    internal class CollectibleBehaviorPaintTool : CollectibleBehavior
    {
        private HashSet<int> changedpixels = new HashSet<int>();
        private Vec2d previousUV = null;
        public CollectibleBehaviorPaintTool(CollectibleObject collObj) : base(collObj) { }

        //All painting happens during OnHeldInteractStep. Attack steps redirect to their Interact equivalent
        #region ClickResponses

        //When something is tagged with this behaviour, redirect any attack on an easel to Easel.OnBlockInteractX();
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (blockSel != null) {
                if(blockSel.Block is BlockMicroBlock)
                {
                    this.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, true, ref handHandling, ref handling);
                    handling = EnumHandling.PreventDefault;
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }

                BlockEasel be;
                if (blockSel.Block is BlockMultiblock)
                {
                    BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                    BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                    be = (BlockEasel)byEntity.World.BlockAccessor.GetBlock(bp);

                    blockSel = blockSel.Clone();
                    blockSel.Position = bp;
                }
                else if (blockSel.Block is BlockEasel)
                {
                    be = (BlockEasel)blockSel.Block;
                }
                else
                {
                    be = null;
                }

                if (be != null)
                {
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    be.OnBlockInteractStart(byEntity.World, byPlayer, blockSel);
                    handling = EnumHandling.PreventDefault;
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }

                //Copy OnContainedInteract() for paint jars
                if (blockSel.Block is BlockGroundStorage)
                {
                    BlockEntityGroundStorage bgse = (BlockEntityGroundStorage)byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    bgse.OnPlayerInteractStart(byPlayer, blockSel);
                    handling = EnumHandling.PreventDefault;
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
            }
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handHandling, ref handling);
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel != null)
            {
                if (blockSel.Block is BlockMicroBlock)
                {
                    this.OnHeldInteractStep(secondsPassed, slot, byEntity, blockSel, entitySel, ref handling);
                    handling = EnumHandling.PreventDefault;
                    return true;
                }

                BlockEasel be = new();
                if (blockSel.Block is BlockMultiblock)
                {
                    BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                    BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                    be = (BlockEasel)byEntity.World.BlockAccessor.GetBlock(bp);

                    blockSel = blockSel.Clone();
                    blockSel.Position = bp;
                }
                else if (blockSel.Block is BlockEasel)
                {
                    be = (BlockEasel)blockSel.Block;
                }
                else
                {
                    be = null;
                }

                if (be != null)
                {
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    handling = EnumHandling.PreventDefault;
                    be.OnBlockInteractStep(secondsPassed, byEntity.World, byPlayer, blockSel);
                    return true;
                }
            }

            return base.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
        {
            if (blockSel != null)
            {
                if (blockSel.Block is BlockMicroBlock)
                {
                    handling = EnumHandling.Handled;
                    OnHeldInteractCancel(secondsPassed, slot, byEntity, blockSel, entitySel, 0, ref handling);
                    OnHeldInteractStop(secondsPassed, slot, byEntity, blockSel, entitySel, ref handling);

                    handling = EnumHandling.PreventSubsequent;
                    return true;
                }

                BlockEasel be = new();
                if (blockSel.Block is BlockMultiblock)
                {
                    BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                    BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                    be = (BlockEasel)byEntity.World.BlockAccessor.GetBlock(bp);

                    blockSel = blockSel.Clone();
                    blockSel.Position = bp;
                }
                else if (blockSel.Block is BlockEasel)
                {
                    be = (BlockEasel)blockSel.Block;
                }
                else
                {
                    be = null;
                }

                if (be != null)
                {
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
                    handling = EnumHandling.PreventDefault;
                    return be.OnBlockInteractCancel(secondsPassed, byEntity.World, byPlayer, blockSel, cancelReason);
                }
            }

            return base.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSel, entitySel, cancelReason, ref handling);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);

        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null || blockSel.Block is not BlockMicroBlock)
            {                
                return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
            }

            BlockMicroBlock bmb = blockSel.Block as BlockMicroBlock;
            BlockEntityMicroBlock bemb = (BlockEntityMicroBlock)byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);

            //TEMP
            int i = blockSel.Face.Index;
            string blockID = "vintagecanvasfresco" + blockSel.Position.ToString() + "-" + i;


            if (FrescoStore.Data.TryGetValue(blockID, out int[] pixeldata))
            {

                FrescoStore.Data[blockID] = ApplyTool(slot.Itemstack, blockID, pixeldata, blockSel);
                bemb.MarkDirty(true);
                bemb.MarkMeshDirty();
            }

            handling = EnumHandling.Handled;
            return true;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
        {
            changedpixels.Clear();
            previousUV = null;
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
        }

        #endregion

        #region ToolApplications
        
        public int[] ApplyBrush(ItemStack held, int[] pixeldata, Vec2i[] basepixels, int canvasSize, BlockSelection blockSel)
        {
            int? brushPaint = held.Attributes.GetAsInt("paintcolor");
            Vec2i[] brushPattern = TextureUtil.brushPatterns[held.Collectible.Variant["size"].ToString()];

            HashSet<int> pixels = new HashSet<int>();

            foreach (Vec2i basepixelvec in basepixels)
            {
                int basepixel = basepixelvec.Y * canvasSize + basepixelvec.X;
                foreach (Vec2i shift in brushPattern)
                {
                    //Check if X coordinate is not on other side of canvas
                    int x1 = basepixel % canvasSize;
                    int x2 = (basepixel + shift[0] + (shift[1] * canvasSize)) % canvasSize;

                    if (Math.Abs(x2 - x1) < 16)
                    {
                        pixels.Add(basepixel + shift[0] + (shift[1] * canvasSize));
                    }
                }
            }

            float opacity = 1;
            if (held.Attributes.HasAttribute("opacity"))
            {
                opacity = held.Attributes.GetFloat("opacity");
            }

            if (brushPaint != 0)
            {
                return PaintPixels(pixels.ToArray(), (int)brushPaint, opacity, held, pixeldata, canvasSize, blockSel);
            }
            else
            {
                return BlendPixels(pixels.ToArray(), held, pixeldata, canvasSize, blockSel);
            }
        }

        //Hashing pixel indices with their block coordinates, so that they don't overlap between blocks
        private int hashBlockPixel(BlockSelection blockSel, int pixel)
        {
            int hash = unchecked(pixel);
            hash += unchecked(blockSel.Position.X * 10000);
            hash += unchecked(blockSel.Position.Y * 1000000);
            hash += unchecked(blockSel.Position.Z * 100000000);
            return hash;
        }

        private int[] PaintPixels(int[] pixelindices, int color, float alpha, ItemStack held, int[] pixeldata, int canvasSize, BlockSelection blockSel)
        {
            if (VintageCanvasModSystem.config.PaintDepletion && held.Attributes.GetInt("paintamount") <= 0
                && held.Collectible.Code.PathStartsWith("brush"))
            {
                return pixeldata;
            }

            held.Attributes.GetInt("paintamount");

            int pixelspainted = 0;

            for (int i = 0; i < pixelindices.Length; i++)
            {
                if (0 <= pixelindices[i] && (int)Math.Pow(canvasSize, 2) > pixelindices[i])
                {
                    if (!changedpixels.Contains(hashBlockPixel(blockSel, pixelindices[i])))
                    {
                        int newColor = TextureUtil.BlendColor(color, pixeldata[pixelindices[i]], alpha);
                        pixeldata[pixelindices[i]] = newColor;

                        //TODO add blockPos to changedpixels for continuity
                        blockSel.Position.ToString();
                        changedpixels.Add(hashBlockPixel(blockSel, pixelindices[i]));
                        pixelspainted++;
                    }
                }
            }

            //Reduce brush quantity when toggled on
            if (VintageCanvasModSystem.config.PaintDepletion && held.Attributes.HasAttribute("paintamount"))
            {
                int pa = held.Attributes.GetInt("paintamount");
                held.Attributes.SetInt("paintamount", pa - pixelspainted);
            }

            return pixeldata;
        }

        private int[] BlendPixels(int[] pixelindices, ItemStack held, int[] pixeldata, int canvasSize, BlockSelection blockSel)
        {
            if (pixeldata == null) return pixeldata;
            //TODO for every pixel, 

            List<int> blendedColors = new();
            List<int> blendedIndices = new();
            for (int i = 0; i < pixelindices.Length; i++)
            {
                if (0 <= pixelindices[i] && (int)Math.Pow(canvasSize, 2) > pixelindices[i])
                {
                    if (!changedpixels.Contains(hashBlockPixel(blockSel, pixelindices[i])))
                    {
                        //test a radius(start with 2 ?), create an average rgb value for all those pixels, then BlendColor the pixel with that value
                        List<int> nearbypixels = new();
                        for (int j = -2; j < 3; j++)
                        {
                            for (int k = -2; k < 3; k++)
                            {
                                int p = pixelindices[i] + j + (k * canvasSize);
                                if (p > 0 && p < Math.Pow(canvasSize, 2) &&
                                    Math.Abs(p % canvasSize - pixelindices[i] % canvasSize) < 8)
                                {
                                    nearbypixels.Add(p);
                                }
                            }
                        }

                        blendedIndices.Add(pixelindices[i]);
                        blendedColors.Add(AverageColors(nearbypixels.ToArray(), pixeldata));
                        changedpixels.Add(hashBlockPixel(blockSel, pixelindices[i]));
                    }
                }
            }

            for (int i = 0; i < blendedIndices.Count; i++)
            {
                int newColor = TextureUtil.BlendColor(pixeldata[blendedIndices[i]], blendedColors[i], 0.5f);
                pixeldata[blendedIndices[i]] = newColor;
            }

            return pixeldata;
        }

        private int[] ApplyTool(ItemStack tool, string id, int[] pixeldata, BlockSelection blockSel)
        {
            Random rnd = new();

            //offset by half a pixel to improve accuracy
            double x = blockSel.HitPosition.X;
            double y = blockSel.HitPosition.Y;
            double z = blockSel.HitPosition.Z;
            int facing = blockSel.Face.Index;

            double xcoord = (facing) switch
            {
                (0) => 1 + x,
                (1) => 1 + z,
                (2) => 1 - x,
                (3) => 1 - z,
                (4) => x,
                (5) => x
            };
            double ycoord = (facing) switch
            {
                (0) => y - 1,
                (1) => y - 1,
                (2) => y - 1,
                (3) => y - 1,
                (4) => z - 1,
                (5) => -z
            };

            Vec2i[] interpolatedPixels = InterpolatePixels(previousUV, new Vec2d(xcoord, ycoord), new Vec2d(0, 0.5 / 32));

            if (tool.Collectible.Code.PathStartsWith("brush"))
            {
                pixeldata = ApplyBrush(tool, pixeldata, interpolatedPixels, 32, blockSel);
            }
            if (tool.Collectible.Code.PathStartsWith("charcoal"))
            {
                List<int> pixelcoords = new();
                foreach (Vec2i pixel in interpolatedPixels)
                {
                    pixelcoords.Add(pixel[0] + 32 * pixel[1]);
                }
                pixeldata = PaintPixels(pixelcoords.ToArray(), TextureUtil.PaintColors["carbonblack"], 0.5f, tool, pixeldata, 32, blockSel);
            }
            if (tool.Collectible.Code.EndVariant() == "limestone" || tool.Collectible.Code.EndVariant() == "chalk")
            {
                List<int> pixelcoords = new();
                foreach (Vec2i pixel in interpolatedPixels)
                {
                    pixelcoords.Add(pixel[0] + 32 * pixel[1]);
                }
                pixeldata = PaintPixels(pixelcoords.ToArray(), 936298687, 0.5f, tool, pixeldata, 32, blockSel);
            }

            return pixeldata;
            //FrescoStore.Data[id] = pixeldata;
        }

        private int AverageColors(int[] nearbypixels, int[] pixeldata)
        {
            if (nearbypixels.Length == 0)
            {
                return 0;
            }
            int r = 0; int g = 0; int b = 0;
            foreach (int pixel in nearbypixels)
            {
                r += (pixeldata[pixel] >> 16) & 0xFF;
                g += (pixeldata[pixel] >> 8) & 0xFF;
                b += (pixeldata[pixel]) & 0xFF;
            }
            r = r / nearbypixels.Length; g = g / nearbypixels.Length; b = b / nearbypixels.Length;

            return (255 << 24) | (r << 16) | (g << 8) | b;

        }

        private Vec2i[] InterpolatePixels(Vec2d? lastUV, Vec2d currentUV, Vec2d UVoffset)
        {
            previousUV = currentUV;
            //float yoffset = Attributes["uvyoffset"].AsFloat();
            //Vec2f UVoffset = new Vec2f(0.5f, yoffset);

            if (lastUV == null)
            {
                int xpixel = (int)((currentUV.X + UVoffset.X) * 32);
                int ypixel = (int)(-(currentUV.Y + UVoffset.Y) * 32);

                return [new Vec2i(xpixel, ypixel)];
            }

            Vec2d stroke = currentUV - lastUV;
            int divisionCount = (int)Math.Ceiling(stroke.Length() * 50f);

            //Safeguard against messing up during laggy strokes / failed stroke termination
            if (divisionCount > 20)
            {
                int xpixel = (int)((currentUV.X + UVoffset.X) * 32);
                int ypixel = (int)(-(currentUV.Y + UVoffset.Y) * 32);

                return [new Vec2i(xpixel, ypixel)];
            }

            double increment = stroke.Length() / divisionCount;

            Vec2i[] pixels = new Vec2i[divisionCount];
            for (int i = 0; i < divisionCount; i++)
            {
                Vec2d UV = lastUV + (stroke * ((float)i / (divisionCount + 1)));

                int xpixel = (int)((UV.X + UVoffset.X) * 32); //Not canvasSize! This is the pixel width of a block, not of the whole canvas.
                int ypixel = (int)(-(UV.Y + UVoffset.Y) * 32);
                pixels[i] = new Vec2i(xpixel, ypixel);
            }

            return pixels;
        }

        #endregion

        #region Animations

        public override string GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity, ref EnumHandling bhHandling)
        {
            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (byPlayer.CurrentBlockSelection != null)
            {
                if (byPlayer.CurrentBlockSelection.Block is BlockEasel
                    || byPlayer.CurrentBlockSelection.Block is BlockMultiblock
                    || byPlayer.CurrentBlockSelection.Block is BlockMicroBlock)
                {
                    bhHandling = EnumHandling.PreventDefault;
                }
            }

            return base.GetHeldTpHitAnimation(slot, byEntity, ref bhHandling);
        }

        #endregion
    }
}
