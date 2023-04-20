using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Engine.Configuration;
using Engine.SharePoint;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using System.Security.Cryptography.X509Certificates;

namespace Engine.Utils;

public class AuthUtils
{
    private static X509Certificate2? _cachedCert = null;
    public static async Task<X509Certificate2> RetrieveKeyVaultCertificate(string name, string tenantId, string clientId, string clientSecret, string keyVaultUrl)
    {
        if (_cachedCert == null)
        {
            var client = new SecretClient(vaultUri: new Uri(keyVaultUrl), credential: new ClientSecretCredential(tenantId, clientId, clientSecret));

            var secret = await client.GetSecretAsync(name);

            _cachedCert = new X509Certificate2(Convert.FromBase64String(secret.Value.Value));
        }
        return _cachedCert;

    }
    public async static Task<ClientContext> GetClientContext(string siteUrl, string tenantId, string clientId, string clientSecret, string keyVaultUrl, string baseServerAddress, ILogger tracer)
    {
        return await GetClientContext(siteUrl, tenantId, clientId, clientSecret, keyVaultUrl, baseServerAddress, tracer, null);
    }
    public async static Task<ClientContext> GetClientContext(string siteUrl, string tenantId, string clientId, string clientSecret, string keyVaultUrl, string baseServerAddress, ILogger tracer, Action<AuthenticationResult>? authResultDelegate)
    {
        if (string.IsNullOrEmpty(siteUrl))
        {
            throw new ArgumentException($"'{nameof(siteUrl)}' cannot be null or empty.", nameof(siteUrl));
        }

        if (string.IsNullOrEmpty(tenantId))
        {
            throw new ArgumentException($"'{nameof(tenantId)}' cannot be null or empty.", nameof(tenantId));
        }

        if (string.IsNullOrEmpty(clientId))
        {
            throw new ArgumentException($"'{nameof(clientId)}' cannot be null or empty.", nameof(clientId));
        }

        if (string.IsNullOrEmpty(clientSecret))
        {
            throw new ArgumentException($"'{nameof(clientSecret)}' cannot be null or empty.", nameof(clientSecret));
        }

        if (string.IsNullOrEmpty(keyVaultUrl))
        {
            throw new ArgumentException($"'{nameof(keyVaultUrl)}' cannot be null or empty.", nameof(keyVaultUrl));
        }

        if (string.IsNullOrEmpty(baseServerAddress))
        {
            throw new ArgumentException($"'{nameof(baseServerAddress)}' cannot be null or empty.", nameof(baseServerAddress));
        }

        var app = await GetNewClientApp(tenantId, clientId, clientSecret, keyVaultUrl);
        var result = await app.AuthForSharePointOnline(baseServerAddress);
        if (authResultDelegate != null)
        {
            authResultDelegate(result);
        }

        var ctx = new ClientContext(siteUrl);
        ctx.ExecutingWebRequest += (s, e) =>
        {
            e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + result.AccessToken;
        };

        ctx.Load(ctx.Web);
        await ctx.ExecuteQueryAsyncWithThrottleRetries(tracer);

        return ctx;
    }
    public async static Task<ClientContext> GetClientContext(IConfidentialClientApplication app, string baseServerAddress, string siteUrl, ILogger tracer)
    {
        var result = await app.AuthForSharePointOnline(baseServerAddress);

        var ctx = new ClientContext(siteUrl);
        ctx.ExecutingWebRequest += (s, e) =>
        {
            e.WebRequestExecutor.WebRequest.UserAgent = "NONISV|GitHubSamBetts|SPOColdStorageMigration/1.0";
            e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + result.AccessToken;
        };

        ctx.Load(ctx.Web);
        await ctx.ExecuteQueryAsyncWithThrottleRetries(tracer);

        return ctx;
    }

    public static async Task<IConfidentialClientApplication> GetNewClientApp(string tenantId, string clientId, string clientSecret, string keyVaultUrl)
    {
        var appRegistrationCert = await AuthUtils.RetrieveKeyVaultCertificate("AzureAutomationSPOAccess", tenantId, clientId, clientSecret, keyVaultUrl);
        var app = ConfidentialClientApplicationBuilder.Create(clientId)
                                              .WithCertificate(appRegistrationCert)
                                              .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                                              .Build();

        return app;
    }

    public static async Task<ClientContext> GetClientContext(Config config, string siteUrl, ILogger tracer, Action<AuthenticationResult>? authResultDelegate)
    {
        return await GetClientContext(siteUrl, config.AzureAdConfig.TenantId!, config.AzureAdConfig.ClientID!,
            config.AzureAdConfig.ClientSecret!, config.KeyVaultUrl, config.BaseServerAddress, tracer, authResultDelegate);
    }

    public static async Task<IConfidentialClientApplication> GetNewClientApp(Config config)
    {
        return await GetNewClientApp(config.AzureAdConfig.TenantId!,
            config.AzureAdConfig.ClientID!, config.AzureAdConfig.ClientSecret!, config.KeyVaultUrl);
    }
}

public static class ConfidentialClientApplicationAuth
{
    public async static Task<AuthenticationResult> AuthForSharePointOnline(this IConfidentialClientApplication app, string baseServerAddress)
    {
        var scopes = new string[] { $"{baseServerAddress}/.default" };
        var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
        return result;
    }
}
