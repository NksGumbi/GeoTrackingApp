using Newtonsoft.Json;
using BitMiracle.LibTiff.Classic;
using System.Drawing;
using GeoTrackingApp.Models;
using System;
using System.IO;

namespace GeoTrackingApp
{
    public class FileHandler
    {
        public static GeoJsonData LoadGeoJsonFile(string filePath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<GeoJsonData>(jsonContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading GeoJSON file: {ex.Message}");
            }
        }

        public static Bitmap LoadTiffFile(string filePath)
        {
            try
            {
                using (Tiff tiff = Tiff.Open(filePath, "r"))
                {
                    if (tiff == null)
                        throw new Exception("Could not open TIFF file");

                    int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                    int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

                    Bitmap bitmap = new Bitmap(width, height);
                    byte[] scanline = new byte[tiff.ScanlineSize()];

                    for (int row = 0; row < height; row++)
                    {
                        tiff.ReadScanline(scanline, row);
                        for (int col = 0; col < width; col++)
                        {
                            byte intensity = scanline[col];
                            bitmap.SetPixel(col, row, Color.FromArgb(intensity, intensity, intensity));
                        }
                    }

                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading TIFF file: {ex.Message}");
            }
        }

        public static void SaveConfiguration(ConfigurationSettings config, string filePath)
        {
            File.WriteAllText(filePath, JsonConvert.SerializeObject(config));
        }

        public static ConfigurationSettings LoadConfiguration(string filePath)
        {
            if (!File.Exists(filePath))
                return new ConfigurationSettings();

            string jsonContent = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ConfigurationSettings>(jsonContent);
        }
    }
}