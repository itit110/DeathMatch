using System;
using UnityEngine;
using R3;

// R3‚ðŽg—p‚Å‚«‚é‚æ‚¤‚É‚·‚é‰Šú‰»
public static class UnityProviderInitializer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void SetDefaultObservableSystem()
    {
        SetDefaultObservableSystem(static ex => UnityEngine.Debug.LogException(ex));
    }

    public static void SetDefaultObservableSystem(Action<Exception> unhandledExceptionHandler)
    {
        ObservableSystem.RegisterUnhandledExceptionHandler(unhandledExceptionHandler);
        ObservableSystem.DefaultTimeProvider = UnityTimeProvider.Update;
        ObservableSystem.DefaultFrameProvider = UnityFrameProvider.Update;
    }
}