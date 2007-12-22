using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using Telephony;

namespace Sipek
{
  public partial class MainForm : Form
  {
    const string HEADER_TEXT = "Sipek2";
 
    Timer tmr = new Timer();  // Refresh Call List
    EUserStatus _lastUserStatus = EUserStatus.AVAILABLE;

    public MainForm()
    {
      InitializeComponent();

      // Initlialize telephony and set proxies
      Telephony.CCallManager.CommonProxy = new Telephony.CSipCommonProxy();
      Telephony.CCallManager.MediaProxy = new Telephony.CMediaPlayerProxy();
      Telephony.CCallManager.CallProxy = new Telephony.CSipCallProxy();

      Telephony.CCallManager.CallLog = CCallLog.getInstance();


      // register callback
      Telephony.CCallManager.getInstance().CallStateChanged += onTelephonyRefresh;
      Telephony.CCallManager.getInstance().MessageReceived += onMessageReceived;
      Telephony.CCallManager.getInstance().BuddyStatusChanged += onBuddyStateChanged;

      Telephony.CCallManager.getInstance().initialize();

      // Initialize & load Call Log
      CCallLog.getInstance().load();

      // Initialize dial combo box
      toolStripComboDial.Items.Clear();
      Stack<CCallRecord> clist = CCallLog.getInstance().getList(ECallType.EDialed);
      foreach (CCallRecord item in clist)
      {
        toolStripComboDial.Items.Add(item.Number);
      }
      this.UpdateCallRegister();

      // Init Buddy list
      this.UpdateBuddyList();

      // Set user status
      toolStripComboBoxUserStatus.SelectedIndex = (int)EUserStatus.AVAILABLE;

      // timer 
      tmr.Interval = 1000;
      tmr.Tick += new EventHandler(UpdateCallTimeout);
    }

    /////////////////////////////////////////////////////////////////////////////////

    private void RefreshForm()
    {
      // Update Call Status
      UpdateCallLines();

      // Update Account Status
      UpdateAccountList();

      // Update Call Register
      UpdateCallRegister();

      // Update Buddy List
      UpdateBuddyList();

      // Refresh toolstripbuttons
      toolStripButtonDND.Checked = CSettings.DND;
      toolStripButtonAA.Checked = CSettings.AA;
      toolStripButtonCFU.Checked = CSettings.CFU;
    }

    private void UpdateAccountList()
    {
      listViewAccounts.Items.Clear();

      int size = CAccounts.getInstance().getSize();
      for (int i = 0; i < size; i++)
      {
        CAccount acc = CAccounts.getInstance()[i];
        string name;

        if (acc.Name.Length == 0)
        {
          name = "--empty--";
        }
        else
        {
          name = acc.Name;
        }

        if (acc.Index == CAccounts.getInstance().DefAccountIndex)
        {
          // todo!!! Coloring!

          // check registration status
          if (acc.RegState == 200)
          {
            toolStripStatusLabel.Text = "Registered"; //acc.Name + " - " + acc.DisplayName; 
            this.Text = HEADER_TEXT + " - " + acc.Name + " (" + acc.DisplayName + ")";
          }
          else if (acc.RegState == 0)
          {
            this.Text = HEADER_TEXT;
            toolStripStatusLabel.Text = "Registering...";
          }
          else
          {
            this.Text = HEADER_TEXT;
            toolStripStatusLabel.Text = "Not registered!";
          }
        }


        ListViewItem item = new ListViewItem(new string[] { name, acc.RegState.ToString() });

        listViewAccounts.Items.Add(item);
      }
    }

    private void UpdateCallRegister()
    {
      listViewCallRegister.Items.Clear();

      Stack<CCallRecord> results = CCallLog.getInstance().getList();
      
      foreach (CCallRecord item in results)
      {
        string duration = item.Duration.ToString();
        if (duration.IndexOf('.') > 0) duration = duration.Remove(duration.IndexOf('.')); // remove miliseconds

        string recorditem = item.Number;
        CBuddyRecord rec = null;
        int buddyId = CBuddyList.getInstance().getBuddyId(item.Number);
        if (buddyId > -1)
        {
          string name = "";
          rec = CBuddyList.getInstance()[buddyId];
          name = rec.FirstName + " " + rec.LastName;
          name = name.Trim();
          recorditem = name + ", " + item.Number;
        }

        ListViewItem lvi = new ListViewItem(new string[] {
             item.Type.ToString(), recorditem.Trim(), item.Time.ToString(), duration});

        lvi.Tag = item;
        
        listViewCallRegister.Items.Add(lvi);
      }
    }

    //////////////////////////////////////////////////////////////////////////////////////
    /// 
    delegate void StateChangedDelegate();
    delegate void MessageReceivedDelegate(string from, string message);
    delegate void BuddyStateChangedDelegate(int buddyId, int status, string text);

    public void onTelephonyRefresh()
    {
      if (this.Created)
        this.BeginInvoke(new StateChangedDelegate(this.RefreshForm));
    }

    public void onMessageReceived(string from, string message)
    {
      if (this.Created)
        this.BeginInvoke(new MessageReceivedDelegate(this.MessageReceived), new object[] { from, message });
    }

    public void onBuddyStateChanged(int buddyId, int status, string text)
    {
      if (this.Created)
        this.BeginInvoke(new BuddyStateChangedDelegate(this.BuddyStateChanged), new object[] { buddyId, status, text});
    }


    /////////////////////////////////////////////////////////////////////////////////////
    /// Buddy List Methods
    #region Buddy List Methods

    private void UpdateBuddyList()
    {
      Dictionary<int, CBuddyRecord> results = CBuddyList.getInstance().getList();
      listViewBuddies.Items.Clear();
      foreach (KeyValuePair<int, CBuddyRecord> kvp in results)
      {
        string status;
        switch (kvp.Value.Status)
        {
          case 0: status = "unknown"; break;
          case 1: status = "online"; break;
          case 2: status = "offline"; break;
          default: status = "?"; break;
        }
        //if ((null != kvp.Value.StatusText) && (kvp.Value.StatusText.Length > 0))
        //{
        //  status += " (" + kvp.Value.StatusText + ")";
        //}
        status = kvp.Value.StatusText;

        ListViewItem item = new ListViewItem(new string[] { kvp.Value.FirstName + kvp.Value.LastName, status });
        item.Tag = kvp.Value.Id;
        //item.BackColor = Color.Blue;
        listViewBuddies.Items.Add(item);
      }
    }

    private void toolStripMenuItemAdd_Click(object sender, EventArgs e)
    {
      (new BuddyForm()).ShowDialog();
    }

    private void tabPageBuddies_Enter(object sender, EventArgs e)
    {
      UpdateBuddyList();
    }

    private void BuddyStateChanged(int buddyId, int status, string text)
    {
      CBuddyList.getInstance()[buddyId].Status = status;
      CBuddyList.getInstance()[buddyId].StatusText = text;
      this.RefreshForm();
    }

    private void MessageReceived(string from, string message)
    {
      // extract buddy ID
      string buddyId = parseFrom(from);

      // check if ChatForm already opened
      foreach (Form ctrl in Application.OpenForms)
      {
        if (ctrl.Name == "ChatForm")
        {
          ((ChatForm)ctrl).BuddyName = buddyId;
          ((ChatForm)ctrl).LastMessage = message;
          ctrl.Focus();
          return;
        }
      }

      // if not, create new instance
      ChatForm bf = new ChatForm();
      int id = CBuddyList.getInstance().getBuddyId(buddyId);
      if (id >= 0)
      {
        //_buddyId = id;        
        CBuddyRecord buddy = CBuddyList.getInstance()[id];
        //_titleText.Caption = buddy.FirstName + ", " + buddy.LastName;
        bf.BuddyId = (int)id;
      }
      bf.BuddyName = buddyId;
      bf.LastMessage = message;
      bf.ShowDialog();
    }

    private string parseFrom(string from)
    {
      string number = from.Replace("<sip:", "");

      int atPos = number.IndexOf('@');
      if (atPos >= 0)
      {
        number = number.Remove(atPos);
        int first = number.IndexOf('"');
        if (first >= 0)
        {
          int last = number.LastIndexOf('"');
          number = number.Remove(0,last + 1);
          number = number.Trim();
        }
      }
      else
      {
        int semiPos = number.IndexOf(';');
        if (semiPos >= 0)
        {
          number = number.Remove(semiPos);
        }
        else
        {
          int colPos = number.IndexOf(':');
          if (colPos >= 0)
          {
            number = number.Remove(colPos);
          }
        }
      }
      return number;
    }

    private void toolStripMenuItemIM_Click(object sender, EventArgs e)
    {
      if (listViewBuddies.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewBuddies.SelectedItems[0];
        ChatForm bf = new ChatForm();
        bf.BuddyId = (int)lvi.Tag;
        bf.ShowDialog();
      }

    }

    private void toolStripMenuItemEdit_Click(object sender, EventArgs e)
    {
      if (listViewBuddies.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewBuddies.SelectedItems[0];

        BuddyForm bf = new BuddyForm();
        bf.BuddyId = (int)lvi.Tag;
        bf.ShowDialog();
      }
    }
    #endregion


    private void exitToolStripMenuItem_Click(object sender, EventArgs e)
    {
      Telephony.CCallManager.getInstance().shutdown();
      this.Close();
    }

    private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
    {
      (new AboutBox()).ShowDialog();
    }

    private void toolStripMenuItem1_Click(object sender, EventArgs e)
    {
      (new SettingsForm()).ShowDialog();
    }

    /// <summary>
    /// Enable or disable menu items regarding to call state...
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void contextMenuStripCalls_Opening(object sender, CancelEventArgs e)
    {
      // Hide all items...
      foreach (ToolStripMenuItem mi in contextMenuStripCalls.Items)
      {
        mi.Visible = false;
      }

      if (listViewCallLines.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewCallLines.SelectedItems[0];

        if (Telephony.CCallManager.getInstance().Count <= 0)
        {
          return;
        }
        else
        {
          EStateId stateId = Telephony.CCallManager.getInstance().getCall((int)lvi.Tag).getStateId();
          switch (stateId)
          {
            case EStateId.INCOMING:
              acceptToolStripMenuItem.Visible = true;
              transferToolStripMenuItem.Visible = true;
              break;
            case EStateId.ACTIVE:
              holdRetrieveToolStripMenuItem.Text = "Hold";
              holdRetrieveToolStripMenuItem.Visible = true;
              transferToolStripMenuItem.Visible = true;
              break;
            case EStateId.HOLDING:
              holdRetrieveToolStripMenuItem.Text = "Retrieve";
              holdRetrieveToolStripMenuItem.Visible = true;
              break;
          }

        }
        // call
        releaseToolStripMenuItem.Visible = true;
      }
    }

    private void tabPageAccounts_Enter(object sender, EventArgs e)
    {
      UpdateAccountList();
    }

    private void MainForm_Activated(object sender, EventArgs e)
    {
      // Refresh data
      //RefreshForm();
      //UpdateBuddyList();
    }

    ///////////////////////////////////////////////////////////////////////////////////
    // Call Related Methods
    #region Call Related Methods

    /// <summary>
    /// UpdateCallLines delegate
    /// </summary>
    private void UpdateCallLines()
    {     
      listViewCallLines.Items.Clear();

      try
      {
        // get entire call list
        Dictionary<int, Telephony.CStateMachine> callList = Telephony.CCallManager.getInstance().CallList;

        foreach (KeyValuePair<int, Telephony.CStateMachine> kvp in callList)
        {
          string number = kvp.Value.CallingNo;
          //string name = kvp.Value.CallingName; //TODO:::get Calling name

          string duration = kvp.Value.Duration.ToString();
          if (duration.IndexOf('.') > 0) duration = duration.Remove(duration.IndexOf('.')); // remove miliseconds

          ListViewItem lvi = new ListViewItem(new string[] {
            kvp.Value.getStateName(), number, duration});

          lvi.Tag = kvp.Value.Session;
          listViewCallLines.Items.Add(lvi);
          lvi.Selected = true;

          // display info
          //toolStripStatusLabel1.Text = item.Value.lastInfoMessage;
        }


        if (callList.Count > 0)
        {
          // control refresh timer
          tmr.Start();

          // Remember last status
          if (toolStripComboBoxUserStatus.SelectedIndex != (int)EUserStatus.OTP) 
            _lastUserStatus = (EUserStatus)toolStripComboBoxUserStatus.SelectedIndex;

          // Set user status "On the Phone"
          toolStripComboBoxUserStatus.SelectedIndex = (int)EUserStatus.OTP;
        }
        else
        {
          toolStripComboBoxUserStatus.SelectedIndex = (int)_lastUserStatus;
        }

      }
      catch (Exception e)
      {
        // TODO!!!!!!!!!!! Sychronize SHARED RESOURCES!!!!
      }
      //listViewCallLines.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
    }

    public void UpdateCallTimeout(object sender, EventArgs e)
    {
      if (listViewCallLines.Items.Count == 0) return;

      for (int i = 0; i < listViewCallLines.Items.Count; i++ )
      {
        ListViewItem item = listViewCallLines.Items[i];
        CStateMachine sm = CCallManager.getInstance().getCall((int)item.Tag);
        if (null == sm) continue;

        string duration = sm.RuntimeDuration.ToString();
        if (duration.IndexOf('.') > 0) duration = duration.Remove(duration.IndexOf('.')); // remove miliseconds

        item.SubItems[2].Text = duration;
      }
      // restart timer
      if (listViewCallLines.Items.Count > 0)
      {
        tmr.Start();
      }

    }

    private void placeACallToolStripMenuItem_Click(object sender, EventArgs e)
    {
      if (listViewBuddies.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewBuddies.SelectedItems[0];

        CBuddyRecord rec = CBuddyList.getInstance().getRecord((int)lvi.Tag);
        if (rec != null)
        {
          Telephony.CCallManager.getInstance().createSession(rec.Number);
        }
      }
    }

    private void toolStripButtonHoldRetrieve_Click(object sender, EventArgs e)
    {
      if (listViewCallLines.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewCallLines.SelectedItems[0];

        Telephony.CCallManager.getInstance().onUserHoldRetrieve((int)lvi.Tag);
      }
    }

    private void toolStripButtonCall_Click(object sender, EventArgs e)
    {
      if (toolStripComboDial.Text.Length > 0)
      {
        //makeCall(toolStripComboDial.Text);
        Telephony.CCallManager.getInstance().createSession(toolStripComboDial.Text);
      }
    }

    private void releaseToolStripMenuItem_Click(object sender, EventArgs e)
    {
      if (listViewCallLines.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewCallLines.SelectedItems[0];
        //telephoneObj.releaseCall((int)lvi.Tag);
        Telephony.CCallManager.getInstance().onUserRelease((int)lvi.Tag);
      }
    }

    private void toolStripComboDial_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyValue == 0x0d)
      {
        if (toolStripComboDial.Text.Length > 0)
        {
          //makeCall(toolStripComboDial.Text);
          Telephony.CCallManager.getInstance().createSession(toolStripComboDial.Text);
        }
      }
    }

    private void listViewCallRegister_DoubleClick(object sender, EventArgs e)
    {
      if (listViewCallRegister.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewCallRegister.SelectedItems[0];
        CCallRecord record = (CCallRecord)lvi.Tag;
        Telephony.CCallManager.getInstance().createSession(record.Number);
      }
    }

    private void acceptToolStripMenuItem_Click(object sender, EventArgs e)
    {
      if (listViewCallLines.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewCallLines.SelectedItems[0];
        Telephony.CCallManager.getInstance().onUserAnswer((int)lvi.Tag);
      }
    }

    #endregion

    private void removeToolStripMenuItem_Click(object sender, EventArgs e)
    {
      if (listViewCallRegister.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewCallRegister.SelectedItems[0];
        CCallRecord record = (CCallRecord) lvi.Tag;
        CCallLog.getInstance().deleteRecord(record);
      }
      this.UpdateCallRegister();

    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
      CCallLog.getInstance().save();
      CBuddyList.getInstance().save();
    }

    private void toolStripTextBoxTransferTo_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyValue == 0x0d)
      {
        if (listViewCallLines.SelectedItems.Count > 0)
        {
          ListViewItem lvi = listViewCallLines.SelectedItems[0];
          if (toolStripTextBoxTransferTo.Text.Length > 0)
          {
            Telephony.CCallManager.getInstance().onUserTransfer((int)lvi.Tag, toolStripTextBoxTransferTo.Text);
          }
        }
        contextMenuStripCalls.Close();
      }
    }

    private void toolStripButtonDND_Click(object sender, EventArgs e)
    {
      CSettings.DND = toolStripButtonDND.Checked;
    }

    private void toolStripButtonAA_Click(object sender, EventArgs e)
    {
      CSettings.AA = toolStripButtonAA.Checked;
    }

    private void toolStripButtonCFU_Click(object sender, EventArgs e)
    {
      CSettings.CFU = toolStripButtonCFU.Checked;
    }

    private void sendInstantMessageToolStripMenuItem_Click(object sender, EventArgs e)
    {
      if (listViewCallRegister.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewCallRegister.SelectedItems[0];
        CCallRecord record = (CCallRecord)lvi.Tag;
        int id = CBuddyList.getInstance().getBuddyId(record.Number);
        if (id > 0)
        {
          ChatForm bf = new ChatForm();
          bf.BuddyId = id;
          bf.ShowDialog();
        }
      }
    }

    private void toolStripComboBoxUserStatus_SelectedIndexChanged(object sender, EventArgs e)
    {
      /*
       * AVAILABLE, BUSY, OTP, IDLE, AWAY, BRB, OFFLINE
       * 
      Available
      Busy
      On the Phone
      Idle
      Away
      Be Right Back
      Offline
       */

      EUserStatus status = (EUserStatus)toolStripComboBoxUserStatus.SelectedIndex;

      CCallManager.CommonProxy.setStatus(CAccounts.getInstance().DefAccountIndex, status);
    }

    private void toolStripKeyboardButton_Click(object sender, EventArgs e)
    {
      (new KeyboardForm(this)).Show();
    }

    public void onUserDialDigit(string digits)
    {
      if (listViewCallLines.SelectedItems.Count > 0)
      {
        ListViewItem lvi = listViewCallLines.SelectedItems[0];
        Telephony.CCallManager.getInstance().onUserDialDigit((int)lvi.Tag, digits, 0);
      }
    }
  }
}