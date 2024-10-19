using Amazon.Lambda.Serialization.SystemTextJson;
using LambdaApi;
using LambdaApi.Dao;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.TypeInfoResolver = ApiSerializerContext.Default;
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, new SourceGeneratorLambdaJsonSerializer<ApiSerializerContext>());

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(opts =>
{
    opts.IncludeScopes = true;
    opts.UseUtcTimestamp = true;
    opts.TimestampFormat = "hh:mm:ss ";
});

string corsOrigins = builder.Configuration.GetValue<string>("CorsOrigins") ?? "*";
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(builder => builder.WithOrigins(corsOrigins.Split(",", StringSplitOptions.TrimEntries)))
);

var app = builder.Build();
app.UseCors();

app.MapGet("/hello", async (HttpContext ctx) => { ctx.Response.StatusCode = 200; await ctx.Response.WriteAsync("Hello World"); });
app.MapPost("/upload", new Handler(app.Configuration, app.Logger).Upload);

await app.RunAsync();
