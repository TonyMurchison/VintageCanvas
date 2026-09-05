using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using VintageCanvas.src.Blocks;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.GameContent;
using static Vintagestory.GameContent.BlockLiquidContainerBase;

namespace VintageCanvas.src.Items
{
    internal class CollectibleBehaviorPaintTool : CollectibleBehavior
    {
        private HashSet<int> changedpixels = new HashSet<int>();
        private Vec2d previousUV = null;
        private float paintFrequency = 20f;
        public CollectibleBehaviorPaintTool(CollectibleObject collObj) : base(collObj) { }
        private HashSet<FrescoLocation> blockPainted = new();

        //All painting happens during OnHeldInteractStep. Attack steps redirect to their Interact equivalent
        #region ClickResponses

        //When something is tagged with this behaviour, redirect any attack on an easel/microblock to Easel.OnBlockInteractX();
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (blockSel != null)
            {
                if (IsPaintTarget(blockSel, byEntity.World))
                {
                    this.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, true, ref handHandling, ref handling);
                    handling = EnumHandling.PreventDefault;
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
                if (IsPaintContainer(blockSel, byEntity.World))
                {
                    //Shelves and groundstorage work differently, so there are custom interaction redirects for each
                    IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

                    if (blockSel.Block is BlockGroundStorage)
                    {
                        BlockEntityGroundStorage begs = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
                        ItemSlot s = begs.GetSlotAt(blockSel);
                        BlockPaintJar bpj = s.Itemstack.Block as BlockPaintJar;
                        bpj.ContainerInteractions(begs, s, byPlayer, blockSel);

                    }
                    if (blockSel.Block is BlockShelf)
                    {
                        BlockEntityShelf bes = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityShelf;
                        BlockShelf bs = blockSel.Block as BlockShelf;
                        ItemStack[] contents = bes.GetContentStacks(false);

                        int selbox = blockSel.SelectionBoxIndex * 2;

                        //check front slot first
                        selbox++;
                        if (contents[selbox] == null) selbox--;

                        if (contents[selbox] != null &&
                            contents[selbox].Block is BlockPaintJar)
                        {
                            BlockPaintJar bpj = contents[selbox].Block as BlockPaintJar;
                            bpj.ContainerInteractions(bes, bes.Inventory[selbox], byPlayer, blockSel);
                        }
                    }

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
                if (IsPaintTarget(blockSel, byEntity.World))
                {
                    this.OnHeldInteractStep(secondsPassed, slot, byEntity, blockSel, entitySel, ref handling);
                    handling = EnumHandling.PreventDefault;
                    return true;
                }
            }

            return base.OnHeldAttackStep(secondsPassed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override bool OnHeldAttackCancel(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
        {
            if (blockSel != null)
            {
                if (IsPaintTarget(blockSel, byEntity.World))
                {
                    handling = EnumHandling.Handled;
                    OnHeldInteractCancel(secondsPassed, slot, byEntity, blockSel, entitySel, 0, ref handling);
                    OnHeldInteractStop(secondsPassed, slot, byEntity, blockSel, entitySel, ref handling);

                    handling = EnumHandling.PreventSubsequent;
                    return true;
                }
            }

            return base.OnHeldAttackCancel(secondsPassed, slot, byEntity, blockSel, entitySel, cancelReason, ref handling);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (blockSel != null && IsPaintTarget(blockSel, byEntity.World))
            {
                handHandling = EnumHandHandling.PreventDefault;
                handling = EnumHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            //frequency limiter: only allow edits if the last edit was > 1/paintfrequency ago

            if (!FrequencyTest(slot.Itemstack, secondsUsed))
            {
                return true;
            }


            handling = EnumHandling.Handled;
            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            if (blockSel == null)
            {
                return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling); ;
            }

            if (blockSel.Block is BlockEasel || blockSel.Block is BlockMultiblock)
            {
                BlockEntityEasel bee = getEaselEntity(blockSel, byPlayer.Entity.World);
                if (bee != null)
                {
                    bee.pixeldata = ApplyTool(slot.Itemstack, bee.pixeldata, blockSel, byPlayer);
                    bee.UpdateTexture();
                }

                handling = EnumHandling.Handled;
            }

            if (blockSel.Block is BlockMicroBlock)
            {
                BlockMicroBlock bmb = blockSel.Block as BlockMicroBlock;
                BlockEntityMicroBlock bemb = (BlockEntityMicroBlock)byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);

                //TEMP
                int i = blockSel.Face.Index;
                string blockID = "vintagecanvasfresco" + blockSel.Position.ToString() + "-" + i;

                if (FrescoStore.Data.TryGetValue(blockID, out int[] pixeldata))
                {
                    FrescoStore.Data[blockID] = ApplyTool(slot.Itemstack, pixeldata, blockSel, byPlayer);
                    blockPainted.Add(new FrescoLocation(i, blockSel.Position));
                    bemb.MarkDirty(true);
                    bemb.MarkMeshDirty();
                }

                handling = EnumHandling.Handled;
            }
            return true;
        }

        private class FrescoLocation
        {
            public int Face;
            public BlockPos Position;

            public FrescoLocation(int f, BlockPos pos)
            {
                Face = f;
                Position = pos;
            }
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
        {
            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            EndStroke(byEntity.World, blockSel, byPlayer);
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
        }

        private static BlockEntityEasel getEaselEntity(BlockSelection blockSel, IWorldAccessor world)
        {
            if (blockSel.Block is BlockEasel)
            {
                BlockEntityEasel ee = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityEasel;
                return ee;
            }

            if (blockSel.Block is BlockMultiblock)
            {
                BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                if (world.BlockAccessor.GetBlock(bp) is BlockEasel)
                {
                    return (BlockEntityEasel)world.BlockAccessor.GetBlockEntity(bp);
                }
            }

            return null;
        }

        private static BlockEasel getEasel(BlockSelection blockSel, IWorldAccessor world)
        {
            if (blockSel.Block is BlockEasel)
            {
                BlockEasel ee = world.BlockAccessor.GetBlock(blockSel.Position) as BlockEasel;
                return ee;
            }

            if (blockSel.Block is BlockMultiblock)
            {
                BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                if (world.BlockAccessor.GetBlock(bp) is BlockEasel)
                {
                    return (BlockEasel)world.BlockAccessor.GetBlock(bp);
                }
            }

            return null;
        }

        private bool FrequencyTest(ItemStack tool, float secondsUsed)
        {
            float? timestamp = tool.Attributes.TryGetFloat("timestamp");
            if (timestamp != null)
            {
                float timelapse = secondsUsed - (float)timestamp;
                if (timelapse > (1f / paintFrequency))
                {
                    return true;
                }
                else return false;
            }
            return true;
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
                if (basepixelvec.Y >= canvasSize || basepixelvec.X >= canvasSize
                    || basepixelvec.Y < 0 || basepixelvec.X < 0) continue;
                int basepixel = basepixelvec.Y * canvasSize + basepixelvec.X;
                foreach (Vec2i shift in brushPattern)
                {
                    //Check if it's not overflowing and
                    //X coordinate is not on other side of canvas
                    int x1 = basepixel % canvasSize;
                    int x2 = (basepixel + shift[0] + (shift[1] * canvasSize)) % canvasSize;

                    int pixelindex = basepixel + shift[0] + (shift[1] * canvasSize);
                    if (Math.Abs(x2 - x1) < 16 && pixelindex < Math.Pow(canvasSize, 2))
                    {
                        pixels.Add(pixelindex);
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
            if (blockSel.Block is BlockMicroBlock)
            {
                hash += unchecked(blockSel.Position.X * 10000);
                hash += unchecked(blockSel.Position.Y * 1000000);
                hash += unchecked(blockSel.Position.Z * 100000000);
            }
            return hash;
        }

        private void EndStroke(IWorldAccessor world, BlockSelection blockSel, IPlayer byPlayer)
        {
            previousUV = null;
            changedpixels.Clear();

            //Transfer each changed pixeldata to the serverside data store
            foreach (FrescoLocation fl in blockPainted)
            {
                BlockEntityMicroBlock bemb = world.BlockAccessor.GetBlockEntity(fl.Position) as BlockEntityMicroBlock;
                if (bemb != null)
                {
                    string id = FrescoStore.compileFrescoID(fl.Position, fl.Face);
                    SynchroniseFrescoTexture(FrescoStore.Data[id], fl.Position, fl.Face, byPlayer.Entity.World);
                }
            }
            blockPainted.Clear();

            if (IsPaintTarget(blockSel, world) && blockSel.Block is not BlockMicroBlock)
            {
                BlockEntityEasel ee = getEaselEntity(blockSel, world);
                if (world.Side == EnumAppSide.Client) ee.SynchroniseTexture();
            }

            int pa = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.GetInt("paintamount");
            if (pa <= 0 && VintageCanvasModSystem.config.PaintDepletion && pa != null)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.SetInt("paintcolor", 0);
            }
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
                    if (blockSel != null && !changedpixels.Contains(hashBlockPixel(blockSel, pixelindices[i])))
                    {
                        int newColor = TextureUtil.BlendColor(color, pixeldata[pixelindices[i]], alpha);
                        pixeldata[pixelindices[i]] = newColor;

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

            List<int> blendedColors = new();
            List<int> blendedIndices = new();
            for (int i = 0; i < pixelindices.Length; i++)
            {
                if (0 <= pixelindices[i] && (int)Math.Pow(canvasSize, 2) > pixelindices[i])
                {
                    if (!changedpixels.Contains(hashBlockPixel(blockSel, pixelindices[i])))
                    {
                        //test a radius, create an average rgb value for all those pixels, then BlendColor the pixel with that value
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

        private int[] ApplyTool(ItemStack tool, int[] pixeldata, BlockSelection blockSel, IPlayer byPlayer)
        {
            Vec2i[] interpolatedPixels = [];
            int canvasSize = 32;

            if (blockSel.Block is BlockMicroBlock)
            {
                double x = blockSel.HitPosition.X;
                double y = blockSel.HitPosition.Y;
                double z = blockSel.HitPosition.Z;
                int facing = blockSel.Face.Index;

                double xcoord = (facing) switch
                //This has changed between versions, I think? Used to require (0) => 1 + x, (1) => 1 + z
                //Genuinely no idea why that would happen
                {
                    (0) => x,
                    (1) => z,
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

                //Offset by 0.5 pixel
                interpolatedPixels = InterpolatePixels(previousUV, new Vec2d(xcoord, ycoord), new Vec2d(0, 0.5 / 32), canvasSize);

            }

            if (blockSel.Block is BlockEasel ||
                blockSel.Block is BlockMultiblock)
            {
                BlockEasel be = getEasel(blockSel, byPlayer.Entity.World);
                Vec2d UV = be.CanvasAngleRaycast(byPlayer.Entity.World, byPlayer, blockSel);

                float yoffset = be.Attributes["uvyoffset"].AsFloat();
                Vec2d UVoffset = new Vec2d(0.5f, yoffset);

                interpolatedPixels = InterpolatePixels(previousUV, UV, UVoffset, canvasSize);
                canvasSize = be.Attributes["canvassize"].AsInt();
            }



            if (tool.Collectible.Code.PathStartsWith("brush"))
            {
                pixeldata = ApplyBrush(tool, pixeldata, interpolatedPixels, canvasSize, blockSel);
            }
            if (tool.Collectible.Code.PathStartsWith("pastel"))
            {
                if (tool.Collectible.Code.EndVariant() == "carbonblack")
                {

                    List<int> pixelcoords = new();
                    foreach (Vec2i pixel in interpolatedPixels)
                    {
                        if (pixel.X < canvasSize && pixel.Y < canvasSize
                            && pixel.X >= 0 && pixel.Y >= 0)
                        {
                            pixelcoords.Add(pixel[0] + canvasSize * pixel[1]);
                        }
                    }
                    pixeldata = PaintPixels(pixelcoords.ToArray(), TextureUtil.PaintColors["carbonblack"], 0.5f, tool, pixeldata, canvasSize, blockSel);
                }

                if (tool.Collectible.Code.EndVariant() == "chalkwhite")
                {
                    List<int> pixelcoords = new();
                    foreach (Vec2i pixel in interpolatedPixels)
                    {
                        if (pixel.X < canvasSize && pixel.Y < canvasSize
                            && pixel.X >= 0 && pixel.Y >= 0)
                        {
                            pixelcoords.Add(pixel[0] + canvasSize * pixel[1]);
                        }
                    }
                    pixeldata = PaintPixels(pixelcoords.ToArray(), 936298687, 0.5f, tool, pixeldata, canvasSize, blockSel);
                }

                tool.Attributes.SetFloat("timestamp", -10f);
                return pixeldata;
            }

            return pixeldata;
        }

        private int AverageColors(int[] nearbypixelindices, int[] pixeldata)
        {
            if (nearbypixelindices.Length == 0)
            {
                return 0;
            }
            //Mixbox mixing behaviour
            if (VintageCanvasModSystem.config.MixBoxBlending)
            {
                int color = pixeldata[nearbypixelindices[0]];
                for (int i = 1; i < nearbypixelindices.Length; i++){
                    color = TextureUtil.BlendColor(color, pixeldata[nearbypixelindices[i]], 1f - (1f / (i + 1f)));
                }
                return color;
            }

            //Default mixing behaviour
            int r = 0; int g = 0; int b = 0;
            foreach (int pixel in nearbypixelindices)
            {
                r += (pixeldata[pixel] >> 16) & 0xFF;
                g += (pixeldata[pixel] >> 8) & 0xFF;
                b += (pixeldata[pixel]) & 0xFF;
            }
            r = r / nearbypixelindices.Length; g = g / nearbypixelindices.Length; b = b / nearbypixelindices.Length;

            return (255 << 24) | (r << 16) | (g << 8) | b;

        }

        private Vec2i[] InterpolatePixels(Vec2d? lastUV, Vec2d currentUV, Vec2d UVoffset, int canvasSize)
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

        public static bool IsPaintTarget(BlockSelection blockSel, IWorldAccessor world)
        {
            if (blockSel == null) return false;
            if (blockSel.Block is BlockEasel ||
                blockSel.Block is BlockMicroBlock)
            {
                return true;
            }

            if (blockSel.Block is BlockMultiblock)
            {
                BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                return (world.BlockAccessor.GetBlockEntity(bp) is BlockEntityEasel);
            }

            return false;
        }

        private bool IsPaintContainer(BlockSelection blockSel, IWorldAccessor world)
        {
            if (blockSel == null) return false;
            if (blockSel.Block is BlockPalette || blockSel.Block is BlockShelf)
            {
                return true;
            }

            if (blockSel.Block is BlockGroundStorage)
            {
                BlockEntityGroundStorage bgs = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage;
                ItemStack groundcontents = bgs.GetSlotAt(blockSel).Itemstack;
                if (groundcontents != null && groundcontents.Collectible is BlockPaintJar)
                {
                    return true;
                }
            }

            return false;
        }

        public void SynchroniseFrescoTexture(int[] pixeldata, BlockPos pos, int face, IWorldAccessor world)
        {

            if (world.BlockAccessor.GetBlock(pos) is BlockMicroBlock)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                VintageCanvasModSystem.NetworkHandler.SendPixelData(pos, TextureUtil.WriteCompressedPixelData(pixeldata), face);
                be.MarkDirty(true);
            }
        }        
        #endregion
        
    }
}
