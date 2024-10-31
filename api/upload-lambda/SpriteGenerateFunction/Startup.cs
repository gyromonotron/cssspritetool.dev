using Amazon.Lambda.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SpriteGenerateFunction
{
    [LambdaStartup]
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var builder = new ConfigurationBuilder()
                                .AddJsonFile("appsettings.json", true)
                                .AddEnvironmentVariables();

            var configuration = builder.Build();
            services.AddSingleton<IConfiguration>(configuration);
        }
    }
}
