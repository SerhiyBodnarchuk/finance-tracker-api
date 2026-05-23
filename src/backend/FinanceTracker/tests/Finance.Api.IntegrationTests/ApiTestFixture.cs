using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Finance.Api.IntegrationTests;

public sealed class ApiTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tells Program.cs to skip UseHttpsRedirection so that the in-memory
        // test server's plain HTTP requests don't get 307'd into the void.
        builder.UseEnvironment("Testing");
    }

    public HttpClient CreateConfiguredClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }
}
