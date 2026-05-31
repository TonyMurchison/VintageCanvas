using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory;
using Vintagestory.API.Client;

namespace VintageCanvas.src.Utility
{
    internal static class IdRegistry
    {   
        static public int currentId = 1;

        static public int getCanvasId()
        {
            return currentId++;
        }         
    }
}