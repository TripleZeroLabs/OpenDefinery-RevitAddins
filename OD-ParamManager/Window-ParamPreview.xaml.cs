using Microsoft.Win32;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OD_ParamManager
{
    /// <summary>
    /// Read-only preview of the exact shared-parameter text lines for a set of parameters.
    /// The user can select whole rows by their line number (Ctrl/Shift for several) or one
    /// field by double-clicking it, then Copy to Clipboard; or save the whole set as a .txt.
    /// </summary>
    public partial class Window_ParamPreview : Window
    {
        private readonly List<PreviewLine> _lines = new List<PreviewLine>();
        private readonly List<DefineryParameter> _defineryParams;

        private int _anchorIndex = -1;
        private TextBox _activeField;

        public Window_ParamPreview(IEnumerable<DocumentSharedParameter> parameters)
        {
            InitializeComponent();

            var list = parameters?.ToList() ?? new List<DocumentSharedParameter>();

            var number = 1;
            foreach (var p in list)
            {
                var raw = BuildParamLine(p);
                _lines.Add(new PreviewLine
                {
                    Number = number++,
                    Raw = raw,
                    Fields = raw.Split('\t').ToList()
                });
            }

            _defineryParams = list.Select(p => new DefineryParameter
            {
                Guid = p.Guid,
                Name = p.Name,
                DataType = p.DataType,
                Description = p.Description ?? string.Empty,
                Visible = "1",
                UserModifiable = "1"
            }).ToList();

            PreviewList.ItemsSource = _lines;
        }

        /// <summary>
        /// The exact tab-delimited PARAM line Revit writes: PARAM, GUID, NAME, DATATYPE,
        /// DATACATEGORY, GROUP, VISIBLE, DESCRIPTION, USERMODIFIABLE.
        /// </summary>
        private static string BuildParamLine(DocumentSharedParameter p)
        {
            return string.Join("\t",
                "PARAM",
                p.Guid.ToString(),
                p.Name,
                p.DataType,
                string.Empty,   // data category (n/a for document params)
                "1",            // group (Default)
                "1",            // visible
                p.Description ?? string.Empty,
                "1");           // user modifiable
        }

        // ---------------------------------------------------------------- row selection

        private void PreviewGutter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var line = (sender as FrameworkElement)?.DataContext as PreviewLine;
            if (line == null) return;

            var index = _lines.IndexOf(line);
            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (shift && _anchorIndex >= 0)
            {
                var lo = Math.Min(_anchorIndex, index);
                var hi = Math.Max(_anchorIndex, index);
                for (int i = 0; i < _lines.Count; i++)
                {
                    _lines[i].IsRowSelected = (i >= lo && i <= hi);
                }
            }
            else if (ctrl)
            {
                line.IsRowSelected = !line.IsRowSelected;
                _anchorIndex = index;
            }
            else
            {
                foreach (var l in _lines) l.IsRowSelected = false;
                line.IsRowSelected = true;
                _anchorIndex = index;
            }

            // Selecting rows supersedes any single-field selection.
            _activeField = null;
            e.Handled = true;
        }

        // ---------------------------------------------------------------- field selection

        private void PreviewField_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
                _activeField = tb;
                e.Handled = true;
            }
        }

        private void PreviewField_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _activeField = sender as TextBox;
        }

        // ---------------------------------------------------------------- actions

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedRows = _lines.Where(l => l.IsRowSelected).ToList();

            string text = null;
            if (selectedRows.Count > 0)
            {
                text = string.Join(Environment.NewLine, selectedRows.Select(r => r.Raw));
            }
            else if (_activeField != null && _activeField.SelectionLength > 0)
            {
                text = _activeField.SelectedText;
            }

            if (string.IsNullOrEmpty(text))
            {
                Window_Message.Show(this, "Nothing to Copy",
                    "Select one or more rows by their line number, or double-click a field, then copy.");
                return;
            }

            try { Clipboard.SetText(text); }
            catch { /* clipboard can be transiently locked; ignore */ }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var content = DefineryParameter.CreateParamTable(_defineryParams);

            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                DefaultExt = ".txt",
                FileName = "SharedParameters.txt",
                RestoreDirectory = true
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, content);
                Window_Message.Show(this, "File Saved",
                    string.Format("Saved {0} parameter{1} to:\n\n{2}",
                        _defineryParams.Count, _defineryParams.Count == 1 ? string.Empty : "s", dialog.FileName));
            }
            catch (Exception ex)
            {
                Window_Message.Show(this, "Could Not Save File", ex.Message);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
