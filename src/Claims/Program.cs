using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Claims
{
    internal static class Program
    {
        private const int BodyLimit = 2048;
        private static readonly Dictionary<string, string> WebAssets = new Dictionary<string, string>
        {
            { "/", LoadAsset("index.html") },
            { "/styles.css", LoadAsset("styles.css") },
            { "/app.js", LoadAsset("app.js") }
        };

        private static string LoadAsset(string name)
        {
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("Claims.Web." + name)
                ?? throw new FileNotFoundException("Missing web asset: " + name))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        private static void Main()
        {
            var prefix = Environment.GetEnvironmentVariable("CLAIMS_PREFIX") ?? "http://localhost:8080/";
            using (var server = new HttpListener())
            {
                server.Prefixes.Add(prefix);
                server.Start();
                Console.WriteLine("Claims analysis listening on " + prefix);
                while (true)
                {
                    var context = server.GetContext();
                    Task.Run(() => Handle(context));
                }
            }
        }

        private static void Handle(HttpListenerContext ctx)
        {
            var json = new JavaScriptSerializer { MaxJsonLength = BodyLimit, RecursionLimit = 4 };
            try
            {
                var req = ctx.Request;
                string path = req.Url.AbsolutePath;
                string asset;
                if (path == "/healthz" && req.HttpMethod == "GET")
                    Send(ctx, 200, json.Serialize(new { status = "ok" }));
                else if (path == "/version" && req.HttpMethod == "GET")
                    Send(ctx, 200, json.Serialize(new { release = Environment.GetEnvironmentVariable("CLAIMS_RELEASE") ?? "local" }));
                else if (req.HttpMethod == "GET" && WebAssets.TryGetValue(path, out asset))
                    Send(ctx, 200, asset, path == "/" ? "text/html" : path == "/styles.css" ? "text/css" : "text/javascript");
                else if (path != "/claims/analyze") Send(ctx, 404, "{\"error\":\"Not found\"}");
                else if (req.HttpMethod != "POST")
                {
                    ctx.Response.Headers["Allow"] = "POST";
                    Send(ctx, 405, "{\"error\":\"Use POST\"}");
                }
                else if (!string.Equals((req.ContentType ?? "").Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
                    Send(ctx, 415, "{\"error\":\"Use application/json\"}");
                else if (req.ContentLength64 > BodyLimit)
                    Send(ctx, 413, "{\"error\":\"Body too large\"}");
                else
                {
                    byte[] body = new byte[BodyLimit + 1];
                    int count = 0, read;
                    while (count < body.Length && (read = req.InputStream.Read(body, count, body.Length - count)) > 0)
                        count += read;
                    if (count > BodyLimit) Send(ctx, 413, "{\"error\":\"Body too large\"}");
                    else
                    {
                        // Never use a type resolver for untrusted JSON.
                        var input = json.DeserializeObject(new UTF8Encoding(false, true).GetString(body, 0, count)) as IDictionary<string, object>;
                        Send(ctx, 200, json.Serialize(ClaimAnalysis.Analyze(input)));
                    }
                }
            }
            catch (ArgumentException) { Send(ctx, 400, "{\"error\":\"Invalid claim; check the input contract\"}"); }
            catch (InvalidOperationException) { Send(ctx, 400, "{\"error\":\"Invalid JSON\"}"); }
            catch (IOException) { Console.Error.WriteLine("Connection interrupted."); }
            catch (HttpListenerException) { Console.Error.WriteLine("HTTP connection interrupted."); }
            finally { ctx.Response.Close(); }
        }

        private static void Send(HttpListenerContext ctx, int status, string content, string contentType = "application/json")
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType + "; charset=utf-8";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src data:; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
    }
}
