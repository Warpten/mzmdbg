/*
 * Created by SharpDevelop.
 * User: perquet
 * Date: 31/05/2014
 * Time: 18:13
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace mzmdbg
{
    partial class MainForm
    {
        /// <summary>
        /// Designer variable used to keep track of non-visual components.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        
        /// <summary>
        /// Disposes resources used by the form.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                if (components != null) {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }
        
        /// <summary>
        /// This method is required for Windows Forms designer support.
        /// Do not change the method contents inside the source code editor. The Forms designer might
        /// not be able to load this method if it was changed manually.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.loadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadGBARomToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveStateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadSaveStateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.controlsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debuggerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.consoleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gbcRendererControl = new OpenTK.GLControl();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            resources.ApplyResources(this.statusStrip1, "statusStrip1");
            this.statusStrip1.Name = "statusStrip1";
            // 
            // menuStrip1
            // 
            resources.ApplyResources(this.menuStrip1, "menuStrip1");
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                                    this.loadToolStripMenuItem,
                                    this.debuggerToolStripMenuItem,
                                    this.consoleToolStripMenuItem});
            this.menuStrip1.Name = "menuStrip1";
            // 
            // loadToolStripMenuItem
            // 
            this.loadToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                                    this.loadGBARomToolStripMenuItem,
                                    this.saveStateToolStripMenuItem,
                                    this.loadSaveStateToolStripMenuItem,
                                    this.controlsToolStripMenuItem});
            this.loadToolStripMenuItem.Name = "loadToolStripMenuItem";
            resources.ApplyResources(this.loadToolStripMenuItem, "loadToolStripMenuItem");
            // 
            // loadGBARomToolStripMenuItem
            // 
            this.loadGBARomToolStripMenuItem.Name = "loadGBARomToolStripMenuItem";
            resources.ApplyResources(this.loadGBARomToolStripMenuItem, "loadGBARomToolStripMenuItem");
            this.loadGBARomToolStripMenuItem.Click += new System.EventHandler(this.OnLoadROMRequest);
            // 
            // saveStateToolStripMenuItem
            // 
            this.saveStateToolStripMenuItem.Name = "saveStateToolStripMenuItem";
            resources.ApplyResources(this.saveStateToolStripMenuItem, "saveStateToolStripMenuItem");
            // 
            // loadSaveStateToolStripMenuItem
            // 
            this.loadSaveStateToolStripMenuItem.Name = "loadSaveStateToolStripMenuItem";
            resources.ApplyResources(this.loadSaveStateToolStripMenuItem, "loadSaveStateToolStripMenuItem");
            // 
            // controlsToolStripMenuItem
            // 
            this.controlsToolStripMenuItem.Name = "controlsToolStripMenuItem";
            resources.ApplyResources(this.controlsToolStripMenuItem, "controlsToolStripMenuItem");
            this.controlsToolStripMenuItem.Click += new System.EventHandler(this.OnControlsMenuToolStrip);
            // 
            // debuggerToolStripMenuItem
            // 
            this.debuggerToolStripMenuItem.Name = "debuggerToolStripMenuItem";
            resources.ApplyResources(this.debuggerToolStripMenuItem, "debuggerToolStripMenuItem");
            this.debuggerToolStripMenuItem.Click += new System.EventHandler(this.OnDebuggerMenuToolStripClick);
            // 
            // consoleToolStripMenuItem
            // 
            this.consoleToolStripMenuItem.Name = "consoleToolStripMenuItem";
            resources.ApplyResources(this.consoleToolStripMenuItem, "consoleToolStripMenuItem");
            this.consoleToolStripMenuItem.Click += new System.EventHandler(this.OnConsoleToolStripClick);
            // 
            // gbcRendererControl
            // 
            this.gbcRendererControl.BackColor = System.Drawing.Color.Black;
            this.gbcRendererControl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            resources.ApplyResources(this.gbcRendererControl, "gbcRendererControl");
            this.gbcRendererControl.Name = "gbcRendererControl";
            this.gbcRendererControl.VSync = true;
            this.gbcRendererControl.Load += new System.EventHandler(this.OnGlLoad);
            this.gbcRendererControl.Paint += new System.Windows.Forms.PaintEventHandler(this.OnGamePaint);
            this.gbcRendererControl.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnKeyDown);
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gbcRendererControl);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MainMenuStrip = this.menuStrip1;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainForm";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        private OpenTK.GLControl gbcRendererControl;
        private System.Windows.Forms.ToolStripMenuItem consoleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debuggerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem controlsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadSaveStateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveStateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadGBARomToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loadToolStripMenuItem;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.StatusStrip statusStrip1;
    }
}
