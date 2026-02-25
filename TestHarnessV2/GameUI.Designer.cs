namespace TestHarnessV2
{
    partial class GameUI2
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            webView_Main = new Microsoft.Web.WebView2.WinForms.WebView2();
            panel2 = new Panel();
            ((System.ComponentModel.ISupportInitialize)webView_Main).BeginInit();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // webView_Main
            // 
            webView_Main.AllowExternalDrop = true;
            webView_Main.CreationProperties = null;
            webView_Main.DefaultBackgroundColor = Color.White;
            webView_Main.Dock = DockStyle.Fill;
            webView_Main.Location = new Point(0, 0);
            webView_Main.Name = "webView_Main";
            webView_Main.Size = new Size(200, 100);
            webView_Main.TabIndex = 0;
            webView_Main.ZoomFactor = 1D;
            // 
            // panel2
            // 
            panel2.Controls.Add(webView_Main);
            panel2.Dock = DockStyle.Fill;
            panel2.Location = new Point(0, 0);
            panel2.Name = "panel2";
            panel2.Size = new Size(800, 450);
            panel2.TabIndex = 0;
            // 
            // GameUI2
            // 
            AutoScaleMode = AutoScaleMode.None;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Padding = new Padding(0);
            Margin = new Padding(0);
            ClientSize = new Size(1920, 1080);
            Controls.Add(panel2);
            Name = "GameUI2";
            Text = "GameUI2";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)webView_Main).EndInit();
            panel2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Microsoft.Web.WebView2.WinForms.WebView2 webView_Main;
        private Panel panel2;
    }
}
