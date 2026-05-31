using System;
using System.Collections.Generic;
using System.Text;
using VintageCanvas.src.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using static VintageCanvas.src.Entities.BlockEntityEasel;

namespace VintageCanvas.src.Items
{
    internal class ItemBrush : Item
    {
        //Keeping track of the brushes that have been renderred, and what colour they've been rendered in
        private Dictionary<int, MultiTextureMeshRef> MeshRefDict = new();
        private Dictionary<int, int> ColorDict = new();

        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack extractedStack = null)
        {
            //Only reliable way I could find of guaranteeing an ID check trigger whenever you obtain a canvas.
            if (extractedStack != null)
            {
                if (extractedStack.Attributes.TryGetInt("brushid") == null && api.Side == EnumAppSide.Server)
                {
                    int Id = IdRegistry.getCanvasId();
                    extractedStack.Attributes.SetInt("brushid", Id);
                    slot.MarkDirty();
                }
            }
            base.OnModifiedInInventorySlot(world, slot, extractedStack);
        }
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int? brushId = itemstack.Attributes.TryGetInt("brushid");
            if (brushId == null) { return; }
            bool updateMesh = false;

            //update brush mesh either when it has never been tesselated, or if the color doesn't match to the registered one
            if (!MeshRefDict.ContainsKey((int)brushId) && itemstack.Attributes.HasAttribute("paintcolor")) { updateMesh = true; }
            if (MeshRefDict.ContainsKey((int)brushId) && itemstack.Attributes.TryGetInt("paintcolor") != ColorDict[(int)brushId]) { updateMesh = true; }
            
            if (updateMesh)                 
            {
                //renderinfo.ModelRef.Dispose();

                //Create renderinfo
                int paintcolor = itemstack.Attributes.GetInt("paintcolor");
                int varietycolor = TextureUtil.BlendColor(-16119286, paintcolor, 0.15f);
                int[] pixeldata = [paintcolor, varietycolor, varietycolor, paintcolor];
                AssetLocation texLoc = new AssetLocation("vintagecanvas", brushId.ToString());

                MeshData mesh = TextureUtil.SwapPaintingTexture(
                    pixeldata,
                    2,
                    texLoc,
                    capi.Tesselator.GetTextureSource(itemstack.Item),
                    itemstack.Item.Code,
                    itemstack.Item.Shape,
                    capi
                    );
                
                var meshref = capi.Render.UploadMultiTextureMesh(mesh);

                MeshRefDict[(int)brushId] = meshref;
                ColorDict[(int)brushId] = paintcolor;
                renderinfo.ModelRef = meshref;
                mesh.Dispose();
                return;
                
            }
            else if(MeshRefDict.ContainsKey((int)brushId))
            {
                renderinfo.ModelRef = MeshRefDict[(int)brushId];
                return;
            }
        }
    }
}
