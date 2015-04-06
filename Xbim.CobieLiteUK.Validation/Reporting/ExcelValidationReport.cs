﻿using System;
using System.Data;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using Xbim.COBieLiteUK;
using Xbim.Common.Logging;

namespace Xbim.CobieLiteUK.Validation.Reporting
{
    /// <summary>
    /// Can create an Excel report containing summary and detailed reports on provided and missing information.
    /// Use the Create function to produce the report.
    /// </summary>
    public class ExcelValidationReport
    {
        internal static readonly ILogger Logger = LoggerFactory.GetLogger();

        /// <summary>
        /// Determines the format to be saved.
        /// </summary>
        public enum SpreadSheetFormat
        {
            /// <summary>
            /// Excel Binary File Format
            /// </summary>
            Xls,
            /// <summary>
            /// Excel xml File Format
            /// </summary>
            Xlsx
        }

        /// <summary>
        /// Creates the report in file format
        /// </summary>
        /// <param name="facility">the result of a DPoW validation to be transformed into report form.</param>
        /// <param name="suggestedFilename">target file for the spreadsheet (warning, the extension is automatically determined depending on the format)</param>
        /// <param name="format">determines the excel format to use</param>
        /// <returns>true if successful, errors are cought and passed to Logger</returns>
        public bool Create(Facility facility, string suggestedFilename, SpreadSheetFormat format)
        {
            var ssFileName = Path.ChangeExtension(suggestedFilename, format == SpreadSheetFormat.Xlsx ? "xlsx" : "xls");
            try
            {
                using (var spreadsheetStream = new FileStream(ssFileName, FileMode.Create, FileAccess.Write))
                {
                    return Create(facility, spreadsheetStream, format);
                }
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("Failed to save {0}, {1}", ssFileName, e.Message);
                return false;
            }
        }

        /// <summary>
        /// Creates the report.
        /// </summary>
        /// <param name="facility">the result of a DPoW validation to be transformed into report form.</param>
        /// <param name="destinationStream">target stream for the spreadsheet</param>
        /// <param name="format">determines the excel format to use</param>
        /// <returns>true if successful, errors are cought and passed to Logger</returns>
        public bool Create(Facility facility, Stream destinationStream, SpreadSheetFormat format)
        {
            var workBook = format == SpreadSheetFormat.Xlsx
                ? (IWorkbook)new XSSFWorkbook()
                : (IWorkbook)new HSSFWorkbook();
            

            var facReport = new FacilityReport(facility);

            var summaryPage = workBook.CreateSheet("Summary");
            if (!CreateSummarySheet(summaryPage, facility)) 
                return false;
            var iRunningWorkBook = 1;

            // ReSharper disable once LoopCanBeConvertedToQuery // might restore once code is stable
            foreach (var assetType in facReport.RequirementGroups)
            {
                // only report items with any assets submitted (a different report should probably be provided otherwise)

                //if (assetType.GetSubmittedAssetsCount() < 1)
                //    continue;

                var validName = WorkbookUtil.CreateSafeSheetName(string.Format(@"{0} {1}", iRunningWorkBook++, assetType.Name));

                var detailPage = workBook.CreateSheet(validName);
                if (!CreateDetailSheet(detailPage, assetType))
                    return false;
            }

            try
            {
                workBook.Write(destinationStream);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("Failed to stream excel report: {1}", e.Message);
                return false;
            }

            return true;
        }

        private static bool CreateDetailSheet(ISheet detailSheet, AssetTypeRequirementPointer requirementPointer)
        {
            try
            {
                var excelRow = detailSheet.GetRow(0) ?? detailSheet.CreateRow(0);
                var excelCell = excelRow.GetCell(0) ?? excelRow.CreateCell(0);
                excelCell.SetCellValue("Asset type requirement report");

                var rep = new AssetTypeDetailedGridReport(requirementPointer);
                rep.PrepareReport();


                var iRunningRow = 2;
                var iRunningColumn = 0;
                excelRow = detailSheet.GetRow(iRunningRow++) ?? detailSheet.CreateRow(iRunningRow - 1); // prepares a row and moves index forward
                (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(@"Name:"); // writes cell and moves index forward
                (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(requirementPointer.Name); // writes cell and moves index forward

                iRunningColumn = 0;
                excelRow = detailSheet.GetRow(iRunningRow++) ?? detailSheet.CreateRow(iRunningRow - 1); // prepares a row and moves index forward
                (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(@"External system:"); // writes cell and moves index forward
                (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(requirementPointer.ExternalSystem); // writes cell and moves index forward

                iRunningColumn = 0;
                excelRow = detailSheet.GetRow(iRunningRow++) ?? detailSheet.CreateRow(iRunningRow - 1); // prepares a row and moves index forward
                (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(@"External id:"); // writes cell and moves index forward
                (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(requirementPointer.ExternalId); // writes cell and moves index forward

                iRunningRow++; // one empty row

                iRunningColumn = 0;
                excelRow = detailSheet.GetRow(iRunningRow++) ?? detailSheet.CreateRow(iRunningRow - 1); // prepares a row and moves index forward
                (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(@"Matching categories:"); // writes cell and moves index forward

                foreach (var cat in rep.RequirementCategories)
                {
                    iRunningColumn = 0;
                    excelRow = detailSheet.GetRow(iRunningRow++) ?? detailSheet.CreateRow(iRunningRow - 1); // prepares a row and moves index forward
                    (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(cat.Classification); // writes cell and moves index forward
                    (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(cat.Code); // writes cell and moves index forward
                    (excelRow.GetCell(iRunningColumn++) ?? excelRow.CreateCell(iRunningColumn - 1)).SetCellValue(cat.Description); // writes cell and moves index forward
                }

                iRunningRow++; // one empty row
                iRunningColumn = 0;

                var cellStyle = detailSheet.Workbook.CreateCellStyle();
                cellStyle.BorderBottom = BorderStyle.Thick;
                cellStyle.FillPattern = FillPattern.SolidForeground;
                cellStyle.FillForegroundColor = IndexedColors.Grey50Percent.Index;

                var table = rep.AttributesGrid;

                excelRow = detailSheet.GetRow(iRunningRow) ?? detailSheet.CreateRow(iRunningRow);
                foreach (DataColumn tCol in table.Columns)
                {
                    if (tCol.AutoIncrement)
                        continue;
                    excelCell = excelRow.GetCell(iRunningColumn) ?? excelRow.CreateCell(iRunningColumn);
                    iRunningColumn++;
                    excelCell.SetCellValue(tCol.Caption);
                    excelCell.CellStyle = cellStyle;
                }
                iRunningRow++;

                foreach (DataRow row in table.Rows)
                {
                    
                    excelRow = detailSheet.GetRow(iRunningRow) ?? detailSheet.CreateRow(iRunningRow);
                    iRunningRow++;

                    iRunningColumn = -1;
                    foreach (DataColumn tCol in table.Columns)
                    {
                        if (tCol.AutoIncrement)
                            continue;
                        iRunningColumn++;
                        if (row[tCol] == DBNull.Value)
                            continue;
                        excelCell = excelRow.GetCell(iRunningColumn) ?? excelRow.CreateCell(iRunningColumn);
                        
                        switch (tCol.DataType.Name)
                        {
                            case "String":
                                excelCell.SetCellValue((string)row[tCol]);
                                break;
                            case "Int32":
                                excelCell.SetCellValue(Convert.ToInt32(row[tCol]));
                                break;
                            default:
                                excelCell.SetCellValue((string)row[tCol]);
                                break;
                        }
                    }
                }

                // sets all used columns to autosize
                for (var irun = 0; irun < iRunningColumn; irun++)
                {
                    detailSheet.AutoSizeColumn(irun);
                }

                return true;
            }
            catch (Exception e)
            {
                //log the error
                Logger.Error("Failed to create detail Sheet", e);
                return false;
            }
        }

        /// <summary>
        /// sets the Classification preferred for priority purposes.
        /// </summary>
        public string PreferredClassification = "Uniclass2015";

        private bool CreateSummarySheet(ISheet summaryPage, Facility facility)
        {
            try
            {
                var excelRow = summaryPage.GetRow(0) ?? summaryPage.CreateRow(0);  
                var excelCell = excelRow.GetCell(0) ?? excelRow.CreateCell(0);
                excelCell.SetCellValue("Validation Report Summary");
                
                var summaryReport = new AssetTypeSummaryReport(facility.AssetTypes);
                var table = summaryReport.GetReport(PreferredClassification);

                var iRunningRow = 2;
                var iRunningColumn = 0;

                var cellStyle = summaryPage.Workbook.CreateCellStyle();
                cellStyle.BorderBottom = BorderStyle.Thick;
                cellStyle.FillPattern = FillPattern.SolidForeground;
                cellStyle.FillForegroundColor = IndexedColors.Grey50Percent.Index;

                var failCellStyle = summaryPage.Workbook.CreateCellStyle();
                failCellStyle.FillPattern = FillPattern.SolidForeground;
                failCellStyle.FillForegroundColor = IndexedColors.Red.Index;

                excelRow = summaryPage.GetRow(iRunningRow) ?? summaryPage.CreateRow(iRunningRow);
                foreach (DataColumn tCol in table.Columns)
                {
                    if (tCol.AutoIncrement)
                        continue;
                    var runCell = excelRow.GetCell(iRunningColumn) ?? excelRow.CreateCell(iRunningColumn);
                    iRunningColumn++;
                    runCell.SetCellValue(tCol.Caption);
                    runCell.CellStyle = cellStyle;
                }
                

                iRunningRow++;
                foreach (DataRow row in table.Rows)
                {
                    
                    excelRow = summaryPage.GetRow(iRunningRow) ?? summaryPage.CreateRow(iRunningRow);
                    iRunningRow++;
                    iRunningColumn = -1;
                    foreach (DataColumn tCol in table.Columns)
                    {
                        if (tCol.AutoIncrement)
                            continue;
                        iRunningColumn++;
                        if (row[tCol] == DBNull.Value)
                            continue;
                        var runCell = excelRow.GetCell(iRunningColumn) ?? excelRow.CreateCell(iRunningColumn);
                        

                        switch (tCol.DataType.Name)
                        {
                            case "String":
                                runCell.SetCellValue((string)row[tCol]);
                                break;
                            case "Int32":
                                runCell.SetCellValue(Convert.ToInt32(row[tCol]));
                                break;
                            default:
                                runCell.SetCellValue((string)row[tCol]);
                                break;
                        }
                    }
                }

                // sets all used columns to autosize
                for (int irun = 0; irun < iRunningColumn; irun++)
                {
                    summaryPage.AutoSizeColumn(irun);
                }

                return true;
            }
            catch (Exception e)
            {
                //log the error
                Logger.Error("Failed to create Summary Sheet", e);
                return false;
            }
        }
    }
}