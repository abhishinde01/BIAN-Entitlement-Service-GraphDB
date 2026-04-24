using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using EntitlementService.Graph;
using Neo4j.Driver;

var builder = WebApplication.CreateBuilder(args);
var keyVaultUri = builder.Configuration["KeyVault:Uri"];

if (!string.IsNullOrEmpty(keyVaultUri))
{
    TokenCredential credential;

    if (builder.Environment.IsDevelopment())
    {
        var tenantId = builder.Configuration["AzureAd:TenantId"];
        var clientId = builder.Configuration["AzureAd:ClientId"];
        var clientSecret = builder.Configuration["AzureAd:ClientSecret"];

        credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    }
    else
    {
        credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
    }

    builder.Configuration.AddAzureKeyVault(
        new SecretClient(new Uri(keyVaultUri), credential),
        new KeyVaultSecretManager()
    );
}

builder.Services.AddSingleton<IDriver>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var uri = config["Neo4j:Uri"]!;
    var username = config["Neo4j:Username"]!;
    var password = config["Neo4j:Password"]!;
    return GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
});

builder.Services.AddSingleton<IGraphService, Neo4jGraphService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BIAN Entitlement Service", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BIAN Entitlement Service v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
