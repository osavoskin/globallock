# Global Lock
A simple, but reliable distributed lock which does not require a dedicated server

## Installation
```shell
dotnet add package SynchronizationUtils.GlobalLock
```

## Usage
```csharp
// Register the service with the IoC container
servises.AddGlobalLock(storageConnectionString);

using var globalLock = serviceProvider.GetRequiredService<IGlobalLock>();
await using var lease = await globalLock.TryAcquire(resource, scope);

if (lease.IsAcquired)
{
    // Got exclusive access to the resource across all nodes/processes
    // Do something with it..
}
else
{
    // Hm, that's unfortunate - the resource is being owned by another process
    // Let's wait while it is available..

    await lease.Wait();

    // Got it, it is safe to proceed now..
}
```