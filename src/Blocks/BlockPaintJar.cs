using Cairo;
using HarmonyLib;
using Microsoft.Win32.SafeHandles;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using VintageCanvas.src.Entities;
using VintageCanvas.src.Items;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.WorldEdit;
using static VintageCanvas.src.Entities.BlockEntityPalette;

namespace VintageCanvas.src.Blocks
{
    public class BlockPaintJar : BlockLiquidContainerTopOpened, IContainedInteractable, IContainedMeshSource
    {
        private Dictionary<int, MultiTextureMeshRef> meshrefs = new();
        //Pull paint recipes and paint mediums from blocktypes/paintjar.json
        private Dictionary<string, AssetLocation> pigmentRecipes = new();
        private Dictionary<string, int> paintColors = new();
        private List<string> mediums = new();

        ICoreClientAPI capi;

        #region Rendering
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;

            Vintagestory.API.Datastructures.JsonObject[] recipes = this.Attributes["pigmentrecipes"].AsArray();
            foreach (Vintagestory.API.Datastructures.JsonObject recipe in recipes){
                pigmentRecipes[recipe["input"].AsString()] = new AssetLocation(recipe["output"].AsString());
            }
            
            
            Vintagestory.API.Datastructures.JsonObject[] mediumlist = this.Attributes["mediums"].AsArray();
            foreach (Vintagestory.API.Datastructures.JsonObject medium in mediumlist)
            {
                mediums.Add(medium["medium"].AsString());
            }

            Vintagestory.API.Datastructures.JsonObject[] paintcolors = this.Attributes["paintcolors"].AsArray();
            foreach (Vintagestory.API.Datastructures.JsonObject color in paintcolors)
            {
                paintColors[color["input"].AsString()] = color["output"].AsInt();
            }
        }

        public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos blockPos)
        {            
            MeshData baseMesh = base.GenMesh(capi, GetContent(slot.Itemstack), blockPos);

            
            if (GetContent(slot.Itemstack) == null) return baseMesh;
            string? painttype = GetContent(slot.Itemstack).Collectible.Variant["color"];
            if (painttype == null) return baseMesh;

            Shape shape = api.Assets.Get("vintagecanvas:shapes/item/paintjar-mess.json")?.ToObject<Shape>();
            Block block = api.World.GetBlock(BlockId);

            capi.BlockTextureAtlas.GetOrInsertTexture(
                new AssetLocation("vintagecanvas:liquid/" + painttype),
                out int _,
                out TextureAtlasPosition texPos,
                null
                );
            MessTexSource mts = new MessTexSource(capi.Tesselator.GetTextureSource(block), texPos, capi.BlockTextureAtlas.Size);

            capi.Tesselator.TesselateShape(
                    "jar",
                    shape,
                    out MeshData mesh,
                    mts
                );

            baseMesh.AddMeshData(mesh);
            mesh.Dispose();
            
            return baseMesh;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            //Check if contentStack already rendered
            ItemStack contentStack = GetContent(itemstack);

            if (contentStack == null) return;

            string? painttype = contentStack.Collectible.Variant["color"];
            if (painttype == null) return;

            int hashcode = GetStackCacheHashCode(contentStack);


            //Else, run the base render, then append mess mesh to dictionary
            if (!meshrefs.TryGetValue(hashcode, out MultiTextureMeshRef meshRef))
            {
                Shape shape = api.Assets.Get("vintagecanvas:shapes/item/paintjar-mess.json")?.ToObject<Shape>();
                Block block = api.World.GetBlock(BlockId);

                capi.BlockTextureAtlas.GetOrInsertTexture(
                    new AssetLocation("vintagecanvas:liquid/" + painttype),
                    out int _,
                    out TextureAtlasPosition texPos,
                    null
                    );
                MessTexSource mts = new MessTexSource(capi.Tesselator.GetTextureSource(block), texPos, capi.BlockTextureAtlas.Size);

                capi.Tesselator.TesselateShape(
                        "jar",
                        shape,
                        out MeshData mesh,
                        mts
                    );

                MeshData basemesh = base.GenMesh(capi, GetContent(itemstack));
                basemesh.AddMeshData(mesh);
                renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(basemesh);
                meshrefs[hashcode] = renderinfo.ModelRef;

                mesh.Dispose();
            }      
            else
            {
                renderinfo.ModelRef = meshrefs[hashcode];
            }

            //TODO If the jar contains brushes, add brush model refs to the renderinfo
        }

        public string GetMeshCacheKey(ItemSlot slot)
        {
            string key = GetContent(slot.Itemstack)?.Collectible.Variant["color"] ?? "empty";
            key += Code.ToShortString();
            if(GetContent(slot.Itemstack) != null)
            {
                key += GetContent(slot.Itemstack).StackSize;
            }
            return key;
        }

        public class MessTexSource : ITexPositionSource
        {
            private readonly ITexPositionSource defaultSrc;
            private readonly TextureAtlasPosition messPos;
            private readonly Size2i atlasSize;

            public MessTexSource(
                ITexPositionSource defaultSrc,
                TextureAtlasPosition messPos,
                Size2i atlasSize)
            {
                this.defaultSrc = defaultSrc;
                this.messPos = messPos;
                this.atlasSize = atlasSize;
            }

            public TextureAtlasPosition this[string textureCode] =>
                textureCode == "mess" ? messPos : defaultSrc[textureCode];

            public Size2i AtlasSize => atlasSize;
        }
        #endregion

        #region Behaviour

        public bool ContainerInteractions(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemStack heldStack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (heldStack == null )
            {
                return base.OnContainedInteractStart(be, slot, byPlayer, blockSel);
            }
            if (heldStack.Collectible is ILiquidInterface liquidInterface ) // AND the jar contains no brushes
            {
                return base.OnContainedInteractStart(be, slot, byPlayer, blockSel);
            }

            else
            {
                ItemStack jarstack = slot.Itemstack;
                ItemStack jarcontent = this.GetContent(jarstack);

                string heldCode = heldStack.Collectible.Code.Path;

                if (jarcontent != null)
                {

                    //Process pigments into their respective paints
                    if (pigmentRecipes.ContainsKey(heldCode) && mediums.Contains(jarcontent.Collectible.Code))
                    {
                        if (pigmentRecipes.TryGetValue(heldCode, out AssetLocation recipeOutput))
                        {
                            jarcontent.Id = api.World.GetItem(recipeOutput).Id;
                            slot.MarkDirty();
                            be.MarkDirty(true);

                            //ItemStack.

                            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                            if (heldStack.StackSize < 1)
                            {
                                heldStack = null;
                            }

                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        }
                    }

                    //Make tempera medium
                    if (jarcontent.Item.Code.BeginsWith("game", "water") && heldStack.Collectible.Code.BeginsWith("game", "egg"))
                    {
                        jarcontent.Id = api.World.GetItem("vintagecanvas:tempera").Id;
                        slot.MarkDirty();
                        be.MarkDirty(true);

                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                        if (heldStack.StackSize < 1)
                        {
                            heldStack = null;
                        }

                        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    }

                    //Behaviour with palette in main hand
                    if (heldCode.StartsWith("palette"))
                    {
                        //Load additional paint
                        if (jarcontent.Collectible.Code.PathStartsWith("paint"))
                        {
                            string paintName = jarcontent.Collectible.Code.EndVariant();
                            int color = TextureUtil.PaintColors[paintName];

                            if (heldStack.Attributes.HasAttribute("slots"))
                            {
                                var slots = SerializerUtil.Deserialize<BlockEntityPalette.Slot[]>(heldStack.Attributes.GetBytes("slots"));

                                int activeslot = heldStack.Attributes.GetInt("activeslot");
                                BlockEntityPalette.Slot activeSlot = slots[activeslot];

                                float paintweight = 1f / (++activeSlot.fullness);
                                activeSlot.paintColor = TextureUtil.BlendColor(color, activeSlot.paintColor, paintweight);

                                slots[activeslot] = activeSlot;
                                heldStack.Attributes.SetBytes("slots", SerializerUtil.Serialize(slots));
                                heldStack.Attributes.SetInt("colorhash", BlockPalette.HashSlotColors(slots));

                                if (VintageCanvasModSystem.config.PaintDepletion)
                                {
                                    TryTakeLiquid(jarstack, 0.01f);
                                    DoLiquidMovedEffects(byPlayer, jarcontent, 1, EnumLiquidDirection.Pour);
                                }
                            }
                        }

                        //Clear slot w/ turpentine if shift key is held
                        if (jarcontent.Collectible.Code.PathStartsWith("turpentine"))
                        {
                            if (byPlayer.Entity.Controls.ShiftKey && heldStack.Attributes.HasAttribute("slots"))
                            {
                                var slots = SerializerUtil.Deserialize<BlockEntityPalette.Slot[]>(heldStack.Attributes.GetBytes("slots"));

                                int activeslot = heldStack.Attributes.GetInt("activeslot");
                                BlockEntityPalette.Slot activeSlot = slots[activeslot];

                                activeSlot.ClearSlot();

                                slots[activeslot] = activeSlot;


                                heldStack.Attributes.SetBytes("slots", SerializerUtil.Serialize(slots));
                                heldStack.Attributes.SetInt("colorhash", BlockPalette.HashSlotColors(slots));
                            }
                            else
                            {
                                string hint = Lang.Get("vintagecanvas:clear-palette");
                                (byPlayer.Entity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "clearpalette", hint);
                            }
                        }
                    }

                    if (heldCode.StartsWith("brush"))
                    {
                        //Incrementally decrease opacity when clicking on a turpentine jar
                        //When holding shift, fully clean the brush
                        if (jarcontent.Collectible.Code.PathStartsWith("turpentine"))
                        {
                            if (byPlayer.Entity.Controls.ShiftKey)
                            {
                                heldStack.Attributes.SetInt("paintcolor", 0);
                                heldStack.Attributes.SetFloat("opacity", 0);

                                if (VintageCanvasModSystem.config.PaintDepletion)
                                {
                                    heldStack.Attributes.SetInt("paintamount", 0);
                                }
                            }
                            else
                            {

                                float curOpacity = heldStack.Attributes.GetFloat("opacity", 1f);
                                heldStack.Attributes.SetFloat("opacity", 0.66f * curOpacity);
                                if (VintageCanvasModSystem.config.PaintDepletion && heldStack.Attributes.HasAttribute("paintamount"))
                                {
                                    heldStack.Attributes.SetInt("paintamount", heldStack.Attributes.GetInt("paintamount") + VintageCanvasModSystem.config.PixelsPerPaintUnit);
                                    this.TryTakeLiquid(jarstack, 0.01f);
                                    DoLiquidMovedEffects(byPlayer, jarcontent, 1, EnumLiquidDirection.Pour);
                                    be.MarkDirty();
                                    return true;
                                }
                            }
                        }

                        if (jarcontent.Collectible.Code.PathStartsWith("paint"))
                        {
                            string paintcode = jarcontent.Collectible.Code.ToString();
                            int paintcolor = paintColors[paintcode];
                            int curpaint = heldStack.Attributes.GetInt("paintcolor");
                            if (paintcolor != 0)
                            {
                                heldStack.Attributes.SetInt("paintcolor", paintcolor);
                                heldStack.Attributes.SetFloat("opacity", 1f);

                                string brushsize = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Item.Variant["size"];
                                int pickupcount = (brushsize) switch
                                {
                                    ("small") => 1,
                                    ("medium") => 2,
                                    ("large") => 4
                                };

                                if (VintageCanvasModSystem.config.PaintDepletion)
                                {
                                    if (heldStack.Attributes.GetInt("paintcolor") == curpaint)
                                    {
                                        int curamount = heldStack.Attributes.GetInt("paintamount");
                                        heldStack.Attributes.SetInt("paintamount", heldStack.Attributes.GetInt("paintamount") + (pickupcount * VintageCanvasModSystem.config.PixelsPerPaintUnit));
                                    }
                                    else
                                    {
                                        heldStack.Attributes.SetInt("paintamount", pickupcount * VintageCanvasModSystem.config.PixelsPerPaintUnit);
                                    }
                                    this.TryTakeLiquid(jarstack, pickupcount * 0.01f);
                                    DoLiquidMovedEffects(byPlayer, jarcontent, 1, EnumLiquidDirection.Pour);
                                    be.MarkDirty();
                                    return true;
                                }
                            }
                        }
                    }
                }
                return true;
            }
        }
        bool IContainedInteractable.OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            //First sees if there are brush storage interactions to be taken.
            if (BrushStorageInteraction(slot, byPlayer)) return true;            

            //Rerouted to be accessible from CollectibleBehaviourPaintTool (left-click behaviour emulation)
            return ContainerInteractions(be, slot, byPlayer, blockSel);           
        }

        private bool BrushStorageInteraction(ItemSlot slot, IPlayer byPlayer)
        {
            //Read any contained brush data
            ItemStack jarcontent = GetContent(slot.Itemstack);
            byte[] brushData = slot.Itemstack.Attributes.GetBytes("brushes");
            JarBrush[] brushes = [];
            if (brushData != null)
            {
                brushes = SerializerUtil.Deserialize<JarBrush[]>(brushData);
            }

            if (jarcontent == null)
            {
                ItemStack held = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

                //Try to place brush if crouching
                if (byPlayer.Entity.Controls.ShiftKey && held != null)
                {
                    if (held.Collectible is ItemBrush && brushes.Length < 3)
                    {
                        JarBrush brush = new JarBrush(
                            held.Collectible.Variant["size"],
                            held.Attributes.GetInt("paintcolor", 0),
                            held.Attributes.GetFloat("opacity", 1f));

                        JarBrush[] newBrushes = brushes.Concat([brush]).ToArray();
                        slot.Itemstack.Attributes.SetBytes("brushes", SerializerUtil.Serialize(newBrushes));
                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

                        return true;
                    }
                }

                //Try to retrieve brush otherwise
                else
                {
                    if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack == null && brushes.Length > 0)
                    {
                        string size = brushes.Last().size;

                        Item brush = api.World.GetItem(new AssetLocation("vintagecanvas:brush-" + size));
                        ItemStack giveStack = new ItemStack(brush, 1);
                        giveStack.Attributes.SetInt("paintcolor", brushes.Last().color);
                        giveStack.Attributes.SetFloat("opacity", brushes.Last().opacity);
                        byPlayer.InventoryManager.TryGiveItemstack(giveStack);

                        JarBrush[] newBrushes = brushes.RemoveAt(brushes.Length - 1);
                        slot.Itemstack.Attributes.SetBytes("brushes", SerializerUtil.Serialize(newBrushes));

                        return true;
                    }
                }
            }

            if (brushes.Length > 0)
            {
                return true;
            }

            return false;
        }

        [ProtoContract]
        private class JarBrush
        {
            [ProtoMember(1)]
            public string size = "small";
            [ProtoMember(2)]
            public int color = 0;
            [ProtoMember(3)]
            public float opacity = 1f;

            public JarBrush() { }

            public JarBrush(string size, int color, float opacity)
            {
                this.size = size;
                this.color = color;
                this.opacity = opacity;
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            BlockPaintJar bpj = inSlot.Itemstack.Block as BlockPaintJar;
            ItemStack jarcontent = bpj.GetContent(inSlot.Itemstack);
            if (jarcontent == null) 
            {
                dsc.AppendLine("\nFill with oil, then add a pigment to create your paint.");
                return;
            }
            if (jarcontent.Collectible.Code.PathStartsWith("paint"))
            {
                if (jarcontent.Collectible.Variant["color"] == "ultramarine") dsc.AppendLine("\nMade from crushed lapis lazuli.");
                else if (jarcontent.Collectible.Variant["color"] == "vermillion") dsc.AppendLine("\nMade from crushed cinnabar.");

                dsc.AppendLine("\nReady to be placed down and used to colour a paint brush or palette.");
            }
            if (jarcontent.Collectible.Code.PathStartsWith("turpentine")) dsc.AppendLine("\nClick with a paintbrush to dilute your paint, or sneak+click to clean it off completely.");
            if (jarcontent.Collectible.Code.PathStartsWith("oil")
                || jarcontent.Collectible.Code.PathStartsWith("tempera")) dsc.AppendLine("\nRight-click with a pigment to finish your paint.");
        }
        #endregion
    }
}
