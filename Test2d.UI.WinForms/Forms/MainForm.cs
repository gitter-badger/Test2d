﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Test2d;

namespace TestWinForms
{
    /// <summary>
    /// 
    /// </summary>
    public partial class MainForm : Form, IView
    {
        private Drawable _drawable;

        /// <summary>
        /// 
        /// </summary>
        public object DataContext { get; set; }
    
        /// <summary>
        /// 
        /// </summary>
        public MainForm()
        {
            InitializeComponent();

            InitializeContext();
            
            InitializePanel();

            SetContainerInvalidation();
            SetPanelSize();

            HandlePanelShorcutKeys();
            HandleMenuShortcutKeys();

            HandleFileDialogs();

            FormClosing += (s, e) => DeInitializeContext();

            UpdateToolMenu();
            UpdateOptionsMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitializeContext()
        {
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.DoubleBuffer
                | ControlStyles.SupportsTransparentBackColor,
                true);

            var context = new EditorContext()
            {
                View = this,
                Renderers = new IRenderer[] { new EmfRenderer(72.0 / 96.0) },
                ProjectFactory = new ProjectFactory(),
                TextClipboard = new TextClipboard(),
                Serializer = new NewtonsoftSerializer(),
                PdfWriter = new PdfWriter(),
                DxfWriter = new DxfWriter(),
                CsvReader = new CsvHelperReader(),
                CsvWriter = new CsvHelperWriter()
            };
            context.InitializeEditor(new TraceLog(), "Test2d.log");
            context.Editor.Renderers[0].State.DrawShapeState.Flags = ShapeStateFlags.Visible;
            context.Editor.GetImageKey = async () => await GetImageKey();

            DataContext = context;
        }

        /// <summary>
        /// 
        /// </summary>
        private void DeInitializeContext()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private void InitializePanel()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            _drawable = new Drawable();

            _drawable.Context = context;
            _drawable.InvalidateContainer = InvalidateContainer;
            _drawable.Initialize();

            _drawable.Anchor = System.Windows.Forms.AnchorStyles.None;
            _drawable.Name = "containerPanel";
            _drawable.TabIndex = 0;

            this.SuspendLayout();
            this.Controls.Add(_drawable);
            this.ResumeLayout(false);

            _drawable.Select();
        }

        /// <summary>
        /// 
        /// </summary>
        private void InvalidateContainer()
        {
            SetContainerInvalidation();
            SetPanelSize();

            var context = DataContext as EditorContext;
            if (context == null)
                return;

            var container = context.Editor.Project.CurrentContainer;
            if (container != null)
            {
                container.Invalidate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void HandleFileDialogs()
        {
            // open container
            this.openFileDialog1.FileOk += (sender, e) =>
            {
                var context = DataContext as EditorContext;
                if (context == null)
                    return;

                string path = openFileDialog1.FileName;
                int filterIndex = openFileDialog1.FilterIndex;
                context.Open(path);
                InvalidateContainer();
            };

            // save container
            this.saveFileDialog1.FileOk += (sender, e) =>
            {
                var context = DataContext as EditorContext;
                if (context == null)
                    return;

                string path = saveFileDialog1.FileName;
                int filterIndex = saveFileDialog1.FilterIndex;
                context.Save(path);
            };

            // export container
            this.saveFileDialog2.FileOk += (sender, e) =>
            {
                var context = DataContext as EditorContext;
                if (context == null)
                    return;

                string path = saveFileDialog2.FileName;
                int filterIndex = saveFileDialog2.FilterIndex;
                switch (filterIndex)
                {
                    case 1:
                        context.ExportAsPdf(path, context.Editor.Project);
                        Process.Start(path);
                        break;
                    case 2:
                        context.ExportAsDxf(path);
                        Process.Start(path);
                        break;
                    default:
                        break;
                }
            };
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetPanelSize()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            var container = context.Editor.Project.CurrentContainer;
            if (container == null)
                return;

            int width = (int)container.Width;
            int height = (int)container.Height;

            int x = (this.Width - width) / 2;
            int y = (((this.Height) - height) / 2) - (this.menuStrip1.Height / 3);

            _drawable.Location = new System.Drawing.Point(x, y);
            _drawable.Size = new System.Drawing.Size(width, height);
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetContainerInvalidation()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            var container = context.Editor.Project.CurrentContainer;
            if (container == null)
                return;

            foreach (var layer in container.Layers)
            {
                layer.InvalidateLayer += (s, e) => _drawable.Invalidate();
            }

            if (container.WorkingLayer != null)
            {
                container.WorkingLayer.InvalidateLayer += (s, e) => _drawable.Invalidate();
            }
            
            if (container.HelperLayer != null)
            {
                container.HelperLayer.InvalidateLayer += (s, e) => _drawable.Invalidate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateToolMenu()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            var tool = context.Editor.CurrentTool;

            noneToolStripMenuItem.Checked = tool == Tool.None;
            selectionToolStripMenuItem.Checked = tool == Tool.Selection;
            pointToolStripMenuItem.Checked = tool == Tool.Point;
            lineToolStripMenuItem.Checked = tool == Tool.Line;
            arcToolStripMenuItem.Checked = tool == Tool.Arc;
            bezierToolStripMenuItem.Checked = tool == Tool.Bezier;
            qBezierToolStripMenuItem.Checked = tool == Tool.QBezier;
            pathToolStripMenuItem.Checked = tool == Tool.Path;
            rectangleToolStripMenuItem.Checked = tool == Tool.Rectangle;
            ellipseToolStripMenuItem.Checked = tool == Tool.Ellipse;
            textToolStripMenuItem.Checked = tool == Tool.Text;
            imageToolStripMenuItem.Checked = tool == Tool.Image;
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateOptionsMenu()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            var options = context.Editor.Project.Options;

            defaultIsFilledToolStripMenuItem.Checked = options.DefaultIsFilled;
            snapToGridToolStripMenuItem.Checked = options.SnapToGrid;
            tryToConnectToolStripMenuItem.Checked = options.TryToConnect;
        }

        /// <summary>
        /// 
        /// </summary>
        private void HandleMenuShortcutKeys()
        {
            // File
            newToolStripMenuItem.Click += (sender, e) => OnNew();
            openToolStripMenuItem.Click += (sender, e) => OnOpen();
            saveAsToolStripMenuItem.Click += (sender, e) => OnSave();
            exportToolStripMenuItem.Click += (sender, e) => OnExport();
            exitToolStripMenuItem.Click += (sender, e) => Close();

            // Tool
            noneToolStripMenuItem.Click += (sender, e) => OnSetToolToNone();
            selectionToolStripMenuItem.Click += (sender, e) => OnSetToolToSelection();
            pointToolStripMenuItem.Click += (sender, e) => OnSetToolToPoint();
            lineToolStripMenuItem.Click += (sender, e) => OnSetToolToLine();
            arcToolStripMenuItem.Click += (sender, e) => OnSetToolToArc();
            bezierToolStripMenuItem.Click += (sender, e) => OnSetToolToBezier();
            qBezierToolStripMenuItem.Click += (sender, e) => OnSetToolToQBezier();
            pathToolStripMenuItem.Click += (sender, e) => OnSetToolToPath();
            rectangleToolStripMenuItem.Click += (sender, e) => OnSetToolToRectangle();
            ellipseToolStripMenuItem.Click += (sender, e) => OnSetToolToEllipse();
            textToolStripMenuItem.Click += (sender, e) => OnSetToolToText();
            imageToolStripMenuItem.Click += (sender, e) => OnSetToolToImage();

            // Options
            defaultIsFilledToolStripMenuItem.Click += (sender, e) => OnSetDefaultIsFilled();
            snapToGridToolStripMenuItem.Click += (sender, e) => OnSetSnapToGrid();
            tryToConnectToolStripMenuItem.Click += (sender, e) => OnSetTryToConnect();
        }

        /// <summary>
        /// 
        /// </summary>
        private void HandlePanelShorcutKeys()
        {
            _drawable.KeyDown += (sender, e) =>
            {
                if (e.Control || e.Alt || e.Shift)
                    return;

                var context = DataContext as EditorContext;
                if (context == null)
                    return;

                switch (e.KeyCode)
                {
                    case Keys.Delete:
                        context.Commands.DeleteCommand.Execute(null);
                        break;
                    case Keys.N:
                        OnSetToolToNone();
                        break;
                    case Keys.S:
                        OnSetToolToSelection();
                        break;
                    case Keys.P:
                        OnSetToolToPoint();
                        break;
                    case Keys.L:
                        OnSetToolToLine();
                        break;
                    case Keys.A:
                        OnSetToolToArc();
                        break;
                    case Keys.B:
                        OnSetToolToBezier();
                        break;
                    case Keys.Q:
                        OnSetToolToQBezier();
                        break;
                    case Keys.H:
                        OnSetToolToPath();
                        break;
                    case Keys.M:
                        OnSetToolToMove();
                        break;
                    case Keys.R:
                        OnSetToolToRectangle();
                        break;
                    case Keys.E:
                        OnSetToolToEllipse();
                        break;
                    case Keys.T:
                        OnSetToolToText();
                        break;
                    case Keys.I:
                        OnSetToolToImage();
                        break;
                    case Keys.F:
                        OnSetDefaultIsFilled();
                        break;
                    case Keys.G:
                        OnSetSnapToGrid();
                        break;
                    case Keys.C:
                        OnSetTryToConnect();
                        break;
                    case Keys.Z:
                        _drawable.ResetZoom();
                        break;
                    case Keys.X:
                        _drawable.AutoFit();
                        break;
                }
            };
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnNew()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.NewCommand.Execute(null);
            InvalidateContainer();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnOpen()
        {
            openFileDialog1.Filter = "Project (*.project)|*.project|All (*.*)|*.*";
            openFileDialog1.FilterIndex = 0;
            openFileDialog1.ShowDialog(this);
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSave()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            saveFileDialog1.Filter = "Project (*.project)|*.project|All (*.*)|*.*";
            saveFileDialog1.FilterIndex = 0;
            saveFileDialog1.FileName = context.Editor.Project.Name;
            saveFileDialog1.ShowDialog(this);
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnExport()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            saveFileDialog2.Filter = "Pdf (*.pdf)|*.pdf|Dxf (*.dxf)|*.dxf|All (*.*)|*.*";
            saveFileDialog2.FilterIndex = 0;
            saveFileDialog2.FileName = context.Editor.Project.Name;
            saveFileDialog2.ShowDialog(this);
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToNone()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolNoneCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToSelection()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolSelectionCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToPoint()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolPointCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToLine()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolLineCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToArc()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolArcCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToBezier()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolBezierCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToQBezier()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolQBezierCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToPath()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolPathCommand.Execute(null);
            UpdateToolMenu();
        }
                
        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToMove()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolMoveCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToRectangle()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolRectangleCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToEllipse()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolEllipseCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToText()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolTextCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetToolToImage()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.ToolImageCommand.Execute(null);
            UpdateToolMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetDefaultIsFilled()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.DefaultIsFilledCommand.Execute(null);
            UpdateOptionsMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetSnapToGrid()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.SnapToGridCommand.Execute(null);
            UpdateOptionsMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnSetTryToConnect()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return;

            context.Commands.TryToConnectCommand.Execute(null);
            UpdateOptionsMenu();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetImageKey()
        {
            var context = DataContext as EditorContext;
            if (context == null)
                return null;

            openFileDialog2.Filter = "All (*.*)|*.*";
            openFileDialog2.FilterIndex = 0;
            var result = openFileDialog2.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                var path = openFileDialog2.FileName;
                var bytes = System.IO.File.ReadAllBytes(path);
                var key = context.Editor.Project.AddImageFromFile(path, bytes);
                return await Task.Run(() => key);
            }
            return null;
        }
    }
}
