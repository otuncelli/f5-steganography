using System;

namespace F5.Crypt
{
    using Org.BouncyCastle.Security;

    internal class UnSecureRandom : Random
    {
        public UnSecureRandom(byte[] seed)
            : base(ComputeHash(seed))
        {
        }

        private static int ComputeHash(byte[] data)
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < data.Length; i++)
                    hash = (hash ^ data[i]) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }
    }

    public class BufferedSecureRandom
    {
        private readonly SecureRandom random;
        private readonly byte[] buffer;
        private readonly int bufferSize;
        private int current;

        public BufferedSecureRandom(byte[] password, int bufferSize = 1024)
        {
            this.bufferSize = bufferSize;
            this.buffer = new byte[bufferSize];
            this.random = new SecureRandom(password);
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
        public byte GetNextByte()
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
