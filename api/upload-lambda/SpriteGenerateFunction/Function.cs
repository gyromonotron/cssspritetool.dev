using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Configuration;
using SpriteGenerateFunction;
using SpriteGenerateFunction.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: LambdaGlobalProperties(GenerateMain = true)]
[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>))]

namespace SpriteGenerateFunction;

public class Function(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;

    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Post, "/")]
    public async Task<IHttpResult> PostFunctionHandler([FromBody] InputDto body, ILambdaContext context)
    {
        context.Logger.LogInformation("Processing request started.");
        context.Logger.LogInformation($"Request body: {JsonSerializer.Serialize(body, LambdaFunctionJsonSerializerContext.Default.InputDto)}");

        try
        {
            var result = await new Handler(_configuration, context.Logger).ProcessAsync(body);
            return result;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "An error occurred while processing the request.");
            return HttpResults.InternalServerError("Sorry, something went wrong :'(");
        }
    }
}

[JsonSerializable(typeof(APIGatewayProxyRequest))]
[JsonSerializable(typeof(APIGatewayProxyResponse))]
[JsonSerializable(typeof(InputDto))]
[JsonSerializable(typeof(SpriteResponse))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}

public record InputDto(string[] Files, string Format);
