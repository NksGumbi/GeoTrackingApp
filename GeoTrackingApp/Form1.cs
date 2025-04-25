using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BitMiracle.LibTiff.Classic;
using GeoTrackingApp.Models;
using GMap.NET;
using Newtonsoft.Json;
using Orientation = System.Windows.Forms.Orientation;

namespace GeoTrackingApp
{
    public partial class Form1 : Form
    {
        private const string CONFIG_FILE = "config.json";
        private ConfigurationSettings config;
        private Panel mapPanel;
        private DataGridView dataGridView;
        private Button btnHistoricalTracking;
        private Button btnLiveTracking;
        private Button btnLoadTiff;
        private Button btnLoadGeoJson;
        private Panel controlPanel;
        private SplitContainer mainSplitContainer;
        private MenuStrip mainMenuStrip;
        private ToolStripMenuItem fileMenuItem;
        private ToolStripMenuItem settingsMenuItem;
        private PictureBox mapPictureBox;
        private List<GeoJsonFeature> currentFeatures;
        private GeoTransform currentTransform;
        private Bitmap currentTiffBitmap;
        private ToolTip mapToolTip;
        private Point lastMousePosition;
        private Panel zoomControlPanel;
        private bool isDragging = false;
        private float zoomScale = 1.0f;

        private static class AppColors
        {
            public static Color PrimaryBackground = Color.FromArgb(248, 249, 250);
            public static Color SecondaryBackground = Color.FromArgb(233, 236, 239);
            public static Color PrimaryText = Color.FromArgb(33, 37, 41);
            public static Color AccentColor = Color.FromArgb(0, 123, 255);
            public static Color SuccessColor = Color.FromArgb(40, 167, 69);
            public static Color DisabledColor = Color.FromArgb(108, 117, 125);
            public static Color ButtonHoverColor = Color.FromArgb(0, 105, 217);
        }

        public class ModernButton : Button
        {
            public ModernButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = AppColors.AccentColor;
                ForeColor = Color.White;
                Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                Cursor = Cursors.Hand;
                Size = new Size(130, 40);

                Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 10, 10));
            }

            [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
            private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect,
                int nBottomRect, int nWidthEllipse, int nHeightEllipse);

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                BackColor = AppColors.ButtonHoverColor;
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                BackColor = AppColors.AccentColor;
            }
        }

        public Form1()
        {
            InitializeComponent();

            this.Size = new Size(1200, 800);
            this.MinimumSize = new Size(800, 600);
            this.Text = "Geo Tracking Application";

            InitializeCustomComponents();
            LoadConfiguration();
            CheckFirstLaunch();
        }

        private void InitializeCustomComponents()
        {
            this.Size = new Size(1200, 800);
            this.Text = "Geo Tracking Application";
            this.BackColor = AppColors.PrimaryBackground;

            InitializeMenuStrip();
            InitializeControlPanel();
            InitializeMapPanel();
            InitializeDataGrid();
            InitializeSplitContainer();

            this.PerformLayout();
        }

        private void InitializeMenuStrip()
        {
            mainMenuStrip = new MenuStrip
            {
                BackColor = AppColors.SecondaryBackground,
                ForeColor = AppColors.PrimaryText
            };

            fileMenuItem = new ToolStripMenuItem("File");
            settingsMenuItem = new ToolStripMenuItem("Settings");

            fileMenuItem.DropDownItems.Add("Exit", null, (s, e) => Application.Exit());
            settingsMenuItem.DropDownItems.Add("Change Directory", null, ChangeDirectory_Click);

            mainMenuStrip.Items.Add(fileMenuItem);
            mainMenuStrip.Items.Add(settingsMenuItem);
            this.MainMenuStrip = mainMenuStrip;
            this.Controls.Add(mainMenuStrip);
        }

        private void InitializeControlPanel()
        {
            controlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = AppColors.SecondaryBackground,
                Padding = new Padding(10)
            };

            btnHistoricalTracking = new ModernButton
            {
                Text = "Historical Tracking",
                Location = new Point(20, 15)
            };
            btnHistoricalTracking.Click += BtnHistoricalTracking_Click;

            btnLiveTracking = new ModernButton
            {
                Text = "Live Tracking",
                Location = new Point(170, 15),
                Enabled = false,
                BackColor = AppColors.DisabledColor
            };

            btnLoadTiff = new ModernButton
            {
                Text = "Load TIFF",
                Location = new Point(320, 15),
                Visible = false
            };
            btnLoadTiff.Click += BtnLoadTiff_Click;

            btnLoadGeoJson = new ModernButton
            {
                Text = "Load GeoJSON",
                Location = new Point(470, 15),
                Visible = false
            };
            btnLoadGeoJson.Click += BtnLoadGeoJson_Click;

            controlPanel.Controls.AddRange(new Control[] {
                btnHistoricalTracking,
                btnLiveTracking,
                btnLoadTiff,
                btnLoadGeoJson
            });

            this.Controls.Add(controlPanel);
        }

        private void InitializeMapPanel()
        {
            mapPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            mapToolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 500,
                ShowAlways = true
            };

            Panel pictureContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            mapPictureBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Normal,
                Location = new Point(0, 0),
                BackColor = Color.White
            };

            zoomControlPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 50,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            Button zoomInButton = new Button
            {
                Text = "+",
                Size = new Size(40, 40),
                Location = new Point(5, 10),
                FlatStyle = FlatStyle.Flat
            };
            zoomInButton.Click += (s, e) => ZoomMap(1.2f);

            Button zoomOutButton = new Button
            {
                Text = "-",
                Size = new Size(40, 40),
                Location = new Point(5, 60),
                FlatStyle = FlatStyle.Flat
            };
            zoomOutButton.Click += (s, e) => ZoomMap(0.8f);

            zoomControlPanel.Controls.AddRange(new Control[] { zoomInButton, zoomOutButton });

            mapPictureBox.MouseWheel += MapPictureBox_MouseWheel;
            mapPictureBox.MouseUp += MapPictureBox_MouseUp;
            mapPictureBox.MouseDown += MapPictureBox_MouseDown;
            mapPictureBox.MouseMove += MapPictureBox_MouseMove;
            mapPictureBox.MouseClick += MapPictureBox_MouseClick;

            pictureContainer.Controls.Add(mapPictureBox);

            mapPanel.Controls.Add(pictureContainer);
            mapPanel.Controls.Add(zoomControlPanel);

            pictureContainer.Dock = DockStyle.Fill;
            zoomControlPanel.Dock = DockStyle.Right;
        }


        private void InitializeDataGrid()
        {
            dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = AppColors.PrimaryBackground,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            dataGridView.DefaultCellStyle.BackColor = AppColors.PrimaryBackground;
            dataGridView.DefaultCellStyle.SelectionBackColor = AppColors.AccentColor;
            dataGridView.DefaultCellStyle.SelectionForeColor = Color.White;
        }

        private void InitializeSplitContainer()
        {
            mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel1MinSize = 200,
                Panel2MinSize = 100,
            };

            this.Controls.Add(mainSplitContainer);

            mainSplitContainer.SplitterDistance = (int)(this.Height * 0.6);

            mainSplitContainer.Panel1.Controls.Add(mapPanel);
            mainSplitContainer.Panel2.Controls.Add(dataGridView);

            mainSplitContainer.BringToFront();
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    config = JsonConvert.DeserializeObject<ConfigurationSettings>(json);
                }
                else
                {
                    config = new ConfigurationSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                config = new ConfigurationSettings();
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                string json = JsonConvert.SerializeObject(config);
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CheckFirstLaunch()
        {
            if (string.IsNullOrEmpty(config.DefaultDirectory))
            {
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select default directory for file storage";
                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        config.DefaultDirectory = folderDialog.SelectedPath;
                        SaveConfiguration();
                    }
                    else
                    {
                        MessageBox.Show("Default directory must be set to continue.");
                        Application.Exit();
                    }
                }
            }
        }

        private void BtnHistoricalTracking_Click(object sender, EventArgs e)
        {
            btnLoadTiff.Visible = true;
            btnLoadGeoJson.Visible = true;
            btnHistoricalTracking.Enabled = false;
            btnLiveTracking.Enabled = false;

            MessageBox.Show(
                "Historical Tracking Mode Activated:\n\n" +
                "1. First, load a TIFF map file\n" +
                "2. Then, load a corresponding GeoJSON data file\n" +
                "3. Points will be plotted on the map",
                "Historical Tracking",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void BtnLoadTiff_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "TIFF files (*.tif;*.tiff)|*.tif;*.tiff|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = config.DefaultDirectory;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadTiffFile(openFileDialog.FileName);
                }
            }
        }

        private void LoadTiffFile(string filePath)
        {
            try
            {
                currentTiffBitmap?.Dispose();

                using (Image originalImage = Image.FromFile(filePath))
                {
                    currentTiffBitmap = new Bitmap(originalImage);
                }

                using (Tiff tiff = Tiff.Open(filePath, "r"))
                {
                    if (tiff == null)
                    {
                        throw new Exception("Could not open TIFF file using LibTiff");
                    }

                    FieldValue[] modelPixelScaleTag = tiff.GetField((TiffTag)33550);
                    FieldValue[] modelTiepointTag = tiff.GetField((TiffTag)33922);

                    currentTransform = (modelPixelScaleTag != null && modelTiepointTag != null)
                        ? new GeoTransform(
                            modelTiepointTag[1].ToDoubleArray(),
                            modelPixelScaleTag[1].ToDoubleArray()
                        )
                        : new GeoTransform(
                            new double[] { 0, 0, 0, -27.664135, 31.994103, 0 },
                            new double[] { 0.001, 0.001, 0 }
                        );
                }

                mapPictureBox.Image = currentTiffBitmap;
                mapPictureBox.Refresh();

                if (currentFeatures != null && currentFeatures.Any())
                {
                    DrawGeoJsonPoints();
                }

                MessageBox.Show("TIFF file loaded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading TIFF file:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void BtnLoadGeoJson_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "GeoJSON files (*.geojson)|*.geojson|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = config.DefaultDirectory;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadGeoJsonData(openFileDialog.FileName);
                }
            }
        }

        private void LoadGeoJsonData(string filePath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                var geoJsonData = JsonConvert.DeserializeObject<GeoJsonData>(jsonContent);

                if (geoJsonData?.features == null || !geoJsonData.features.Any())
                {
                    MessageBox.Show(
                        "The GeoJSON file is empty or invalid.\n" +
                        "Please ensure the file contains valid geographic features.",
                        "Invalid GeoJSON",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                var invalidFeatures = geoJsonData.features
                    .Where(f => f?.geometry?.coordinates == null ||
                                f.geometry.coordinates.Length != 2)
                    .ToList();

                if (invalidFeatures.Any())
                {
                    MessageBox.Show(
                        $"{invalidFeatures.Count} features have invalid coordinates and will be skipped.",
                        "Coordinate Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }

                currentFeatures = geoJsonData.features
                    .Where(f => f?.geometry?.coordinates != null &&
                                f.geometry.coordinates.Length == 2)
                    .ToList();

                UpdateDataGridView();

                if (currentTiffBitmap != null)
                {
                    DrawGeoJsonPoints();
                }

                MessageBox.Show(
                    $"GeoJSON data loaded successfully.\n" +
                    $"Total valid features: {currentFeatures.Count}",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (JsonException jex)
            {
                MessageBox.Show(
                    $"JSON Parsing Error:\n{jex.Message}\n\n" +
                    "Please check the GeoJSON file format.",
                    "JSON Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading GeoJSON file:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void DrawGeoJsonPoints()
        {
            if (mapPictureBox.Image == null || currentFeatures == null)
            {
                MessageBox.Show("No image or features to map.", "Mapping Error");
                return;
            }

            Bitmap drawingBitmap = new Bitmap(mapPictureBox.Image);

            using (Graphics g = Graphics.FromImage(drawingBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var feature in currentFeatures)
                {
                    try
                    {
                        double longitude = feature.geometry.coordinates[0];
                        double latitude = feature.geometry.coordinates[1];

                        int pixelX = (int)((longitude - 31.99) / 0.01 * drawingBitmap.Width);
                        int pixelY = (int)((latitude - (-27.67)) / 0.01 * drawingBitmap.Height);

                        pixelX = (int)(pixelX * zoomScale);
                        pixelY = (int)(pixelY * zoomScale);

                        if (pixelX >= 0 && pixelX < drawingBitmap.Width &&
                            pixelY >= 0 && pixelY < drawingBitmap.Height)
                        {
                            int pointSize = (int)(10 * zoomScale);
                            g.FillEllipse(Brushes.Red,
                                pixelX - pointSize / 2,
                                pixelY - pointSize / 2,
                                pointSize,
                                pointSize);

                            float bearing = (float)feature.properties.TNBearing;
                            float angleRad = (90 - bearing) * (float)Math.PI / 180f;
                            float arrowLength = 20 * zoomScale;

                            float endX = pixelX + arrowLength * (float)Math.Cos(angleRad);
                            float endY = pixelY - arrowLength * (float)Math.Sin(angleRad);

                            using (Pen arrowPen = new Pen(Brushes.Yellow, 2 * zoomScale))
                            {
                                g.DrawLine(arrowPen, pixelX, pixelY, endX, endY);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error mapping point: {ex.Message}");
                    }
                }
            }

            mapPictureBox.Image = drawingBitmap;
            mapPictureBox.Refresh();
        }

        private void UpdateDataGridView()
        {
            var gridData = currentFeatures.Select(f => new
            {
                Latitude = f.geometry.coordinates[1],
                Longitude = f.geometry.coordinates[0],
                SignalStrength = f.properties.SignalStrength,
                Altitude = f.properties.Altitude,
                DateTime = f.properties.DateTime,
                Frequency = f.properties.Frequency,
                TNBearing = f.properties.TNBearing
            }).ToList();

            dataGridView.DataSource = gridData;
        }

        private void MapPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (currentFeatures == null || e.Button != MouseButtons.Left) return;

            foreach (var feature in currentFeatures)
            {
                try
                {
                    // GeoJSON coordinates are [longitude, latitude]
                    double longitude = feature.geometry.coordinates[0];
                    double latitude = feature.geometry.coordinates[1];

                    // Calculate pixel coordinates
                    int pixelX = (int)((longitude - 31.99) / 0.01 * mapPictureBox.Image.Width);
                    int pixelY = (int)((latitude - (-27.67)) / 0.01 * mapPictureBox.Image.Height);

                    // Scale point based on zoom
                    pixelX = (int)(pixelX * zoomScale);
                    pixelY = (int)(pixelY * zoomScale);

                    // Check if click is near the point
                    if (Math.Abs(e.X - pixelX) < 10 && Math.Abs(e.Y - pixelY) < 10)
                    {
                        ShowFeatureDetails(feature);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in point click: {ex.Message}");
                }
            }
        }
        private string FormatFeatureTooltip(GeoJsonFeature feature)
        {
            return $"Signal Strength: {feature.properties.SignalStrength:F2}\n" +
                   $"Altitude: {feature.properties.Altitude:F1} m\n" +
                   $"Frequency: {feature.properties.Frequency:F4} MHz\n" +
                   $"Date & Time: {feature.properties.DateTime}\n" +
                   $"TN Bearing: {feature.properties.TNBearing:F2}°";
        }

        private void ShowFeatureDetails(GeoJsonFeature feature)
        {
            string details = $"Location: {feature.geometry.coordinates[1]:F6}, {feature.geometry.coordinates[0]:F6}\n\n" +
                           $"Signal Strength: {feature.properties.SignalStrength:F2}\n" +
                           $"Altitude: {feature.properties.Altitude:F1} m\n" +
                           $"Date & Time: {feature.properties.DateTime}\n" +
                           $"Frequency: {feature.properties.Frequency:F4} MHz\n" +
                           $"TN Bearing: {feature.properties.TNBearing:F2}°";

            using (Form detailForm = new Form())
            {
                detailForm.Text = "Point Details";
                detailForm.Size = new Size(400, 300);
                detailForm.StartPosition = FormStartPosition.CenterParent;

                RichTextBox detailsTextBox = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    Text = details,
                    ReadOnly = true,
                    Font = new Font("Consolas", 10),
                    BackColor = Color.White
                };

                detailForm.Controls.Add(detailsTextBox);
                detailForm.ShowDialog();
            }
        }

        private void ZoomMap(float zoomFactor)
        {
            if (currentTiffBitmap == null) return;

            zoomScale *= zoomFactor;
            zoomScale = Math.Max(0.1f, Math.Min(zoomScale, 5.0f));

            int newWidth = (int)(currentTiffBitmap.Width * zoomScale);
            int newHeight = (int)(currentTiffBitmap.Height * zoomScale);

            Bitmap zoomedBitmap = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(zoomedBitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                g.DrawImage(currentTiffBitmap, 0, 0, newWidth, newHeight);
            }

            mapPictureBox.Image = zoomedBitmap;
            mapPictureBox.Size = new Size(newWidth, newHeight);

            if (currentFeatures != null)
            {
                DrawGeoJsonPoints();
            }
        }


        private void MapPictureBox_MouseWheel(object sender, MouseEventArgs e)
        {
            float zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
            ZoomMap(zoomFactor);
        }

        private void MapPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
                Cursor.Current = Cursors.Default;
            }
        }

        private void MapPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                lastMousePosition = e.Location;
                Cursor.Current = Cursors.SizeAll;
            }
        }

        private void MapPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                int deltaX = e.X - lastMousePosition.X;
                int deltaY = e.Y - lastMousePosition.Y;

                mapPictureBox.Left += deltaX;
                mapPictureBox.Top += deltaY;

                lastMousePosition = e.Location;
            }

            if (currentFeatures == null) return;

            // Track if a point was hovered
            bool pointHovered = false;

            foreach (var feature in currentFeatures)
            {
                try
                {
                    // GeoJSON coordinates are [longitude, latitude]
                    double longitude = feature.geometry.coordinates[0];
                    double latitude = feature.geometry.coordinates[1];

                    // Calculate pixel coordinates
                    int pixelX = (int)((longitude - 31.99) / 0.01 * mapPictureBox.Image.Width);
                    int pixelY = (int)((latitude - (-27.67)) / 0.01 * mapPictureBox.Image.Height);

                    // Scale point based on zoom
                    pixelX = (int)(pixelX * zoomScale);
                    pixelY = (int)(pixelY * zoomScale);

                    // Check if mouse is near the point
                    if (Math.Abs(e.X - pixelX) < 10 && Math.Abs(e.Y - pixelY) < 10)
                    {
                        // Format tooltip text
                        string tooltipText = FormatFeatureTooltip(feature);
                
                        // Show tooltip
                        mapToolTip.SetToolTip(mapPictureBox, tooltipText);
                
                        // Change cursor
                        mapPictureBox.Cursor = Cursors.Hand;
                
                        pointHovered = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in mouse hover: {ex.Message}");
                }
            }

            // Reset cursor if no point is hovered
            if (!pointHovered)
            {
                mapPictureBox.Cursor = isDragging ? Cursors.SizeAll : Cursors.Default;
                mapToolTip.SetToolTip(mapPictureBox, null);
            }
        }


        private void ChangeDirectory_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select new default directory";
                folderDialog.SelectedPath = config.DefaultDirectory;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    config.DefaultDirectory = folderDialog.SelectedPath;
                    SaveConfiguration();
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            currentTiffBitmap?.Dispose();
            mapPictureBox.Image?.Dispose();
        }
    }
}