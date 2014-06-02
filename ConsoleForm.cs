/*
 * Created by SharpDevelop.
 * User: Warpten
 * Date: 01/06/2014
 * Time: 11:14
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Drawing;
using System.Windows.Forms;

namespace mzmdbg
{
    /// <summary>
    /// Description of ConsoleForm.
    /// </summary>
    public partial class ConsoleForm : Form
    {
        public ConsoleForm()
        {
            //
            // The InitializeComponent() call is required for Windows Forms designer support.
            //
            InitializeComponent();
        }
        
        public void LogLine(string message)
        {
            consoleRTB.AppendText(message + Environment.NewLine);
        }
        
        void OnClickMenuClear(object sender, EventArgs e)
        {
            consoleRTB.Clear();
        }
    }
}
