using System.Collections.Generic;
using Jellyfin.Plugin.ArrProfileSwitcher.Api;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Tests;

public class TryGetProviderIdTests
{
    [Fact]
    public void ValidNumericValue_ReturnsTrueAndParsedValue()
    {
        var providerIds = new Dictionary<string, string> { ["Tmdb"] = "12345" };

        var found = ArrProfileSwitcherController.TryGetProviderId(providerIds, "Tmdb", out var value);

        Assert.True(found);
        Assert.Equal(12345, value);
    }

    [Fact]
    public void MissingKey_ReturnsFalse()
    {
        var providerIds = new Dictionary<string, string> { ["Tvdb"] = "999" };

        var found = ArrProfileSwitcherController.TryGetProviderId(providerIds, "Tmdb", out _);

        Assert.False(found);
    }

    [Fact]
    public void NonNumericValue_ReturnsFalse()
    {
        var providerIds = new Dictionary<string, string> { ["Tmdb"] = "not-a-number" };

        var found = ArrProfileSwitcherController.TryGetProviderId(providerIds, "Tmdb", out _);

        Assert.False(found);
    }

    [Fact]
    public void NullDictionary_ReturnsFalse()
    {
        var found = ArrProfileSwitcherController.TryGetProviderId(null!, "Tmdb", out _);

        Assert.False(found);
    }
}
