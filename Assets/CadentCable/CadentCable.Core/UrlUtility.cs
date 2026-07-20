#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CadentCable.Core
{
    public static class UrlUtility
    {
        public static Uri JoinUrl(string serverUrl, string path)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new ArgumentException("Server URL is required.", nameof(serverUrl));
            }

            UriBuilder builder = CreateDirectoryBuilder(serverUrl);
            builder.Path = AppendPath(builder.Path, path);
            builder.Query = string.Empty;
            return builder.Uri;
        }

        public static Uri BuildWebSocketUrl(
            string serverUrl,
            string path,
            IReadOnlyDictionary<string, object?>? parameters = null)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new ArgumentException("Server URL is required.", nameof(serverUrl));
            }

            UriBuilder builder = CreateDirectoryBuilder(serverUrl);
            builder.Path = AppendPath(builder.Path, path);
            builder.Query = BuildQuery(parameters);

            string scheme = builder.Scheme.ToLowerInvariant();
            if (scheme == Uri.UriSchemeHttp)
            {
                builder.Scheme = "ws";
            }
            else if (scheme == Uri.UriSchemeHttps)
            {
                builder.Scheme = "wss";
            }
            else if (scheme != "ws" && scheme != "wss")
            {
                builder.Scheme = "ws";
            }

            return builder.Uri;
        }

        private static UriBuilder CreateDirectoryBuilder(string serverUrl)
        {
            Uri uri = new Uri(serverUrl, UriKind.Absolute);
            UriBuilder builder = new UriBuilder(uri)
            {
                Query = string.Empty,
                Fragment = string.Empty,
            };

            if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
            {
                builder.Path += "/";
            }

            return builder;
        }

        private static string AppendPath(string basePath, string path)
        {
            string normalizedPath = (path ?? string.Empty).TrimStart('/');
            return basePath + normalizedPath;
        }

        private static string BuildQuery(IReadOnlyDictionary<string, object?>? parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, object?> pair in parameters)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                string value = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                if (value.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(pair.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(value));
            }

            return builder.ToString();
        }
    }
}
