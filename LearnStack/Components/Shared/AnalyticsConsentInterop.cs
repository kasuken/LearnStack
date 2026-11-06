using Microsoft.JSInterop;

namespace LearnStack.Components.Shared;

public sealed class AnalyticsConsentInterop(IJSRuntime js) : IAsyncDisposable
{
    internal const string ModulePath = "./Components/Shared/AnalyticsConsent.razor.js";
    internal const string InitializeMethod = "initialize";
    internal const string ConsentMethod = "setConsent";
    internal const string DisposeMethod = "dispose";

    private IJSObjectReference? _module;

    public async ValueTask<string> InitializeAsync(string projectId)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        return await _module.InvokeAsync<string>(InitializeMethod, projectId);
    }

    public async ValueTask SetConsentAsync(bool accepted)
    {
        if (_module is null)
            throw new InvalidOperationException("Analytics consent has not been initialized.");

        await _module.InvokeVoidAsync(ConsentMethod, accepted);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync(DisposeMethod);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The browser owns tracking; a disconnected server circuit cannot clean up its module.
        }
    }
}
