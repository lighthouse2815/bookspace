using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace BookSpace.Api.Common;

public sealed class TrustedForwardingOptions
{
    public const string SectionName = "ForwardedHeaders";

    public int ForwardLimit { get; init; } = 1;
    public string[] KnownProxies { get; init; } = [];
    public string[] KnownNetworks { get; init; } = [];
}

public static class TrustedForwardingServiceCollectionExtensions
{
    public static IServiceCollection AddBookSpaceTrustedForwarding(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(TrustedForwardingOptions.SectionName)
            .Get<TrustedForwardingOptions>() ?? new TrustedForwardingOptions();

        if (settings.ForwardLimit <= 0)
        {
            throw new InvalidOperationException(
                "ForwardedHeaders:ForwardLimit phải là số nguyên dương.");
        }

        var knownProxies = settings.KnownProxies.Select(ParseProxy).ToArray();
        var knownNetworks = settings.KnownNetworks.Select(ParseNetwork).ToArray();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = settings.ForwardLimit;

            foreach (var proxy in knownProxies)
            {
                if (!options.KnownProxies.Contains(proxy))
                {
                    options.KnownProxies.Add(proxy);
                }
            }

            foreach (var network in knownNetworks)
            {
                if (!options.KnownIPNetworks.Contains(network))
                {
                    options.KnownIPNetworks.Add(network);
                }
            }
        });

        return services;
    }

    private static IPAddress ParseProxy(string value)
    {
        if (IPAddress.TryParse(value?.Trim(), out var address))
        {
            return address;
        }

        throw new InvalidOperationException(
            $"ForwardedHeaders:KnownProxies chứa địa chỉ IP không hợp lệ: '{value}'.");
    }

    private static System.Net.IPNetwork ParseNetwork(string value)
    {
        if (System.Net.IPNetwork.TryParse(value?.Trim(), out var network))
        {
            return network;
        }

        throw new InvalidOperationException(
            $"ForwardedHeaders:KnownNetworks chứa CIDR không hợp lệ: '{value}'.");
    }
}
