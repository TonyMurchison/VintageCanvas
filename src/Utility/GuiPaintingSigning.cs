using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VintageCanvas.src.Utility
{
    internal class GuiPaintingSigning : GuiDialogGeneric
    {

        protected int maxWidth = 400;
        public String Title;
        public bool IsSigned = false;
        protected CairoFont font = CairoFont.TextInput().WithFontSize(18);

        public GuiPaintingSigning(ICoreClientAPI capi) : base("", capi)
        {
            Compose();
        }
        protected virtual void Compose()
        {
            double lineHeight = font.GetFontExtents().Height * font.LineHeightMultiplier / RuntimeEnv.GUIScale;
            ElementBounds titleBounds = ElementBounds.Fixed(0, 30, maxWidth, 24);
            ElementBounds signButtonBounds = ElementBounds.FixedSize(0, 0).FixedUnder(titleBounds, 25).WithAlignment(EnumDialogArea.CenterFixed).WithFixedPadding(10, 2);

            // Padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(signButtonBounds);

            // Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0)
            ;

            SingleComposer = capi.Gui
                .CreateCompo("canvassigning", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("Name canvas"), OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddTextInput(titleBounds, null, CairoFont.TextInput().WithFontSize(18), "title")
                    .AddSmallButton(Lang.Get("Sign"), OnButtonSign, signButtonBounds)
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetTextInput("title").SetPlaceHolderText(Lang.Get("Canvas"));
            SingleComposer.GetTextInput("title").SetValue("");
            SingleComposer.GetTextInput("title").SetMaxLength(ModSystemEditableBook.MaxTitleLength);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            SingleComposer.FocusElement(SingleComposer.GetTextInput("title").TabIndex);
        }

        private bool OnButtonSign()
        {
            Title = SingleComposer.GetTextInput("title").GetText();
            IsSigned = true;
            TryClose();
            return true;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }
    }
}
