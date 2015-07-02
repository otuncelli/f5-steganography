using System;
using System.Collections.Generic;

namespace F5.Crypt
{
    internal sealed class Permutation
    {
        private int[] shuffled; // shuffled sequence

        // The constructor of class Permutation creates a shuffled
        // sequence of the integers 0 ... (size-1).
        internal Permutation(int size, F5Random random)
        {
            int i, randomIndex;
            this.shuffled = new int[size];

            // To create the shuffled sequence, we initialise an array
            // with the integers 0 ... (size-1).
            for (i = 0; i < size; i++)
            {
                // initialise with "size" integers
                this.shuffled[i] = i;
            }
            int maxRandom = size; // set number of entries to shuffle
            for (i = 0; i < size; i++)
            {
                // shuffle entries
                randomIndex = random.GetNextValue(maxRandom--);
                Swap(ref this.shuffled[maxRandom], ref this.shuffled[randomIndex]);
            }
        }

        private static void Swap(ref int a, ref int b)
        {
            int temp = a;
            a = b;
            b = temp;
        }

        /// <summary>
        /// get value #i from the shuffled sequence
        /// </summary>
        public int GetShuffled(int i)
        {
            return this.shuffled[i];
        }

        public int Length
        {
            get { return this.shuffled.Length; }
        }

        public FilteredCollection Filter(int[] coeff, int startIndex)
        {
            return new FilteredCollection(this.shuffled, coeff, startIndex);
        }
    }

    internal sealed class FilteredCollection
    {
        private readonly int[] iterable;
        private readonly int[] coeff;
        private int now;
        public FilteredCollection(int[] iterable, int[] coeff)
        {
            this.iterable = iterable;
            this.coeff = coeff;
        }
        public FilteredCollection(int[] iterable, int[] coeff, int startIndex)
            : this(iterable, coeff)
        {
            this.now = startIndex;
        }
        public int Current
        {
            get { return this.iterable[this.now]; }
        }
        private bool IsValid(int n)
        {
            return n % 64 != 0 && this.coeff[n] != 0;
        }
        public List<int> Offer(int count)
        {
            List<int> result = new List<int>(count);
            while (count > 0)
            {
                while (this.now < this.iterable.Length && !IsValid(Current))
                {
                    this.now++;
                }
                if (this.now < this.iterable.Length)
                {
                    count--;
                    result.Add(Current);
                    this.now++;
                }
            }
            return result;
        }
        public int Offer()
        {
            return Offer(1)[0];
        }
    }
}