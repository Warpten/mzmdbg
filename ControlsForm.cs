/*
 * Created by SharpDevelop.
 * User: Warpten
 * Date: 03/06/2014
 * Time: 16:42
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Configuration;

namespace mzmdbg
{
    /// <summary>
    /// Description of ControlsForm.
    /// </summary>
    public partial class ControlsForm : Form
    {
        public ControlsForm()
        {
            //
            // The InitializeComponent() call is required for Windows Forms designer support.
            //
            InitializeComponent();
        }
        
        void OnFormLoad(object sender, EventArgs e)
        {
            var keyA = Keys.A;
            var keyB = Keys.Z;
            var keyStart = Keys.Space;
            var keySelect = Keys.Enter;
            var keyUp = Keys.Up;
            var keyDown = Keys.Down;
            var keyLeft = Keys.Left;
            var keyRight = Keys.Right;
            var keyLT = Keys.Q;
            var keyRT = Keys.S;
            
            UpdateKeybind("ControlA", ref keyA);
            UpdateKeybind("ControlB", ref keyB);
            UpdateKeybind("ControlStart", ref keyStart);
            UpdateKeybind("ControlSelect", ref keySelect);
            UpdateKeybind("ControlLT", ref keyLT);
            UpdateKeybind("ControlRT", ref keyRT);
        }
        
        private void UpdateKeybind(string settingName, ref Keys value)
        {
            if (ConfigurationManager.AppSettings[settingName].CompareTo(value.ToString()) != 0)
                value = (Keys)Enum.Parse(typeof(Keys), ConfigurationManager.AppSettings[settingName]);
            
            Control[] ctrls = this.Controls.Find("textbox" + settingName.Substring(7), false);
            if (ctrls != null && ctrls.Length == 1)
            {
                (ctrls[0] as TextBox).Text = value.ToString();
                (ctrls[0] as TextBox).KeyUp += (sender, e) =>
                {
                    var keyName = (e as KeyEventArgs).KeyCode;
                    (sender as TextBox).Text = keyName.ToString();
                    // TODO: refuse if new keybind is already used
                };
                (ctrls[0] as TextBox).KeyPress += (sender, e) =>
                {
                    (sender as TextBox).Text = "";
                };
            }
        }
        
        void SaveKeybinds(object sender, EventArgs e)
        {
            Configuration conf = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            foreach (var control in this.Controls)
            {
                TextBox ctrl = (control as TextBox);
                if (ctrl == null)
                    continue;

                ConfigurationManager.AppSettings.Set((string)ctrl.Tag, ctrl.Text);
            }
            MessageBox.Show("Keybinds saved.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            conf.Save(ConfigurationSaveMode.Modified); //! TODO: Figure out why nonworking
        }
    }
}
