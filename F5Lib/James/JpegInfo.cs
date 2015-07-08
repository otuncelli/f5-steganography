using F5.Util;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace F5.James
{
    internal sealed class JpegInfo
    {
        // the following are set as the default
        public const byte Precision = 8;
        public const byte NumberOfComponents = 3;
        public const byte Ss = 0;
        public const byte Se = 63;
        public const byte Ah = 0;
        public const byte Al = 0;

        internal float[][][] Components;
        int[] compWidth, compHeight;
        Bitmap bmp;
        public String Comment;
        public int ImageHeight, ImageWidth;
        public int[] BlockHeight, BlockWidth;

        // public int[] HsampFactor = {1, 1, 1};
        // public int[] VsampFactor = {1, 1, 1};
        public readonly int[] HsampFactor = { 2, 1, 1 };
        public readonly int[] VsampFactor = { 2, 1, 1 };
        public readonly int[] QtableNumber = { 0, 1, 1 };
        public readonly int[] DCtableNumber = { 0, 1, 1 };
        public readonly int[] ACtableNumber = { 0, 1, 1 };

        public JpegInfo(Image image, String comment)
        {
            this.Components = new float[NumberOfComponents][][];
            this.compWidth = new int[NumberOfComponents];
            this.compHeight = new int[NumberOfComponents];
            this.BlockWidth = new int[NumberOfComponents];
            this.BlockHeight = new int[NumberOfComponents];
            this.bmp = (Bitmap)image;
            this.ImageWidth = image.Width;
            this.ImageHeight = image.Height;
            this.Comment = comment ?? "JPEG Encoder Copyright 1998, James R. Weeks and BioElectroMech.  ";
            InitYCC();
        }

        public JpegInfo(Image image)
            : this(image, String.Empty)
        {
        }

        /// <summary>
        /// This method creates and fills three arrays, Y, Cb, and Cr using the input image.
        /// </summary>
        private void InitYCC()
        {
            int MaxHsampFactor, MaxVsampFactor;
            int i, x, y, width, height, stride;
            int size, pixelSize, offset, yPos;
            byte r, g, b;
            byte[] pixelData;

            MaxHsampFactor = MaxVsampFactor = 1;
            for (i = 0; i < NumberOfComponents; i++)
            {
                MaxHsampFactor = Math.Max(MaxHsampFactor, HsampFactor[i]);
                MaxVsampFactor = Math.Max(MaxVsampFactor, VsampFactor[i]);
            }

            for (i = 0; i < NumberOfComponents; i++)
            {
                this.compWidth[i] = this.ImageWidth % 8 != 0 ? (int)Math.Ceiling(this.ImageWidth / 8.0) * 8 : this.ImageWidth;
                this.compWidth[i] = this.compWidth[i] / MaxHsampFactor * HsampFactor[i];
                this.BlockWidth[i] = (int)Math.Ceiling(this.compWidth[i] / 8.0);

                this.compHeight[i] = this.ImageHeight % 8 != 0 ? (int)Math.Ceiling(this.ImageHeight / 8.0) * 8 : this.ImageHeight;
                this.compHeight[i] = this.compHeight[i] / MaxVsampFactor * VsampFactor[i];
                this.BlockHeight[i] = (int)Math.Ceiling(this.compHeight[i] / 8.0);
            }

            float[][] Y = ArrayHelper.CreateJagged<float>(this.compHeight[0], this.compWidth[0]);
            float[][] Cr1 = ArrayHelper.CreateJagged<float>(this.compHeight[0], this.compWidth[0]);
            float[][] Cb1 = ArrayHelper.CreateJagged<float>(this.compHeight[0], this.compWidth[0]);
            float[][] Cb2 = ArrayHelper.CreateJagged<float>(this.compHeight[1], this.compWidth[1]);
            float[][] Cr2 = ArrayHelper.CreateJagged<float>(this.compHeight[2], this.compWidth[2]);

            using (this.bmp)
            {
                width = bmp.Width;
                height = bmp.Height;
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                stride = bmpData.Stride;
                size = stride * height;
                pixelSize = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
                pixelData = new byte[size];
                Marshal.Copy(bmpData.Scan0, pixelData, 0, size);
                bmp.UnlockBits(bmpData);
            }

            // In order to minimize the chance that grabPixels will throw an
            // exception it may be necessary to grab some pixels every few scanlines and
            // process those before going for more. The time expense may be prohibitive.
            // However, for a situation where memory overhead is a concern, this may
            // be the only choice.
            for (y = 0; y < height; y++)
            {
                yPos = stride * y;
                for (x = 0; x < width; x++)
                {
                    offset = yPos + x * pixelSize;
                    b = pixelData[offset];
                    g = pixelData[offset + 1];
                    r = pixelData[offset + 2];

                    Y[y][x] = (float)(0.299 * r + 0.587 * g + 0.114 * b);
                    Cb1[y][x] = 128 + (float)(-0.16874 * r - 0.33126 * g + 0.5 * b);
                    Cr1[y][x] = 128 + (float)(0.5 * r - 0.41869 * g - 0.08131 * b);
                }
            }

            // Need a way to set the H and V sample factors before allowing
            // downsampling.
            // For now (04/04/98) downsampling must be hard coded.
            // Until a better downsampler is implemented, this will not be done.
            // Downsampling is currently supported. The downsampling method here
            // is a simple box filter.
            Cb2 = DownSample(Cb1, 1);
            Cr2 = DownSample(Cr1, 2);

            this.Components[0] = Y;
            this.Components[1] = Cb2;
            this.Components[2] = Cr2;
        }

        float[][] DownSample(float[][] C, int comp)
        {
            int inrow = 0, incol = 0, outrow, outcol, bias;
            float[][] output = ArrayHelper.CreateJagged<float>(this.compHeight[comp], this.compWidth[comp]);
            float temp;
            for (outrow = 0; outrow < this.compHeight[comp]; outrow++)
            {
                bias = 1;
                for (outcol = 0; outcol < this.compWidth[comp]; outcol++)
                {
                    temp = C[inrow][incol++]; // 00
                    temp += C[inrow++][incol--]; // 01
                    temp += C[inrow][incol++]; // 10
                    temp += C[inrow--][incol++] + bias; // 11 -> 02
                    output[outrow][outcol] = temp / (float)4.0;
                    bias ^= 3;
                }
                inrow += 2;
                incol = 0;
            }
            return output;
        }
    }
}