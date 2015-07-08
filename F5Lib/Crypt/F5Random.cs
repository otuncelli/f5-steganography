namespace F5.Crypt
{
    using Org.BouncyCastle.Crypto.Digests;
    using Org.BouncyCastle.Crypto.Prng;

    public class BufferedSecureRandom
    {
        private readonly DigestRandomGenerator random;
        private readonly byte[] buffer;
        private readonly int bufferSize;
        private int current;

        public BufferedSecureRandom(byte[] password, int bufferSize = 1024)
        {
            this.bufferSize = bufferSize;
            this.buffer = new byte[bufferSize];
            this.random = new DigestRandomGenerator(new Sha1Digest());
            this.random.AddSeedMaterial(password);
            this.random.NextBytes(this.buffer);
        }

        public byte Next()
        {
            if (this.current >= bufferSize)
            {
                this.random.NextBytes(this.buffer);
                this.current = 0;
            }
            return this.buffer[this.current++];
        }
    }

    internal class F5Random
    {
        private readonly BufferedSecureRandom random;

        public F5Random(byte[] password)
        {
            this.random = new BufferedSecureRandom(password);
        }

        /// <summary>
        /// get a random byte
        /// </summary>
        /// <returns>random signed byte</returns>
        public int GetNextByte()
        {
            return this.random.Next();
        }

        /// <summary>
        /// get a random integer 0 ... (maxValue-1)
        /// </summary>
        /// <param name="maxValue">maxValue (excluding)</param>
        /// <returns>random integer</returns>
        public int GetNextValue(int maxValue)
        {
            int retVal = GetNextByte() | GetNextByte() << 8 | GetNextByte() << 16 | GetNextByte() << 24;
            retVal %= maxValue;
            return retVal < 0 ? retVal + maxValue : retVal;
        }
    }
}
