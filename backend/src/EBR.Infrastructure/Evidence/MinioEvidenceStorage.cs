using EBR.Application.Evidence;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace EBR.Infrastructure.Evidence;

/// <summary>
/// Implementación de <see cref="IEvidenceStorage"/> sobre un servicio compatible con S3 (MinIO). Se
/// activa cuando la configuración declara un extremo en <c>Evidence:Endpoint</c>; en su ausencia la
/// aplicación usa <see cref="FileSystemEvidenceStorage"/>. El bucket se crea si no existe, de modo que
/// una instalación nueva de MinIO no exige preparación manual.
/// </summary>
public sealed class MinioEvidenceStorage : IEvidenceStorage
{
    private readonly IMinioClient _client;
    private readonly string _bucket;

    public MinioEvidenceStorage(IOptions<EvidenceStorageOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
            throw new InvalidOperationException("Evidence:Endpoint es obligatorio para usar el almacenamiento MinIO.");

        _bucket = settings.Bucket;
        _client = new MinioClient()
            .WithEndpoint(settings.Endpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey)
            .WithSSL(settings.UseSsl)
            .Build();
    }

    public async Task SaveAsync(string objectKey, string contentType, Stream content, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        await _client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(objectKey)
                .WithStreamData(content)
                .WithObjectSize(content.Length)
                .WithContentType(contentType),
            cancellationToken);
    }

    public async Task<Stream?> OpenAsync(string objectKey, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        try
        {
            await _client.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(_bucket)
                    .WithObject(objectKey)
                    .WithCallbackStream(async (stream, token) => await stream.CopyToAsync(buffer, token)),
                cancellationToken);
        }
        catch (ObjectNotFoundException)
        {
            await buffer.DisposeAsync();
            return null;
        }

        buffer.Position = 0;
        return buffer;
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket), cancellationToken);
        if (exists) return;
        await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), cancellationToken);
    }
}
