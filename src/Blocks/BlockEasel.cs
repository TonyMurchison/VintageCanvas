using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using static VintageCanvas.src.Entities.BlockEntityPalette;

namespace VintageCanvas.src.Blocks
{

    //Responsibilities:
    //Perform raycasting, calculate paint intersections
    public class BlockEasel : Block
    {
        ICoreClientAPI capi;
        public string easelName = "vintagecanvas:easel";
        public string allowedCanvas = "canvas";
        public int canvasSize = 32;

        public override Vec4f GetSelectionColor(ICoreClientAPI capi, BlockPos pos)
        {
            return new Vec4f(0f, 0f, 0f, 0.05f);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            BlockEntityEasel ee = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityEasel;
            if (ee != null)
            {
                Block easelBlock = api.World.GetBlock(new AssetLocation(easelName + "-" + Variant["woodtype"] + "-" + "none" + "-" + "north"));

                ItemStack[] dropStack = [new ItemStack(easelBlock), new ItemStack()];

                if (ee.pixeldata != null)
                {
                    dropStack[1] = ee.CanvasSlot.Itemstack;
                    var pdata = SerializerUtil.Serialize(ee.pixeldata);
                    dropStack[1].Attributes.SetBytes("vc_pixeldata", pdata);
                    dropStack[1].Attributes.SetBool("vc_rendered", false);
                }
                return dropStack;
            }
            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //world.Logger.Debug("Easel click location: " + blockSel.HitPosition);
            BlockEntityEasel ee = getEaselEntity(blockSel, world);
            if (ee == null) return false;
            ItemStack held = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

            //Retrieve canvas with empty hand
            if (held == null && !ee.CanvasSlot.Empty)
            {
                ee.GetCanvas(byPlayer);
                return true;
            }

            if (held != null) {
                //canvas placement
                if (held.Collectible.Code.Path.StartsWith(allowedCanvas) 
                    && ee.CanvasSlot.Empty)
                {
                    SetCanvas(held, byPlayer, world, ee);
                    return true;
                }

                else if (held.Collectible.Code.Path.StartsWith("canvas") || held.Collectible.Code.Path.StartsWith("largecanvas"))
                {
                    string hint = Lang.Get("vintagecanvas:canvas-size");
                    (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "wrongsize", hint);
                    return true;
                }

                //Signing
                else if (held.Collectible.Attributes?.IsTrue("writingTool") == true
                    && !ee.CanvasSlot.Empty ) {

                    //Only the first person to sign a canvas is allowed to change the name
                    if (!ee.CanvasSlot.Itemstack.Attributes.HasAttribute("canvasname")
                        || ee.CanvasSlot.Itemstack.Attributes.GetAsString("authorname") == byPlayer.PlayerName.ToString())
                    {
                        if (api.Side == EnumAppSide.Client)
                        {
                            var dlg = new GuiPaintingSigning(api as ICoreClientAPI);
                            dlg.OnClosed += () =>
                            {
                                if (dlg.IsSigned)
                                {
                                    ee.CanvasSlot.Itemstack.Attributes.SetString("canvasname", dlg.Title);
                                    ee.CanvasSlot.Itemstack.Attributes.SetString("authorname", byPlayer.PlayerName);

                                    VintageCanvasModSystem.NetworkHandler.SendNameData(blockSel.Position, dlg.Title, byPlayer.PlayerName);
                                }
                            };
                            dlg.TryOpen();
                        }
                    }
                    else
                    {
                        string hint = Lang.Get("vintagecanvas:already-signed");
                        (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "alreadysigned", hint);
                    }
                
                }

            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public Vec2d CanvasAngleRaycast(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //returns the raycast intersection with the canvas plane. Normalised to a UV space centred around x 0.5, z0

            //Player ray
            EntityPlayer player = byPlayer.Entity;
            var pru = new PickingRayUtil();
            ClientMain mainworld = capi.World as ClientMain;
            Ray playerray = pru.GetPickingRayByMouseCoordinates(mainworld);

            Vec3d raystart = playerray.origin;
            Vec3d raydir = playerray.dir;

            //plane normal and origin

            BlockPos bp = blockSel.Position;

            if (blockSel.Block is BlockMultiblock)
            {
                BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                bp = blockSel.Clone().Position.Add(bm.OffsetInv);
            }

            Vec3d origin = bp.ToVec3d();
            BlockFacing blockFace = blockSel.Face;
            Vec3d faceNormal = blockSel.Face.Normald;

            //shared origin for every direction over the block centre
            origin.X = origin.X + 0.5;            
            origin.Y = origin.Y + Attributes["originheight"].AsFloat();
            origin.Z = origin.Z + 0.5;

            //Predefined normal vectors for 'canvasangle' degree planes
            float anglerad = Attributes["canvasangle"].AsFloat() * (float)Math.PI / 180f;
            Vec3d canvasNormal = new Vec3d(0.0, Math.Sin(anglerad), 0.0);
            Variant.TryGetValue("side", out string side);
            switch (side)
            {
                case "north":
                    canvasNormal.Z = Math.Cos(anglerad);
                    break;
                case "south":
                    canvasNormal.Z = -Math.Cos(anglerad);
                    break;
                case "east":
                    canvasNormal.X = -Math.Cos(anglerad);
                    break;
                case "west":
                    canvasNormal.X = Math.Cos(anglerad);
                    break;
            }

            //function: distance = (planeorigin - casterorigin) dot planenormal / (raydir dot planenormal)
            // intersectionpoint = raystart + (distance * raydir)
            Vec3d hitPoint = new Vec3d();

            double denominator = raydir.Dot(canvasNormal);
            Vec3d planeToRay = origin - raystart;
            double t = planeToRay.Dot(canvasNormal) / denominator;
            hitPoint = raystart + raydir * t;
            //world.Logger.Debug("Abs intersection point: " + hitPoint.X + ", " + hitPoint.Y + ", " + hitPoint.Z);

            //Construct UV space relative to canvas plane origin
            Vec3d Uvec = new Vec3d(canvasNormal.Z, 0, -canvasNormal.X);
            Uvec.Normalize();
            Vec3d Vvec = canvasNormal.Cross(Uvec);
            Vvec.Normalize();

            //Project hitPoint onto UV axes
            Vec3d offset = hitPoint.SubCopy(origin);
            Vec2d UVhitPoint = new Vec2d(offset.Dot(Uvec), offset.Dot(Vvec));

            return (UVhitPoint);
        }

        private void SetCanvas(ItemStack held, IPlayer byPlayer, IWorldAccessor world, BlockEntityEasel ee)
        {
            //Read or initialise canvas ID
            string canvasId = held.Attributes.GetString("canvasid");

            //Transfer to easel inventory
            ItemStack transfer = held.Clone();
            transfer.StackSize = 1;
            ee.PlaceCanvas(transfer);
            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return;
        }

        
        private static BlockEntityEasel getEaselEntity(BlockSelection blockSel, IWorldAccessor world)
        {
            BlockEntityEasel ee = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityEasel;
            if( ee != null) { return ee;  }
            
            if (blockSel.Block is BlockMultiblock)
            {
                BlockMultiblock bm = (BlockMultiblock)blockSel.Block;
                BlockPos bp = blockSel.Clone().Position.Add(bm.OffsetInv);
                ee = (BlockEntityEasel)world.BlockAccessor.GetBlockEntity(bp);
            }

            if (blockSel.Block is BlockEasel)
            {
                ee = blockSel.Block.GetBlockEntity<BlockEntityEasel>(blockSel);
            }

            return ee;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            string s = base.GetPlacedBlockInfo(world, pos, forPlayer);
            BlockEntityEasel ee = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityEasel;
            if (ee == null) return s;

            if (ee.CanvasSlot.Itemstack != null 
                && ee.CanvasSlot.Itemstack.Attributes.HasAttribute("canvasname"))
            {
                s += "Name: " + ee.CanvasSlot.Itemstack.Attributes.GetAsString("canvasname");
                s += "\nAuthor: " + ee.CanvasSlot.Itemstack.Attributes.GetAsString("authorname");
            }
            else if (ee.CanvasSlot.Itemstack != null)
            {
                s += Lang.Get("Name: ") + Lang.Get("Unknown");
                s += "\n" + Lang.Get("Author: ") + Lang.Get("Unknown");
            }
            else
            {
                s += Lang.Get("No canvas");
            }

            return s;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Block is not BlockEaselH)
            {
                dsc.AppendLine("\nA small, portable frame for holding similarly small canvases while you paint.");
            }
            else
            {
                dsc.AppendLine("\nAn imposing frame that will hold the biggest of canvases as you work.");
            }
        }
    }
}

