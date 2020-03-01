using System.Threading.Tasks;
using Microsoft.AspNetCore.Blazor.Hosting;
using System.Globalization;

namespace GGNet.Site
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-GB");

            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            await builder.Build().RunAsync();
        }
    }
}
