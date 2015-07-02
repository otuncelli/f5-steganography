using log4net;
using System;
using System.IO;
using System.Text;

namespace F5
{
    using F5.Crypt;
    using F5.Ortega;

    public class JpegExtract : IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(JpegExtract));

        Stream output;
        F5Random random;
        int availableExtractedBits = 0;
        int extractedFileLength = 0;
        int nBytesExtracted = 0;
        int extractedByte = 0;
        int shuffledIndex = 0;
        int extractedBit, pos;

        public JpegExtract(Stream output, String password)
        {
            this.output = output;
            this.random = new F5Random(Encoding.ASCII.GetBytes(password));
        }

        public static byte[] Extract(Stream input, String password)
        {
            using (MemoryStream output = new MemoryStream())
            using (JpegExtract je = new JpegExtract(output, password))
            using (input)
            {
                je.Extract(input);
                output.Position = 0;
                byte[] result = new byte[output.Length];
                output.Read(result, 0, (int)output.Length);
                return result;
            }
        }

        public void Extract(Stream input)
        {
            int[] coeff;
            int i, n, k, hash, code;
            bool leaveContext = false;

            using (HuffmanDecode hd = new HuffmanDecode(input))
            {
                 coeff = hd.Decode();
            }

            logger.Info("Permutation starts");
            Permutation permutation = new Permutation(coeff.Length, random);
            logger.Info(coeff.Length + " indices shuffled");

            // extract length information
            CalcEmbeddedLength(permutation, coeff);
            k = (this.extractedFileLength >> 24) % 32;
            n = (1 << k) - 1;
            this.extractedFileLength &= 0x007fffff;

            logger.Info("Length of embedded file: " + extractedFileLength + " bytes");

            if (n > 0)
            {
                while (true)
                {
                    hash = 0;
                    code = 1;
                    while (code <= n)
                    {
                        this.pos++;
                        if (this.pos >= coeff.Length)
                        {
                            leaveContext = true;
                            break;
                        }
                        this.shuffledIndex = permutation.GetShuffled(this.pos);
                        this.extractedBit = ExtractBit(coeff);
                        if (this.extractedBit == -1)
                            continue;
                        else if (this.extractedBit == 1)
                            hash ^= code;
                        code++;
                    }

                    if (leaveContext)
                        break;

                    for (i = 0; i < k; i++)
                    {
                        this.extractedByte |= (hash >> i & 1) << this.availableExtractedBits++;
                        if (this.availableExtractedBits == 8)
                        {
                            WriteExtractedByte();
                            // check for pending end of embedded data
                            if (this.nBytesExtracted == this.extractedFileLength)
                            {
                                leaveContext = true;
                                break;
                            }
                        }
                    }

                    if (leaveContext)
                        break;
                }
            }
            else
            {
                while (++this.pos < coeff.Length && this.pos < permutation.Length)
                {
                    this.shuffledIndex = permutation.GetShuffled(this.pos);
                    this.extractedBit = ExtractBit(coeff);
                    if (this.extractedBit == -1)
                        continue;
                    this.extractedByte |= this.extractedBit << this.availableExtractedBits++;
                    if (this.availableExtractedBits == 8)
                    {
                        WriteExtractedByte();
                        if (this.nBytesExtracted == extractedFileLength)
                            break;
                    }
                }
            }
            if (this.nBytesExtracted < this.extractedFileLength)
            {
                logger.Warn("Incomplete file: only " + this.nBytesExtracted + 
                    " of " + this.extractedFileLength + " bytes extracted");
            }
        }

        /// <summary>
        /// extract length information
        /// </summary>
        private void CalcEmbeddedLength(Permutation permutation, int[] coeff)
        {
            this.extractedFileLength = 0;
            this.pos = -1;

            int i = 0;
            while (i < 32 && ++this.pos < coeff.Length)
            {
                this.shuffledIndex = permutation.GetShuffled(this.pos);
                this.extractedBit = ExtractBit(coeff);
                if (this.extractedBit == -1)
                    continue;
                this.extractedFileLength |= this.extractedBit << i++;
            }

            // remove pseudo random pad
            this.extractedFileLength ^= this.random.GetNextByte();
            this.extractedFileLength ^= this.random.GetNextByte() << 8;
            this.extractedFileLength ^= this.random.GetNextByte() << 16;
            this.extractedFileLength ^= this.random.GetNextByte() << 24;
        }
        private void WriteExtractedByte()
        {
            // remove pseudo random pad
            this.extractedByte ^= this.random.GetNextByte();
            this.output.WriteByte((byte)this.extractedByte);
            this.extractedByte = 0;
            this.availableExtractedBits = 0;
            this.nBytesExtracted++;
        }
        private int ExtractBit(int[] coeff)
        {
            int coeffVal;
            int mod64 = this.shuffledIndex % 64;
            if (mod64 == 0)
                return -1;
            this.shuffledIndex = this.shuffledIndex - mod64 + HuffmanDecode.deZigZag[mod64];
            coeffVal = coeff[this.shuffledIndex];
            if (coeffVal == 0)
                return -1;
            return coeffVal > 0 ? coeffVal & 1 : 1 - (coeffVal & 1);
        }

        #region IDisposable
        bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~JpegExtract()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (this._disposed)
                return;
            if (disposing)
                this.output.Dispose();
            this._disposed = true;
        }
        #endregion
    }
}