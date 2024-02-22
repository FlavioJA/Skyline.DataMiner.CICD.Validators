﻿namespace Skyline.DataMiner.CICD.Tools.Validator.OutputWriters
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    using Microsoft.Extensions.Logging;

    using Skyline.DataMiner.CICD.Tools.Validator.OutputWriters.HtmlWriter;
    using Skyline.DataMiner.CICD.Validators.Common.Model;

    internal class ResultWriterHtml : IResultWriter
    {
        private readonly string resultsFilePath;
        private readonly ILogger logger;
        private readonly bool includeSuppressed;

        public ResultWriterHtml(string resultsFilePath, ILogger logger, bool includeSuppressed)
        {
            this.resultsFilePath = resultsFilePath;
            this.logger = logger;
            this.includeSuppressed = includeSuppressed;
        }

        public void WriteResults(ValidatorResults validatorResults)
        {
            logger.LogInformation("  Writing results to " + resultsFilePath + "...");
            var templateStart = Resources.validatorResultsTemplateStart;
            templateStart = templateStart.Replace("$protocolName$", validatorResults.Protocol);
            templateStart = templateStart.Replace("$protocolVersion$", validatorResults.Version);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(templateStart);
            stringBuilder.AppendFormat("<h1>{0} v{1}</h1>", validatorResults.Protocol, validatorResults.Version);
            stringBuilder.Append("    <table id=\"resultstable\">\r\n        <tr>\r\n            <th>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;Description</th>\r\n            <th>State</th>\r\n            <th>Certainty</th>\r\n            <th>Fix Impact</th>\r\n            <th>Category</th>\r\n            <th>Code</th>\r\n            <th>Line</th>\r\n            <th>Column</th>\r\n            <th>DVE</th>\r\n        </tr>");
            var criticalIssues = new List<ValidatorResult>();
            var majorIssues = new List<ValidatorResult>();
            var minorIssues = new List<ValidatorResult>();
            var warningIssues = new List<ValidatorResult>();

            foreach (var result in validatorResults.Issues)
            {
                switch (result.Severity)
                {
                    case Severity.Critical:
                        criticalIssues.Add(result);
                        break;
                    case Severity.Major:
                        majorIssues.Add(result);
                        break;
                    case Severity.Minor:
                        minorIssues.Add(result);
                        break;
                    case Severity.Warning:
                        warningIssues.Add(result);
                        break;
                }
            }

            AddProtocolLine(validatorResults, stringBuilder);
            AddCategoryLine("Critical", criticalIssues.Count, validatorResults.CriticalIssueCount, validatorResults.SuppressedCriticalIssueCount, stringBuilder);
            var convertedCriticalResults = ConvertResults(criticalIssues);
            WriteItems(convertedCriticalResults, stringBuilder, includeSuppressed);

            AddCategoryLine("Major", majorIssues.Count, validatorResults.MajorIssueCount, validatorResults.SuppressedMajorIssueCount, stringBuilder);
            var convertedMajorResults = ConvertResults(majorIssues);
            WriteItems(convertedMajorResults, stringBuilder, includeSuppressed);

            AddCategoryLine("Minor", minorIssues.Count, validatorResults.MinorIssueCount, validatorResults.SuppressedMinorIssueCount, stringBuilder);
            var convertedMinorResults = ConvertResults(minorIssues);
            WriteItems(convertedMinorResults, stringBuilder, includeSuppressed);

            AddCategoryLine("Warning", warningIssues.Count, validatorResults.WarningIssueCount, validatorResults.SuppressedWarningIssueCount, stringBuilder);
            var convertedWarningResults = ConvertResults(warningIssues);
            WriteItems(convertedWarningResults, stringBuilder, includeSuppressed);

            var templateEnd = Resources.validatorResultsTemplateEnd;
            stringBuilder.Append(templateEnd);

            stringBuilder.AppendFormat("{0}<footer>Generated by <a href=\"https://github.com/SkylineCommunications/Skyline.DataMiner.CICD.Validators\" target=\"_blank\">Skyline.DataMiner.CICD.Tools.Validator</a> v{1} at {2}.</footer>{0}</body>{0}</html>", Environment.NewLine, validatorResults.ValidatorVersion, validatorResults.ValidationTimeStamp);

            File.WriteAllText(resultsFilePath, stringBuilder.ToString());
        }

        private void WriteItems(List<ValidatorResultTreeItem> convertedResults, StringBuilder stringBuilder, bool includeSuppressed)
        {
            foreach (var item in convertedResults)
            {
                item.WriteHtml(stringBuilder, includeSuppressed);
            }
        }

        private static List<ValidatorResultTreeItem> ConvertResults(IList<ValidatorResult> results)
        {
            List<ValidatorResultTreeItem> treeItems = new List<ValidatorResultTreeItem>();

            foreach (var result in results)
            {
                if (result?.SubResults?.Count > 0)
                {
                    var validatorResult = CreateTreeNode(result);
                    AddSubResults(validatorResult, result);
                    validatorResult.UpdateCounts();
                    treeItems.Add(validatorResult);
                }
                else
                {
                    var validatorResult = CreateTreeLeaf(result);
                    treeItems.Add(validatorResult);
                }
            }

            return treeItems;
        }

        private static ValidatorResultTreeLeaf CreateTreeLeaf(ValidatorResult validatorResult)
        {
            return new ValidatorResultTreeLeaf(validatorResult);
        }

        private static ValidatorResultTreeNode CreateTreeNode(ValidatorResult validatorResult)
        {
            return new ValidatorResultTreeNode(validatorResult);
        }

        private static void AddSubResults(ValidatorResultTreeNode validatorResult, ValidatorResult result)
        {
            foreach (var subresult in result.SubResults)
            {
                if (subresult.SubResults?.Count > 0)
                {
                    var node = CreateTreeNode(subresult);
                    validatorResult.SubResults.Add(node);
                    AddSubResults(node, subresult);
                    node.UpdateCounts();
                }
                else
                {
                    var leaf = CreateTreeLeaf(subresult);
                    validatorResult.SubResults.Add(leaf);
                }
            }
        }

        private void AddProtocolLine(ValidatorResults validatorResults, StringBuilder stringBuilder)
        {
            int totalActive = validatorResults.CriticalIssueCount + validatorResults.MajorIssueCount + validatorResults.MinorIssueCount + validatorResults.WarningIssueCount;
            int totalSuppressed = validatorResults.SuppressedCriticalIssueCount + validatorResults.SuppressedMajorIssueCount + validatorResults.SuppressedMinorIssueCount + validatorResults.SuppressedWarningIssueCount;

            stringBuilder.AppendFormat("        <tr data-depth=\"0\" class=\"collapse level0\">{0}            <td>", Environment.NewLine);
            stringBuilder.AppendFormat("<span class=\"toggle collapse\"></span>&nbsp;<span>&nbsp;</span>&nbsp;{0} v{1} ", validatorResults.Protocol, validatorResults.Version);

            if(includeSuppressed)
            {
                stringBuilder.AppendFormat("({0} active, {1} suppressed)", totalActive, totalSuppressed);
            }
            else
            {
                stringBuilder.AppendFormat("({0} active)", totalActive);
            }

            stringBuilder.AppendFormat("</td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}        </tr>", Environment.NewLine);
        }

        private void AddCategoryLine(string category, int childIssueCount, int activeCount, int suppressedCount, StringBuilder stringBuilder)
        {
            stringBuilder.AppendFormat("        <tr data-depth=\"1\" class=\"collapse level1\">{0}            <td>", Environment.NewLine);

            if (childIssueCount > 0)
            {
                stringBuilder.Append("<span class=\"toggle collapse\"></span>");
            }
            else
            {
                stringBuilder.Append("<span class=\"notoggle\"></span>");
            }

            stringBuilder.AppendFormat("&nbsp;<span class=\"{0}\" >&nbsp;</span>&nbsp;{1} ", category.ToLower(), category);

            if (includeSuppressed)
            {
                stringBuilder.AppendFormat("({0} active, {1} suppressed)", activeCount, suppressedCount);
            }
            else
            {
                stringBuilder.AppendFormat("({0} active)", activeCount);
            }

            stringBuilder.AppendFormat("</td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}            <td></td>{0}        </tr>", Environment.NewLine);
        }
    }
}