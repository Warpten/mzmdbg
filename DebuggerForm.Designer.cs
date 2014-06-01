/*
 * Created by SharpDevelop.
 * User: perquet
 * Date: 31/05/2014
 * Time: 18:16
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace mzmdbg
{
	partial class DebuggerForm
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
			this.debuggerTabControl = new System.Windows.Forms.TabControl();
			this.disassemblerView = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.debuggerTabControl.SuspendLayout();
			this.SuspendLayout();
			// 
			// debuggerTabControl
			// 
			this.debuggerTabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
									| System.Windows.Forms.AnchorStyles.Left) 
									| System.Windows.Forms.AnchorStyles.Right)));
			this.debuggerTabControl.Controls.Add(this.disassemblerView);
			this.debuggerTabControl.Controls.Add(this.tabPage2);
			this.debuggerTabControl.Location = new System.Drawing.Point(1, 2);
			this.debuggerTabControl.Name = "debuggerTabControl";
			this.debuggerTabControl.SelectedIndex = 0;
			this.debuggerTabControl.Size = new System.Drawing.Size(518, 306);
			this.debuggerTabControl.TabIndex = 0;
			// 
			// disassemblerView
			// 
			this.disassemblerView.Location = new System.Drawing.Point(4, 22);
			this.disassemblerView.Name = "disassemblerView";
			this.disassemblerView.Padding = new System.Windows.Forms.Padding(3);
			this.disassemblerView.Size = new System.Drawing.Size(510, 280);
			this.disassemblerView.TabIndex = 0;
			this.disassemblerView.Text = "Disassembler";
			this.disassemblerView.UseVisualStyleBackColor = true;
			// 
			// tabPage2
			// 
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(510, 280);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "NYI";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// DebuggerForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(520, 308);
			this.Controls.Add(this.debuggerTabControl);
			this.Name = "DebuggerForm";
			this.Text = "Debugger";
			this.debuggerTabControl.ResumeLayout(false);
			this.ResumeLayout(false);
		}
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.TabPage disassemblerView;
		private System.Windows.Forms.TabControl debuggerTabControl;
	}
}
