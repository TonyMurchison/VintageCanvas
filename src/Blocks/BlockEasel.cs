using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using VintageCanvas.src.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Blocks
{

    //Responsibilities:
    //Track player interactions, perform raycasting, calculate paint intersections
    public class EaselBlockBehavior : BlockBehavior
    {
        Dictionary<string, int[]> brushPatterns = new Dictionary<string, int[]> {
                        {"small", [0] },
                        {"medium", [-33, -32, -31, -1, 0, 1, 31, 32, 33] },
                        {"large",  [-65, -64, -63, -34, -33, -32, -31, -30, -2, -1, 0, 1, 2, 30, 31, 32, 33, 34, 63, 64, 65]}
                    };

        //items which trigger ee.PaintPixels() when right-clicked
        private string[] paintingTools = [
                "game:charcoal",
                "vintagecanvas:brush-small",
                "vintagecanvas:brush-medium",
                "vintagecanvas:brush-large"
            ];

        ICoreClientAPI capi;
        public EaselBlockBehavior(Block block) : base(block) { }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            float paintInterval = 0.3f;
            BlockEntityEasel ee = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityEasel;
            if (ee == null) return false;
            ItemStack held = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

            //world.Logger.Event("Frequency test: " + secondsUsed + ", " + paintInterval);
            if (held != null)
            {
                if (paintingTools.Contains<string>(held.Collectible.Code) && !ee.CanvasSlot.Empty)
                {
                    applyPaintingTool(world, byPlayer, held, blockSel, ee);
                    return true;
                }
            }
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel, ref handling);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
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
                if (held.Collectible.Code.Path.StartsWith("canvas") && ee.CanvasSlot.Empty)
                {
                    string canvasid = SetCanvas(held, byPlayer, world, ee);
                    world.Api.Logger.Event("ID: " + canvasid);
                    return true;
                }

                //applying painting tools
                if (paintingTools.Contains<string>(held.Collectible.Code) && !ee.CanvasSlot.Empty)
                {
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

        public Vec2d CanvasAngleRaycast(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, double canvasAngle)
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
            Vec3d origin = blockSel.Position.ToVec3d();
            BlockFacing blockFace = blockSel.Face;
            Vec3d faceNormal = blockSel.Face.Normald;

            //shared origin for every direction over the block centre
            origin.X = origin.X + 0.5;
            origin.Y = origin.Y + 2.332; //Where all four directions share a plane point
            origin.Z = origin.Z + 0.5;

            //Predefined normal vectors for 15 degree planes
            Vec3d canvasNormal = new Vec3d(0.0, 0.259, 0.0);
            this.block.Variant.TryGetValue("side", out string side);
            switch (side)
            {
                case "north":
                    canvasNormal.Z = 0.966;
                    break;
                case "south":
                    canvasNormal.Z = -0.966;
                    break;
                case "east":
                    canvasNormal.X = -0.966;
                    break;
                case "west":
                    canvasNormal.X = 0.966;
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

        private string SetCanvas(ItemStack held, IPlayer byPlayer, IWorldAccessor world, BlockEntityEasel ee)
        {
            //Read or initialise canvas ID
            string canvasId = held.Attributes.GetString("canvasid");

            //Transfer to easel inventory
            ItemStack transfer = held.Clone();
            transfer.StackSize = 1;
            ee.PlaceCanvas(transfer);
            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

            return held.Attributes.GetString("canvasid");
        }

        private void applyPaintingTool(IWorldAccessor world, IPlayer byPlayer, ItemStack held, BlockSelection blockSel, BlockEntityEasel ee)
        {
            //Return 
            if (blockSel.Face.IsVertical || world.Side == EnumAppSide.Server)
            {
                return;
            }
            Vec2d canvasIntersect = CanvasAngleRaycast(world, byPlayer, blockSel, 15);
            //world.Logger.Debug("UV intersection point: " + canvasIntersect.X + ", " + canvasIntersect.Y);

            //Square canvas covers approx x[-0.5, 0.5], y[-0.62, -1.62]
            int xpixel = (int)((canvasIntersect.X + 0.5) * 32);
            int ypixel = (int)(-(canvasIntersect.Y + 0.62) * 32);

            if (xpixel < 0 || ypixel < 0) {
                return;
            }           


            //Then, update the canvas texture according to whatever brush settings required
            if (held.Collectible.Code.BeginsWith("vintagecanvas", "brush"))
            {
                int? brushPaint = held.Attributes.GetAsInt("paintcolor");
                Random rnd = new Random();
                if (brushPaint != 0)
                {
                    int basepixel = ypixel * 32 + xpixel;
                    List<int> pixels = new List<int>();
                    int[] brushPattern = brushPatterns[held.Collectible.Variant["size"].ToString()];
                    
                    foreach (int shift in brushPattern)
                    {
                        //Check if X coordinate is not on other side of canvas
                        int x1 = basepixel % 32;
                        int x2 = (basepixel + shift) % 32;

                        if (Math.Abs(x2 - x1) < 16)
                        {
                            pixels.Add(basepixel + shift);
                        }
                    }                        

                    float opacity = 1;
                    if (held.Attributes.HasAttribute("opacity")){
                        opacity = held.Attributes.GetFloat("opacity"); }

                    ee.PaintPixels(pixels.ToArray(), (int)brushPaint, opacity);
                }
            }
        }
    }
    public class BlockEasel : Block
    {
        public override Vec4f GetSelectionColor(ICoreClientAPI capi, BlockPos pos)
        {
            return new Vec4f(0f, 0f, 0f, 0.05f);
        }
    }
}

