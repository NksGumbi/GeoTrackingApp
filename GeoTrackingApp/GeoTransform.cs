using GMap.NET;
using System;
using System.Drawing;

namespace GeoTrackingApp
{
    public class GeoTransform
    {
        private double[] coefficients;

        public GeoTransform(double[] modelTiepoint, double[] modelPixelScale)
        {
            coefficients = new double[6];
            coefficients[0] = modelTiepoint[3]; 
            coefficients[1] = modelPixelScale[0]; 
            coefficients[2] = 0; 
            coefficients[3] = modelTiepoint[4];
            coefficients[4] = 0;
            coefficients[5] = -modelPixelScale[1];
        }

        public PointLatLng PixelToWorld(Point pixel)
        {
            double x = coefficients[0] + pixel.X * coefficients[1] + pixel.Y * coefficients[2];
            double y = coefficients[3] + pixel.X * coefficients[4] + pixel.Y * coefficients[5];
            return new PointLatLng(y, x);
        }

        public Point WorldToPixel(PointLatLng world)
        {
            double deltaX = world.Lng - coefficients[0];
            double deltaY = world.Lat - coefficients[3];

            int x = (int)Math.Round(deltaX / coefficients[1]);
            int y = (int)Math.Round(deltaY / coefficients[5]);

            return new Point(x, y);
        }
    }
}
