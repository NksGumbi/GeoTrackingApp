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
                AutoScroll = true,
                BackColor = Color.White
            };

            mapPictureBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.AutoSize,
                Location = new Point(0, 0),
                BackColor = Color.White
            };

            mapPictureBox.MouseClick += MapPictureBox_MouseClick;
            mapPictureBox.MouseMove += MapPictureBox_MouseMove;
            mapPanel.Controls.Add(mapPictureBox);
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
                if (currentTiffBitmap != null)
                {
                    currentTiffBitmap.Dispose();
                }

                // First try to load the image directly
                using (Image originalImage = Image.FromFile(filePath))
                {
                    currentTiffBitmap = new Bitmap(originalImage);
                }

                using (Tiff tiff = Tiff.Open(filePath, "r"))
                {
                    if (tiff == null)
                    {
                        MessageBox.Show("Failed to open TIFF file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Get georeference information
                    FieldValue[] modelPixelScaleTag = tiff.GetField((TiffTag)33550);
                    FieldValue[] modelTiepointTag = tiff.GetField((TiffTag)33922);

                    if (modelPixelScaleTag != null && modelTiepointTag != null)
                    {
                        double[] modelPixelScale = modelPixelScaleTag[1].ToDoubleArray();
                        double[] modelTiepoint = modelTiepointTag[1].ToDoubleArray();
                        currentTransform = new GeoTransform(modelTiepoint, modelPixelScale);
                    }
                    else
                    {
                        // Use default transform if no georeferencing is available
                        currentTransform = new GeoTransform(
                            new double[] { 0, 0, 0, -27.664135, 31.994103, 0 },
                            new double[] { 0.001, 0.001, 0 }
                        );
                    }
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
                MessageBox.Show($"Error loading TIFF file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    MessageBox.Show("Invalid or empty GeoJSON file", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                currentFeatures = geoJsonData.features;
                UpdateDataGridView();

                if (currentTiffBitmap != null)
                {
                    DrawGeoJsonPoints();
                }

                MessageBox.Show("GeoJSON data loaded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading GeoJSON file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DrawGeoJsonPoints()
        {
            if (mapPictureBox.Image == null || currentFeatures == null || currentTransform == null)
                return;

            // Create a copy of the current TIFF image
            Bitmap drawingBitmap = new Bitmap(currentTiffBitmap);

            using (Graphics g = Graphics.FromImage(drawingBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var feature in currentFeatures)
                {
                    var coordinates = feature.geometry.coordinates;
                    var pixelPoint = currentTransform.WorldToPixel(
                        new PointLatLng(coordinates[1], coordinates[0])
                    );

                    // Draw point
                    using (SolidBrush pointBrush = new SolidBrush(Color.FromArgb(200, Color.Red)))
                    {
                        g.FillEllipse(pointBrush, pixelPoint.X - 5, pixelPoint.Y - 5, 10, 10);
                    }

                    // Draw direction arrow
                    float bearing = (float)feature.properties.TNBearing;
                    float angleRad = (90 - bearing) * (float)Math.PI / 180f;
                    float arrowLength = 20;

                    float endX = pixelPoint.X + arrowLength * (float)Math.Cos(angleRad);
                    float endY = pixelPoint.Y - arrowLength * (float)Math.Sin(angleRad);

                    using (Pen arrowPen = new Pen(Color.FromArgb(200, Color.Yellow), 2))
                    {
                        g.DrawLine(arrowPen, pixelPoint.X, pixelPoint.Y, endX, endY);
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
            if (currentFeatures == null || currentTransform == null)
                return;

            Point clickPoint = e.Location;
            foreach (var feature in currentFeatures)
            {
                var coordinates = feature.geometry.coordinates;
                var pixelPoint = currentTransform.WorldToPixel(
                    new PointLatLng(coordinates[1], coordinates[0])
                );

                if (Math.Abs(clickPoint.X - pixelPoint.X) < 10 && Math.Abs(clickPoint.Y - pixelPoint.Y) < 10)
                {
                    ShowFeatureDetails(feature);
                    break;
                }
            }
        }

        private void ShowFeatureDetails(GeoJsonFeature feature)
        {
            string details = $"Location: {feature.geometry.coordinates[1]}, {feature.geometry.coordinates[0]}\n" +
                           $"Signal Strength: {feature.properties.SignalStrength}\n" +
                           $"Altitude: {feature.properties.Altitude}\n" +
                           $"Date & Time: {feature.properties.DateTime}\n" +
                           $"Frequency: {feature.properties.Frequency}\n" +
                           $"TN Bearing: {feature.properties.TNBearing}°";

            MessageBox.Show(details, "Point Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MapPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (currentFeatures == null || currentTransform == null)
                return;

            Point mousePoint = e.Location;
            foreach (var feature in currentFeatures)
            {
                var coordinates = feature.geometry.coordinates;
                var pixelPoint = currentTransform.WorldToPixel(
                    new PointLatLng(coordinates[1], coordinates[0])
                );

                if (Math.Abs(mousePoint.X - pixelPoint.X) < 10 && Math.Abs(mousePoint.Y - pixelPoint.Y) < 10)
                {
                    mapPictureBox.Cursor = Cursors.Hand;
                    return;
                }
            }
            mapPictureBox.Cursor = Cursors.Default;
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