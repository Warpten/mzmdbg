/*
 * Created by SharpDevelop.
 * User: perquet
 * Date: 01/06/2014
 * Time: 11:14
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace mzmdbg
{
	partial class ConsoleForm
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
			this.consoleRTB = new System.Windows.Forms.RichTextBox();
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.clearToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuStrip1.SuspendLayout();
			this.SuspendLayout();
			// 
			// consoleRTB
			// 
			this.consoleRTB.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
									| System.Windows.Forms.AnchorStyles.Left) 
									| System.Windows.Forms.AnchorStyles.Right)));
			this.consoleRTB.BackColor = System.Drawing.SystemColors.ControlText;
			this.consoleRTB.BorderStyle = System.Windows.Forms.BorderStyle.None;
			this.consoleRTB.Cursor = System.Windows.Forms.Cursors.Default;
			this.consoleRTB.DetectUrls = false;
			this.consoleRTB.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.consoleRTB.ForeColor = System.Drawing.SystemColors.Menu;
			this.consoleRTB.Location = new System.Drawing.Point(0, 27);
			this.consoleRTB.Name = "consoleRTB";
			this.consoleRTB.ReadOnly = true;
			this.consoleRTB.Size = new System.Drawing.Size(327, 235);
			this.consoleRTB.TabIndex = 0;
			this.consoleRTB.Text = "";
			// 
			// menuStrip1
			// 
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
									this.clearToolStripMenuItem});
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(330, 24);
			this.menuStrip1.TabIndex = 1;
			this.menuStrip1.Text = "menuStrip1";
			// 
			// clearToolStripMenuItem
			// 
			this.clearToolStripMenuItem.Name = "clearToolStripMenuItem";
			this.clearToolStripMenuItem.Size = new System.Drawing.Size(46, 20);
			this.clearToolStripMenuItem.Text = "Clear";
			this.clearToolStripMenuItem.Click += new System.EventHandler(this.OnClickMenuClear);
			// 
			// ConsoleForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(330, 264);
			this.Controls.Add(this.consoleRTB);
			this.Controls.Add(this.menuStrip1);
			this.MainMenuStrip = this.menuStrip1;
			this.Name = "ConsoleForm";
			this.Text = "Console";
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();
		}
		private System.Windows.Forms.ToolStripMenuItem clearToolStripMenuItem;
		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.RichTextBox consoleRTB;
	}
}
