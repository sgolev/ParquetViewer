
using ParquetViewer.Controls;
using ParquetViewer.Helpers;
using System.Windows.Forms;

namespace ParquetViewer
{
    partial class MainForm
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            mainTableLayoutPanel = new TableLayoutPanel();
            runQueryButton = new Button();
            searchFilterLabel = new LinkLabel();
            searchFilterTextBox = new TextBox();
            clearFilterButton = new Button();
            mainGridView = new ParquetGridView();
            openParquetFileDialog = new OpenFileDialog();
            mainMenuStrip = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            newToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            openFolderToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator = new ThemableToolStripSeperator();
            saveAsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ThemableToolStripSeperator();
            exitToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            changeFieldsMenuStripButton = new ToolStripMenuItem();
            goToRowToolStripMenuItem = new ToolStripMenuItem();
            changeDateFormatToolStripMenuItem = new ToolStripMenuItem();
            defaultToolStripMenuItem = new ToolStripMenuItem();
            iSO8601ToolStripMenuItem = new ToolStripMenuItem();
            customDateFormatToolStripMenuItem = new ToolStripMenuItem();
            darkModeToolStripMenuItem = new ToolStripMenuItem();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            getSQLCreateTableScriptToolStripMenuItem = new ToolStripMenuItem();
            metadataViewerToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            userGuideToolStripMenuItem = new ToolStripMenuItem();
            shareAnonymousUsageDataToolStripMenuItem = new ToolStripMenuItem();
            languageToolStripMenuItem = new ToolStripMenuItem();
            englishToolStripMenuItem = new ToolStripMenuItem();
            turkishToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            leftSpringStatusBarLabel = new ToolStripStatusLabel();
            previousPageButton = new ToolStripButton();
            pageStatusBarLabel = new ToolStripStatusLabel();
            pageNumberTextBox = new ToolStripTextBox();
            pageCountStatusBarLabel = new ToolStripStatusLabel();
            nextPageButton = new ToolStripButton();
            rightSpringStatusBarLabel = new ToolStripStatusLabel();
            mainStatusStrip = new StatusStrip();
            exportFileDialog = new SaveFileDialog();
            openFolderDialog = new FolderBrowserDialog();
            fileIntegrityCheckingTimer = new Timer(components);
            mainTableLayoutPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mainGridView).BeginInit();
            mainMenuStrip.SuspendLayout();
            mainStatusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // mainTableLayoutPanel
            // 
            resources.ApplyResources(mainTableLayoutPanel, "mainTableLayoutPanel");
            mainTableLayoutPanel.Controls.Add(runQueryButton, 4, 0);
            mainTableLayoutPanel.Controls.Add(searchFilterLabel, 0, 0);
            mainTableLayoutPanel.Controls.Add(searchFilterTextBox, 2, 0);
            mainTableLayoutPanel.Controls.Add(clearFilterButton, 5, 0);
            mainTableLayoutPanel.Controls.Add(mainGridView, 0, 1);
            mainTableLayoutPanel.Name = "mainTableLayoutPanel";
            //
            // runQueryButton
            // 
            resources.ApplyResources(runQueryButton, "runQueryButton");
            runQueryButton.ForeColor = System.Drawing.Color.DarkRed;
            runQueryButton.Image = Resources.Icons.exclamation_icon;
            runQueryButton.Name = "runQueryButton";
            runQueryButton.UseVisualStyleBackColor = true;
            runQueryButton.Click += RunQueryButton_Click;
            // 
            // searchFilterLabel
            // 
            resources.ApplyResources(searchFilterLabel, "searchFilterLabel");
            mainTableLayoutPanel.SetColumnSpan(searchFilterLabel, 2);
            searchFilterLabel.LinkColor = System.Drawing.Color.Navy;
            searchFilterLabel.Name = "searchFilterLabel";
            searchFilterLabel.TabStop = true;
            searchFilterLabel.LinkClicked += SearchFilterLabel_Click;
            // 
            // searchFilterTextBox
            // 
            resources.ApplyResources(searchFilterTextBox, "searchFilterTextBox");
            mainTableLayoutPanel.SetColumnSpan(searchFilterTextBox, 2);
            searchFilterTextBox.Name = "searchFilterTextBox";
            searchFilterTextBox.Enter += SearchFilterTextBox_Enter;
            searchFilterTextBox.KeyPress += SearchFilterTextBox_KeyPress;
            searchFilterTextBox.Leave += SearchFilterTextBox_Leave;
            // 
            // clearFilterButton
            // 
            resources.ApplyResources(clearFilterButton, "clearFilterButton");
            clearFilterButton.ForeColor = System.Drawing.Color.Black;
            clearFilterButton.Name = "clearFilterButton";
            clearFilterButton.UseVisualStyleBackColor = true;
            clearFilterButton.Click += ClearFilterButton_Click;
            // 
            // mainGridView
            // 
            mainGridView.AllowUserToAddRows = false;
            mainGridView.AllowUserToDeleteRows = false;
            mainGridView.AllowUserToOrderColumns = true;
            resources.ApplyResources(mainGridView, "mainGridView");
            mainGridView.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            mainGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.ControlLight;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            mainGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            mainGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            mainGridView.ColumnNameEscapeFormat = "[{0}]";
            mainTableLayoutPanel.SetColumnSpan(mainGridView, 6);
            mainGridView.CopyAsWhereIcon = (System.Drawing.Image)resources.GetObject("mainGridView.CopyAsWhereIcon");
            mainGridView.CopyToClipboardIcon = (System.Drawing.Image)resources.GetObject("mainGridView.CopyToClipboardIcon");
            mainGridView.DateValueEscapeFormat = "#{0}#";
            mainGridView.EnableHeadersVisualStyles = false;
            mainGridView.Name = "mainGridView";
            mainGridView.ReadOnly = true;
            mainTableLayoutPanel.SetRowSpan(mainGridView, 2);
            mainGridView.ShowCellToolTips = false;
            mainGridView.ShowCopyAsWhereContextMenuItem = true;
            mainGridView.DataBindingComplete += MainGridView_DataBindingComplete;
            //
            // openParquetFileDialog
            // 
            resources.ApplyResources(openParquetFileDialog, "openParquetFileDialog");
            // 
            // mainMenuStrip
            // 
            mainMenuStrip.BackColor = System.Drawing.SystemColors.Control;
            mainMenuStrip.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, toolsToolStripMenuItem, helpToolStripMenuItem });
            resources.ApplyResources(mainMenuStrip, "mainMenuStrip");
            mainMenuStrip.Name = "mainMenuStrip";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newToolStripMenuItem, openToolStripMenuItem, openFolderToolStripMenuItem, toolStripSeparator, saveAsToolStripMenuItem, toolStripSeparator1, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            resources.ApplyResources(fileToolStripMenuItem, "fileToolStripMenuItem");
            // 
            // newToolStripMenuItem
            // 
            resources.ApplyResources(newToolStripMenuItem, "newToolStripMenuItem");
            newToolStripMenuItem.Name = "newToolStripMenuItem";
            newToolStripMenuItem.Click += NewToolStripMenuItem_Click;
            // 
            // openToolStripMenuItem
            // 
            resources.ApplyResources(openToolStripMenuItem, "openToolStripMenuItem");
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.Click += OpenToolStripMenuItem_Click;
            // 
            // openFolderToolStripMenuItem
            // 
            openFolderToolStripMenuItem.Name = "openFolderToolStripMenuItem";
            resources.ApplyResources(openFolderToolStripMenuItem, "openFolderToolStripMenuItem");
            openFolderToolStripMenuItem.Click += OpenFolderToolStripMenuItem_Click;
            // 
            // toolStripSeparator
            // 
            toolStripSeparator.Name = "toolStripSeparator";
            resources.ApplyResources(toolStripSeparator, "toolStripSeparator");
            // 
            // saveAsToolStripMenuItem
            // 
            resources.ApplyResources(saveAsToolStripMenuItem, "saveAsToolStripMenuItem");
            saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            saveAsToolStripMenuItem.Click += SaveAsToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(toolStripSeparator1, "toolStripSeparator1");
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            resources.ApplyResources(exitToolStripMenuItem, "exitToolStripMenuItem");
            exitToolStripMenuItem.Click += ExitToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { changeFieldsMenuStripButton, goToRowToolStripMenuItem, changeDateFormatToolStripMenuItem, darkModeToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            resources.ApplyResources(editToolStripMenuItem, "editToolStripMenuItem");
            // 
            // changeFieldsMenuStripButton
            // 
            resources.ApplyResources(changeFieldsMenuStripButton, "changeFieldsMenuStripButton");
            changeFieldsMenuStripButton.Name = "changeFieldsMenuStripButton";
            changeFieldsMenuStripButton.Click += ChangeFieldsMenuStripButton_Click;
            //
            // goToRowToolStripMenuItem
            //
            goToRowToolStripMenuItem.Enabled = false;
            goToRowToolStripMenuItem.Name = "goToRowToolStripMenuItem";
            goToRowToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.G;
            resources.ApplyResources(goToRowToolStripMenuItem, "goToRowToolStripMenuItem");
            goToRowToolStripMenuItem.Click += GoToRowToolStripMenuItem_Click;
            //
            // changeDateFormatToolStripMenuItem
            // 
            changeDateFormatToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { defaultToolStripMenuItem, iSO8601ToolStripMenuItem, customDateFormatToolStripMenuItem });
            resources.ApplyResources(changeDateFormatToolStripMenuItem, "changeDateFormatToolStripMenuItem");
            changeDateFormatToolStripMenuItem.Name = "changeDateFormatToolStripMenuItem";
            // 
            // defaultToolStripMenuItem
            // 
            defaultToolStripMenuItem.Name = "defaultToolStripMenuItem";
            resources.ApplyResources(defaultToolStripMenuItem, "defaultToolStripMenuItem");
            defaultToolStripMenuItem.Tag = "0";
            defaultToolStripMenuItem.Click += DateFormatMenuItem_Click;
            // 
            // iSO8601ToolStripMenuItem
            // 
            iSO8601ToolStripMenuItem.Name = "iSO8601ToolStripMenuItem";
            resources.ApplyResources(iSO8601ToolStripMenuItem, "iSO8601ToolStripMenuItem");
            iSO8601ToolStripMenuItem.Tag = "2";
            iSO8601ToolStripMenuItem.Click += DateFormatMenuItem_Click;
            // 
            // customDateFormatToolStripMenuItem
            // 
            customDateFormatToolStripMenuItem.Name = "customDateFormatToolStripMenuItem";
            resources.ApplyResources(customDateFormatToolStripMenuItem, "customDateFormatToolStripMenuItem");
            customDateFormatToolStripMenuItem.Tag = "6";
            customDateFormatToolStripMenuItem.Click += DateFormatMenuItem_Click;
            //
            // darkModeToolStripMenuItem
            // 
            darkModeToolStripMenuItem.Name = "darkModeToolStripMenuItem";
            resources.ApplyResources(darkModeToolStripMenuItem, "darkModeToolStripMenuItem");
            darkModeToolStripMenuItem.Click += DarkModeToolStripMenuItem_Click;
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { getSQLCreateTableScriptToolStripMenuItem, metadataViewerToolStripMenuItem });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            resources.ApplyResources(toolsToolStripMenuItem, "toolsToolStripMenuItem");
            // 
            // getSQLCreateTableScriptToolStripMenuItem
            // 
            resources.ApplyResources(getSQLCreateTableScriptToolStripMenuItem, "getSQLCreateTableScriptToolStripMenuItem");
            getSQLCreateTableScriptToolStripMenuItem.Name = "getSQLCreateTableScriptToolStripMenuItem";
            getSQLCreateTableScriptToolStripMenuItem.Click += GetSQLCreateTableScriptToolStripMenuItem_Click;
            getSQLCreateTableScriptToolStripMenuItem.MouseEnter += GetSQLCreateTableScriptToolStripMenuItem_MouseEnter;
            // 
            // metadataViewerToolStripMenuItem
            // 
            resources.ApplyResources(metadataViewerToolStripMenuItem, "metadataViewerToolStripMenuItem");
            metadataViewerToolStripMenuItem.Name = "metadataViewerToolStripMenuItem";
            metadataViewerToolStripMenuItem.Click += MetadataViewerToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { userGuideToolStripMenuItem, shareAnonymousUsageDataToolStripMenuItem, languageToolStripMenuItem, aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            resources.ApplyResources(helpToolStripMenuItem, "helpToolStripMenuItem");
            // 
            // userGuideToolStripMenuItem
            // 
            userGuideToolStripMenuItem.Image = Resources.Icons.external_link_icon;
            userGuideToolStripMenuItem.Name = "userGuideToolStripMenuItem";
            resources.ApplyResources(userGuideToolStripMenuItem, "userGuideToolStripMenuItem");
            userGuideToolStripMenuItem.Click += UserGuideToolStripMenuItem_Click;
            // 
            // shareAnonymousUsageDataToolStripMenuItem
            // 
            shareAnonymousUsageDataToolStripMenuItem.Name = "shareAnonymousUsageDataToolStripMenuItem";
            resources.ApplyResources(shareAnonymousUsageDataToolStripMenuItem, "shareAnonymousUsageDataToolStripMenuItem");
            shareAnonymousUsageDataToolStripMenuItem.CheckedChanged += ShareAnonymousUsageDataToolStripMenuItem_CheckedChanged;
            shareAnonymousUsageDataToolStripMenuItem.Click += ShareAnonymousUsageDataToolStripMenuItem_Click;
            // 
            // languageToolStripMenuItem
            // 
            languageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { englishToolStripMenuItem, turkishToolStripMenuItem });
            languageToolStripMenuItem.Image = Resources.Icons.localization_icon;
            languageToolStripMenuItem.Name = "languageToolStripMenuItem";
            resources.ApplyResources(languageToolStripMenuItem, "languageToolStripMenuItem");
            // 
            // englishToolStripMenuItem
            // 
            englishToolStripMenuItem.Name = "englishToolStripMenuItem";
            resources.ApplyResources(englishToolStripMenuItem, "englishToolStripMenuItem");
            englishToolStripMenuItem.Tag = "en-US";
            englishToolStripMenuItem.Click += LanguageToolStripMenuItem_Click;
            // 
            // turkishToolStripMenuItem
            // 
            turkishToolStripMenuItem.Name = "turkishToolStripMenuItem";
            resources.ApplyResources(turkishToolStripMenuItem, "turkishToolStripMenuItem");
            turkishToolStripMenuItem.Tag = "tr-TR";
            turkishToolStripMenuItem.Click += LanguageToolStripMenuItem_Click;
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            resources.ApplyResources(aboutToolStripMenuItem, "aboutToolStripMenuItem");
            aboutToolStripMenuItem.Click += AboutToolStripMenuItem_Click;
            //
            // leftSpringStatusBarLabel
            //
            leftSpringStatusBarLabel.Name = "leftSpringStatusBarLabel";
            leftSpringStatusBarLabel.Spring = true;
            //
            // previousPageButton
            //
            previousPageButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            previousPageButton.Enabled = false;
            previousPageButton.Name = "previousPageButton";
            resources.ApplyResources(previousPageButton, "previousPageButton");
            previousPageButton.Click += PreviousPageButton_Click;
            //
            // pageStatusBarLabel
            //
            pageStatusBarLabel.Margin = new Padding(8, 3, 0, 2);
            pageStatusBarLabel.Name = "pageStatusBarLabel";
            resources.ApplyResources(pageStatusBarLabel, "pageStatusBarLabel");
            //
            // pageNumberTextBox
            //
            pageNumberTextBox.AutoSize = false;
            pageNumberTextBox.Enabled = false;
            pageNumberTextBox.Name = "pageNumberTextBox";
            pageNumberTextBox.Size = new System.Drawing.Size(70, 23);
            pageNumberTextBox.TextBoxTextAlign = HorizontalAlignment.Center;
            pageNumberTextBox.KeyDown += PageNumberTextBox_KeyDown;
            pageNumberTextBox.KeyPress += PageNumberTextBox_KeyPress;
            pageNumberTextBox.Leave += PageNumberTextBox_Leave;
            //
            // pageCountStatusBarLabel
            //
            pageCountStatusBarLabel.Margin = new Padding(0, 3, 8, 2);
            pageCountStatusBarLabel.Name = "pageCountStatusBarLabel";
            //
            // nextPageButton
            //
            nextPageButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            nextPageButton.Enabled = false;
            nextPageButton.Name = "nextPageButton";
            resources.ApplyResources(nextPageButton, "nextPageButton");
            nextPageButton.Click += NextPageButton_Click;
            //
            // rightSpringStatusBarLabel
            //
            rightSpringStatusBarLabel.Name = "rightSpringStatusBarLabel";
            rightSpringStatusBarLabel.Spring = true;
            //
            // mainStatusStrip
            //
            mainStatusStrip.Items.AddRange(new ToolStripItem[] { leftSpringStatusBarLabel, previousPageButton, pageStatusBarLabel, pageNumberTextBox, pageCountStatusBarLabel, nextPageButton, rightSpringStatusBarLabel });
            resources.ApplyResources(mainStatusStrip, "mainStatusStrip");
            mainStatusStrip.Name = "mainStatusStrip";
            mainStatusStrip.ShowItemToolTips = true;
            // 
            // exportFileDialog
            // 
            exportFileDialog.DefaultExt = "csv";
            resources.ApplyResources(exportFileDialog, "exportFileDialog");
            exportFileDialog.RestoreDirectory = true;
            // 
            // openFolderDialog
            // 
            resources.ApplyResources(openFolderDialog, "openFolderDialog");
            openFolderDialog.ShowNewFolderButton = false;
            // 
            // fileIntegrityCheckingTimer
            // 
            fileIntegrityCheckingTimer.Enabled = true;
            fileIntegrityCheckingTimer.Interval = 2500;
            fileIntegrityCheckingTimer.Tick += FileIntegrityCheckingTimer_Tick;
            // 
            // MainForm
            // 
            AllowDrop = true;
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(mainTableLayoutPanel);
            Controls.Add(mainStatusStrip);
            Controls.Add(mainMenuStrip);
            Icon = Resources.Icons.parquet_icon_32x32;
            KeyPreview = true;
            MainMenuStrip = mainMenuStrip;
            Name = "MainForm";
            Load += MainForm_Load;
            DragDrop += MainForm_DragDrop;
            DragEnter += MainForm_DragEnter;
            KeyDown += MainForm_KeyDown;
            Resize += MainForm_Resize;
            mainTableLayoutPanel.ResumeLayout(false);
            mainTableLayoutPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)mainGridView).EndInit();
            mainMenuStrip.ResumeLayout(false);
            mainMenuStrip.PerformLayout();
            mainStatusStrip.ResumeLayout(false);
            mainStatusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel mainTableLayoutPanel;
        private System.Windows.Forms.OpenFileDialog openParquetFileDialog;
        private ParquetGridView mainGridView;
        private System.Windows.Forms.MenuStrip mainMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private ThemableToolStripSeperator toolStripSeparator;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private ThemableToolStripSeperator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem changeFieldsMenuStripButton;
        private System.Windows.Forms.ToolStripMenuItem goToRowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.TextBox searchFilterTextBox;
        private System.Windows.Forms.Button runQueryButton;
        private System.Windows.Forms.Button clearFilterButton;
        private System.Windows.Forms.ToolStripStatusLabel leftSpringStatusBarLabel;
        private System.Windows.Forms.ToolStripButton previousPageButton;
        private System.Windows.Forms.ToolStripStatusLabel pageStatusBarLabel;
        private System.Windows.Forms.ToolStripTextBox pageNumberTextBox;
        private System.Windows.Forms.ToolStripStatusLabel pageCountStatusBarLabel;
        private System.Windows.Forms.ToolStripButton nextPageButton;
        private System.Windows.Forms.ToolStripStatusLabel rightSpringStatusBarLabel;
        private System.Windows.Forms.StatusStrip mainStatusStrip;
        private System.Windows.Forms.LinkLabel searchFilterLabel;
        private System.Windows.Forms.SaveFileDialog exportFileDialog;
        private System.Windows.Forms.ToolStripMenuItem userGuideToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem changeDateFormatToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem defaultToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem iSO8601ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem getSQLCreateTableScriptToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem metadataViewerToolStripMenuItem;
        private System.Windows.Forms.FolderBrowserDialog openFolderDialog;
        private System.Windows.Forms.ToolStripMenuItem openFolderToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem shareAnonymousUsageDataToolStripMenuItem;
        private ToolStripMenuItem customDateFormatToolStripMenuItem;
        private ToolStripMenuItem darkModeToolStripMenuItem;
        private ToolStripMenuItem languageToolStripMenuItem;
        private ToolStripMenuItem englishToolStripMenuItem;
        private ToolStripMenuItem turkishToolStripMenuItem;
        private Timer fileIntegrityCheckingTimer;
    }
}

