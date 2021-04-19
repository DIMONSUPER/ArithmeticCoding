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

            private const int _chunkSize = 14;

            private string _path;

            private Dictionary<byte, decimal> _FrequencyTable;

            private Dictionary<byte, decimal> _CummulativeFreqTable;

            private Dictionary<byte, (decimal LowerBound, decimal UpperBound)> _IntervalTable;

            #endregion

            public ArithmeticEncoder(string path)
            {
                _path = path;
            }

            #region -- Public methods --

            public void EncodeDecod(ECodingMode mode = ECodingMode.Encoding, string output = "")
            {
                if(string.IsNullOrWhiteSpace(output))
                {
                    output = mode == ECodingMode.Encoding ? "encoded" : "decoded";
                }

                if (mode == ECodingMode.Encoding)
                {
                    var bytesArray = File.ReadAllBytes(_path);

                    InitForEncoding(bytesArray);

                    var msg = GetFrequencyTableAsString() + GetEncodedMessage(bytesArray);
                    var bytes = Encoding.UTF8.GetBytes(msg);

                    File.WriteAllBytes(output, bytes);
                }
                else if (mode == ECodingMode.Decoding)
                {
                    var bytesArray = File.ReadAllBytes(_path);

                    var str = Encoding.UTF8.GetString(bytesArray);
                    var last = str.LastIndexOf(':');
                    var dict = str.Substring(0, last);
                    var msg = str.Substring(last + 1);

                    InitForDecoding(dict);

                    var a = GetDecodedMessage(msg, dict);

                    var bytes = Encoding.UTF8.GetBytes(a);

                    File.WriteAllBytes(output, bytes);
                }
            }

            #endregion

            #region -- Private methods --

            private string GetDecodedMessage(string msg, string freqTableAsString)
            {
                IList<string> tmpList = new List<string>();

                var chunks = msg.Split('\n');

                foreach (var chunk in chunks)
                {
                    if (chunk.Contains('^'))
                    {
                        var arr = chunk.Split('^');

                        tmpList.Add(GetDecodedMessageForChunk(decimal.Parse(arr[0]), int.Parse(arr[1])));
                    }
                    else
                    {
                        tmpList.Add(GetDecodedMessageForChunk(decimal.Parse(chunk)));
                    }

                    InitForDecoding(freqTableAsString);
                }
                return string.Join(string.Empty, tmpList.ToArray());
            }

            private string GetEncodedMessage(byte[] bytesArray)
            {
                var stringToEncode = Encoding.UTF8.GetString(bytesArray, 0, bytesArray.Length);

                var chunks = GetChunks(stringToEncode);

                IList<string> tmpList = new List<string>();

                for (int i = 0; i < chunks.Length; i++)
                {
                    if (i + 1 < chunks.Length)
                    {
                        tmpList.Add(GetEncodedMessageFromChunk(chunks[i]) + '\n');
                    }
                    else
                    {
                        tmpList.Add(GetEncodedMessageFromChunk(chunks[i]));
                    }

                    //if chunk size is less then needed
                    if (chunks[i].Length < _chunkSize)
                    {
                        tmpList.Add($"^{chunks[i].Length}");
                    }
                }

                return string.Join(string.Empty, tmpList.ToArray());
            }

            private void InitForEncoding(byte[] bytesArray)
            {
                InitFrequencyTable(bytesArray);

                InitCummulativeTable();

                InitIntervalTable();
            }

            private void InitForDecoding(string freqTableAsString)
            {
                InitFrequencyTableFromString(freqTableAsString);

                InitCummulativeTable();

                InitIntervalTable();
            }

            private string GetDecodedMessageForChunk(decimal msg, int chunkSize = _chunkSize)
            {
                IList<char> result = new List<char>();
                var keysList = _IntervalTable.Keys.ToList();

                var i = 0;

                while (i < chunkSize)
                {
                    var index = 0;

                    var interval = _IntervalTable[keysList[index]];

                    while (msg > interval.UpperBound)
                    {
                        index++;
                        interval = _IntervalTable[keysList[index]];
                    }

                    result.Add(Convert.ToChar(keysList[index]));
                    UpdateIntervalTable(interval);

                    i++;
                }

                return string.Join(string.Empty, result.ToArray());
            }

            private string GetEncodedMessageFromChunk(string msg)
            {
                var interval = (0m, 0m);

                foreach (var ch in msg)
                {
                    interval = _IntervalTable[Convert.ToByte(ch)];
                    UpdateIntervalTable(interval);
                }

                var result = (interval.Item1 + interval.Item2) / 2;

                InitIntervalTable();
                return result.ToString();
            }

            private string[] GetChunks(string str)
            {
                IList<string> result = new List<string>();

                for (int i = 0; i < str.Length; i++)
                {
                    if (i + _chunkSize >= str.Length)
                    {
                        result.Add(str.Substring(i));
                        i += str.Substring(i).Length;
                    }
                    else
                    {
                        result.Add(str.Substring(i, _chunkSize));
                        i += _chunkSize - 1;
                    }
                }

                return result.ToArray();
            }

            private string GetFrequencyTableAsString()
            {
                IList<string> result = new List<string>();

                foreach (var pair in _FrequencyTable)
                {
                    result.Add(Convert.ToChar(pair.Key) + pair.Value.ToString() + ":");
                }

                return string.Join(string.Empty, result.ToArray());
            }

            private void InitFrequencyTableFromString(string st)
            {
                Dictionary<byte, decimal> result = new Dictionary<byte, decimal>();

                var s = st.Split(':');

                for (int i = 0; i < s.Length; i++)
                {
                    if (!string.IsNullOrEmpty(s[i]))
                    {
                        result[Convert.ToByte(s[i][0])] = decimal.Parse(s[i].Substring(1));
                    }
                    else if (i + 1 < s.Length)
                    {
                        i++;
                        result[Convert.ToByte(':')] = decimal.Parse(s[i].Substring(1));
                    }
                }

                _FrequencyTable = result;
            }

            private void UpdateIntervalTable((decimal LowerBound, decimal UpperBound) interval)
            {
                var keysList = _IntervalTable.Keys.ToList();
                var d = interval.UpperBound - interval.LowerBound;

                for (int i = 0; i < keysList.Count; i++)
                {
                    if (i == 0)
                    {
                        var newValue = interval.LowerBound + d * _FrequencyTable[keysList[i]];
                        _IntervalTable[keysList[i]] = (interval.LowerBound, newValue);
                    }
                    else
                    {
                        decimal newValue = _IntervalTable[keysList[i - 1]].UpperBound + d * _FrequencyTable[keysList[i]];
                        _IntervalTable[keysList[i]] = (_IntervalTable[keysList[i - 1]].UpperBound, newValue);
                    }
                }
            }

            private void InitIntervalTable()
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
            }

            private void InitCummulativeTable()
            {
                _CummulativeFreqTable = new Dictionary<byte, decimal>();

                var sum = 0m;

                foreach (var key in _FrequencyTable.Keys.ToList())
                {
                    sum += _FrequencyTable[key];
                    _CummulativeFreqTable[key] = sum;
                }
            }

            private void NormalizeIfSumNotOne(decimal sum)
            {
                if (sum != 1)
                {
                    var dif = 1 - sum;
                    var s = 0m;
                    foreach (var key in _FrequencyTable.Keys.ToList())
                    {
                        _FrequencyTable[key] += dif / _FrequencyTable.Count;
                        s += _FrequencyTable[key];
                    }


                }
            }

            private void InitFrequencyTable(byte[] bytesArray)
            {
                _FrequencyTable = new Dictionary<byte, decimal>();

                var sum = 0m;

                var keys = bytesArray.Distinct();

                foreach (var key in keys)
                {
                    _FrequencyTable[key] = Convert.ToDecimal(bytesArray.Where(b => b == key).Count()) / bytesArray.Count();

                    sum += _FrequencyTable[key];
                }

                NormalizeIfSumNotOne(sum);
            }

            #endregion
        }
    }
}
