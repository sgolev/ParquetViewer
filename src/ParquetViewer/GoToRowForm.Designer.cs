using System.Windows.Forms;

namespace ParquetViewer
{
    partial class GoToRowForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            rowIndexLabel = new Label();
            rowIndexNumericUpDown = new NumericUpDown();
            goButton = new Button();
            cancelButton = new Button();
            ((System.ComponentModel.ISupportInitialize)rowIndexNumericUpDown).BeginInit();
            SuspendLayout();
            //
            // rowIndexLabel
            //
            rowIndexLabel.AutoSize = true;
            rowIndexLabel.Location = new System.Drawing.Point(12, 15);
            rowIndexLabel.Name = "rowIndexLabel";
            rowIndexLabel.Size = new System.Drawing.Size(0, 15);
            rowIndexLabel.TabIndex = 0;
            //
            // rowIndexNumericUpDown
            //
            rowIndexNumericUpDown.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            rowIndexNumericUpDown.Location = new System.Drawing.Point(12, 38);
            rowIndexNumericUpDown.Maximum = new decimal(new int[] { 0, 0, 0, 0 });
            rowIndexNumericUpDown.Name = "rowIndexNumericUpDown";
            rowIndexNumericUpDown.Size = new System.Drawing.Size(296, 23);
            rowIndexNumericUpDown.TabIndex = 1;
            rowIndexNumericUpDown.ThousandsSeparator = true;
            //
            // goButton
            //
            goButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            goButton.DialogResult = DialogResult.OK;
            goButton.Location = new System.Drawing.Point(152, 76);
            goButton.Name = "goButton";
            goButton.Size = new System.Drawing.Size(75, 25);
            goButton.TabIndex = 2;
            goButton.UseVisualStyleBackColor = true;
            //
            // cancelButton
            //
            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new System.Drawing.Point(233, 76);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(75, 25);
            cancelButton.TabIndex = 3;
            cancelButton.UseVisualStyleBackColor = true;
            //
            // GoToRowForm
            //
            AcceptButton = goButton;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = cancelButton;
            ClientSize = new System.Drawing.Size(320, 113);
            Controls.Add(cancelButton);
            Controls.Add(goButton);
            Controls.Add(rowIndexNumericUpDown);
            Controls.Add(rowIndexLabel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = Resources.Icons.parquet_icon_32x32;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "GoToRowForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Shown += GoToRowForm_Shown;
            ((System.ComponentModel.ISupportInitialize)rowIndexNumericUpDown).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label rowIndexLabel;
        private NumericUpDown rowIndexNumericUpDown;
        private Button goButton;
        private Button cancelButton;
    }
}
