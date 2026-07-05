using HarmonyLib;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.WorldEdit;

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

        bool IContainedInteractable.OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemStack heldStack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (heldStack == null || heldStack.Collectible is ILiquidInterface liquidInterface)
            {
                return base.OnContainedInteractStart(be, slot, byPlayer, blockSel);
            }
            else
            {              
                ItemStack jarstack = slot.Itemstack;
                ItemStack jarcontent = this.GetContent(jarstack);

                string heldCode = heldStack.Collectible.Code.Path;

                if (jarcontent != null) {

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

                if (heldCode.StartsWith("brush"))
                    {
                        //Incrementally decrease opacity when clicking on a turpentine jar
                        //When holding shift, fully clean the brush
                        if (jarcontent.Collectible.Code.PathStartsWith("turpentine")){
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
    }
}
