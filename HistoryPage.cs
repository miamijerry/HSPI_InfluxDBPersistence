﻿using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models.Responses;
using Scheduler;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;

namespace Hspi
{
    using static System.FormattableString;

    internal partial class ConfigPage : PageBuilderAndMenu.clsPageBuilder
    {
        private static string FirstCharToUpper(string input)
        {
            switch (input)
            {
                case null: return null;
                case "": return string.Empty;
                default: return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }

        private string BuildHistoryPage(NameValueCollection parts, DevicePersistenceData data)
        {
            StringBuilder stb = new StringBuilder();
            IncludeResourceCSS(stb, "jquery.dataTables.css");
            IncludeResourceScript(stb, "jquery.dataTables.min.js");

            HSHelper hsHelper = new HSHelper(HS);

            string header = Invariant($"History - {hsHelper.GetName(data.DeviceRefId)}");

            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("ftmDeviceHistory", "IdHistory", "Post"));
            stb.Append(@"<div>");

            stb.Append(@"<table class='full_width_table'>");

            stb.Append(Invariant($"<tr><td class='tableheader'>{header}</td></tr>"));
            var queries = GetDefaultValueQueries(data);
            string querySelection = parts[QueryPartId];
            if (string.IsNullOrWhiteSpace(querySelection))
            {
                querySelection = queries?.FirstOrDefault().Key;
            }

            NameValueCollection collection = new NameValueCollection();
            foreach (var query in queries)
            {
                collection.Add(query.Key, query.Key);
            }

            stb.Append(Invariant($"<tr><td>{FormDropDown(HistoryQueryTypeId, collection, querySelection, 250, string.Empty, true)}</td></tr>"));

            string finalQuery = Invariant(queries[querySelection]);
            stb.Append(Invariant($"<tr height='10'><td>{HtmlTextBox(PersistenceId, data.Id.ToString(), @type: "hidden")}</td></tr>"));
            stb.Append("<tr><td>");
            stb.Append(DivStart(QueryTestDivId, string.Empty));
            stb.Append(Invariant($"{TextArea(QueryTestId, finalQuery)}"));
            stb.Append(DivEnd());
            stb.Append(Invariant($"&nbsp;{FormButton(HistoryRunQueryButtonName, "Run Query", "Run Query")}</td></tr>"));
            stb.Append("<tr height='5'><td></td></tr>");
            stb.Append(Invariant($"<tr><td class='tableheader'>Results</td></tr>"));
            stb.Append("<tr><td>");
            stb.Append(DivStart(HistoryResultDivId, string.Empty));
            BuildTable(GetData(finalQuery), stb);
            stb.Append(DivEnd());
            stb.Append("</td><tr>");
            stb.Append("</table>");
            stb.Append(@"</div>");
            stb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

            return stb.ToString();
        }

        private void BuildTable(IEnumerable<Serie> data, StringBuilder stb)
        {
            var queryData = data.ToArray();

            if (queryData.Length > 0)
            {
                int columns = queryData[0].Columns.Count;

                stb.Append("<table id=\"results\" class=\"display compact\" style=\"width:100%\">");
                stb.Append(@"<thead><tr>");
                foreach (var column in queryData[0].Columns)
                {
                    stb.Append(Invariant($"<th>{ HttpUtility.HtmlEncode(FirstCharToUpper(column))}</th>"));
                }
                stb.Append(@"</tr></thead>");
                stb.Append(@"<tbody>");

                string dateTimePattern = CultureInfo.CurrentUICulture.DateTimeFormat.LongDatePattern +
                                 " " + CultureInfo.CurrentUICulture.DateTimeFormat.LongTimePattern;

                foreach (var row in queryData[0].Values)
                {
                    stb.Append(@"<tr>");
                    foreach (var column in row)
                    {
                        string value = string.Empty;
                        string sortValue = null;
                        switch (column)
                        {
                            case DateTime dateColumn:
                                value = dateColumn.ToString(dateTimePattern, CultureInfo.CurrentUICulture);
                                sortValue = dateColumn.Ticks.ToString();
                                break;

                            case null:
                                break;

                            default:
                                value = column.ToString();
                                break;
                        }

                        if (sortValue != null)
                        {
                            stb.Append(Invariant($"<td data-order='{HttpUtility.HtmlEncode(sortValue)}'>{ HttpUtility.HtmlEncode(value)}</td>"));
                        }
                        else
                        {
                            stb.Append(Invariant($"<td>{HttpUtility.HtmlEncode(value)}</td>"));
                        }
                    }
                    stb.Append(@"</tr>");
                }
                stb.Append(@"</tbody>");
                stb.Append(@"</table>");

                stb.AppendLine("<script type='text/javascript'>");
                stb.AppendLine(@"$(document).ready(function() {");
                stb.AppendLine(@"$('#results').DataTable({
                                       'pageLength':25,
                                        'columnDefs': [
                                            { 'className': 'dt-left', 'targets': '_all'}
                                        ]
                                    });
                                });");
                stb.AppendLine("</script>");
            }
        }

        private IEnumerable<Serie> GetData(string query)
        {
            var loginInformation = pluginConfig.DBLoginInformation;
            var influxDbClient = new InfluxDbClient(loginInformation.DBUri.ToString(), loginInformation.User, loginInformation.Password, InfluxDbVersion.v_1_3);
            return influxDbClient.Client.QueryAsync(query, loginInformation.DB).Result;
        }

        private IDictionary<string, FormattableString> GetDefaultValueQueries(DevicePersistenceData data)
        {
            return new Dictionary<string, FormattableString>()
            {
                {"Last 100 stored values", $"SELECT {data.Field} from {data.Measurement} WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' ORDER BY time DESC LIMIT 100" },
                {"Last 100 stored distinct values", $"SELECT DISTINCT({data.Field}) from {data.Measurement} WHERE {PluginConfig.DeviceRefIdTag}='{data.DeviceRefId}' ORDER BY time DESC LIMIT 100" },
            };
        }

        private void HandleHistoryPagePostBack(NameValueCollection parts, string form)
        {
            string finalQuery = null;
            if (form == NameToIdWithPrefix(HistoryQueryTypeId))
            {
                var queryType = parts[HistoryQueryTypeId];
                var id = parts[PersistenceId];
                var data = pluginConfig.DevicePersistenceData[id];
                var queries = GetDefaultValueQueries(data);

                finalQuery = Invariant(queries[queryType]);
                this.divToUpdate.Add(QueryTestDivId, TextArea(QueryTestId, finalQuery));
            }
            else if (form == NameToIdWithPrefix(HistoryRunQueryButtonName))
            {
                finalQuery = parts[QueryTestId];
            }

            StringBuilder stb = new StringBuilder();
            BuildTable(GetData(finalQuery), stb);
            this.divToUpdate.Add(HistoryResultDivId, stb.ToString());
        }

        private void IncludeResourceCSS(StringBuilder stb, string scriptFile)
        {
            stb.AppendLine("<style type=\"text/css\">");
            stb.AppendLine(Resource.ResourceManager.GetString(scriptFile.Replace('.', '_'), Resource.Culture));
            stb.AppendLine("</style>");
            this.AddScript(stb.ToString());
        }

        private void IncludeResourceScript(StringBuilder stb, string scriptFile)
        {
            stb.AppendLine("<script type='text/javascript'>");
            stb.AppendLine(Resource.ResourceManager.GetString(scriptFile.Replace('.', '_'), Resource.Culture));
            stb.AppendLine("</script>");
            this.AddScript(stb.ToString());
        }

        private const string HistoryQueryTypeId = "historyquerytypeid";
        private const string HistoryResultDivId = "historyresultdivid";
        private const string HistoryRunQueryButtonName = "historyrunquery";
        private const string QueryPartId = "querypart";
        private const string QueryTestDivId = "querytestdivid";
        private const string QueryTestId = "querytextid";
    }
}