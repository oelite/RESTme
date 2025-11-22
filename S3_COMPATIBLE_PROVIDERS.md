# S3-Compatible Providers Support

The OElite.Restme.S3 package now supports S3-compatible object storage providers in addition to Amazon S3. This includes providers like Backblaze B2, MinIO, DigitalOcean Spaces, and many others that offer S3-compatible APIs.

## Connection String Format

The connection string supports the following parameters:

```
AccessKeyId=your_access_key;SecretAccessKey=your_secret_key;ServiceUrl=https://your-endpoint.com;BucketName=your-bucket;ForcePathStyle=true;UseHttp=false
```

### Parameters

| Parameter | Description | Required | Default |
|-----------|-------------|----------|---------|
| `AccessKeyId` | Your access key ID | Yes | - |
| `SecretAccessKey` | Your secret access key | Yes | - |
| `ServiceUrl` or `Endpoint` | The S3-compatible service endpoint URL | No* | Uses AWS S3 |
| `BucketName` | Default bucket name for cache operations | No | `restme-cache` |
| `Region` | AWS region (only used when ServiceUrl is not provided) | No | `us-east-1` |
| `ForcePathStyle` | Use path-style URLs (required for most S3-compatible services) | No | `false` |
| `UseHttp` | Use HTTP instead of HTTPS | No | `false` |

*Required for S3-compatible providers, optional for Amazon S3

## Supported Providers

### 1. Amazon S3 (Default)
```csharp
var connectionString = "AccessKeyId=AKIA...;SecretAccessKey=...;Region=us-west-2;BucketName=my-bucket";
var rest = new Rest(connectionString, RestMode.S3Client);
```

### 2. Backblaze B2
```csharp
var connectionString = "AccessKeyId=your_key_id;SecretAccessKey=your_application_key;ServiceUrl=https://s3.us-west-004.backblazeb2.com;BucketName=my-bucket;ForcePathStyle=true";
var rest = new Rest(connectionString, RestMode.S3Client);
```

### 3. MinIO
```csharp
var connectionString = "AccessKeyId=minioadmin;SecretAccessKey=minioadmin;ServiceUrl=http://localhost:9000;BucketName=my-bucket;ForcePathStyle=true;UseHttp=true";
var rest = new Rest(connectionString, RestMode.S3Client);
```

### 4. DigitalOcean Spaces
```csharp
var connectionString = "AccessKeyId=your_spaces_key;SecretAccessKey=your_spaces_secret;ServiceUrl=https://nyc3.digitaloceanspaces.com;BucketName=my-bucket;ForcePathStyle=true";
var rest = new Rest(connectionString, RestMode.S3Client);
```

### 5. Cloudflare R2
```csharp
var connectionString = "AccessKeyId=your_r2_token;SecretAccessKey=your_r2_secret;ServiceUrl=https://your-account-id.r2.cloudflarestorage.com;BucketName=my-bucket;ForcePathStyle=true";
var rest = new Rest(connectionString, RestMode.S3Client);
```

### 6. Wasabi
```csharp
var connectionString = "AccessKeyId=your_wasabi_key;SecretAccessKey=your_wasabi_secret;ServiceUrl=https://s3.wasabisys.com;BucketName=my-bucket;ForcePathStyle=true";
var rest = new Rest(connectionString, RestMode.S3Client);
```

### 7. Scaleway Object Storage
```csharp
var connectionString = "AccessKeyId=your_scaleway_key;SecretAccessKey=your_scaleway_secret;ServiceUrl=https://s3.fr-par.scw.cloud;BucketName=my-bucket;ForcePathStyle=true";
var rest = new Rest(connectionString, RestMode.S3Client);
```

## Usage Examples

### Cache Operations
```csharp
// Initialize with S3-compatible provider
var rest = new Rest(connectionString, RestMode.S3Client);
var cacheProvider = rest.GetProvider<ICacheProvider>();

// Cache data with expiry (using ICacheProvider directly)
await cacheProvider.SetAsync("user:123", userData, TimeSpan.FromMinutes(60));

// Cache data with expiry (using CachemeAsync extension)
var success = await cacheProvider.CachemeAsync("user:123", userData, expiryInSeconds: 3600); // 60 minutes

// Retrieve cached data (using ICacheProvider directly)
var cachedUser = await cacheProvider.GetAsync<User>("user:123");

// Retrieve cached data (using FindmeAsync extension with validation)
var cachedUserWithValidation = await cacheProvider.FindmeAsync<User>("user:123");

// Remove from cache
await cacheProvider.RemoveAsync("user:123");

// Force expiry (using ExpiremeAsync extension)
await cacheProvider.ExpiremeAsync("user:123");
```

### Storage Operations
```csharp
// Get storage provider and store data
var storageProvider = rest.GetProvider<IStorageProvider>();
await storageProvider.PutAsync("documents/report.pdf", fileData);

// Retrieve data
var fileData = await storageProvider.GetAsync<byte[]>("documents/report.pdf");

// Check if exists
var exists = await storageProvider.ExistsAsync("documents/report.pdf");

// Delete data
await storageProvider.DeleteAsync("documents/report.pdf");
```

### Generic Operations
```csharp
// GET operation (retrieves from cache or storage)
var data = await rest.GetAsync<MyData>("key");

// POST operation (stores in cache and storage)
var result = await rest.PostAsync<MyData>("key", data, TimeSpan.FromHours(1));

// PUT operation (updates cache and storage)
var updated = await rest.PutAsync<MyData>("key", updatedData, TimeSpan.FromHours(2));

// DELETE operation (removes from cache and storage)
await rest.DeleteAsync<MyData>("key");
```

## Provider-Specific Notes

### Backblaze B2
- Use your B2 application key as the `SecretAccessKey`
- The `ServiceUrl` should be your B2 S3-compatible endpoint
- Always set `ForcePathStyle=true`

### MinIO
- Default credentials are `minioadmin`/`minioadmin`
- Use `UseHttp=true` for local development
- Always set `ForcePathStyle=true`

### DigitalOcean Spaces
- Use your Spaces access key and secret
- The `ServiceUrl` format is `https://{region}.digitaloceanspaces.com`
- Always set `ForcePathStyle=true`

### Cloudflare R2
- Use your R2 API token credentials
- The `ServiceUrl` format is `https://{account-id}.r2.cloudflarestorage.com`
- Always set `ForcePathStyle=true`

## Error Handling

The providers will throw `InvalidOperationException` with descriptive messages if:
- Required credentials are missing
- The service endpoint is unreachable
- Bucket operations fail
- Authentication fails

## Performance Considerations

- **CDN Integration**: Both cache and storage providers set appropriate cache headers for CDN distribution
- **Connection Pooling**: The AWS SDK handles connection pooling automatically
- **Retry Logic**: Built-in retry logic for transient failures
- **Compression**: Consider compressing large objects before storage

## Security Best Practices

1. **Use HTTPS**: Always use `UseHttp=false` (default) for production
2. **Rotate Keys**: Regularly rotate your access keys
3. **Bucket Policies**: Configure appropriate bucket policies for your use case
4. **VPC**: Use VPC endpoints when available for additional security
5. **IAM**: Use IAM roles when possible instead of access keys

## Troubleshooting

### Common Issues

1. **403 Forbidden**: Check your credentials and bucket permissions
2. **404 Not Found**: Ensure the bucket exists and you have access
3. **Connection Timeout**: Verify the ServiceUrl is correct and accessible
4. **SSL Errors**: Check if you need `UseHttp=true` for local development

### Debug Mode

Enable debug logging to see detailed request/response information:

```csharp
var rest = new Rest(connectionString, RestMode.S3Client);
rest.Logger = logger; // Your ILogger instance
```

This will help diagnose connection and authentication issues.
