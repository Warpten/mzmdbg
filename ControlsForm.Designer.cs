/*
 * Created by SharpDevelop.
 * User: Warpten
 * Date: 03/06/2014
 * Time: 16:42
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace mzmdbg
{
    partial class ControlsForm
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
            this.saveButton = new System.Windows.Forms.Button();
            this.resetButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.textboxA = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textboxB = new System.Windows.Forms.TextBox();
            this.textboxStart = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textboxSelect = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textboxLT = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textboxRT = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // saveButton
            // 
            this.saveButton.Location = new System.Drawing.Point(12, 6);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(91, 25);
            this.saveButton.TabIndex = 0;
            this.saveButton.Text = "Save";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.SaveKeybinds);
            // 
            // resetButton
            // 
            this.resetButton.Location = new System.Drawing.Point(10, 37);
            this.resetButton.Name = "resetButton";
            this.resetButton.Size = new System.Drawing.Size(93, 25);
            this.resetButton.TabIndex = 1;
            this.resetButton.Text = "Reset";
            this.resetButton.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(4, 78);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(36, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "A";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textboxA
            // 
            this.textboxA.Location = new System.Drawing.Point(46, 78);
            this.textboxA.Name = "textboxA";
            this.textboxA.Size = new System.Drawing.Size(57, 20);
            this.textboxA.TabIndex = 3;
            this.textboxA.Tag = "ControlA";
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(4, 101);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(36, 15);
            this.label2.TabIndex = 4;
            this.label2.Text = "B";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textboxB
            // 
            this.textboxB.Location = new System.Drawing.Point(46, 101);
            this.textboxB.Name = "textboxB";
            this.textboxB.Size = new System.Drawing.Size(57, 20);
            this.textboxB.TabIndex = 5;
            this.textboxB.Tag = "ControlB";
            // 
            // textboxStart
            // 
            this.textboxStart.Location = new System.Drawing.Point(46, 125);
            this.textboxStart.Name = "textboxStart";
            this.textboxStart.Size = new System.Drawing.Size(57, 20);
            this.textboxStart.TabIndex = 7;
            this.textboxStart.Tag = "ControlStart";
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(4, 129);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(36, 15);
            this.label3.TabIndex = 6;
            this.label3.Text = "Start";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textboxSelect
            // 
            this.textboxSelect.Location = new System.Drawing.Point(46, 151);
            this.textboxSelect.Name = "textboxSelect";
            this.textboxSelect.Size = new System.Drawing.Size(57, 20);
            this.textboxSelect.TabIndex = 9;
            this.textboxSelect.Tag = "ControlSelect";
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(-5, 153);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(45, 15);
            this.label4.TabIndex = 8;
            this.label4.Text = "Select";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // textboxLT
            // 
            this.textboxLT.Location = new System.Drawing.Point(46, 177);
            this.textboxLT.Name = "textboxLT";
            this.textboxLT.Size = new System.Drawing.Size(57, 20);
            this.textboxLT.TabIndex = 11;
            this.textboxLT.Tag = "ControlLT";
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(4, 179);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(36, 15);
            this.label5.TabIndex = 10;
            this.label5.Text = "LT";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // textboxRT
            // 
            this.textboxRT.Location = new System.Drawing.Point(46, 203);
            this.textboxRT.Name = "textboxRT";
            this.textboxRT.Size = new System.Drawing.Size(57, 20);
            this.textboxRT.TabIndex = 13;
            this.textboxRT.Tag = "ControlRT";
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(4, 205);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(36, 15);
            this.label6.TabIndex = 12;
            this.label6.Text = "RT";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // ControlsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(115, 234);
            this.Controls.Add(this.textboxRT);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textboxLT);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textboxSelect);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textboxStart);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textboxB);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textboxA);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.resetButton);
            this.Controls.Add(this.saveButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ControlsForm";
            this.Text = "Controls";
            this.Load += new System.EventHandler(this.OnFormLoad);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textboxRT;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textboxLT;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textboxSelect;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textboxStart;
        private System.Windows.Forms.TextBox textboxB;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textboxA;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.Button saveButton;
    }
}
