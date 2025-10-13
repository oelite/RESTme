using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OElite.Abstractions;

namespace OElite.Base
{
    /// <summary>
    /// Local filesystem storage provider. Uses a base directory as the storage root.
    /// No external dependencies required.
    /// </summary>
    public sealed class LocalFileSystemStorageProvider : IStorageProvider
    {
        private readonly string _baseDirectory;
        private bool _disposed;

        public LocalFileSystemStorageProvider(string? baseDirectory = null)
        {
            _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "restme_storage")
                : baseDirectory!;
            Directory.CreateDirectory(_baseDirectory);
        }

        public Task<T?> GetAsync<T>(string objectKey, CancellationToken cancellationToken = default) where T : class
        {
            var path = ResolvePath(objectKey);
            if (!File.Exists(path)) return Task.FromResult<T?>(null);

            if (typeof(T) == typeof(string))
            {
                var content = File.ReadAllText(path, Encoding.UTF8);
                return Task.FromResult(content as T);
            }

            if (typeof(T).IsSubclassOf(typeof(Stream)))
            {
                var stream = (Stream)File.OpenRead(path);
                return Task.FromResult(stream as T);
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            var obj = json.JsonDeserialize<T>();
            return Task.FromResult(obj);
        }

        public Task<T?> PutAsync<T>(string objectKey, T value, CancellationToken cancellationToken = default) where T : class
        {
            var path = ResolvePath(objectKey);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (value is string s)
            {
                File.WriteAllText(path, s, Encoding.UTF8);
                return Task.FromResult(value);
            }

            if (value is Stream stream)
            {
                using var fs = File.Create(path);
                stream.CopyTo(fs);
                return Task.FromResult(value);
            }

            var json = value.JsonSerialize();
            File.WriteAllText(path, json, Encoding.UTF8);
            return Task.FromResult(value);
        }

        public Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(objectKey);
            if (!File.Exists(path)) return Task.FromResult(false);
            File.Delete(path);
            return Task.FromResult(true);
        }

        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(objectKey);
            return Task.FromResult(File.Exists(path));
        }

        public Task<string?> GetStringAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(objectKey);
            if (!File.Exists(path)) return Task.FromResult<string?>(null);
            return Task.FromResult<string?>(File.ReadAllText(path, Encoding.UTF8));
        }

        public Task<string?> PutStringAsync(string objectKey, string value, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(objectKey);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, value, Encoding.UTF8);
            return Task.FromResult<string?>(value);
        }

        public Task<Stream?> GetStreamAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(objectKey);
            if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
            return Task.FromResult<Stream?>(File.OpenRead(path));
        }

        public Task<bool> PutStreamAsync(string objectKey, Stream stream, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(objectKey);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = File.Create(path);
            stream.CopyTo(fs);
            return Task.FromResult(true);
        }

        public Task<T> GetStreamAsync<T>(string objectKey, CancellationToken cancellationToken = default) where T : Stream
        {
            var stream = File.OpenRead(ResolvePath(objectKey));
            if (stream is T typed)
            {
                return Task.FromResult(typed);
            }
            stream.Dispose();
            throw new InvalidCastException($"Stored stream is not of type {typeof(T).FullName}");
        }

        private string ResolvePath(string objectKey)
        {
            objectKey = objectKey.Replace('\\', '/');
            objectKey = objectKey.TrimStart('/');
            return Path.Combine(_baseDirectory, objectKey);
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}


