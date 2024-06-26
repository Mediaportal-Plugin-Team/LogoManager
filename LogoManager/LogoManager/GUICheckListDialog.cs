﻿using System.Collections.Generic;
using MediaPortal.GUI.Library;
using MediaPortal.Dialogs;

namespace LogoManager
{
    public enum ModalResult
    {
        None = 1,
        OK = 2,
        Cancel = 4
    }
    public class GUICheckListDialog : GUIDialogMenu

    {
        #region Skin attributes
        [SkinControlAttribute(3)]
        public GUICheckListControl SelectionList = null;

        [SkinControlAttribute(10)]
        protected GUIButtonControl btnOK = null;

        [SkinControlAttribute(11)]
        protected GUIButtonControl btnCancel = null;
        #endregion

        #region Attributes
        public ModalResult DialogModalResult = ModalResult.None;
        protected bool m_bRunning = true;
        public List<GUIListItem> ListItems = new List<GUIListItem>();
        public const int ID = 2101;

        #endregion

        #region Overrides
        public GUICheckListDialog()
        {
            GetID = ID;
        }
        
        public override bool Init()
        {
            return Load(GUIGraphicsContext.Skin + @"\DialogCheckList.xml");
        }

        public override int GetID
        {
            get
            {
                return ID;
            }
        }

        
        public override string GetModuleName()
        {
            return "MultiSelectDialog";
        }

        protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
        {
            if (control == btnOK)
            {
                DialogModalResult = ModalResult.OK;
                Close();
            }

            if (control == btnCancel)
            {
                DialogModalResult = ModalResult.Cancel;
                Close();
            }

            if (control == SelectionList && actionType == Action.ActionType.ACTION_SELECT_ITEM)
            {
                if (SelectionList.SelectedListItem != null)
                {
                    SelectionList.SelectedListItem.Selected = !SelectionList.SelectedListItem.Selected;

                    return;
                }
            }

            base.OnClicked(controlId, control, actionType);
        }

        public override void DoModal(int dwParentId)
        {
            m_bRunning = true;
            DialogModalResult = ModalResult.None;
            base.DoModal(dwParentId);
        }

        public new void Reset()
        {
            ListItems.Clear();
            base.Reset();
        }

        public new void Add(string strLabel)
        {
            int iItemIndex = ListItems.Count + 1;
            GUIListItem pItem = new GUIListItem();
            if (base.ShowQuickNumbers)
                pItem.Label = iItemIndex.ToString() + " " + strLabel;
            else
                pItem.Label = strLabel;

            pItem.ItemId = iItemIndex;
            ListItems.Add(pItem);

            base.Add(strLabel);
        }

        public new void Add(GUIListItem pItem)
        {
            ListItems.Add(pItem);
            base.Add(pItem);
        }

        #endregion

        #region Virtual methods
        public virtual void Close()
        {
            if (m_bRunning == false) return;
            m_bRunning = false;
            GUIWindowManager.IsSwitchingToNewWindow = true;
            lock (this)
            {
                GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT, GetID, 0, 0, 0, 0, null);
                base.OnMessage(msg);

                GUIWindowManager.UnRoute();
                m_bRunning = false;
            }
            GUIWindowManager.IsSwitchingToNewWindow = false;
        }
        #endregion
    }
}
