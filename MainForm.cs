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
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace mzmdbg
{
    /// <summary>
    /// Description of MainForm.
    /// </summary>
    public partial class MainForm : Form
    {
        private IROM _rom;
        private bool _gbcRendererLoaded = false;
        private bool _gbaRendererLoaded = false;

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
                gbcRendererControl.Top = (this.ClientSize.Height - 144) / 2;
                gbcRendererControl.Left = (this.ClientSize.Width - 160) / 2;
                gbcRendererControl.Width = 160;
                gbcRendererControl.Height = 144;
                gbcRendererControl.Show();
                
                GBCEmulator.LoadROM(romBuffer, ref gbcRendererControl);
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
        
        void OnGlLoad(object sender, EventArgs e)
        {
            _gbcRendererLoaded = true;
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, gbcRendererControl.Width, 0, gbcRendererControl.Height, -1, 1);
            GL.Viewport(0, 0, gbcRendererControl.Width, gbcRendererControl.Height);
        }
        
        void OnGamePaint(object sender, PaintEventArgs e)
        {
            if (!_gbcRendererLoaded)
                return;
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            gbcRendererControl.SwapBuffers();
        }
        
        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_gbcRendererLoaded && !_gbaRendererLoaded)
                GBCEmulator.OnKeyPress(e.KeyCode);
            // else if (!_gbcRendererLoaded && _gbaRendererLoaded)
            //     GBAEmulator.OnKeyPress(e.KeyCode);
        }
        
        void OnControlsMenuToolStrip(object sender, EventArgs e)
        {
            if (Application.OpenForms["ControlsForm"] != null)
            {
                Application.OpenForms["ControlsForm"].BringToFront();
                return;
            }
            var console = new ControlsForm();
            console.Show();
        }
    }
}
