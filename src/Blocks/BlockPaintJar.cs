using System;
using System.Collections.Generic;
using System.IO;
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
    public class BlockPaintJar : BlockLiquidContainerTopOpened, IContainedInteractable
    {
        //Pull paint recipes and paint mediums from blocktypes/paintjar.json
        private Dictionary<string, AssetLocation> pigmentRecipes = new();
        private Dictionary<string, int> paintColors = new();
        private List<string> mediums = new();
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
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

                            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                            if (heldStack.StackSize < 1)
                            {
                                heldStack = null;
                            }

                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        }                        
                    }

                if (heldCode.StartsWith("brush"))
                    {
                        //Incrementally decrease opacity when clicking on a turpentine jar
                        if (jarcontent.Collectible.Code.PathStartsWith("turpentine")){
                            float curOpacity = heldStack.Attributes.GetFloat("opacity", 1f);
                            heldStack.Attributes.SetFloat("opacity", 0.66f * curOpacity);
                        }

                        if (jarcontent.Collectible.Code.PathStartsWith("paint"))
                        {
                            
                            string paintcode = jarcontent.Collectible.Code.ToString();
                            int paintcolor = paintColors[paintcode];
                            if (paintcolor != 0)
                            {
                                heldStack.Attributes.SetInt("paintcolor", paintcolor);
                                heldStack.Attributes.SetFloat("opacity", 1f);
                            }
                        }
                    }
                }
                return true;
            }
        }
    }
}
