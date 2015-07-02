using System;
using System.Security.Cryptography;

namespace F5.Crypt
{
    internal class SecureRandom : Random
    {
        public SecureRandom(byte[] seed)
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

    internal class F5Random
    {
        private readonly SecureRandom random;

        public F5Random(byte[] password)
        {
            this.random = new SecureRandom(password);
        }

        /// <summary>
        /// get a random byte
        /// </summary>
        /// <returns>random signed byte</returns>
        public int GetNextByte()
        {
            return this.random.Next(-128, 127);
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
