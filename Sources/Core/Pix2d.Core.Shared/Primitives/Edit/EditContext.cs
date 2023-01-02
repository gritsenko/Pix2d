using System;
using Pix2d.Abstract;

namespace Pix2d.Services.Edit
{
    public class EditContext
    {
        public EditContextType EditContextType { get; set; } = EditContextType.General;

        public string Title => GetEditContextTitle();

        private string GetEditContextTitle()
        {
            throw new NotImplementedException();
            //if (EditContextType == Pix2dServices.EditService.DefaultEditContextType)
            //    return null;

            //switch (EditContextType)
            //{
            //    case EditContextType.General: return null;
            //    case EditContextType.Sprite: return "Sprite edit mode";
            //    case EditContextType.Vector: return "Vector edit mode";
            //    case EditContextType.Text: return "Text edit mode";
            //    default: return null;
            //}
        }

        public static EditContext FromEditContextType(EditContextType editContextType)
        {
            return new EditContext() {EditContextType = editContextType};
        }
    }
}