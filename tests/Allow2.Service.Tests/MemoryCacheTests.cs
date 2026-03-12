// Copyright 2017-2026 Allow2 Pty Ltd. All rights reserved.

using Allow2.Service.Cache;

namespace Allow2.Service.Tests;

public class MemoryCacheTests
{
    [Fact]
    public async Task SetAndGet_ReturnsValue()
    {
        var cache = new MemoryCache();

        await cache.SetAsync("key1", "value1", ttlSeconds: 60);
        var result = await cache.GetAsync("key1");

        Assert.Equal("value1", result);
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var cache = new MemoryCache();

        var result = await cache.GetAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task Get_ExpiredEntry_ReturnsNull()
    {
        var cache = new MemoryCache();

        // Set with 0 TTL (already expired)
        await cache.SetAsync("key1", "value1", ttlSeconds: 0);

        // Wait a tiny bit to ensure expiry
        await Task.Delay(10);

        var result = await cache.GetAsync("key1");

        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        var cache = new MemoryCache();

        await cache.SetAsync("key1", "value1", ttlSeconds: 60);
        await cache.DeleteAsync("key1");
        var result = await cache.GetAsync("key1");

        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_NonexistentKey_DoesNotThrow()
    {
        var cache = new MemoryCache();
        await cache.DeleteAsync("nonexistent"); // Should not throw
    }

    [Fact]
    public async Task Set_OverwritesExistingValue()
    {
        var cache = new MemoryCache();

        await cache.SetAsync("key1", "value1", ttlSeconds: 60);
        await cache.SetAsync("key1", "value2", ttlSeconds: 60);
        var result = await cache.GetAsync("key1");

        Assert.Equal("value2", result);
    }

    [Fact]
    public async Task Set_WithPositiveTtl_IsAccessibleImmediately()
    {
        var cache = new MemoryCache();

        await cache.SetAsync("key1", "value1", ttlSeconds: 5);
        var result = await cache.GetAsync("key1");

        Assert.Equal("value1", result);
    }

    [Fact]
    public async Task MultipleKeys_AreIndependent()
    {
        var cache = new MemoryCache();

        await cache.SetAsync("key1", "value1", ttlSeconds: 60);
        await cache.SetAsync("key2", "value2", ttlSeconds: 60);

        Assert.Equal("value1", await cache.GetAsync("key1"));
        Assert.Equal("value2", await cache.GetAsync("key2"));

        await cache.DeleteAsync("key1");

        Assert.Null(await cache.GetAsync("key1"));
        Assert.Equal("value2", await cache.GetAsync("key2"));
    }
}
