﻿//-:cnd:noEmit

namespace Boilerplate.Client.Core.Services;

/// <summary>
/// For more information <see cref="IPrerenderStateService"/> docs.
/// </summary>
public class PrerenderStateService : IPrerenderStateService, IAsyncDisposable
{
    private PersistingComponentStateSubscription? subscription;
    private readonly PersistentComponentState applicationState;
    private readonly ConcurrentDictionary<string, object?> values = new();

    public PrerenderStateService(IServiceProvider serviceProvider)
    {
        if (AppRenderMode.PrerenderEnabled)
        {
            applicationState = serviceProvider.GetRequiredService<PersistentComponentState>();
            subscription = applicationState.RegisterOnPersisting(PersistAsJson, AppRenderMode.Current);
        }
    }

    public async Task<T?> GetValue<T>(string key, Func<Task<T?>> factory)
    {
        if (AppRenderMode.PrerenderEnabled is false)
            return await factory();

        if (applicationState.TryTakeFromJson(key, out T? value)) return value;

        var result = await factory();
        Persist(key, result);
        return result;
    }

    void Persist<T>(string key, T value)
    {
        if (AppRenderMode.PrerenderEnabled is false)
            return;

        values.TryRemove(key, out object? _);
        values.TryAdd(key, value);
    }

    async Task PersistAsJson()
    {
        foreach (var item in values)
        {
            applicationState.PersistAsJson(item.Key, item.Value);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (AppRenderMode.PrerenderEnabled is false)
            return;

        subscription?.Dispose();
    }
}