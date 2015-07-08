using log4net;
using System;
using System.IO;

namespace F5.Ortega
{
    using F5.Util;

    internal sealed class HuffmanDecode : IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(HuffmanDecode));

        private static readonly byte[] APP = new byte[] 
        {
            0xE0, 0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xEB, 0xEC, 0xED, 0xEE, 0xEF
        };

        const int DRI = 0xDD;
        const int DNL = 0xDC;
        const int EOI = 0xD9;

        // Instance variables
        // Declare header variables
        int Lf, P, X, Y, Nf; // SOF0 parameters
        int[] C, H, V, T; // SOF0 parameters
        int Ls, Ns, Ss, Se, Ah, Al; // SOS parameters
        int Lh, Tc, Th; // DHT parameters
        int Lq, Pq, Tq; // DQT parameters

        // other variables
        int B, CNT, Diff, Pred;
        int size;
        int K, Ssss, RS, R, J;
        int lp, cnt, hftbl;
        int[, ,] Cr, Cb;
        int[][] HuffVal = new int[4][];
        int[][] ValPtr = new int[4][];
        int[][] MinCode = new int[4][];
        int[][] MaxCode = new int[4][];
        int[] ZZ = new int[64];
        int[,] QNT = new int[4, 64];
        int RI;

        static readonly byte[,] deZZ = 
        {
            { 0, 0 }, { 0, 1 }, { 1, 0 }, { 2, 0 }, 
            { 1, 1 }, { 0, 2 }, { 0, 3 }, { 1, 2 }, 
            { 2, 1 }, { 3, 0 }, { 4, 0 }, { 3, 1 }, 
            { 2, 2 }, { 1, 3 }, { 0, 4 }, { 0, 5 }, 
            { 1, 4 }, { 2, 3 }, { 3, 2 }, { 4, 1 }, 
            { 5, 0 }, { 6, 0 }, { 5, 1 }, { 4, 2 }, 
            { 3, 3 }, { 2, 4 }, { 1, 5 }, { 0, 6 }, 
            { 0, 7 }, { 1, 6 }, { 2, 5 }, { 3, 4 }, 
            { 4, 3 }, { 5, 2 }, { 6, 1 }, { 7, 0 }, 
            { 7, 1 }, { 6, 2 }, { 5, 3 }, { 4, 4 }, 
            { 3, 5 }, { 2, 6 }, { 1, 7 }, { 2, 7 }, 
            { 3, 6 }, { 4, 5 }, { 5, 4 }, { 6, 3 }, 
            { 7, 2 }, { 7, 3 }, { 6, 4 }, { 5, 5 }, 
            { 4, 6 }, { 3, 7 }, { 4, 7 }, { 5, 6 }, 
            { 6, 5 }, { 7, 4 }, { 7, 5 }, { 6, 6 }, 
            { 5, 7 }, { 6, 7 }, { 7, 6 }, { 7, 7 } 
        };

        // added for decode()
        internal static readonly byte[] deZigZag = 
        {
            0, 1, 5, 6, 14, 15, 27, 28, 
            2, 4, 7, 13, 16, 26, 29, 42, 
            3, 8, 12, 17, 25, 30, 41, 43, 
            9, 11, 18, 24, 31, 40, 44, 53, 
            10, 19, 23, 32, 39, 45, 52, 54, 
            20, 22, 33, 38, 46, 51, 55, 60, 
            21, 34, 37, 47, 50, 56, 59, 61,
            35, 36, 48, 49, 57, 58, 62, 63 
        };

        EmbedData dis;

        // Constructor Method
        public HuffmanDecode(Stream data)
        {
            this.size = (int)data.Length;
            this.dis = new EmbedData(data);
            // Parse out markers and header info
            bool cont = true;
            byte b;

            while (cont)
            {
                b = this.dis.Read();
                if (b == 255)
                {
                    b = this.dis.Read();
                    switch (b)
                    {
                        case 192:
                            SetSOF0();
                            break;
                        case 196:
                            SetDHT();
                            break;
                        case 219:
                            SetDqt();
                            break;
                        case 217:
                        case 218:
                            cont = false;
                            break;
                        case DRI:
                            SetDri();
                            break;
                        default:
                            if (Array.IndexOf(APP, b) > -1)
                            {
                                this.dis.Seek(this.dis.ReadInt() - 2, SeekOrigin.Current);
                            }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Return image data
        /// </summary>
        public int[] Decode()
        {
            int tmp;
            int[] Cs, Ta, Td; // SOS parameters
            int[] PRED = new int[this.Nf];
            for (int nComponent = 0; nComponent < this.Nf; nComponent++)
            {
                PRED[nComponent] = 0;
            }
            this.CNT = 0;
            // Read in Scan Header information
            this.Ls = this.dis.ReadInt();
            this.Ns = this.dis.Read();

            Cs = new int[this.Ns];
            Td = new int[this.Ns];
            Ta = new int[this.Ns];

            // get table information
            for (this.lp = 0; this.lp < this.Ns; this.lp++)
            {
                Cs[this.lp] = this.dis.Read();
                Td[this.lp] = this.dis.Read();
                Ta[this.lp] = Td[this.lp] & 0x0f;
                Td[this.lp] >>= 4;
            }

            this.Ss = this.dis.Read();
            this.Se = this.dis.Read();
            this.Ah = this.dis.Read();
            this.Al = this.Ah & 0x0f;
            this.Ah >>= 4;

            // Calculate the Number of blocks encoded
            // warum doppelt so viel?
            int[] buff = new int[2 * 8 * 8 * GetBlockCount()];
            int pos = 0;
            int MCUCount = 0;

            bool bDoIt = true;
            while (bDoIt)
            {
                // Get component 1 of MCU
                for (int nComponent = 0; nComponent < this.Nf; nComponent++)
                {
                    for (this.cnt = 0; this.cnt < this.H[nComponent] * this.V[nComponent]; this.cnt++)
                    {
                        // Get DC coefficient
                        this.hftbl = Td[nComponent] * 2;
                        tmp = DECODE();
                        this.Diff = Receive(tmp);
                        this.ZZ[0] = PRED[0] + Extend(this.Diff, tmp);
                        PRED[nComponent] = this.ZZ[0];

                        // Get AC coefficients
                        this.hftbl = Ta[nComponent] * 2 + 1;
                        Decode_AC_Coefficients();

                        for (this.lp = 0; this.lp < 64; this.lp++)
                        {
                            // Zickzack???
                            // buff[pos++]=ZZ[deZigZag[lp]];
                            buff[pos++] = this.ZZ[this.lp];
                        }
                    }
                }

                MCUCount++;
                if (MCUCount == this.RI)
                {
                    MCUCount = 0;
                    this.CNT = 0;
                    for (int nComponent = 0; nComponent < this.Nf; nComponent++)
                    {
                        PRED[nComponent] = 0;
                    }
                    this.dis.Read();
                    int tmpB = this.dis.Read();
                    if (tmpB == EOI)
                    {
                        break;
                    }
                }

                if (this.dis.Available <= 2)
                {
                    if (this.dis.Available == 2)
                    {
                        this.dis.Read();
                        if (this.dis.Read() != EOI)
                        {
                            logger.Warn("file does not end with EOI");
                        }
                    }
                    else
                    {
                        logger.Warn("file does not end with EOI");
                    }
                    break;
                }
            }
            int[] tmpBuff = new int[pos];
            Array.Copy(buff, 0, tmpBuff, 0, pos);
            return tmpBuff;
        }

        private int DECODE()
        {
            int value;
            int cd = NextBit();
            int i = 1;
            while (true)
            {
                if (cd > this.MaxCode[this.hftbl][i])
                {
                    cd = (cd << 1) + NextBit();
                    i++;
                }
                else
                {
                    break;
                }
            }
            this.J = this.ValPtr[this.hftbl][i];
            this.J = this.J + cd - this.MinCode[this.hftbl][i];
            value = this.HuffVal[this.hftbl][this.J];
            return value;
        }

        private void Decode_AC_Coefficients()
        {
            this.K = 1;

            // Zero out array ZZ[]
            for (this.lp = 0; this.lp < 64; this.lp++)
            {
                this.ZZ[this.lp] = 0;
            }

            while (true)
            {
                // System.out.println(hftbl);
                this.RS = DECODE();
                this.Ssss = this.RS % 16;
                this.R = this.RS >> 4;
                if (this.Ssss == 0)
                {
                    if (this.R == 15)
                    {
                        this.K += 16;
                    }
                    else
                        return;
                }
                else
                {
                    this.K = this.K + this.R;
                    Decode_ZZ(this.K);
                    if (this.K == 63)
                        return;
                    else
                    {
                        this.K++;
                    }
                }
            }
        }

        private void Decode_ZZ(int k)
        {
            // Decoding a nonzero AC coefficient
            this.ZZ[k] = Receive(this.Ssss);
            this.ZZ[k] = Extend(this.ZZ[k], this.Ssss);
        }

        private void FillDHT(int index)
        {
            HuffTable ht = new HuffTable(this.dis, this.Lh);
            this.Lh -= ht.Len;
            this.HuffVal[index] = ht.HuffVal;
            this.ValPtr[index] = ht.ValPtr;
            this.MaxCode[index] = ht.MaxCode;
            this.MinCode[index] = ht.MinCode;
        }

        private void SetDHT()
        {
            // Read in Huffman tables
            // Lh length
            // Th index
            // Tc AC?
            this.Lh = this.dis.ReadInt();
            while (this.Lh > 0)
            {
                this.Tc = this.dis.Read();
                this.Th = this.Tc & 0x0f;
                this.Tc >>= 4;
                if (this.Th == 0)
                {
                    if (this.Tc == 0)
                        FillDHT(0);
                    else
                        FillDHT(1);
                }
                else
                {
                    if (this.Tc == 0)
                        FillDHT(2);
                    else
                        FillDHT(3);
                }
            }
        }

        private void SetDqt()
        {
            // Read in quatization tables
            this.Lq = this.dis.ReadInt();
            this.Pq = this.dis.Read();
            this.Tq = this.Pq & 0x0f;
            this.Pq >>= 4;

            switch (this.Tq)
            {
                case 0:
                    for (this.lp = 0; this.lp < 64; this.lp++)
                    {
                        this.QNT[0, this.lp] = this.dis.Read();
                    }
                    break;

                case 1:
                    for (this.lp = 0; this.lp < 64; this.lp++)
                    {
                        this.QNT[1, this.lp] = this.dis.Read();
                    }
                    break;

                case 2:
                    for (this.lp = 0; this.lp < 64; this.lp++)
                    {
                        this.QNT[2, this.lp] = this.dis.Read();
                    }
                    break;

                case 3:
                    for (this.lp = 0; this.lp < 64; this.lp++)
                    {
                        this.QNT[3, this.lp] = this.dis.Read();
                    }
                    break;
            }
        }

        private void SetDri()
        {
            this.dis.ReadInt();
            this.RI = this.dis.ReadInt();
        }

        private static int Extend(int v, int t)
        {
            int Vt = 0x01 << t - 1;
            if (v < Vt)
            {
                Vt = (-1 << t) + 1;
                v += Vt;
            }
            return v;
        }

        // Calculate the Number of blocks encoded
        public int GetBlockCount()
        {
            switch (this.Nf)
            {
                case 1:
                    return (this.X + 7) / 8 * ((this.Y + 7) / 8);
                case 3:
                    return 6 * ((this.X + 15) / 16) * ((this.Y + 15) / 16);
                default:
                    logger.Warn("Nf weder 1 noch 3");
                    break;
            }
            return 0;
        }

        public int GetComp()
        {
            return this.Nf;
        }

        public int GetPrec()
        {
            return this.P;
        }

        // Public get methods
        public int GetX()
        {
            return this.X;
        }

        public int GetY()
        {
            return this.Y;
        }

        /// <summary>
        /// Return image data
        /// </summary>
        public void HuffDecode(int[, ,] buffer)
        {
            int x, y, tmp;
            int sz = this.X * this.Y;
            int[,] Block = new int[8, 8];
            int Cs, Ta, Td, blocks;

            // Read in Scan Header information
            this.Ls = this.dis.ReadInt();
            this.Ns = this.dis.Read();
            Cs = this.dis.Read();
            Td = this.dis.Read();
            Ta = Td & 0x0f;
            Td >>= 4;

            this.Ss = this.dis.Read();
            this.Se = this.dis.Read();
            this.Ah = this.dis.Read();
            this.Al = this.Ah & 0x0f;
            this.Ah >>= 4;

            // Calculate the Number of blocks encoded
            // blocks = X * Y / 64;
            blocks = GetBlockCount() / 6;

            // decode image data and return image data in array
            for (this.cnt = 0; this.cnt < blocks; this.cnt++)
            {
                // Get DC coefficient
                if (Td == 0)
                {
                    this.hftbl = 0;
                }
                else
                {
                    this.hftbl = 2;
                }
                tmp = DECODE();
                this.Diff = Receive(tmp);
                this.ZZ[0] = this.Pred + Extend(this.Diff, tmp);
                this.Pred = this.ZZ[0];

                // Get AC coefficients
                if (Ta == 0)
                {
                    this.hftbl = 1;
                }
                else
                {
                    this.hftbl = 3;
                }
                Decode_AC_Coefficients();

                // dezigzag and dequantize block
                for (this.lp = 0; this.lp < 64; this.lp++)
                {
                    Block[deZZ[this.lp, 0], deZZ[this.lp, 1]] = this.ZZ[this.lp] * this.QNT[0, this.lp];
                }

                // store blocks in buffer
                for (x = 0; x < 8; x++)
                {
                    for (y = 0; y < 8; y++)
                    {
                        buffer[this.cnt, x, y] = Block[x, y];
                    }
                }
            }
            this.dis.Close();
        }

        /// <summary>
        /// Get one bit from entropy coded data stream
        /// </summary>
        private int NextBit()
        {
            int b2;
            int bit;

            if (this.CNT == 0)
            {
                this.CNT = 8;
                this.B = this.dis.Read();
                if (255 == this.B)
                {
                    b2 = this.dis.Read();
                }
            }
            bit = this.B & 0X80; // get MSBit of B
            bit >>= 7; // move MSB to LSB
            this.CNT--; // Decrement counter
            this.B <<= 1; // Shift left one bit
            return bit;
        }

        /// <summary>
        /// Return quantized coefficients
        /// </summary>
        public void RawDecode(int[, ,] buffer)
        {
            int x, y, tmp;
            int[,] block = new int[8, 8];
            int Cs, Ta, Td, blocks;

            // Read in Scan Header information
            this.Ls = this.dis.ReadInt();
            this.Ns = this.dis.Read();
            Cs = this.dis.Read();
            Td = this.dis.Read();
            Ta = Td & 0x0f;
            Td >>= 4;

            this.Ss = this.dis.Read();
            this.Se = this.dis.Read();
            this.Ah = this.dis.Read();
            this.Al = this.Ah & 0x0f;
            this.Ah >>= 4;

            // Calculate the Number of blocks encoded
            blocks = GetBlockCount() / 6;

            // decode image data and return image data in array
            for (this.cnt = 0; this.cnt < blocks; this.cnt++)
            {
                // Get DC coefficient
                if (Td == 0)
                {
                    this.hftbl = 0;
                }
                else
                {
                    this.hftbl = 2;
                }
                tmp = DECODE();
                this.Diff = Receive(tmp);
                this.ZZ[0] = this.Pred + Extend(this.Diff, tmp);
                this.Pred = this.ZZ[0];

                // Get AC coefficients
                if (Ta == 0)
                {
                    this.hftbl = 1;
                }
                else
                {
                    this.hftbl = 3;
                }
                Decode_AC_Coefficients();

                // dezigzag
                for (this.lp = 0; this.lp < 64; this.lp++)
                {
                    block[deZZ[this.lp, 0], deZZ[this.lp, 1]] = this.ZZ[this.lp];
                }

                // store blocks in buffer
                logger.Info(this.cnt + " ");
                for (x = 0; x < 8; x++)
                {
                    for (y = 0; y < 8; y++)
                    {
                        buffer[this.cnt, x, y] = block[x, y];
                    }
                }
            }
            this.dis.Close();
        }

        private int Receive(int sss)
        {
            int v = 0, i = 0;
            while (true)
            {
                if (i == sss)
                    return v;
                i++;
                v = (v << 1) + NextBit();
            }
        }

        // Return image data for RGB images
        public void RGB_Decode(int[, ,] Lum)
        {
            int x, y, a, b, line, tmp;
            int sz = this.X * this.Y;
            int blocks;
            int[,] Block = new int[8, 8];
            int[] Cs, Ta, Td;
            int[] PRED = { 0, 0, 0 };

            // Read in Scan Header information
            this.Ls = this.dis.ReadInt();
            this.Ns = this.dis.Read();
            Cs = new int[this.Ns];
            Td = new int[this.Ns];
            Ta = new int[this.Ns];

            // get table information
            for (this.lp = 0; this.lp < this.Ns; this.lp++)
            {
                Cs[this.lp] = this.dis.Read();
                Td[this.lp] = this.dis.Read();
                Ta[this.lp] = Td[this.lp] & 0x0f;
                Td[this.lp] >>= 4;
            }

            this.Ss = this.dis.Read();
            this.Se = this.dis.Read();
            this.Ah = this.dis.Read();
            this.Al = this.Ah & 0x0f;
            this.Ah >>= 4;

            // Calculate the Number of blocks encoded
            // blocks = X * Y / 64;
            blocks = GetBlockCount() / 6;

            // decode image data and return image data in array
            for (a = 0; a < 32; a++)
            {
                for (b = 0; b < 32; b++)
                {
                    // Get component 1 of MCU
                    for (this.cnt = 0; this.cnt < 4; this.cnt++)
                    {
                        // Get DC coefficient
                        this.hftbl = 0;
                        tmp = DECODE();
                        this.Diff = Receive(tmp);
                        this.ZZ[0] = PRED[0] + Extend(this.Diff, tmp);
                        PRED[0] = this.ZZ[0];

                        // Get AC coefficients
                        this.hftbl = 1;
                        Decode_AC_Coefficients();

                        // dezigzag and dequantize block
                        for (this.lp = 0; this.lp < 64; this.lp++)
                        {
                            Block[deZZ[this.lp, 0], deZZ[this.lp, 1]] = this.ZZ[this.lp] * this.QNT[0, this.lp];
                        }

                        if (this.cnt < 2)
                        {
                            line = 0;
                        }
                        else
                        {
                            line = 62;
                        }

                        // store blocks in buffer
                        for (x = 0; x < 8; x++)
                        {
                            for (y = 0; y < 8; y++)
                            {
                                Lum[b * 2 + this.cnt + line + a * 128, x, y] = Block[x, y];
                            }
                        }
                    }

                    // getComponent 2 and 3 of image
                    for (this.cnt = 0; this.cnt < 2; this.cnt++)
                    {
                        // Get DC coefficient
                        this.hftbl = 2;
                        tmp = DECODE();
                        this.Diff = Receive(tmp);
                        this.ZZ[0] = PRED[this.cnt + 1] + Extend(this.Diff, tmp);
                        PRED[this.cnt + 1] = this.ZZ[0];

                        // Get AC coefficients
                        this.hftbl = 3;
                        Decode_AC_Coefficients();

                        // dezigzag and dequantize block
                        for (this.lp = 0; this.lp < 64; this.lp++)
                        {
                            Block[deZZ[this.lp, 0], deZZ[this.lp, 1]] = this.ZZ[this.lp] * this.QNT[1, this.lp];
                        }

                        // store blocks in buffer
                        if (this.cnt == 0)
                        {
                            for (x = 0; x < 8; x++)
                            {
                                for (y = 0; y < 8; y++)
                                {
                                    this.Cb[a * 32 + b, x, y] = Block[x, y];
                                }
                            }
                        }
                        else
                        {
                            for (x = 0; x < 8; x++)
                            {
                                for (y = 0; y < 8; y++)
                                {
                                    this.Cr[a * 32 + b, x, y] = Block[x, y];
                                }
                            }
                        }
                    }
                }
            }
            this.dis.Close();
        }

        public void SetCb(int[, ,] chrome)
        {
            this.Cb = chrome;
        }

        public void SetCr(int[, ,] chrome)
        {
            this.Cr = chrome;
        }

        private void SetSOF0()
        {
            // Read in start of frame header data
            this.Lf = this.dis.ReadInt();
            this.P = this.dis.Read();
            this.Y = this.dis.ReadInt();
            this.X = this.dis.ReadInt();
            this.Nf = this.dis.Read();

            this.C = new int[this.Nf];
            this.H = new int[this.Nf];
            this.V = new int[this.Nf];
            this.T = new int[this.Nf];

            // Read in quatization table identifiers
            for (this.lp = 0; this.lp < this.Nf; this.lp++)
            {
                this.C[this.lp] = this.dis.Read();
                this.H[this.lp] = this.dis.Read();
                this.V[this.lp] = this.H[this.lp] & 0x0f;
                this.H[this.lp] >>= 4;
                this.T[this.lp] = this.dis.Read();
            }
        }

        #region IDisposable
        bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~HuffmanDecode()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
                this.dis.Dispose();
            _disposed = true;
        }
        #endregion
    }
}
