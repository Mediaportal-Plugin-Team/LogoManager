using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;

namespace LogoManager
{
    public class GUIDialogPreview : GUIDialogMenu
    {
        public const int ID = 4587;
        
        [SkinControlAttribute(6)]
        protected GUIImage Preview = null;

        public override int GetID
        {
            get { return ID; }
            
        }

        public GUIDialogPreview()
        {
            GetID = ID;
        }

        public string PreviewFilename
        {
            get { return Preview.FileName; }
            set { Preview.FileName = value; }
        }
   
        public override void OnAction(MediaPortal.GUI.Library.Action action)
        {
            //UpdatePreview();
            base.OnAction(action);
        }

        public void UpdatePreview(string filename)
        {
            Preview.SetFileName(listView.SelectedListItem.IconImage);
            //Process();
        }

        public override bool Init()
        {
            return Load(GUIGraphicsContext.Skin + @"\DialogPreview.xml");
        }
    }
}
