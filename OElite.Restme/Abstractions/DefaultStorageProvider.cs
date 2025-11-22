using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OElite.Restme.Abstractions;

/// <summary>
/// Default storage provider that throws helpful error
/// </summary>
public class DefaultStorageProvider : IStorageProvider
{
    /// <summary>
    /// Provider name for debugging and logging
    /// </summary>
    public string ProviderName => "DefaultStorage";

    /// <summary>
    /// Configuration used to create this provider
    /// </summary>
    public RestConfig Configuration => new RestConfig();

    /// <summary>
    /// Capabilities supported by this provider
    /// </summary>
    public ProviderCapabilities Capabilities => ProviderCapabilities.Storage;

    public void Dispose() { }

    public Task<T?> GetAsync<T>(string objectKey) where T : class
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<T?> PutAsync<T>(string objectKey, T value) where T : class
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<bool> DeleteAsync(string objectKey)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<bool> ExistsAsync(string objectKey)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<string?> GetStringAsync(string objectKey)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<string?> PutStringAsync(string objectKey, string value)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<Stream?> GetStreamAsync(string objectKey)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<bool> PutStreamAsync(string objectKey, Stream stream)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<T> GetStreamAsync<T>(string objectKey) where T : Stream
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    // Cancellation token overloads
    public Task<T?> GetAsync<T>(string objectKey, CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<T?> PutAsync<T>(string objectKey, T value, CancellationToken cancellationToken = default) where T : class
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<string?> GetStringAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<string?> PutStringAsync(string objectKey, string value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<Stream?> GetStreamAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<bool> PutStreamAsync(string objectKey, Stream stream, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }

    public Task<T> GetStreamAsync<T>(string objectKey, CancellationToken cancellationToken = default) where T : Stream
    {
        throw new NotImplementedException("Storage provider not loaded. Please reference OElite.Restme.Azure or OElite.Restme.S3 package.");
    }
}