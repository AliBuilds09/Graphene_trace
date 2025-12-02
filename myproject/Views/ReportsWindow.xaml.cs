using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Printing;
using System.Windows.Documents;
using MyProject.Controllers;

namespace MyProject.Views
{
    public partial class ReportsWindow : Window
    {
        private List<ReportEntry> _entries = new();

        public ReportsWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            MetricsGrid.SelectionChanged += MetricsGrid_SelectionChanged;
            ExportPdfButton.Click += ExportPdfButton_Click;
            CompareButton.Click += CompareButton_Click;
            ExportCsvButton.Click += ExportCsvButton_Click;
        }

        public ReportsWindow(List<ReportEntry> entries) : this()
        {
            _entries = entries ?? new List<ReportEntry>();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_entries == null || _entries.Count == 0)
            {
                // Fallback: generate 10 dummy snapshots
                var rand = new Random();
                _entries = new List<ReportEntry>();
                for (int i = 0; i < 10; i++)
                {
                    var values = new int[32 * 32];
                    for (int j = 0; j < values.Length; j++) values[j] = rand.Next(1, 256);
                    var peak = values.Max();
                    var areaPercent = (int)Math.Round(values.Count(v => v > 0) * 100.0 / (32 * 32));
                    _entries.Add(new ReportEntry
                    {
                        Time = DateTime.Now.AddMinutes(i),
                        PeakPressure = peak,
                        ContactArea = areaPercent,
                        Values = values
                    });
                }
            }

            MetricsGrid.ItemsSource = _entries;
            MetricsGrid.SelectedIndex = 0;

            // Summary
            var peaks = _entries.Select(e => e.PeakPressure).ToList();
            var areas = _entries.Select(e => e.ContactArea).ToList();
            if (peaks.Count > 0)
            {
                double avgPeak = peaks.Average();
                int minPeak = peaks.Min();
                int maxPeak = peaks.Max();
                double avgArea = areas.Average();
                int minArea = areas.Min();
                int maxArea = areas.Max();
                SummaryText.Text = $"Summary: Peak(min/avg/max) {minPeak}/{avgPeak:F1}/{maxPeak} | Area%(min/avg/max) {minArea}/{avgArea:F1}/{maxArea}";
            }

            // Compare 6h vs 24h
            var now = DateTime.Now;
            var e6 = _entries.Where(x => x.Time >= now.AddHours(-6)).ToList();
            var e24 = _entries.Where(x => x.Time >= now.AddHours(-24)).ToList();
            if (e6.Count >= 1 && e24.Count >= 1)
            {
                double avg6Peak = e6.Average(x => x.PeakPressure);
                double avg24Peak = e24.Average(x => x.PeakPressure);
                double avg6Area = e6.Average(x => x.ContactArea);
                double avg24Area = e24.Average(x => x.ContactArea);
                double dPeak = avg6Peak - avg24Peak;
                double dArea = avg6Area - avg24Area;
                CompareSummaryText.Text = $"Avg Peak: 6h {avg6Peak:F1} vs 24h {avg24Peak:F1} (Δ {dPeak:F1}) | Avg Area%: 6h {avg6Area:F1} vs 24h {avg24Area:F1} (Δ {dArea:F1})";
            }
        }

        private void MetricsGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selected = MetricsGrid.SelectedItem as ReportEntry;
            if (selected == null) return;
            DrawSnapshot(selected);
            SelectedPeakText.Text = selected.PeakPressure.ToString();
            SelectedAreaText.Text = $"{selected.ContactArea}%";
        }

        private void DrawSnapshot(ReportEntry entry)
        {
            SnapshotHeatmap.Children.Clear();
            foreach (var v in entry.Values)
            {
                var rect = new Rectangle
                {
                    Width = 10,
                    Height = 10,
                    Fill = new SolidColorBrush(ValueToColor(v)),
                    Stroke = Brushes.Transparent
                };
                SnapshotHeatmap.Children.Add(rect);
            }
        }

        private void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            var idx = MetricsGrid.SelectedIndex;
            if (idx <= 0)
            {
                MessageBox.Show("No previous data to compare.");
                return;
            }

            var current = _entries[idx];
            var prev = _entries[idx - 1];
            var peakDiff = current.PeakPressure - prev.PeakPressure;
            var areaDiff = current.ContactArea - prev.ContactArea;
            MessageBox.Show($"Compared to previous:\nPeak Pressure: {prev.PeakPressure} → {current.PeakPressure} (Δ {peakDiff})\nContact Area%: {prev.ContactArea} → {current.ContactArea} (Δ {areaDiff})");
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"metrics_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (dlg.ShowDialog() == true)
                {
                    using (var sw = new System.IO.StreamWriter(dlg.FileName))
                    {
                        sw.WriteLine("Time,PeakPressure,ContactAreaPercent");
                        foreach (var entry in _entries.OrderBy(x => x.Time))
                        {
                            sw.WriteLine($"{entry.Time:yyyy-MM-dd HH:mm:ss},{entry.PeakPressure},{entry.ContactArea}");
                        }
                    }
                    MessageBox.Show("CSV export completed.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV Export Error: {ex.Message}");
            }
        }

        private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    var doc = new FlowDocument
                    {
                        PagePadding = new Thickness(50),
                        ColumnWidth = double.PositiveInfinity
                    };

                    doc.Blocks.Add(new Paragraph(new Run($"Pressure Metrics Report - Generated {DateTime.Now:yyyy-MM-dd HH:mm}"))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Bold
                    });

                    // Summary
                    doc.Blocks.Add(new Paragraph(new Run(SummaryText.Text)));

                    // Compare summary
                    if (!string.IsNullOrWhiteSpace(CompareSummaryText.Text))
                    {
                        doc.Blocks.Add(new Paragraph(new Run(CompareSummaryText.Text)));
                    }

                    // Metrics table
                    var table = new Table();
                    table.Columns.Add(new TableColumn());
                    table.Columns.Add(new TableColumn());
                    table.Columns.Add(new TableColumn());
                    var headerRowGroup = new TableRowGroup();
                    var headerRow = new TableRow();
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Time"))) { FontWeight = FontWeights.Bold });
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("PeakPressure"))) { FontWeight = FontWeights.Bold });
                    headerRow.Cells.Add(new TableCell(new Paragraph(new Run("ContactArea%"))) { FontWeight = FontWeights.Bold });
                    headerRowGroup.Rows.Add(headerRow);

                    var bodyGroup = new TableRowGroup();
                    foreach (var entry in _entries.OrderBy(x => x.Time))
                    {
                        var row = new TableRow();
                        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.Time.ToString("yyyy-MM-dd HH:mm")))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.PeakPressure.ToString()))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.ContactArea.ToString()))));
                        bodyGroup.Rows.Add(row);
                    }
                    table.RowGroups.Add(headerRowGroup);
                    table.RowGroups.Add(bodyGroup);
                    doc.Blocks.Add(table);

                    // Alerts section
                    if (IncludeAlertsCheckBox.IsChecked == true)
                    {
                        doc.Blocks.Add(new Paragraph(new Run("Alerts")) { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 0) });
                        var alertsTable = new Table();
                        alertsTable.Columns.Add(new TableColumn());
                        alertsTable.Columns.Add(new TableColumn());
                        var aHeader = new TableRowGroup();
                        var aHeaderRow = new TableRow();
                        aHeaderRow.Cells.Add(new TableCell(new Paragraph(new Run("Time"))) { FontWeight = FontWeights.Bold });
                        aHeaderRow.Cells.Add(new TableCell(new Paragraph(new Run("Message"))) { FontWeight = FontWeights.Bold });
                        aHeader.Rows.Add(aHeaderRow);
                        var aBody = new TableRowGroup();
                        foreach (var a in AlertsController.Alerts.OrderBy(x => x.Timestamp).Take(50))
                        {
                            var r = new TableRow();
                            r.Cells.Add(new TableCell(new Paragraph(new Run(a.Timestamp.ToString("yyyy-MM-dd HH:mm")))));
                            r.Cells.Add(new TableCell(new Paragraph(new Run(a.Message))));
                            aBody.Rows.Add(r);
                        }
                        alertsTable.RowGroups.Add(aHeader);
                        alertsTable.RowGroups.Add(aBody);
                        doc.Blocks.Add(alertsTable);
                    }

                    // Comments section
                    if (IncludeCommentsCheckBox.IsChecked == true)
                    {
                        doc.Blocks.Add(new Paragraph(new Run("Comments")) { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 0) });
                        var cTable = new Table();
                        cTable.Columns.Add(new TableColumn());
                        cTable.Columns.Add(new TableColumn());
                        cTable.Columns.Add(new TableColumn());
                        var cHeader = new TableRowGroup();
                        var cHeaderRow = new TableRow();
                        cHeaderRow.Cells.Add(new TableCell(new Paragraph(new Run("Time"))) { FontWeight = FontWeights.Bold });
                        cHeaderRow.Cells.Add(new TableCell(new Paragraph(new Run("Author(Role)"))) { FontWeight = FontWeights.Bold });
                        cHeaderRow.Cells.Add(new TableCell(new Paragraph(new Run("Text / Reply"))) { FontWeight = FontWeights.Bold });
                        cHeader.Rows.Add(cHeaderRow);
                        var cBody = new TableRowGroup();
                        foreach (var c in CommentsController.Comments.OrderBy(x => x.Timestamp).Take(50))
                        {
                            var r = new TableRow();
                            r.Cells.Add(new TableCell(new Paragraph(new Run(c.Timestamp.ToString("yyyy-MM-dd HH:mm")))));
                            r.Cells.Add(new TableCell(new Paragraph(new Run($"{c.Author} ({c.Role})"))));
                            var text = string.IsNullOrWhiteSpace(c.Reply) ? c.Text : $"{c.Text}\nReply: {c.Reply}";
                            r.Cells.Add(new TableCell(new Paragraph(new Run(text))));
                            cBody.Rows.Add(r);
                        }
                        cTable.RowGroups.Add(cHeader);
                        cTable.RowGroups.Add(cBody);
                        doc.Blocks.Add(cTable);
                    }

                    var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                    pd.PrintDocument(paginator, "Pressure Metrics Report");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF Export Error: {ex.Message}");
            }
        }

        private Color ValueToColor(int value)
        {
            byte r = (byte)value;
            byte g = 0;
            byte b = (byte)(255 - value);
            return Color.FromRgb(r, g, b);
        }
    }

    public class ReportEntry
    {
        public DateTime Time { get; set; }
        public int PeakPressure { get; set; }
        public int ContactArea { get; set; }
        public int[] Values { get; set; } = Array.Empty<int>();
    }
}