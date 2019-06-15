using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BlinkDetect
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
            propertyGrid1.SelectedObject = SettingsHolder.Instance;
        }

        private void fSettings_FormClosing(object sender, FormClosingEventArgs e)
        {
            //update?
        }
    }
}
