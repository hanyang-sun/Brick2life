// Adapted from tompazourek/Colourful, commit 9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c.
// Copyright (c) 2023 Tomáš Pažourek. MIT; see LICENSE.txt in this directory.
// The sRGB companding and XYZ-to-Lab subset is specialized to D65 sRGB.
// RGB brightness normalization and classification distance are project adaptations.
using System;
using BrickDefense.Core;

namespace BrickDefense.ThirdParty
{
    internal readonly struct NormalizedLab
    {
        private readonly double l,a,b;
        public readonly double Brightness;

        private NormalizedLab(double l,double a,double b,double brightness)
        { this.l=l; this.a=a; this.b=b; Brightness=brightness; }

        public static NormalizedLab From(Rgb24 rgb)
        {
            double max = Math.Max(rgb.R,Math.Max(rgb.G,rgb.B));
            if (max == 0) return new NormalizedLab(0,0,0,0);
            // Normalize before companding: scalar changes in captured sRGB brightness
            // keep the same chromatic descriptor. This is not a physical light estimator.
            double r = ToLinear(rgb.R/max),g = ToLinear(rgb.G/max),b = ToLinear(rgb.B/max);
            double x = LabCurve((0.4124564*r+0.3575761*g+0.1804375*b)/0.95047);
            double y = LabCurve(0.2126729*r+0.7151522*g+0.0721750*b);
            double z = LabCurve((0.0193339*r+0.1191920*g+0.9503041*b)/1.08883);
            return new NormalizedLab(116*y-16,500*(x-y),200*(y-z),max/255);
        }

        public static double Distance(NormalizedLab first,NormalizedLab second)
        {
            double lightness = (first.l-second.l)/100*0.15;
            double redGreen = (first.a-second.a)/128,blueYellow = (first.b-second.b)/128;
            return Math.Sqrt(lightness*lightness+redGreen*redGreen+blueYellow*blueYellow);
        }

        private static double ToLinear(double value) => value <= 0.04045 ? value/12.92 :
            Math.Pow((value+0.055)/1.055,2.4);
        private static double LabCurve(double value) => value > 216.0/24389 ? Math.Pow(value,1.0/3) :
            (24389.0/27*value+16)/116;
    }
}
