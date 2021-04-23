using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lab_1_6_Arithmetic_
{
    class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length > 1)
                {
                    var encoder = new ArithmeticEncoder(args[1]);

                    if (args[0].ToLower().Contains("encode"))
                    {
                        if (args.Length > 2)
                        {
                            encoder.EncodeDecod(ECodingMode.Encoding, args[2]);
                        }
                        else
                        {
                            encoder.EncodeDecod(ECodingMode.Encoding);
                        }
                    }
                    else if (args[0].ToLower().Contains("decode"))
                    {
                        if (args.Length > 2)
                        {
                            encoder.EncodeDecod(ECodingMode.Decoding, args[2]);
                        }
                        else
                        {
                            encoder.EncodeDecod(ECodingMode.Decoding);
                        }
                    }
                }
                else
                {
                    var encoder = new ArithmeticEncoder("encoded");
                    encoder.EncodeDecod(ECodingMode.Decoding);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public class ArithmeticEncoder
        {
            #region -- Private helpers --

            private const int _chunkSize = 6;

            private string _path;

            private Dictionary<byte, double> _FrequencyTable;

            private Dictionary<byte, double> _CummulativeFreqTable;

            private Dictionary<byte, (double LowerBound, double UpperBound)> _IntervalTable;

            #endregion

            public ArithmeticEncoder(string path)
            {
                _path = path;
            }

            #region -- Public methods --

            public void EncodeDecod(ECodingMode mode = ECodingMode.Encoding, string output = "")
            {
                if (string.IsNullOrWhiteSpace(output)) 
                {
                    output = mode == ECodingMode.Encoding ? "encoded" : "decoded";
                }

                if (mode == ECodingMode.Encoding)
                {
                    var bytesArray = File.ReadAllBytes("file.txt");

                    InitForEncoding(bytesArray);

                    var size = BitConverter.GetBytes(GetFrequencyTableAsBytes().Length);

                    var msg = size.Concat(GetFrequencyTableAsBytes().Concat(GetEncodedMessage(bytesArray))).ToArray();

                    File.WriteAllBytes(output, msg);
                }
                else if (mode == ECodingMode.Decoding)
                {
                    var bytesArray = File.ReadAllBytes(_path);

                    var size = BitConverter.ToInt32(bytesArray.Take(4).ToArray(), 0);

                    var dict = bytesArray.Skip(4).Take(size).ToArray();

                    InitForDecoding(dict, size);

                    var msg = bytesArray.Skip(dict.Count() + 4).ToArray();

                    var a = GetDecodedMessage(msg, dict, size);

                    File.WriteAllBytes(output, a);
                }
            }

            #endregion

            #region -- Private methods --

            private byte[] GetDecodedMessage(byte[] msg, byte[] dict, int size)
            {
                List<byte> tmpList = new List<byte>();
                var sizeOfLastChunk = msg[msg.Length - 1];

                for (int i = 0; i < msg.Length - 1; i += 8)
                {
                    byte[] decodedChunk;
                    var chunk = msg.Slice(i, sizeof(double));

                    if (i + 8 >= msg.Length - 1)
                    {
                        decodedChunk = GetDecodedMessageForChunk(chunk, sizeOfLastChunk);
                    }
                    else
                    {
                        decodedChunk = GetDecodedMessageForChunk(chunk);
                    }

                    tmpList.AddRange(decodedChunk);
                    InitIntervalTable();
                }

                return tmpList.ToArray();
            }

            private IEnumerable<byte> GetEncodedMessage(byte[] bytesArray)
            {
                var chunks = GetChunks(bytesArray);

                List<byte> tmpList = new List<byte>();

                for (int i = 0; i < chunks.Length; i++)
                {
                    tmpList.AddRange(GetEncodedMessageFromChunk(chunks[i]));
                }

                //size of last chunk
                tmpList.Add((byte)chunks.Last().Length);

                return tmpList;
            }

            private void InitForEncoding(byte[] bytesArray)
            {
                InitFrequencyTable(bytesArray);

                InitCummulativeTable();

                InitIntervalTable();
            }

            private void InitForDecoding(byte[] dic, int size)
            {
                InitFrequencyTableFromBytes(dic, size);

                InitCummulativeTable();

                InitIntervalTable();
            }

            private byte[] GetDecodedMessageForChunk(byte[] msg, byte size = _chunkSize)
            {
                IList<byte> result = new List<byte>();
                var keysList = _IntervalTable.Keys.ToList();
                var message = BitConverter.ToDouble(msg, 0);

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

            private byte[] GetEncodedMessageFromChunk(byte[] msg)
            {
                var interval = (0d, 0d);

                for (byte i = 0; i < msg.Length; i++)
                {
                    interval = _IntervalTable[msg[i]];
                    UpdateIntervalTable(interval);
                }

                InitIntervalTable();
                return BitConverter.GetBytes((interval.Item1 + interval.Item2) / 2);
            }

            private byte[][] GetChunks(byte[] str)
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

            private byte[] GetFrequencyTableAsBytes()
            {
                IList<byte> result = new List<byte>();

                foreach (var pair in _FrequencyTable)
                {
                    result.Add(pair.Key);
                    result = result.Concat(BitConverter.GetBytes(pair.Value)).ToList();
                }

                return result.ToArray();
            }

            private void InitFrequencyTableFromBytes(byte[] dic, int size)
            {
                _FrequencyTable = new Dictionary<byte, double>();

                for (int i = 0; i < size; i++)
                {
                    var key = dic[i];
                    var value = BitConverter.ToDouble(dic.Skip(i + 1).Take(sizeof(double)).ToArray(), 0);
                    i += sizeof(double);
                    _FrequencyTable[key] = value;
                }
            }

            private void UpdateIntervalTable((double LowerBound, double UpperBound) interval)
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

            private void InitIntervalTable()
            {
                _IntervalTable = new Dictionary<byte, (double, double)>();

                var keysList = _FrequencyTable.Keys.ToList();

                for (int i = 0; i < keysList.Count; i++)
                {
                    if (i == 0)
                    {
                        _IntervalTable[keysList[i]] = (0d, _CummulativeFreqTable[keysList[i]]);
                    }
                    else
                    {
                        _IntervalTable[keysList[i]] = (_CummulativeFreqTable[keysList[i - 1]], _CummulativeFreqTable[keysList[i]]);
                    }
                }
            }

            private void InitCummulativeTable()
            {
                _CummulativeFreqTable = new Dictionary<byte, double>();

                var sum = 0d;

                foreach (var key in _FrequencyTable.Keys.ToList())
                {
                    sum += _FrequencyTable[key];
                    _CummulativeFreqTable[key] = sum;
                }
            }

            private void NormalizeIfSumNotOne(double sum)
            {
                if (sum != 1)
                {
                    var dif = 1 - sum;
                    _FrequencyTable[_FrequencyTable.Keys.ToList()[0]] += dif;
                }
            }

            private void InitFrequencyTable(byte[] bytesArray)
            {
                _FrequencyTable = new Dictionary<byte, double>();

                var sum = 0d;

                var keys = bytesArray.Distinct();

                foreach (var key in keys)
                {
                    _FrequencyTable[key] = 1d / keys.Count();
                    sum += _FrequencyTable[key];
                }

                NormalizeIfSumNotOne(sum);
            }

            #endregion
        }
    }
}
