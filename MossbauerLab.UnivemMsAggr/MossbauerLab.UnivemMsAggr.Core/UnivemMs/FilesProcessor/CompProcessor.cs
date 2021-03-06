﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MossbauerLab.UnivemMsAggr.Core.Data;
using MossbauerLab.UnivemMsAggr.Core.Data.FitsInfo;
using MossbauerLab.UnivemMsAggr.Core.Data.FitsInfo.CalculatedValues;
using MossbauerLab.UnivemMsAggr.Core.Data.SpectralComponents;

namespace MossbauerLab.UnivemMsAggr.Core.UnivemMs.FilesProcessor
{
    public static class CompProcessor
    {
        public enum SortOrder
        {
            Asc,
            Dsc
        }

        /// <summary>
        ///     These files encoding is CP-1251
        /// </summary>
        /// <param name="componentsFile"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public static SpectrumFit Process(String componentsFile, SortOrder order = SortOrder.Dsc)
        {
            SpectrumFit fit = new SpectrumFit();
            if (!File.Exists(Path.GetFullPath(componentsFile)))
                return null;
            IList<String> content = File.ReadAllLines(Path.GetFullPath(componentsFile), Encoding.GetEncoding(CompFilesCodePage));
            if (!content.Any(line => String.Equals(line, ComponentsFileSign)))
                return null;
            fit.FileName = componentsFile;
            UInt16 cahnnelsNumber = GetValue<UInt16>(content, ChannelsNumberKey, ChannelsNumberPattern);
            Decimal velocityStep = GetValue<Decimal>(content, VelocityStepKey, VelocityStepLinePattern);
            Decimal chiSquareValue = GetValue<Decimal>(content, ChiSquareKey,ChiSquareLinePattern);
            fit.Info = new ComponentsInfo(chiSquareValue, velocityStep, EmpiricCalculations.CalculateHyperfineFieldError(velocityStep), cahnnelsNumber);
            IList<String> componentsStartStr = content.Where(line => Regex.Match(line, ComponentStartPattern).Success).ToList();
            IList<Int32> indexes = componentsStartStr.Select(line => content.IndexOf(line)).ToList();
            IList<Int32> sextets = indexes.Where(index => content[index].Contains(SextetKey)).ToList();
            IList<Int32> doublets = indexes.Where(index => content[index].Contains(DoubletKey)).ToList();
            fit.Sextets = GetSextets(content, sextets, order);
            fit.Doublets = GetDoublets(content, doublets, order);
            return fit;
        }

        private static T GetValue<T>(IList<String> fileContent, String key, String pattern) where T : IConvertible
        {
            String selectedLine = null;
            foreach (String line in fileContent)
            {
                if (Regex.Match(line, pattern).Success)
                {
                    selectedLine = line;
                    break;
                }
            }
            if (selectedLine == null)
                return default(T);
            Int32 index = selectedLine.IndexOf(key, StringComparison.InvariantCulture);
            if(index < 0)
                return default(T);
            index += key.Length;
            String residual = selectedLine.Substring(index).Trim();
            String[] parts = residual.Split(' ');
            NumberFormatInfo format = new NumberFormatInfo();
            format.NumberDecimalSeparator = ".";
            return (T)Convert.ChangeType(parts[0], typeof(T), format);
        }

        private static IList<Sextet> GetSextets(IList<String> fileContent, IList<Int32> startPosIndexes, SortOrder order)
        {
            IList<Sextet> sextets = new List<Sextet>();
            while (startPosIndexes.Count > 0)
            {
                Int32 index = GetNextComponentIndex(fileContent, startPosIndexes, order, true);
                startPosIndexes.Remove(index);
                Tuple<Decimal, Decimal?> hyperfineField = GetValue(fileContent[index + HyperfineFieldLineOffsetInSextet], HyperfineFieldKey);
                Tuple<Decimal, Decimal?> quadrupolShift = GetValue(fileContent[index + QuadrupolShiftLineOffsetInSextet], QuadrupolShiftKey);
                Tuple<Decimal, Decimal?> isomerShift = GetValue(fileContent[index + IsomerShiftLineOffsetInSextet], IsomerShiftKey);
                Tuple<Decimal, Decimal?> lineWidth = GetValue(fileContent[index + LineWidthOffsetInSextet], LineWidthKey);
                Tuple<Decimal, Decimal?> area = GetValue(fileContent[index + RelativeAreaLineOffsetInSextet], RelativeAreaKey, false);
                Sextet sextet = new Sextet(lineWidth.Item1, lineWidth.Item2, 
                                           isomerShift.Item1, isomerShift.Item2,
                                           quadrupolShift.Item1, quadrupolShift.Item2,
                                           hyperfineField.Item1, hyperfineField.Item2,
                                           area.Item1, area.Item1 * 0.1m);
                sextets.Add(sextet);
            }
            return sextets;
        }

        private static IList<Doublet> GetDoublets(IList<String> fileContent, IList<Int32> startPosIndexes, SortOrder order)
        {
            IList<Doublet> doublets = new List<Doublet>();
            while (startPosIndexes.Count > 0)
            {
                Int32 index = GetNextComponentIndex(fileContent, startPosIndexes, order, false);
                startPosIndexes.Remove(index);
                Tuple<Decimal, Decimal?> quadrupolShift = GetValue(fileContent[index + QuadrupolShiftLineOffsetInDoublet], QuadrupolShiftKey);
                Tuple<Decimal, Decimal?> isomerShift = GetValue(fileContent[index + IsomerShiftLineOffsetInDoublet], IsomerShiftKey);
                Tuple<Decimal, Decimal?> lineWidth = GetValue(fileContent[index + LineWidthOffsetInDoublet], LineWidthKey);
                Tuple<Decimal, Decimal?> area = GetValue(fileContent[index + RelativeAreaLineOffsetInDoublet], RelativeAreaKey, false);
                Doublet doublet = new Doublet(lineWidth.Item1, lineWidth.Item2,
                                              isomerShift.Item1, isomerShift.Item2,
                                              quadrupolShift.Item1, quadrupolShift.Item2,
                                              area.Item1, area.Item1 * 0.1m);
                doublets.Add(doublet);
            }
            return doublets;
        }

        private static Int32 GetNextComponentIndex(IList<String> fileContent, IList<Int32> startPosIndexes, SortOrder order, Boolean isSextet)
        {
            Decimal  value = order == SortOrder.Asc ? 1000000 : 0;
            Int32 selectedIndex = 0;
            foreach (Int32 index in startPosIndexes)
            {
                String fieldStr = fileContent[index + (isSextet ? HyperfineFieldLineOffsetInSextet : QuadrupolShiftLineOffsetInDoublet)];
                Tuple<Decimal, Decimal?> parameter = GetValue(fieldStr, isSextet ? HyperfineFieldKey : QuadrupolShiftKey);
                Decimal error = parameter.Item2 ?? 0;
                if ((order == SortOrder.Asc && value > parameter.Item1 + error) ||
                    (order == SortOrder.Dsc && value < parameter.Item1 + error))
                {
                     value = parameter.Item1 + error;
                    selectedIndex = index;
                }
            }
            return selectedIndex;
        }

        private static Tuple<Decimal, Decimal?> GetValue(String line, String key, Boolean errorPresent = true)
        {
            Int32 startIndex = line.IndexOf(key, StringComparison.InvariantCulture);
            if (startIndex == -1)
                throw new InvalidOperationException("Unexpected line format");
            startIndex += key.Length;
            String lineResidual = line.Substring(startIndex);
            Int32 errorStartIndex = lineResidual.IndexOf("(", StringComparison.InvariantCulture);
            Int32 errorEndIndex = lineResidual.IndexOf(")", StringComparison.InvariantCulture);
            Decimal? error = null;
            Boolean processErrorValue = true;
            String errorValue = String.Empty;
            if (!errorPresent)
                processErrorValue = false;
            else
            {
                errorValue = lineResidual.Substring(errorStartIndex + 1, errorEndIndex - errorStartIndex - 1).Trim();

                if (String.Equals(errorValue, LimitErrorKey) || String.Equals(errorValue, FixedErrorKey))
                {
                    error = 0;
                    processErrorValue = false;
                }
                else if (String.Equals(errorValue, NotDefinedErrorKey))
                    processErrorValue = false;
            }
            String[] parts = lineResidual.Trim().Split(' ');
            parts[0] = parts[0].EndsWith(";") ? parts[0].Substring(0, parts[0].Length - 1) : parts[0];
            NumberFormatInfo format = new NumberFormatInfo();
            format.NumberDecimalSeparator = ".";
            // ReSharper disable PossibleNullReferenceException
            Decimal value = (Decimal)Convert.ChangeType(parts[0], typeof(Decimal), format);
            if (processErrorValue)
                error = (Decimal) Convert.ChangeType(errorValue, typeof(Decimal), format);
            // ReSharper restore PossibleNullReferenceException
            return  new Tuple<Decimal, Decimal?>(value, error);
        }

        private const String CompFilesCodePage = "Windows-1251";

        private const String ComponentsFileSign = "Исп.единицы: Is,G,Qs - мм/с; H - кЭ; A - %; отн.G,A - б/разм.";
        private const String VelocityStepKey = "Цена канала: ";
        private const String VelocityStepLinePattern = @"^[\w\W]*" + VelocityStepKey + @"[\w\W]*$";
        private const String ChiSquareKey = "Xi_2 =";
        private const String ChiSquareLinePattern = @"^" + ChiSquareKey + @"[\w\W]*$";
        private const String ChannelsNumberKey = "Точек подгонки (интервалы):";
        private const String ChannelsNumberPattern = @"^Точек подгонки \(интервалы\):[\w\W]*$";
        private const String ComponentStartPattern = @"^\d{1,2}: Имя - [\w\W]*$";

        private const String NotDefinedErrorKey = "не опр.";
        private const String LimitErrorKey = "граница";
        private const String FixedErrorKey = "фиксир.";

        private const String SextetKey = "Sextet";
        private const String DoubletKey = "Doublet";

        private const String HyperfineFieldKey = "H =";
        private const String QuadrupolShiftKey = "Qs=";
        private const String IsomerShiftKey = "Is=";
        private const String LineWidthKey = "G1=";
        private const String RelativeAreaKey = "Отн.площадь,% =";

        private const Int32 RelativeAreaLineOffsetInSextet = 1;
        private const Int32 HyperfineFieldLineOffsetInSextet = 3;
        private const Int32 QuadrupolShiftLineOffsetInSextet = 3;
        private const Int32 IsomerShiftLineOffsetInSextet = 3;
        private const Int32 LineWidthOffsetInSextet = 5;

        private const Int32 RelativeAreaLineOffsetInDoublet = 1;
        private const Int32 QuadrupolShiftLineOffsetInDoublet = 2;
        private const Int32 IsomerShiftLineOffsetInDoublet = 2;
        private const Int32 LineWidthOffsetInDoublet = 4;
    }
}
