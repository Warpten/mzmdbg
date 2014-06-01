/*
 * Created by SharpDevelop.
 * User: Warpten
 * Date: 31/05/2014
 * Time: 18:13
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace mzmdbg
{
    /// <summary>
    /// Description of MainForm.
    /// </summary>
    public partial class MainForm : Form
    {
        private IROM _rom;

        public MainForm()
        {
            InitializeComponent();
        }
        
        public static void LogLine(string message)
        {
            var console = Application.OpenForms["ConsoleForm"];
            if (console == null)
                console = new ConsoleForm();
            if (console.Visible == false)
                console.Show();
            console.BringToFront();
            (console as ConsoleForm).LogLine(message);
        }
        
        public static void LogLine(string message, params object[] args)
        {
            LogLine(String.Format(message, args));
        }
        
        void OnDebuggerMenuToolStripClick(object sender, EventArgs e)
        {
            if (Application.OpenForms["DebuggerForm"] != null)
            {
                Application.OpenForms["DebuggerForm"].BringToFront();
                return;
            }
            var debuggerForm = new DebuggerForm();
            debuggerForm.Show();
        }
        
        void OnLoadROMRequest(object sender, EventArgs e)
        {
            OpenFileDialog o = new OpenFileDialog();
            o.Title = "Open ROM";
            o.Filter = "GBA ROMs|*.gba|GB ROMs|*.gb";
            if (o.ShowDialog() != DialogResult.OK)
                return;
            
            byte[] romBuffer = File.ReadAllBytes(o.FileName);
            var romExtension = Path.GetExtension(o.FileName).ToLower();
            if (romExtension == ".gba")
            {
                // GBCEmulator.LoadROM(romBuffer);
                throw new NotImplementedException();
                // _rom = new GBAROM(romBuffer);
            }
            else // if (romExtension == ".gb")
            {
                // throw new NotImplementedException();
                GBCEmulator.LoadROM(romBuffer);
            }
        }
        
        void OnConsoleToolStripClick(object sender, EventArgs e)
        {
            if (Application.OpenForms["ConsoleForm"] != null)
            {
                Application.OpenForms["ConsoleForm"].BringToFront();
                return;
            }
            var console = new ConsoleForm();
            console.Show();
        }
    }
}
