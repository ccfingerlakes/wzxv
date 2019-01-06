using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    static class CsvReader
    {
        const char QuoteChar = '"';
        const char LineFeedChar = '\n';
        const char ReturnChar = '\r';

        public static IEnumerable<string[]> Parse(string csv)
        {
            if (string.IsNullOrEmpty(csv))
                return Array.Empty<string[]>();

            var rows = new List<string[]>();

            foreach(var line in ParseLines(csv))
            {
                if (!string.IsNullOrEmpty(line))
                    rows.Add(ParseFields(line));
            }

            return rows.ToArray();
        }

        public static string[] ParseLines(string csv)
        {
            if (string.IsNullOrEmpty(csv))
                return Array.Empty<string>();

            var rows = new List<string>();
            var withinQuotes = false;
            var lastPos = 0;

            var i = -1;
            var len = csv.Length;
            while (++i < len)
            {
                var c = csv[i];
                if (c == QuoteChar)
                {
                    var isLiteralQuote = i + 1 < len && csv[i + 1] == QuoteChar;
                    if (isLiteralQuote)
                    {
                        i++;
                        continue;
                    }

                    withinQuotes = !withinQuotes;
                }

                if (withinQuotes)
                    continue;

                if (c == LineFeedChar)
                {
                    var str = i > 0 && csv[i - 1] == ReturnChar
                        ? csv.Substring(lastPos, i - lastPos - 1)
                        : csv.Substring(lastPos, i - lastPos);

                    if (str.Length > 0)
                        rows.Add(str);
                    lastPos = i + 1;
                }
            }

            if (i > lastPos)
            {
                var str = csv.Substring(lastPos, i - lastPos);
                if (str.Length > 0)
                    rows.Add(str);
            }

            return rows.ToArray();
        }

        public static string[] ParseFields(string line)
        {
            if (string.IsNullOrEmpty(line))
                return Array.Empty<string>();

            var to = new List<string>();
            var i = -1;
            var len = line.Length;
            while (++i <= len)
            {
                var value = EatValue(line, ref i);
                to.Add(value);
            }

            return to.ToArray();
        }

        public static string EatValue(string value, ref int i)
        {
            var tokenStartPos = i;
            var valueLength = value.Length;
            if (i == valueLength)
                return null;

            var valueChar = value[i];
            const char itemSeperator = ',';

            if (valueChar == itemSeperator)
                return null;

            if (valueChar == QuoteChar) //Is Within Quotes, i.e. "..."
            {
                while (++i < valueLength)
                {
                    valueChar = value[i];

                    if (valueChar != QuoteChar)
                        continue;

                    var isLiteralQuote = i + 1 < valueLength && value[i + 1] == QuoteChar;

                    i++; //skip quote
                    if (!isLiteralQuote)
                        break;
                }

                return value.Substring(tokenStartPos, i - tokenStartPos);
            }

            while (++i < valueLength)
            {
                valueChar = value[i];

                if (valueChar == itemSeperator)
                    break;
            }

            return value.Substring(tokenStartPos, i - tokenStartPos);
        }
    }
}