using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using VintageCanvas.src.Blocks;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Entities
{
    public class BlockEntityPalette : BlockEntity
    {
        public int slotcount = 9;
        ICoreClientAPI capi;
        public Slot[] slots = new Slot[9];
        public int? paletteId;
        MeshData paintMesh = null;
        int[] pixeldata = new int[256];

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = new Slot();
                }
            }
            UpdateTexture();
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if(byItemStack == null) { return; }

            paletteId = byItemStack.Attributes.GetInt("paletteid");
            if (byItemStack.Attributes.HasAttribute("slots"))
            {
                slots = SerializerUtil.Deserialize<Slot[]>(
                    byItemStack.Attributes.GetBytes("slots"));
            }
            UpdateTexture();
        }

        //Returns index of the specific slot clicked on
        public int PaletteIndex(BlockSelection blockSel)
        {
            Vec3d clicked = blockSel.HitPosition;

            int x = (clicked.X < 0.3875, clicked.X > 0.5) switch
            {
                (true, false) => 0,
                (false, false) => 1,
                (false, true) => 2
            };

            int z = (clicked.Z < 0.422, clicked.Z > 0.578) switch
            {
                (true, false) => 0,
                (false, false) => 1,
                (false, true) => 2
            };

            return x + (3 * z);
        }

        public void BrushInteract(ItemStack playerStack, BlockSelection blockSel)
        {
            int slotindex = PaletteIndex(blockSel);
            playerStack.Attributes.SetFloat("opacity", 1f);

            if (VintageCanvasModSystem.config.PaintDepletion)
            {
                string brushsize = playerStack.Item.Variant["size"];
                int pickupcount = (brushsize) switch
                {
                    ("small") => 1,
                    ("medium") => 2,
                    ("large") => 4
                };

                if (slots[slotindex].paintColor == playerStack.Attributes.GetInt("paintcolor")) {
                    int curpaint = playerStack.Attributes.GetInt("paintamount");
                    playerStack.Attributes.SetInt("paintamount", VintageCanvasModSystem.config.PixelsPerPaintUnit * pickupcount + curpaint);
                }
                else
                {
                    playerStack.Attributes.SetInt("paintamount", VintageCanvasModSystem.config.PixelsPerPaintUnit * pickupcount);

                }
                playerStack.Attributes.SetInt("paintcolor", slots[slotindex].paintColor);

                slots[slotindex].fullness -= pickupcount;
                if (slots[slotindex].fullness <= 0)
                {
                    slots[slotindex].ClearSlot();
                    UpdateTexture();
                }
            }
            else {
                playerStack.Attributes.SetInt("paintcolor", slots[slotindex].paintColor);
            }
        }

        public void PaintJarInteract(ItemStack jarStack, BlockSelection blockSel, IPlayer byPlayer)
        {
            Vec3d clicked = blockSel.HitPosition;
            bool left = clicked.X < 0.40625; //X and Z of the midpoint between paint slots
            bool top = clicked.Z < 0.5;

            ItemStack content = new ItemStack();

            int slotindex = PaletteIndex(blockSel);

            if (jarStack.Collectible is ILiquidInterface liquidInterface)
            {
                content = liquidInterface.GetContent(jarStack);
                if (content.Collectible.Code.PathStartsWith("paint"))
                {
                    string paintName = content.Collectible.Code.EndVariant();
                    int color = TextureUtil.PaintColors[paintName];
                    slots[slotindex].AddPaint(color);

                    if (VintageCanvasModSystem.config.PaintDepletion)
                    {
                        BlockPaintJar jar = jarStack.Collectible as BlockPaintJar;
                        jar.TryTakeLiquid(jarStack, 0.01f);
                    }
                }
                if (content.Collectible.Code.PathStartsWith("turpentine"))
                {
                    //if(byPlayer.)
                    slots[slotindex].ClearSlot();

                    if (VintageCanvasModSystem.config.PaintDepletion)
                    {
                        BlockPaintJar jar = jarStack.Collectible as BlockPaintJar;
                        jar.TryTakeLiquid(jarStack, 0.01f);
                    }
                }
            }

            VintageCanvasModSystem.NetworkHandler.SendSlotData(Pos, slots);
            MarkDirty(true);
            UpdateTexture();
        }

        public void UpdatePixelData(int[] pixeldata)
        {
            this.pixeldata = pixeldata;
        }

        private void UpdateTexture()
        {
            if (Api.Side == EnumAppSide.Client)
            {

                Random rnd = new Random(1);
                //set pixeldata to match up
                for (int j = 0; j < pixeldata.Length; j++)
                {
                    int x = (j % 16 < 5, j % 16 > 10) switch
                    {
                        (true, false) => 0,
                        (false, false) => 1,
                        (false, true) => 2
                    };

                    int z = (j / 16 < 5, j / 16 > 10) switch
                    {
                        (true, false) => 0,
                        (false, false) => 1,
                        (false, true) => 2
                    };

                    int slotindex = x + (3 * z);

                    int targetcolor = (int)slots[slotindex].paintColor;
                    int variation = rnd.Next(10);
                    if (targetcolor != 0)
                    {
                        targetcolor = TextureUtil.BlendColor(-16119286, targetcolor, variation / 60f);
                    }
                    pixeldata[j] = targetcolor;
                }                

                //send pixeldata to server
                VintageCanvasModSystem.NetworkHandler.SendPixelData(Pos, SerializerUtil.Serialize(pixeldata));
                AssetLocation texLoc = new AssetLocation("vintagecanvas", "palette" + paletteId.ToString());

                 paintMesh = TextureUtil.SwapPaintingTextureMesh(
                    pixeldata,
                    16,
                    texLoc,
                    capi.Tesselator.GetTextureSource(Block),
                    Block.Code,
                    Block.Shape,
                    capi
                    );      

            }

            MarkDirty(true);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (paintMesh != null)
            {
                mesher.AddMeshData(paintMesh.Clone());
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        public void UpdateSlots(Slot[] slots)
        {
            this.slots = slots;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            if (tree.HasAttribute("slots"))
            {
                slots = SerializerUtil.Deserialize<Slot[]>(tree.GetBytes("slots"));
            }
            if (tree.HasAttribute("paletteid"))
            {
                paletteId = tree.GetInt("paletteid");
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("slots", SerializerUtil.Serialize<Slot[]>(slots));
            if (paletteId != null)
            {
                tree.SetInt("paletteid", (int)paletteId);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            paintMesh?.Dispose();
        }

        [ProtoContract]
        public class Slot()
        {
            [ProtoMember(1)]
            public int paintColor = 0;
            [ProtoMember(2)]
            public int fullness = 0;

            public void ClearSlot()
            {
                paintColor = 0;
                fullness = 0;
            }

            public void AddPaint(int color)
            {
                if (paintColor == 0)
                {
                    fullness++;
                    paintColor = color;
                    return;
                }
                else
                {
                    fullness++;
                    float alpha = 1f / fullness;
                    paintColor = TextureUtil.BlendColor((int)paintColor, color, 1 - alpha);
                }
            }
        }
    }
}
