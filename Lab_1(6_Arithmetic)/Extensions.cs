using System;
using System.Collections.Generic;

namespace Lab_1_6_Arithmetic_
{
    public static class Extensions
    {
        public static T[] Slice<T>(this T[] source, int index, int length)
        {
            T[] slice = new T[length];
            Array.Copy(source, index, slice, 0, length);
            return slice;
        }

        public static byte[] ToBytes(this decimal d)
        {
            var result = new List<byte>();

            var a = decimal.GetBits(d);
            foreach(var i in a)
            {
                result.AddRange(BitConverter.GetBytes(i));
            }

            return result.ToArray();
        }

        public static decimal ToDecimal(this byte[] bytes)
        {
            var result = new List<int>();

            for (int i = 0; i < bytes.Length; i += 4)
            {
                result.Add(BitConverter.ToInt32(bytes.Slice(i, 4), 0));
            }

            return new decimal(result.ToArray());
        }
    }
}
