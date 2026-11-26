using System.Text.Json;
using Claims;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
if (string.IsNullOrEmpty(builder.Configuration["urls"]) && string.IsNullOrEmpty(builder.Configuration["http_ports"]))
    builder.WebHost.UseUrls("http://localhost:8080");
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ClaimsJsonContext.Default));

var app = builder.Build();
var release = app.Configuration["CLAIMS_RELEASE"] ?? "local";

app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.CacheControl = "no-store";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers.ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src data:; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
        return Task.CompletedTask;
    });
    return next(context);
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.MapGet("/healthz", () => new Health("ok"));
app.MapGet("/version", () => new ReleaseInfo(release));
app.Map("/claims/analyze", Analyze);
app.MapFallback("{**path}", () => Error(StatusCodes.Status404NotFound, "Not found"));

app.Run();

static async Task<IResult> Analyze(HttpRequest request)
{
    if (!HttpMethods.IsPost(request.Method))
    {
        request.HttpContext.Response.Headers.Allow = "POST";
        return Error(StatusCodes.Status405MethodNotAllowed, "Use POST");
    }
    if (!string.Equals(request.ContentType?.Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
        return Error(StatusCodes.Status415UnsupportedMediaType, "Use application/json");
    if (request.ContentLength > ClaimAnalysis.BodyLimit)
        return Error(StatusCodes.Status413PayloadTooLarge, "Body too large");

    var body = new byte[ClaimAnalysis.BodyLimit + 1];
    int count = 0, read;
    while (count < body.Length && (read = await request.Body.ReadAsync(body.AsMemory(count), request.HttpContext.RequestAborted)) > 0)
        count += read;
    if (count > ClaimAnalysis.BodyLimit)
        return Error(StatusCodes.Status413PayloadTooLarge, "Body too large");

    try
    {
        using var document = JsonDocument.Parse(body.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 4 });
        return Results.Json(ClaimAnalysis.Analyze(document.RootElement), ClaimsJsonContext.Default.ClaimResult);
    }
    catch (JsonException) { return Error(StatusCodes.Status400BadRequest, "Invalid JSON"); }
    catch (ArgumentException) { return Error(StatusCodes.Status400BadRequest, "Invalid claim; check the input contract"); }
}

static IResult Error(int status, string message) =>
    Results.Json(new ErrorBody(message), ClaimsJsonContext.Default.ErrorBody, statusCode: status);

public partial class Program;
