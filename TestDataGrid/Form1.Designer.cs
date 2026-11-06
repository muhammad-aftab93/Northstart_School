using EnterpriseUI.Grids;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;

namespace TestDataGrid
{
    partial class Form1
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
            EnterpriseUI.Printing.PrintTemplate printTemplate1 = new EnterpriseUI.Printing.PrintTemplate();
            EnterpriseUI.Printing.PrintPageSettings printPageSettings1 = new EnterpriseUI.Printing.PrintPageSettings();
            EnterpriseUI.Printing.PrintStyle printStyle1 = new EnterpriseUI.Printing.PrintStyle();
            dataGrid = new StudentDataGrid();
            SuspendLayout();
            // 
            // dataGrid
            // 
            dataGrid.AccessibleName = "Data Grid";
            dataGrid.AccessibleRole = AccessibleRole.Table;
            dataGrid.AllowRowDelete = false;
            dataGrid.AllowRowDrag = false;
            dataGrid.AllowRowDrop = false;
            dataGrid.AriaLabel = "Data Grid";
            dataGrid.AutoSizeMode = GridAutoSizeMode.Fill;
            dataGrid.ClipboardCopyMode = GridClipboardCopyMode.CellsWithHeaders;
            dataGrid.CustomDataSource = null;
            dataGrid.DataSource = null;
            dataGrid.DetailTemplate = null;
            dataGrid.Dock = DockStyle.Fill;
            dataGrid.EditMode = GridEditMode.None;
            dataGrid.EnableAlternateRows = true;
            dataGrid.EnableFiltering = true;
            dataGrid.EnableGrouping = true;
            dataGrid.EnableMasterDetail = false;
            dataGrid.EnableMultiColumnSorting = true;
            dataGrid.EnableQuickFilterRow = false;
            dataGrid.EnableSearchPanel = true;
            dataGrid.EnableSorting = true;
            dataGrid.EnableVirtualization = true;
            dataGrid.FillColumnsToWidth = true;
            dataGrid.Font = new Font("Segoe UI", 9F);
            dataGrid.GroupsExpandedByDefault = true;
            dataGrid.ItemHeight = 36D;
            dataGrid.ItemWidth = 120D;
            dataGrid.Location = new Point(0, 0);
            dataGrid.Name = "dataGrid";
            dataGrid.ObserveItemChanges = true;
            dataGrid.PageSize = 50;
            dataGrid.PagingMode = GridPagingMode.Client;
            printTemplate1.FooterCenter = "Page {page} of {pages}";
            printTemplate1.FooterLeft = null;
            printTemplate1.FooterRight = null;
            printTemplate1.HeaderCenter = null;
            printTemplate1.HeaderLeft = "{title}";
            printTemplate1.HeaderRight = "{date}";
            printTemplate1.Name = "Standard";
            printPageSettings1.FontSize = 9D;
            printPageSettings1.IsLandscape = false;
            printPageSettings1.MarginBottom = 36D;
            printPageSettings1.MarginLeft = 36D;
            printPageSettings1.MarginRight = 36D;
            printPageSettings1.MarginTop = 36D;
            printPageSettings1.MaxRows = 100000;
            printPageSettings1.PageOrder = EnterpriseUI.Printing.PrintPageOrder.DownThenOver;
            printPageSettings1.PaperSize = "Letter";
            printPageSettings1.RepeatHeaderRows = true;
            printPageSettings1.ScaleMode = EnterpriseUI.Printing.PrintScaleMode.FitToWidth;
            printPageSettings1.ScalePercent = 100D;
            printPageSettings1.ShowGridLines = true;
            printTemplate1.Settings = printPageSettings1;
            printStyle1.AlternateRowHex = null;
            printStyle1.BandBackHex = "#DCE6F2";
            printStyle1.FontFamily = "Segoe UI";
            printStyle1.GridLineHex = "#C8C8C8";
            printStyle1.GroupBackHex = "#E9EEF5";
            printStyle1.HeaderBackHex = "#F0F0F0";
            printStyle1.HeaderForeHex = "#000000";
            printStyle1.Name = "Standard";
            printStyle1.SummaryBackHex = "#F3F3F3";
            printStyle1.TextHex = "#000000";
            printTemplate1.Style = printStyle1;
            printTemplate1.Title = "Sample Title";
            dataGrid.PrintTemplate = printTemplate1;
            dataGrid.RejectInvalidEdits = true;
            dataGrid.RowBuffer = 100;
            dataGrid.SearchPanelText = null;
            dataGrid.SelectedItem = null;
            dataGrid.SelectedRowIndex = -1;
            dataGrid.SelectionMode = GridSelectionMode.SingleRow;
            dataGrid.ShowBandedHeaders = true;
            dataGrid.ShowColumnSummaries = true;
            dataGrid.ShowConditionalDataBars = true;
            dataGrid.ShowFilterRow = true;
            dataGrid.ShowGroupFooters = false;
            dataGrid.ShowGroupPanel = true;
            dataGrid.ShowPager = true;
            dataGrid.ShowSearchPanel = true;
            dataGrid.ShowSummaryFooter = true;
            dataGrid.Size = new Size(800, 450);
            dataGrid.TabIndex = 0;
            dataGrid.Text = "enterpriseDataGrid1";
            dataGrid.ThemeOverride = null;
            // 
            // Form1
            // 
            ClientSize = new Size(800, 450);
            Controls.Add(dataGrid);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private StudentDataGrid dataGrid;
    }
}
