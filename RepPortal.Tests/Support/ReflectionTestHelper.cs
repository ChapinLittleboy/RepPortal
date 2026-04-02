using System.Reflection;

namespace RepPortal.Tests.Support;

internal static class ReflectionTestHelper
{
    public static object? InvokeNonPublicStatic(Type type, string methodName, params object?[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {type.FullName}.");

        return method.Invoke(null, args);
    }

    public static object? InvokeNonPublicInstance(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {instance.GetType().FullName}.");

        return method.Invoke(instance, args);
    }

    public static object? InvokeNonPublicInstanceWithArguments(object instance, string methodName, object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {instance.GetType().FullName}.");

        return method.Invoke(instance, args);
    }
}
