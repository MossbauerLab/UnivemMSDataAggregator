﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MossbauerLab.UnivemMsAggr.Core.Data;
using Microsoft.Office.Interop.Word;
using MossbauerLab.UnivemMsAggr.Core.Data.SpectralComponents;

namespace MossbauerLab.UnivemMsAggr.Core.Export
{
    public class SpectrumFitToMsWord : ISpectrumFitExport
    {
        public SpectrumFitToMsWord()
        {
            _parametersFormatInfo.NumberDecimalSeparator = ".";
            _parametersFormatInfo.NumberDecimalDigits = 3;
            _hypFieldFormatInfo.NumberDecimalSeparator = ".";
            _hypFieldFormatInfo.NumberDecimalDigits = 1;
            _areaFormatInfo.NumberDecimalSeparator = ".";
            _areaFormatInfo.NumberDecimalDigits = 2;
        }

        public Boolean Export(String destination, SpectrumFit data)
        {
            try
            {
                Boolean doubletsOnly = data.Sextets == null || data.Sextets.Count == 0;
                Int32 rows = (!doubletsOnly) ? data.Sextets.Count + data.Doublets.Count + 1 : data.Doublets.Count + 1;
                Int32 columns = (!doubletsOnly) ? _tableHeaderMixedCompEn.Count : _tableHeaderDoubletsOnlyEn.Count;
                Table componentsTable = CreateDocTable(rows, columns);
                CreateTableHeader(componentsTable, !doubletsOnly);
                Boolean result = ExportFitImpl(data, componentsTable, !doubletsOnly, 2, columns);
                SpectrumFitProcessedHandler(data.FileName);
                AddRemark();
                _msWordDocument.SaveAs(Path.GetFullPath(destination));
                _msWordDocument.Close();
                return result;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public Boolean Export(String destination, IList<SpectrumFit> data)
        {
            try
            {
                Boolean result = true;
                Boolean doubletsOnly = !data.Any(item => item.Sextets != null && item.Sextets.Count > 0);
                Int32 sextets = data.Select(item => item.Sextets.Count).Aggregate((item, total) =>
                {
                    total += item;
                    return total;
                });
                Int32 doublets = data.Select(item => item.Doublets.Count).Aggregate((item, total) =>
                {
                    total += item;
                    return total;
                });
                Int32 rows = sextets + doublets + 1;
                Int32 columns = (!doubletsOnly) ? _tableHeaderMixedCompEn.Count : _tableHeaderDoubletsOnlyEn.Count;
                Table componentsTable = CreateDocTable(rows, columns);
                CreateTableHeader(componentsTable, !doubletsOnly);
                Int32 startIndex = 2;
                for (Int32 i = 0; i < data.Count; i++)
                {

                    result &= ExportFitImpl(data[i], componentsTable, !doubletsOnly, startIndex, columns);
                    startIndex += data[i].Sextets.Count + data[i].Doublets.Count;
                    SpectrumFitProcessedHandler(data[i].FileName);
                }
                AddRemark();
                _msWordDocument.SaveAs(Path.GetFullPath(destination));
                _msWordDocument.Close();
                return result;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Boolean ExportFitImpl(SpectrumFit data, Table componentsTable, Boolean mixedComponents, Int32 startRowIndex,  Int32 columns)
        {
            try
            {
                if (data.Sextets != null && data.Sextets.Count > 0)
                {
                    Int32 sextetCounter = 0;
                    for (Int32 row = startRowIndex; row < startRowIndex + data.Sextets.Count; row++)
                    {
                        for (Int32 column = 1; column <= _tableHeaderMixedCompEn.Count; column++)
                        {
                            if (row == startRowIndex && column == 1)
                                componentsTable.Cell(row, column).Range.Text = data.SampleName;
                            else if (row == startRowIndex && column == ChiSquareValueSextetIndex)
                                componentsTable.Cell(row, column).Range.Text = data.Info.ChiSquareValue.ToString(CultureInfo.InvariantCulture);
                            else if (column == ComponentNameSextetIndex)
                                componentsTable.Cell(row, column).Range.Text = "S" + (sextetCounter + 1);
                            else
                                componentsTable.Cell(row, column).Range.Text = GetComponentColumnValue(data.Sextets[sextetCounter], column,
                                                                                                       data.Info.VelocityStep,
                                                                                                       data.Info.HyperfineFieldPerMmS);
                        }
                        sextetCounter++;
                    }
                }
                if (data.Doublets != null)
                {
                    Int32 doubletCounter = 0;
                    Int32 startIndex = (data.Sextets != null ? data.Sextets.Count : 0) + startRowIndex;
                    for (Int32 row = startIndex; row < data.Doublets.Count + startIndex; row++)
                    {
                        for (Int32 column = 1; column <= columns; column++)
                        {
                             if (row == startRowIndex && column == 1)
                                 componentsTable.Cell(row, column).Range.Text = data.SampleName;
                             else if (row == startRowIndex && column == 6)
                                 componentsTable.Cell(row, column).Range.Text = data.Info.ChiSquareValue.ToString(CultureInfo.InvariantCulture);
                             else if ((column == ComponentNameDoubletIndex && !mixedComponents) ||
                                      (column == ComponentNameSextetIndex && mixedComponents))
                                 componentsTable.Cell(row, column).Range.Text = "D" + (doubletCounter + 1);
                             else
                                 componentsTable.Cell(row, column).Range.Text = GetComponentColumnValue(data.Doublets[doubletCounter], 
                                                                                                        column,
                                                                                                        data.Info.VelocityStep,
                                                                                                        data.Info.HyperfineFieldPerMmS,
                                                                                                        mixedComponents);
                        }
                        doubletCounter++;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private void CreateTableHeader(Table table, Boolean mixedComponents)
        {
            IList<String> selectedHeader = mixedComponents ? _tableHeaderMixedCompEn : _tableHeaderDoubletsOnlyEn;
            for (Int32 column = 1; column < selectedHeader.Count + 1; column++)
            {
                table.Cell(1, column).Range.Text = selectedHeader[column - 1];
                table.Cell(1, column).Range.Font.Bold = 1;
            }
        }

        private String GetComponentColumnValue<T>(T component, Int32 index, Decimal velocityStep, 
                                                  Decimal hyperfineFieldError, Boolean mixedComponents = true) where T : class
        {
            if (component is Sextet)
            {
                Sextet sextet = component as Sextet;
                switch (index)
                {
                    case LineWidthSextetIndex:
                         return GetParameter(sextet.LineWidth, sextet.LineWidthError, 2 * velocityStep, 3, _parametersFormatInfo);
                    case IsomerShiftSextetIndex:
                         return GetParameter(sextet.IsomerShift, sextet.IsomerShiftError, velocityStep, 3, _parametersFormatInfo);
                    case QuadrupolSplittingSextetIndex:
                         return GetParameter(sextet.QuadrupolShift, sextet.QuadrupolShiftError, velocityStep, 3, _parametersFormatInfo);
                    case HyperfineFieldSextetIndex:
                         return GetParameter(sextet.HyperfineField, sextet.HyperfineFieldError, hyperfineFieldError, 1, _hypFieldFormatInfo);
                    case RelativeAreaSextetIndex:
                         return GetParameter(sextet.RelativeArea, null /*sextet.RelativeAreaError*/, 0, 2, _areaFormatInfo, false);
                }
            }
            else if (component is Doublet)
            {
                Doublet doublet = component as Doublet;
                switch (index)
                {
                    case LineWidthDoubletIndex:
                         return GetParameter(doublet.LineWidth, doublet.LineWidthError, 2 * velocityStep, 3, _parametersFormatInfo);
                    case IsomerShiftDoubletIndex:
                         return GetParameter(doublet.IsomerShift, doublet.IsomerShiftError, velocityStep, 3, _parametersFormatInfo);
                    case QuadrupolSplittingDoubletIndex:
                         return GetParameter(doublet.QuadrupolSplitting, doublet.QuadrupolSplittingError, velocityStep, 3, _parametersFormatInfo);
                    case RelativeAreaDoubletIndex:
                         if (!mixedComponents)
                             return GetParameter(doublet.RelativeArea, null /*sextet.RelativeAreaError*/, 0, 2, _areaFormatInfo, false);
                         break;
                    case RelativeAreaSextetIndex:
                         if (mixedComponents)
                             return GetParameter(doublet.RelativeArea, null /*sextet.RelativeAreaError*/, 0, 2, _areaFormatInfo, false);
                         break;
                }
            }
            else throw new InvalidOperationException("component can't be only Doublet or Sextet");
            return String.Empty;
        }

        private String GetParameter(Decimal value, Decimal? error, Decimal comparator, Int32 round, NumberFormatInfo format, Boolean appendRemark = true)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Decimal.Round(value, round).ToString(format));
            if (error != null)
            {
                builder.Append("±");
                Decimal errorValue = error > comparator ? error.Value : comparator;
                builder.Append(Decimal.Round(errorValue, round).ToString(format));
            }
            else if(appendRemark)
            {
                builder.Append("*");
                _remarkPresent = true;
            }
            return builder.ToString();
        }

        private Table CreateDocTable(Int32 rows, Int32 columns)
        {
            //_msWordApplication.Visible = true;
            _msWordDocument = _msWordApplication.Documents.Add(); // without template, create no template and others ...
            // creating bookmark
            Object missing = System.Reflection.Missing.Value;
            // ReSharper disable UseIndexedProperty
            Object range = _msWordDocument.Bookmarks.get_Item(ref _endOfDoc).Range; //go to end of the page
            
            Paragraph paragraph = _msWordDocument.Content.Paragraphs.Add(ref range); //add paragraph at end of document
            paragraph.Range.Font.Name = "Times New Roman";
            paragraph.Range.Font.Size = 12.0F;
            paragraph.Range.Text = "Table N. Mössbauer parameters for XXXXXX samples."; //add some text in paragraph
            paragraph.Format.SpaceAfter = 10; //define some style
            paragraph.Range.InsertParagraphAfter(); //insert paragraph
            Range wordRange = _msWordDocument.Bookmarks.get_Item(ref _endOfDoc).Range;
            // ReSharper restore UseIndexedProperty
            // creating table
            Table table = _msWordDocument.Tables.Add(wordRange, rows, columns, ref missing, ref missing);
            SetTableStyle(table);
            return table;
        }

        private void SetTableStyle(Table table)
        {
            table.Borders.InsideLineStyle = WdLineStyle.wdLineStyleSingle;
            table.Borders.OutsideLineStyle = WdLineStyle.wdLineStyleSingle;
            table.Range.Font.Name = "Times New Roman";
            table.Range.Font.Size = 12.0F;
            table.Range.Font.Bold = 0;
            table.Range.ParagraphFormat.Alignment = WdParagraphAlignment.wdAlignParagraphCenter;
            table.AutoFitBehavior(WdAutoFitBehavior.wdAutoFitContent);
            table.AllowAutoFit = true;
        }

        private void AddRemark()
        {
            if (_remarkPresent)
            {
                // ReSharper disable once UseIndexedProperty
                Object range = _msWordDocument.Bookmarks.get_Item(ref _endOfDoc).Range; //go to end of the page

                Paragraph paragraph = _msWordDocument.Content.Paragraphs.Add(ref range); //add paragraph at end of document
                paragraph.Range.Font.Name = "Times New Roman";
                paragraph.Range.Font.Size = 12.0F;
                paragraph.Range.Text = "* - Error was not defined"; //add some text in paragraph
                paragraph.Format.SpaceAfter = 10; //define some style
                paragraph.Range.InsertParagraphAfter(); //insert paragraph
                _remarkPresent = false;
            }
        }

        protected virtual void SpectrumFitProcessedHandler(String processedFit)
        {
            EventHandler<ProcessedSpectrumFitEventArgs> handler = SpectrumFitProcessed;
            if (handler != null)
                handler(this, new ProcessedSpectrumFitEventArgs(processedFit));
        }

        public event EventHandler<ProcessedSpectrumFitEventArgs> SpectrumFitProcessed;

        private const Int32 LineWidthSextetIndex = 2;
        private const Int32 IsomerShiftSextetIndex = 3;
        private const Int32 QuadrupolSplittingSextetIndex = 4;
        private const Int32 HyperfineFieldSextetIndex = 5;
        private const Int32 RelativeAreaSextetIndex = 6;
        private const Int32 ChiSquareValueSextetIndex = 7;
        private const Int32 ComponentNameSextetIndex = 8;

        private const Int32 LineWidthDoubletIndex = 2;
        private const Int32 IsomerShiftDoubletIndex = 3;
        private const Int32 QuadrupolSplittingDoubletIndex = 4;
        private const Int32 RelativeAreaDoubletIndex = 5;
        //private const Int32 ChiSquareValueDoubletIndex = 6;
        private const Int32 ComponentNameDoubletIndex = 7;

        private readonly NumberFormatInfo _parametersFormatInfo = new NumberFormatInfo();
        private readonly NumberFormatInfo _hypFieldFormatInfo = new NumberFormatInfo();
        private readonly NumberFormatInfo _areaFormatInfo = new NumberFormatInfo();

        private readonly _Application _msWordApplication = new Application();
        private Object _endOfDoc = "\\endofdoc";
        private _Document _msWordDocument;
        private readonly IList<String> _tableHeaderMixedCompEn = new List<String>()
        {
            "Sample", "Γ, mm/s", "δ, mm/s", "2έ, mm/s", "Heff, kOe", "A, %", "χ2", "Component"
        };

        private readonly IList<String> _tableHeaderDoubletsOnlyEn = new List<String>()
        {
            "Sample", "Γ, mm/s", "δ, mm/s", "2έ, mm/s", "A, %", "χ2", "Component"
        };

        private Boolean _remarkPresent;
    }
}
