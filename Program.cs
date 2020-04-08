using System.Globalization;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GGNet.Site
{
    using Data;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            CultureInfo cultureInfo = new CultureInfo("en-GB");
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            builder.Services.AddBaseAddressHttpClient();
            builder.Services.AddScoped<Covid19Service>();

            await builder.Build().RunAsync();
        }
    }
}
