using System;
using System.Linq;
using System.Windows;
using MyProject.Controllers;
using MyProject.Models;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Linq;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace MyProject.Views
{
    public partial class DashboardWindow : Window
    {
        private readonly string _role;
        private readonly string _username;
        private int[,] _currentMatrix = new int[32, 32];
        private List<SensorData> _frames = new List<SensorData>();
        private int _frameIndex = 0;
        private System.Windows.Threading.DispatcherTimer? _playTimer;
        private double _playIntervalMs = 200;
        private List<MetricSample> _metricSamples = new List<MetricSample>();
        private ObservableCollection<User> _pendingUsers = new ObservableCollection<User>();

        private class MetricSample
        {
            public DateTime Timestamp { get; set; }
            public int PeakPressure { get; set; }
            public double ContactAreaPercent { get; set; }
        }
        public DashboardWindow(string role, string username)
        {
            _role = role;
            _username = string.IsNullOrWhiteSpace(username) ? "User" : username;
            InitializeComponent();
            Loaded += OnLoaded;
            CreateUserButton.Visibility = _role == "Admin" ? Visibility.Visible : Visibility.Collapsed;
            CreateUserButton.Click += (s, e) =>
            {
                try
                {
                    var win = new CreateUserWindow(_username) { Owner = this };
                    win.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Create User Error: {ex.Message}");
                }
            };
            OpenReportsButton.Click += (s, e) =>
            {
                try
                {
                    var entries = new List<ReportEntry>();
                    int threshold = 10;
                    if (!int.TryParse(MetricsThresholdInput.Text, out threshold) || threshold < 1) threshold = 10;

                    if (_frames.Count > 0)
                    {
                        for (int idx = 0; idx < _frames.Count; idx++)
                        {
                            var mat = _frames[idx].Matrix;
                            var (peak, areaPercent) = ComputeMetrics(mat, threshold);
                            var values = new int[32 * 32];
                            int k = 0;
                            for (int i = 0; i < 32; i++)
                                for (int j = 0; j < 32; j++)
                                    values[k++] = mat[i, j];
                            var ts = _frames[idx].Timestamp == DateTime.MinValue ? DateTime.Now : _frames[idx].Timestamp;
                            entries.Add(new ReportEntry
                            {
                                Time = ts,
                                PeakPressure = peak,
                                ContactArea = (int)Math.Round(areaPercent),
                                Values = values
                            });
                        }
                    }
                    else
                    {
                        var mat = _currentMatrix;
                        var (peak, areaPercent) = ComputeMetrics(mat, threshold);
                        var values = new int[32 * 32];
                        int k = 0;
                        for (int i = 0; i < 32; i++)
                            for (int j = 0; j < 32; j++)
                                values[k++] = mat[i, j];
                        entries.Add(new ReportEntry
                        {
                            Time = DateTime.Now,
                            PeakPressure = peak,
                            ContactArea = (int)Math.Round(areaPercent),
                            Values = values
                        });
                    }

                    var reports = new ReportsWindow(entries) { Owner = this };
                    reports.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Open Reports Error: {ex.Message}");
                }
            };
            SubmitCommentButton.Click += (s, e) =>
            {
                CommentsController.AddComment(_username, _role, CommentInput.Text);
                CommentInput.Clear();
            };
            LogoutButton.Click += (s, e) =>
            {
                var login = new MainWindow();
                login.Show();
                this.Close();
            };
            ReplyButton.Click += (s, e) =>
            {
                if (CommentsList.SelectedItem is Comment c)
                {
                    CommentsController.AddReply(c, ReplyInput.Text);
                    ReplyInput.Clear();
                }
            };
            ImportCsvButton.Click += (s, e) =>
            {
                try
                {
                    ImportAndDisplayFirstFrame();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"CSV Import Error: {ex.Message}");
                }
            };
            PickCsvFilesButton.Click += (s, e) =>
            {
                try
                {
                    var dlg = new OpenFileDialog
                    {
                        Filter = "CSV Files (*.csv)|*.csv",
                        Multiselect = true,
                        Title = "Select CSV Files"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        var frames = CsvImportController.LoadFramesFromFiles(dlg.FileNames);
                        ShowFirstFrameInfoAndDisplay(frames);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"CSV Import Error: {ex.Message}");
                }
            };
            PickCsvFolderButton.Click += (s, e) =>
            {
                try
                {
                    var dlg = new OpenFileDialog
                    {
                        Filter = "CSV Files (*.csv)|*.csv",
                        Multiselect = false,
                        Title = "Select any CSV inside the target folder"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        var folder = System.IO.Path.GetDirectoryName(dlg.FileName)!;
                        var frames = CsvImportController.LoadFramesFromFolder(folder);
                        ShowFirstFrameInfoAndDisplay(frames);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"CSV Import Error: {ex.Message}");
                }
            };

            // Playback controls
            PlayButton.Click += (s, e) =>
            {
                try
                {
                    EnsureTimer();
                    _playIntervalMs = PlaybackSpeedSlider.Value;
                    _playTimer!.Interval = TimeSpan.FromMilliseconds(_playIntervalMs);
                    _playTimer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Play Error: {ex.Message}");
                }
            };
            PauseButton.Click += (s, e) =>
            {
                try { _playTimer?.Stop(); }
                catch (Exception ex) { MessageBox.Show($"Pause Error: {ex.Message}"); }
            };
            NextButton.Click += (s, e) =>
            {
                try
                {
                    _playTimer?.Stop();
                    AdvanceFrame();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Next Error: {ex.Message}");
                }
            };
            PlaybackSpeedSlider.ValueChanged += (s, e) =>
            {
                try
                {
                    _playIntervalMs = PlaybackSpeedSlider.Value;
                    if (_playTimer != null) _playTimer.Interval = TimeSpan.FromMilliseconds(_playIntervalMs);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Speed Error: {ex.Message}");
                }
            };

            // Trends controls
            PopulateDummyMetricsButton.Click += (s, e) =>
            {
                try { PopulateDummyMetricsAndRedraw(); }
                catch (Exception ex) { MessageBox.Show($"Populate Error: {ex.Message}"); }
            };
            TimeFilterCombo.SelectionChanged += (s, e) =>
            {
                try { DrawMetricsChart(); }
                catch (Exception ex) { MessageBox.Show($"Filter Error: {ex.Message}"); }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure Admin-only controls are correctly visible after load
            try
            {
                CreateUserButton.Visibility = _role == "Admin" ? Visibility.Visible : Visibility.Collapsed;
                this.Title = $"Dashboard - {_username} ({_role})";

                // Role-based UI visibility per user stories:
                // - Clinician: can import CSVs and generate reports
                // - Patient: can view playback/heatmaps and add comments (no import)
                // - Admin: manages users/approvals; not for clinical import
                ImportCsvButton.Visibility = Session.IsClinician ? Visibility.Visible : Visibility.Collapsed;
                PickCsvFilesButton.Visibility = Session.IsClinician ? Visibility.Visible : Visibility.Collapsed;
                PickCsvFolderButton.Visibility = Session.IsClinician ? Visibility.Visible : Visibility.Collapsed;
                OpenReportsButton.Visibility = Session.IsClinician ? Visibility.Visible : Visibility.Collapsed;

                if (Session.IsAdmin)
                {
                    // Admin should be able to see analytics panels too; keep them enabled
                    HeatmapGrid.Visibility = Visibility.Visible;
                    TrendCanvas.Visibility = Visibility.Visible;
                    AlertsList.Visibility = Visibility.Visible;

                    PendingPanel.Visibility = Visibility.Visible;
                    RefreshPendingButton.Click += (s, e2) => { TryLoadPending(); };
                    // Single handler for all item-level buttons (Approve/Reject/Update Role)
                    PendingUsersList.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnPendingButtonClick));
                    TryLoadPending();
                }
            }
            catch { /* ignore if not ready */ }

            // Generate dummy 32x32 matrix (values 1-255)
            var rand = new Random();
            for (int i = 0; i < 32; i++)
                for (int j = 0; j < 32; j++)
                    _currentMatrix[i, j] = rand.Next(1, 256);

            RenderMatrix(_currentMatrix);
            UpdateMetricsFromCurrentMatrix();

            // Trend chart (simple polyline of 32 points)
            DrawTrend(rand);
            DrawMetricsChart();

            // Comments UI setup
            CommentsList.ItemsSource = CommentsController.Comments;
            ClinicianReplyPanel.Visibility = Session.IsClinician ? Visibility.Visible : Visibility.Collapsed;
            AlertsList.ItemsSource = AlertsController.Alerts;
            FrameInfoText.Text = "Frame -/-";

            RecalculateMetricsButton.Click += (s, e) =>
            {
                try { UpdateMetricsFromCurrentMatrix(); }
                catch (Exception ex) { MessageBox.Show($"Metrics Error: {ex.Message}"); }
            };
            TestMetricsButton.Click += (s, e) =>
            {
                try { RunMetricsTest(); }
                catch (Exception ex) { MessageBox.Show($"Test Error: {ex.Message}"); }
            };
        }

        private void TryLoadPending()
        {
            try
            {
                if (AuthController.AdminGetPendingRegistrations(out var users, out var error))
                {
                    _pendingUsers = new ObservableCollection<User>(users);
                    PendingUsersList.ItemsSource = _pendingUsers;
                }
                else
                {
                    MessageBox.Show(error ?? "Unable to load pending registrations.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load Pending Error: {ex.Message}");
            }
        }

        private void OnPendingButtonClick(object? sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button btn) return;
            var dc = (btn.DataContext as User);
            if (dc == null) return;
            var userId = dc.UserId;
            var label = btn.Content?.ToString() ?? string.Empty;
            try
            {
                if (string.Equals(label, "Approve", StringComparison.OrdinalIgnoreCase))
                {
                    if (AuthController.AdminApproveRegistration(_username, userId, out var error))
                    {
                        MessageBox.Show($"Approved '{dc.Username}'.");
                        _pendingUsers.Remove(dc);
                    }
                    else
                    {
                        MessageBox.Show(error ?? "Approve failed.");
                    }
                }
                else if (string.Equals(label, "Reject", StringComparison.OrdinalIgnoreCase))
                {
                    if (AuthController.AdminRejectRegistration(_username, userId, false, out var error))
                    {
                        MessageBox.Show($"Rejected '{dc.Username}'.");
                        _pendingUsers.Remove(dc);
                    }
                    else
                    {
                        MessageBox.Show(error ?? "Reject failed.");
                    }
                }
                else if (string.Equals(label, "Update Role", StringComparison.OrdinalIgnoreCase))
                {
                    // Find the sibling ComboBox in the visual tree to get selected role
                    var itemContainer = PendingUsersList.ItemContainerGenerator.ContainerFromItem(dc) as ListBoxItem;
                    if (itemContainer != null)
                    {
                        var combo = FindChild<ComboBox>(itemContainer);
                        var selected = (combo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? dc.Role;
                        if (AuthController.AdminEditUserRole(_username, userId, selected, out var error))
                        {
                            MessageBox.Show($"Updated role for '{dc.Username}' to {selected}.");
                            // Update in collection to reflect change
                            dc.Role = selected;
                            PendingUsersList.Items.Refresh();
                        }
                        else
                        {
                            MessageBox.Show(error ?? "Role update failed.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Admin Action Error: {ex.Message}");
            }
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var sub = FindChild<T>(child);
                if (sub != null) return sub;
            }
            return null;
        }

        private void ImportAndDisplayFirstFrame()
        {
            var frames = CsvImportController.LoadAllFrames();
            ShowFirstFrameInfoAndDisplay(frames);
        }

        private void ShowFirstFrameInfoAndDisplay(List<SensorData> frames)
        {
            var info = CsvImportController.DescribeFirstFrame(frames);
            MessageBox.Show(info);

            if (frames.Count == 0) return;

            // replace heatmap with first frame matrix
            var mat = frames[0].Matrix;
            _currentMatrix = mat;
            RenderMatrix(mat);
            UpdateMetricsFromCurrentMatrix();

            // store frames and start dynamic playback if multiple frames present
            _frames = frames;
            _frameIndex = 0;
            StartPlaybackIfMultipleFrames();
            FrameInfoText.Text = $"Frame 1/{_frames.Count}";

            // seed initial metrics sample
            int thresholdSeed = 10;
            if (!int.TryParse(MetricsThresholdInput.Text, out thresholdSeed) || thresholdSeed < 1) thresholdSeed = 10;
            var (seedPeak, seedArea) = ComputeMetrics(_currentMatrix, thresholdSeed);
            var seedTs = _frames[0].Timestamp == DateTime.MinValue ? DateTime.Now : _frames[0].Timestamp;
            _metricSamples.Add(new MetricSample { Timestamp = seedTs, PeakPressure = seedPeak, ContactAreaPercent = seedArea });
            DrawMetricsChart();
        }

        private void RenderMatrix(int[,] mat)
        {
            // Initialize rectangles once, then update fills for dynamic refresh
            if (HeatmapGrid.Children.Count != 32 * 32)
            {
                HeatmapGrid.Children.Clear();
                for (int i = 0; i < 32; i++)
                {
                    for (int j = 0; j < 32; j++)
                    {
                        int v = mat[i, j];
                        var rect = new Rectangle
                        {
                            // Let rectangles stretch to fill the UniformGrid cells
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            Margin = new Thickness(0.5),
                            Fill = new SolidColorBrush(ValueToColor(v)),
                            Stroke = Brushes.Transparent
                        };
                        HeatmapGrid.Children.Add(rect);
                    }
                }
            }
            else
            {
                // Update existing rectangles' colors
                int index = 0;
                for (int i = 0; i < 32; i++)
                {
                    for (int j = 0; j < 32; j++)
                    {
                        int v = mat[i, j];
                        if (HeatmapGrid.Children[index] is Rectangle r)
                        {
                            var brush = r.Fill as SolidColorBrush;
                            var color = ValueToColor(v);
                            if (brush == null || brush.Color != color)
                            {
                                r.Fill = new SolidColorBrush(color);
                            }
                        }
                        index++;
                    }
                }
            }
        }

        private void StartPlaybackIfMultipleFrames()
        {
            // Stop any existing timer
            _playTimer?.Stop();

            if (_frames.Count <= 1)
            {
                _playTimer = null;
                return;
            }

            // Create or reconfigure the timer
            _playTimer ??= new System.Windows.Threading.DispatcherTimer();
            _playIntervalMs = PlaybackSpeedSlider.Value;
            _playTimer.Interval = TimeSpan.FromMilliseconds(_playIntervalMs); // controlled by slider
            _playTimer.Tick -= OnPlayTick; // ensure no duplicate handlers
            _playTimer.Tick += OnPlayTick;
            _playTimer.Start();
        }

        private void OnPlayTick(object? sender, EventArgs e)
        {
            AdvanceFrame();
        }

        private void AdvanceFrame()
        {
            if (_frames.Count == 0) return;
            _frameIndex = (_frameIndex + 1) % _frames.Count;
            _currentMatrix = _frames[_frameIndex].Matrix;
            RenderMatrix(_currentMatrix);
            UpdateMetricsFromCurrentMatrix();
            // record metrics sample for trends and redraw chart
            int threshold = 10;
            if (!int.TryParse(MetricsThresholdInput.Text, out threshold) || threshold < 1) threshold = 10;
            var (peak, area) = ComputeMetrics(_currentMatrix, threshold);
            var ts = _frames[_frameIndex].Timestamp == DateTime.MinValue ? DateTime.Now : _frames[_frameIndex].Timestamp;
            _metricSamples.Add(new MetricSample { Timestamp = ts, PeakPressure = peak, ContactAreaPercent = area });
            DrawMetricsChart();
            FrameInfoText.Text = $"Frame {_frameIndex + 1}/{_frames.Count}";
        }

        private void EnsureTimer()
        {
            if (_playTimer == null)
            {
                _playTimer = new System.Windows.Threading.DispatcherTimer();
                _playTimer.Tick += OnPlayTick;
                _playTimer.Interval = TimeSpan.FromMilliseconds(_playIntervalMs);
            }
        }

        private void UpdateMetricsFromCurrentMatrix()
        {
            int threshold = 10;
            if (!int.TryParse(MetricsThresholdInput.Text, out threshold) || threshold < 1)
                threshold = 10;

            var (peakPressure, contactAreaPercent) = ComputeMetrics(_currentMatrix, threshold);
            PeakPressureText.Text = peakPressure.ToString();
            ContactAreaText.Text = $"{contactAreaPercent:F1}%";

            if (peakPressure > threshold)
            {
                AlertsController.AddAlert(peakPressure, threshold, "Dashboard");
            }
        }

        private (int peakPressure, double contactAreaPercent) ComputeMetrics(int[,] mat, int threshold)
        {
            int rows = 32, cols = 32;
            int total = rows * cols;

            // Contact area: pixels >= threshold
            int above = 0;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    if (mat[i, j] >= threshold) above++;
            double percent = (above * 100.0) / total;

            // Peak pressure: exclude connected areas < 10 pixels
            bool[,] visited = new bool[rows, cols];
            int peak = 0;
            int[] di = new[] { -1, 1, 0, 0 };
            int[] dj = new[] { 0, 0, -1, 1 };
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (visited[i, j] || mat[i, j] < threshold) continue;
                    var q = new Queue<(int r, int c)>();
                    q.Enqueue((i, j));
                    visited[i, j] = true;
                    int size = 0;
                    int localMax = 0;
                    while (q.Count > 0)
                    {
                        var (r, c) = q.Dequeue();
                        size++;
                        if (mat[r, c] > localMax) localMax = mat[r, c];
                        for (int k = 0; k < 4; k++)
                        {
                            int nr = r + di[k];
                            int nc = c + dj[k];
                            if (nr < 0 || nr >= rows || nc < 0 || nc >= cols) continue;
                            if (visited[nr, nc]) continue;
                            if (mat[nr, nc] < threshold) continue;
                            visited[nr, nc] = true;
                            q.Enqueue((nr, nc));
                        }
                    }
                    if (size >= 10 && localMax > peak) peak = localMax;
                }
            }
            return (peak, percent);
        }

        private void RunMetricsTest()
        {
            int threshold = 10;
            int[,] test = new int[32, 32];

            // Small cluster 3x3 of value 200 (size 9) -> excluded for peak
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    test[2 + i, 2 + j] = 200;

            // Large cluster 4x3 (size 12) with varying values up to 81 -> included
            int[,] cluster = new int[4, 3]
            {
                { 50, 60, 70 },
                { 55, 65, 75 },
                { 58, 68, 78 },
                { 59, 69, 81 }
            };
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 3; j++)
                    test[10 + i, 10 + j] = cluster[i, j];

            // Expected: peak 81 (from large cluster), contact area: (9 + 12) / 1024 * 100
            var (peak, percent) = ComputeMetrics(test, threshold);
            double expectedPercent = ((9 + 12) * 100.0) / (32 * 32);
            int expectedPeak = 81;

            MessageBox.Show($"Test Metrics\nComputed -> Peak: {peak}, Contact Area: {percent:F2}%\nExpected -> Peak: {expectedPeak}, Contact Area: {expectedPercent:F2}%");

            if (peak > threshold)
            {
                AlertsController.AddAlert(peak, threshold, "Test");
            }
        }

        private void DrawTrend(Random rand)
        {
            TrendCanvas.Children.Clear();
            int points = 32;
            double width = TrendCanvas.Width;
            double height = TrendCanvas.Height;

            // Axes (optional visuals)
            TrendCanvas.Children.Add(new Line
            {
                X1 = 0, Y1 = height - 1, X2 = width, Y2 = height - 1,
                Stroke = Brushes.Gray, StrokeThickness = 1
            });
            TrendCanvas.Children.Add(new Line
            {
                X1 = 1, Y1 = 0, X2 = 1, Y2 = height,
                Stroke = Brushes.Gray, StrokeThickness = 1
            });

            var poly = new Polyline { Stroke = Brushes.SteelBlue, StrokeThickness = 2 };
            for (int i = 0; i < points; i++)
            {
                int val = rand.Next(1, 256);
                double x = i * (width / (points - 1));
                double y = height - (val / 255.0) * height;
                poly.Points.Add(new Point(x, y));
            }
            TrendCanvas.Children.Add(poly);
        }

        private void PopulateDummyMetricsAndRedraw()
        {
            _metricSamples.Clear();
            var now = DateTime.Now;
            var rand = new Random();
            // one sample every 10 minutes for last 24 hours
            for (int i = 0; i <= 24 * 6; i++)
            {
                var ts = now.AddMinutes(-10 * (24 * 6 - i));
                var peak = rand.Next(20, 100);
                var area = rand.Next(10, 80) + rand.NextDouble();
                _metricSamples.Add(new MetricSample { Timestamp = ts, PeakPressure = peak, ContactAreaPercent = area });
            }
            DrawMetricsChart();
        }

        private void DrawMetricsChart()
        {
            TrendCanvas.Children.Clear();

            var width = TrendCanvas.ActualWidth > 0 ? TrendCanvas.ActualWidth : TrendCanvas.Width;
            var height = TrendCanvas.ActualHeight > 0 ? TrendCanvas.ActualHeight : TrendCanvas.Height;

            // axes
            TrendCanvas.Children.Add(new Line { X1 = 30, X2 = width - 10, Y1 = height - 20, Y2 = height - 20, Stroke = Brushes.Gray, StrokeThickness = 0.5 });
            TrendCanvas.Children.Add(new Line { X1 = 30, X2 = 30, Y1 = 10, Y2 = height - 20, Stroke = Brushes.Gray, StrokeThickness = 0.5 });

            // determine filter window
            int hours = 1;
            if (TimeFilterCombo.SelectedItem is ComboBoxItem item)
            {
                var content = item.Content?.ToString()?.Trim();
                if (content == "6h") hours = 6; else if (content == "24h") hours = 24; else hours = 1;
            }
            var cutoff = DateTime.Now.AddHours(-hours);
            var filtered = _metricSamples.Where(s => s.Timestamp >= cutoff).OrderBy(s => s.Timestamp).ToList();
            if (filtered.Count < 2) return;

            DateTime minT = filtered.First().Timestamp;
            DateTime maxT = filtered.Last().Timestamp;
            double rangeSeconds = Math.Max(1, (maxT - minT).TotalSeconds);
            double left = 30, right = width - 10;
            double top = 10, bottom = height - 20;

            var peakLine = new Polyline { Stroke = Brushes.Crimson, StrokeThickness = 1.5 };
            var areaLine = new Polyline { Stroke = Brushes.DodgerBlue, StrokeThickness = 1.5 };

            foreach (var s in filtered)
            {
                double tx = (s.Timestamp - minT).TotalSeconds / rangeSeconds;
                double x = left + tx * (right - left);
                double yPeak = bottom - (s.PeakPressure / 255.0) * (bottom - top);
                double yArea = bottom - (s.ContactAreaPercent / 100.0) * (bottom - top);
                peakLine.Points.Add(new Point(x, yPeak));
                areaLine.Points.Add(new Point(x, yArea));
            }

            TrendCanvas.Children.Add(peakLine);
            TrendCanvas.Children.Add(areaLine);
            TrendCanvas.Children.Add(new TextBlock { Text = "Peak", Foreground = Brushes.Crimson, Margin = new Thickness(40, 6, 0, 0) });
            TrendCanvas.Children.Add(new TextBlock { Text = "Area%", Foreground = Brushes.DodgerBlue, Margin = new Thickness(100, 6, 0, 0) });
        }

        private Color ValueToColor(int value)
        {
            // Map 1..255 to a blue->red gradient
            byte r = (byte)value;
            byte g = 0;
            byte b = (byte)(255 - value);
            return Color.FromRgb(r, g, b);
        }
    }
}