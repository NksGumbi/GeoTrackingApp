using GMap.NET;
using System;
using System.Drawing;

namespace GeoTrackingApp
{
    public class GeoTransform
    {
        private readonly double[] geoTransform;

        public GeoTransform(double[] tiepoint, double[] pixelScale)
        {
            // Standard GDAL-style geotransform
            geoTransform = new double[6];

            // Origin (top-left corner)
            geoTransform[0] = tiepoint[3];  // X origin
            geoTransform[3] = tiepoint[4];  // Y origin

            // Pixel size and rotation
            geoTransform[1] = pixelScale[0];  // X pixel size
            geoTransform[5] = -pixelScale[1]; // Y pixel size (negative for top-down images)

            // No rotation in this simple transform
            geoTransform[2] = 0;
            geoTransform[4] = 0;
        }

        public PointLatLng PixelToWorld(Point pixelCoord)
        {
            double worldX = geoTransform[0] + (pixelCoord.X * geoTransform[1]) + (pixelCoord.Y * geoTransform[2]);
            double worldY = geoTransform[3] + (pixelCoord.X * geoTransform[4]) + (pixelCoord.Y * geoTransform[5]);

            return new PointLatLng(worldY, worldX);
        }

        public Point WorldToPixel(PointLatLng worldCoord)
        {
            // Invert the geotransform
            double det = geoTransform[1] * geoTransform[5] - geoTransform[2] * geoTransform[4];
            if (Math.Abs(det) < 1e-10)
            {
                throw new InvalidOperationException("Invalid geotransform");
            }

            double invDet = 1.0 / det;

            double deltaX = worldCoord.Lng - geoTransform[0];
            double deltaY = worldCoord.Lat - geoTransform[3];

            int pixelX = (int)(
                ((deltaX * geoTransform[5] - deltaY * geoTransform[2]) * invDet)
            );

            int pixelY = (int)(
                ((deltaY * geoTransform[1] - deltaX * geoTransform[4]) * invDet)
            );

            return new Point(pixelX, pixelY);
        }
    }
}
