using F5.Util;
using System;
using System.Collections.Generic;
using System.IO;

namespace F5.James
{
    internal sealed class Huffman
    {
        public List<int[]> Bits, Val;
        /// <summary>
        /// JpegNaturalOrder[i] is the natural-order position of the i'th element of zigzag order.
        /// </summary>
        public static readonly int[] JpegNaturalOrder = 
        {
            0, 1, 8, 16, 9, 2, 3, 10, 
            17, 24, 32, 25, 18, 11, 4, 5, 
            12, 19, 26, 33, 40, 48, 41, 34, 
            27, 20, 13, 6, 7, 14, 21, 28, 
            35, 42, 49, 56, 57, 50, 43, 36, 
            29, 22, 15, 23, 30, 37, 44, 51, 
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63
        };

        private int BufferPutBits, BufferPutBuffer;
        private int[][] DC_Matrix0 = ArrayHelper.CreateJagged<int>(12, 2);
        private int[][] DC_Matrix1 = ArrayHelper.CreateJagged<int>(12, 2);
        private int[][] AC_Matrix0 = ArrayHelper.CreateJagged<int>(255, 2);
        private int[][] AC_Matrix1 = ArrayHelper.CreateJagged<int>(255, 2);
        private int[][][] DC_Matrix, AC_Matrix;

        private int[] BitsDCluminance = { 
                                            0x00, 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 
                                        };

        private int[] ValDCluminance = { 
                                           0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 
                                       };

        private int[] BitsDCchrominance = { 
                                             0x01, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 
                                         };

        private int[] ValDCchrominance = { 
                                            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 
                                        };

        private int[] BitsACluminance = { 
                                           0x10, 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d 
                                       };

        private int[] BitsACchrominance = {
                                             0x11, 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 
                                         };

        private int[] ValACluminance = 
        {
            0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07, 0x22, 0x71,
            0x14, 0x32, 0x81, 0x91, 0xa1, 0x08, 0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0, 0x24, 0x33, 0x62, 0x72,
            0x82, 0x09, 0x0a, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x34, 0x35, 0x36, 0x37,
            0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
            0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x83,
            0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3,
            0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
            0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
            0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa 
        };

        private int[] ValACchrominance = 
        {
            0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71, 0x13, 0x22,
            0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0, 0x15, 0x62, 0x72, 0xd1,
            0x0a, 0x16, 0x24, 0x34, 0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x35, 0x36,
            0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
            0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a,
            0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a,
            0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba,
            0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
            0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa 
        };

        /// <summary>
        /// The Huffman class constructor
        /// </summary>
        internal Huffman()
        {
            this.Bits = new List<int[]>(4)
            {
                this.BitsDCluminance,
                this.BitsACluminance,
                this.BitsDCchrominance,
                this.BitsACchrominance
            };

            this.Val = new List<int[]>(4)
            {
                this.ValDCluminance,
                this.ValACluminance,
                this.ValDCchrominance,
                this.ValACchrominance
            };

            InitHuffman();
        }

        private void CalcMatrix(int[] bits, int[] val, int[][] matrix)
        {
            int i, j, v;
            int p = 0, code = 0;
            for (j = 1; j < bits.Length; j++)
            {
                for (i = 0; i < bits[j]; i++)
                {
                    v = val[p];
                    matrix[v][0] = code++;
                    matrix[v][1] = j;
                    p++;
                }
                code <<= 1;
            }
        }

        /// <summary>
        /// Initialisation of the Huffman codes for Luminance and Chrominance. This 
        /// code results in the same tables created in the IJG Jpeg-6a library.
        /// </summary>
        private void InitHuffman()
        {
            this.DC_Matrix = new int[][][] { this.DC_Matrix0, this.DC_Matrix1 };
            this.AC_Matrix = new int[][][] { this.AC_Matrix0, this.AC_Matrix1 };

            // init of the DC values for the luminance [][0] is the code [][1] 
            // is the number of bit
            CalcMatrix(this.BitsDCluminance, this.ValDCluminance, this.DC_Matrix0);

            // Init of the AC hufmann code for luminance matrix [][][0] 
            // is the code & matrix[][][1] is the number of bit
            CalcMatrix(this.BitsACluminance, this.ValACluminance, this.AC_Matrix0);

            // init of the DC values for the chrominance [][0] is the code [][1] 
            // is the number of bit
            CalcMatrix(this.BitsDCchrominance, this.ValDCchrominance, this.DC_Matrix1);

            // Init of the AC hufmann code for the chrominance matrix [][][0] 
            // is the code & matrix[][][1] is the number of bit needed
            CalcMatrix(this.BitsACchrominance, this.ValACchrominance, this.AC_Matrix1);
        }

        private void WriteByte(Stream streamOut, int b)
        {
            streamOut.WriteByte((byte)b);
        }

        private void BufferIt(Stream streamOut, int code, int size)
        {
            int PutBuffer = code;
            int PutBits = this.BufferPutBits;

            PutBuffer &= (1 << size) - 1;
            PutBits += size;
            PutBuffer <<= 24 - PutBits;
            PutBuffer |= this.BufferPutBuffer;

            while (PutBits >= 8)
            {
                int c = PutBuffer >> 16 & 0xFF;
                WriteByte(streamOut, c);
                if (c == 0xFF)
                    WriteByte(streamOut, 0);
                PutBuffer <<= 8;
                PutBits -= 8;
            }
            this.BufferPutBuffer = PutBuffer;
            this.BufferPutBits = PutBits;
        }


        /// <summary>
        /// Uses an integer long (32 bits) buffer to store the Huffman encoded bits 
        /// and sends them to outStream by the byte.
        /// </summary>
        public void FlushBuffer(BufferedStream outStream)
        {
            int PutBuffer = this.BufferPutBuffer;
            int PutBits = this.BufferPutBits;
            while (PutBits >= 8)
            {
                int c = PutBuffer >> 16 & 0xFF;
                WriteByte(outStream, c);
                if (c == 0xFF)
                    WriteByte(outStream, 0);
                PutBuffer <<= 8;
                PutBits -= 8;
            }
            if (PutBits > 0)
            {
                int c = PutBuffer >> 16 & 0xFF;
                WriteByte(outStream, c);
            }
        }

        /// <summary>
        /// HuffmanBlockEncoder run length encodes and Huffman encodes the quantized data.
        /// </summary>
        public void HuffmanBlockEncoder(Stream streamOut, int[] zigzag, int prec, int DCcode, int ACcode)
        {
            int temp, temp2, nbits, k, r, i;

            // The DC portion
            temp = temp2 = zigzag[0] - prec;
            if (temp < 0)
            {
                temp = -temp;
                temp2--;
            }
            nbits = 0;
            while (temp != 0)
            {
                nbits++;
                temp >>= 1;
            }
            // if (nbits > 11) nbits = 11;
            BufferIt(streamOut, this.DC_Matrix[DCcode][nbits][0], this.DC_Matrix[DCcode][nbits][1]);
            // The arguments in bufferIt are code and size.
            if (nbits != 0)
            {
                BufferIt(streamOut, temp2, nbits);
            }

            // The AC portion
            r = 0;
            for (k = 1; k < 64; k++)
            {
                if ((temp = zigzag[JpegNaturalOrder[k]]) == 0)
                {
                    r++;
                }
                else
                {
                    while (r > 15)
                    {
                        BufferIt(streamOut, this.AC_Matrix[ACcode][0xF0][0], this.AC_Matrix[ACcode][0xF0][1]);
                        r -= 16;
                    }
                    temp2 = temp;
                    if (temp < 0)
                    {
                        temp = -temp;
                        temp2--;
                    }
                    nbits = 1;
                    while ((temp >>= 1) != 0)
                    {
                        nbits++;
                    }
                    i = (r << 4) + nbits;
                    BufferIt(streamOut, this.AC_Matrix[ACcode][i][0], this.AC_Matrix[ACcode][i][1]);
                    BufferIt(streamOut, temp2, nbits);

                    r = 0;
                }
            }

            if (r > 0)
            {
                BufferIt(streamOut, this.AC_Matrix[ACcode][0][0], this.AC_Matrix[ACcode][0][1]);
            }
        }
    }
}