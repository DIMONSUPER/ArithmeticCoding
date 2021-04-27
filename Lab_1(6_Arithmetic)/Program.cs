using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lab_1_6_Arithmetic_
{
    class Program
    {
        public static void Main(string[] args)
        {
            Controller(args);
        }

        private static void Controller(string[] parameters)
        {
            string command = string.Empty;
            string inputFileName = string.Empty;
            string outputFileName = string.Empty;

            switch(parameters.Length)
            {
                case 0:
                    command = "encode";
                    inputFileName = "input.txt";
                    outputFileName = "output.txt";
                    break;
                case 1:
                    command = parameters[0];
                    inputFileName = "input.txt";
                    outputFileName = "output.txt";
                    break;
                case 2:
                    command = parameters[0];
                    inputFileName = parameters[1];
                    outputFileName = "output.txt";
                    break;
                case 3:
                    command = parameters[0];
                    inputFileName = parameters[1];
                    outputFileName = parameters[2];
                    break;
            }

            if (command.ToLower().Contains("encode"))
            {
                var encodedMessage = ArithmeticEncoder.GetEncodedMessage(inputFileName);

                File.WriteAllBytes(outputFileName, encodedMessage);
            }
            else if (command.ToLower().Contains("decode"))
            {
                var decodedMessage = ArithmeticEncoder.GetDecodedMessage(inputFileName);

                File.WriteAllBytes(outputFileName, decodedMessage);
            }
        }

        public static class ArithmeticEncoder
        {
            #region -- Private helpers --

            private const int _chunkSize = 12;

            private static Dictionary<byte, decimal> _FrequencyTable;

            private static Dictionary<byte, decimal> _CummulativeFreqTable;

            private static Dictionary<byte, (decimal LowerBound, decimal UpperBound)> _IntervalTable;

            private static Dictionary<byte, (decimal LowerBound, decimal UpperBound)> _IntervalTableCopy;

            #endregion

            #region -- Public methods --

            public static byte[] GetEncodedMessage(string input)
            {
                var bytesArray = File.ReadAllBytes(input);

                InitForEncoding(bytesArray);

                var size = BitConverter.GetBytes(GetFrequencyTableAsBytes().Length);

                return size.Concat(GetFrequencyTableAsBytes().Concat(GetEncodedMessage(bytesArray))).ToArray();
            }

            public static byte[] GetDecodedMessage(string input)
            {
                var bytesArray = File.ReadAllBytes(input);

                var size = BitConverter.ToInt32(bytesArray.Take(4).ToArray(), 0);

                var dict = bytesArray.Skip(4).Take(size).ToArray();

                InitForDecoding(dict, size);

                var msg = bytesArray.Skip(dict.Count() + 4).ToArray();

                return GetDecodedMessage(msg);
            }

            #endregion

            #region -- Private methods --

            private static byte[] GetDecodedMessage(byte[] msg)
            {
                List<byte> tmpList = new List<byte>();
                var sizeOfLastChunk = msg[msg.Length - 1];

                for (int i = 0; i < msg.Length - 1; i += sizeof(decimal))
                {
                    byte[] decodedChunk;
                    var chunk = msg.Slice(i, sizeof(decimal));

                    if (i + sizeof(decimal) >= msg.Length - 1)
                    {
                        decodedChunk = GetDecodedMessageForChunk(chunk, sizeOfLastChunk);
                    }
                    else
                    {
                        decodedChunk = GetDecodedMessageForChunk(chunk);
                    }

                    tmpList.AddRange(decodedChunk);
                    _IntervalTable = new Dictionary<byte, (decimal LowerBound, decimal UpperBound)>(_IntervalTableCopy);
                }

                return tmpList.ToArray();
            }

            private static IEnumerable<byte> GetEncodedMessage(byte[] bytesArray)
            {
                var chunks = GetChunks(bytesArray);

                List<byte> tmpList = new List<byte>();

                for (int i = 0; i < chunks.Length; i++)
                {
                    tmpList.AddRange(GetEncodedMessageFromChunk(chunks[i]));
                }

                //size of last chunk
                tmpList.Add((byte)chunks[chunks.Length - 1].Length);

                return tmpList;
            }

            private static void InitForEncoding(byte[] bytesArray)
            {
                InitFrequencyTable(bytesArray);

                InitCummulativeTable();

                InitIntervalTable();
            }

            private static void InitForDecoding(byte[] dic, int size)
            {
                InitFrequencyTableFromBytes(dic, size);

                InitCummulativeTable();

                InitIntervalTable();
            }

            private static byte[] GetDecodedMessageForChunk(byte[] msg, byte size = _chunkSize)
            {
                IList<byte> result = new List<byte>();
                var keysList = _IntervalTable.Keys.ToList();
                var message = msg.ToDecimal();

                var i = 0;

                while (i < size)
                {
                    var index = 0;

                    var interval = _IntervalTable[keysList[index]];

                    while (message > interval.UpperBound)
                    {
                        index++;
                        interval = _IntervalTable[keysList[index]];
                    }

                    result.Add(keysList[index]);
                    UpdateIntervalTable(interval);

                    i++;
                }

                return result.ToArray();
            }

            private static byte[] GetEncodedMessageFromChunk(byte[] msg)
            {
                var interval = (0m, 0m);

                for (byte i = 0; i < msg.Length; i++)
                {
                    interval = _IntervalTable[msg[i]];
                    UpdateIntervalTable(interval);
                }

                _IntervalTable = new Dictionary<byte, (decimal LowerBound, decimal UpperBound)>(_IntervalTableCopy);
                var dd = (interval.Item1 + interval.Item2) / 2;

                return dd.ToBytes();
            }

            private static byte[][] GetChunks(byte[] str)
            {
                IList<IEnumerable<byte>> result = new List<IEnumerable<byte>>();

                for (int i = 0; i < str.Length; i += _chunkSize)
                {
                    if (str.Length - i < _chunkSize)
                    {
                        result.Add(str.Slice(i, str.Length - i));
                    }
                    else
                    {
                        result.Add(str.Slice(i, _chunkSize));
                    }
                }

                return result.Select(x => x.ToArray()).ToArray();
            }

            private static byte[] GetFrequencyTableAsBytes()
            {
                IList<byte> result = new List<byte>();

                foreach (var pair in _FrequencyTable)
                {
                    result.Add(pair.Key);
                    result = result.Concat(pair.Value.ToBytes()).ToList();
                }

                return result.ToArray();
            }

            private static void InitFrequencyTableFromBytes(byte[] dic, int size)
            {
                _FrequencyTable = new Dictionary<byte, decimal>();

                for (int i = 0; i < size; i++)
                {
                    var key = dic[i];
                    var value = dic.Skip(i + 1).Take(sizeof(decimal)).ToArray().ToDecimal();
                    i += sizeof(decimal);
                    _FrequencyTable[key] = value;
                }
            }

            private static void UpdateIntervalTable((decimal LowerBound, decimal UpperBound) interval)
            {
                var keysList = _IntervalTable.Keys.ToList();
                var d = interval.UpperBound - interval.LowerBound;

                for (int i = 0; i < keysList.Count; i++)
                {
                    if (i == 0)
                    {
                        _IntervalTable[keysList[i]] = (interval.LowerBound, interval.LowerBound + d * _FrequencyTable[keysList[i]]);
                    }
                    else
                    {
                        _IntervalTable[keysList[i]] = (_IntervalTable[keysList[i - 1]].UpperBound, _IntervalTable[keysList[i - 1]].UpperBound + d * _FrequencyTable[keysList[i]]);
                    }
                }
            }

            private static void InitIntervalTable()
            {
                _IntervalTable = new Dictionary<byte, (decimal, decimal)>();

                var keysList = _FrequencyTable.Keys.ToList();

                for (int i = 0; i < keysList.Count; i++)
                {
                    if (i == 0)
                    {
                        _IntervalTable[keysList[i]] = (0m, _CummulativeFreqTable[keysList[i]]);
                    }
                    else
                    {
                        _IntervalTable[keysList[i]] = (_CummulativeFreqTable[keysList[i - 1]], _CummulativeFreqTable[keysList[i]]);
                    }
                }

                _IntervalTableCopy = new Dictionary<byte, (decimal LowerBound, decimal UpperBound)>(_IntervalTable);
            }

            private static void InitCummulativeTable()
            {
                _CummulativeFreqTable = new Dictionary<byte, decimal>();

                var sum = 0m;

                foreach (var key in _FrequencyTable.Keys.ToList())
                {
                    sum += _FrequencyTable[key];
                    _CummulativeFreqTable[key] = sum;
                }
            }

            private static void NormalizeIfSumNotOne(decimal sum)
            {
                if (sum != 1)
                {
                    var dif = 1 - sum;
                    _FrequencyTable[_FrequencyTable.Keys.ToList()[0]] += dif;
                }
            }

            private static void InitFrequencyTable(byte[] bytesArray)
            {
                _FrequencyTable = new Dictionary<byte, decimal>();

                var sum = 0m;

                var keys = bytesArray.Distinct().ToArray();

                foreach (var key in keys)
                {
                    _FrequencyTable.Add(key, 1m / keys.Length);
                    sum += 1m / keys.Length;
                }

                NormalizeIfSumNotOne(sum);
            }

            #endregion
        }
    }
}
