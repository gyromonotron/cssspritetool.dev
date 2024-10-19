using Amazon.Lambda.APIGatewayEvents;
using LambdaApi.Model;
using System.Text.Json.Serialization;


namespace LambdaApi;

[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(SpriteResponse))]
public partial class ApiSerializerContext : JsonSerializerContext
{
}
