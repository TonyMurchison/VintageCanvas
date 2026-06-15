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
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Blocks
{

    //Responsibilities:
    //Track player interactions, perform raycasting, calculate paint intersections
    public class BlockEasel : Block
    {
        float paintFrequency = 20f;
        ICoreClientAPI capi;
        public string easelName = "vintagecanvas:easel";
        public string allowedCanvas = "canvas";
        public int canvasSize = 32;
        private DateTime clearingClock;

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

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            if(cancelReason != EnumItemUseCancelReason.MovedAway)
            {
                BlockEntityEasel ee = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityEasel;
                ee.SynchroniseTexture();
            }
               
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {            
            ItemStack held = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

            if(held == null)
            {
                return false;
            }

            //frequency limiter: only allow edits if the last edit was > 1/paintfrequency ago
            float? timestamp = held.Attributes.TryGetFloat("timestamp");            
            if (timestamp != null)
            {
                float timelapse = secondsUsed - (float)timestamp;
                if (timelapse < (1f / paintFrequency))
                {
                    return true;
                }
            }
            BlockEntityEasel ee = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityEasel;
            if (ee == null)
            {
                return false;
            }
            

            if (held != null)
            {
                if (TextureUtil.paintingTools.Contains<string>(held.Collectible.Code) && !ee.CanvasSlot.Empty)
                {
                    applyPaintingTool(world, byPlayer, held, blockSel, ee);
                    held.Attributes.SetFloat("timestamp", secondsUsed);
                    clearingClock = DateTime.Now;
                    return true;
                }
            }
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //world.Logger.Debug("Easel click location: " + blockSel.HitPosition);
            BlockEntityEasel ee = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityEasel;
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
                if (held.Collectible.Code.Path.StartsWith(allowedCanvas) && ee.CanvasSlot.Empty)
                {
                    SetCanvas(held, byPlayer, world, ee);
                    return true;
                }
                else if ( held.Collectible.Code.Path.StartsWith("canvas") || held.Collectible.Code.Path.StartsWith("largecanvas") )
                {
                    string hint = Lang.Get("vintagecanvas:canvas-size");
                    (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "wrongsize", hint);
                    return true;
                }

                //applying painting tools
                if (TextureUtil.paintingTools.Contains<string>(held.Collectible.Code) && !ee.CanvasSlot.Empty)
                {
                    if (clearingClock != null)
                    {
                        TimeSpan diff = DateTime.Now.Subtract(clearingClock);
                        if( diff.TotalMilliseconds > 100)
                        {
                            ee.changedpixels.Clear();
                        }
                    }
                    else
                    {
                        ee.changedpixels.Clear();
                    }
                    held.Attributes.SetFloat("timestamp", -10f);
                    return true;
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
            //api.World.Logger.Event("Click coordinates: " + blockSel.HitPosition.X + ", " + blockSel.HitPosition.Y + ", " + blockSel.HitPosition.Z);

            Vec3d raystart = playerray.origin;
            Vec3d raydir = playerray.dir;

            //plane normal and origin
            Vec3d origin = blockSel.Position.ToVec3d();
            BlockFacing blockFace = blockSel.Face;
            Vec3d faceNormal = blockSel.Face.Normald;

            //shared origin for every direction over the block centre
            origin.X = origin.X + 0.5;            
            origin.Y = origin.Y + Attributes["originheight"].AsFloat();
            origin.Z = origin.Z + 0.5;

            //Predefined normal vectors for 15 degree planes
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

        private void applyPaintingTool(IWorldAccessor world, IPlayer byPlayer, ItemStack held, BlockSelection blockSel, BlockEntityEasel ee)
        {
            //Return 
            if (blockSel.Face.IsVertical || world.Side == EnumAppSide.Server)
            {
                return;
            }
            Vec2d canvasIntersect = CanvasAngleRaycast(world, byPlayer, blockSel);
            //world.Logger.Debug("UV intersection point: " + canvasIntersect.X + ", " + canvasIntersect.Y);

            float yoffset = Attributes["uvyoffset"].AsFloat();
            Vec2f UVoffset = new Vec2f(0.5f, yoffset);

            //Square canvas covers approx u[-0.5, 0.5], v[-0.3898, -1.3898]
            int xpixel = (int)((canvasIntersect.X + UVoffset.X) * 32); //Not canvasSize! This is the pixel width of a block, not of the whole canvas.
            int ypixel = (int)(-(canvasIntersect.Y + UVoffset.Y) * 32);

            if (xpixel < 0 || ypixel < 0 || xpixel > canvasSize || ypixel > canvasSize) {
                return;
            }           


            //Then, update the canvas texture according to whatever brush settings required
            if (held.Collectible.Code.BeginsWith("vintagecanvas", "brush"))
            {
                int? brushPaint = held.Attributes.GetAsInt("paintcolor");
                Random rnd = new Random();
                if (brushPaint != 0)
                {
                    int basepixel = ypixel * canvasSize + xpixel;
                    List<int> pixels = new List<int>();
                    
                    Vec2i[] brushPattern = TextureUtil.brushPatterns[held.Collectible.Variant["size"].ToString()];
                    
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

                    float opacity = 1;
                    if (held.Attributes.HasAttribute("opacity")){
                        opacity = held.Attributes.GetFloat("opacity"); }

                    ee.PaintPixels(pixels.ToArray(), (int)brushPaint, opacity);
                }
            }
            if (held.Collectible.Code.BeginsWith("game", "charcoal")){
                Random rnd = new Random();

                ee.PaintPixels([ypixel * canvasSize + xpixel], -16119286, 0.5f);
            }
        }
    }
}

