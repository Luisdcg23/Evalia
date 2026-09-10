using EBR.Application.Evidence;
using Microsoft.Extensions.Options;

namespace EBR.Infrastructure.Evidence;

/// <summary>
/// Implementación de <see cref="IEvidenceStorage"/> sobre el sistema de archivos local, pensada para
/// ejecutar la aplicación sin depender de un servicio de objetos. Reproduce la semántica de un bucket:
/// la clave del objeto se traduce a una ruta relativa dentro de la raíz configurada y nunca puede
/// salirse de ella.
/// </summary>
public sealed class FileSystemEvidenceStorage(IOptions<EvidenceStorageOptions> options) : IEvidenceStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalRoot);

    public async Task SaveAsync(string objectKey, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var path = Resolve(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenAsync(string objectKey, CancellationToken cancellationToken)
    {
        var path = Resolve(objectKey);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    private string Resolve(string objectKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("La clave de objeto sale de la raíz del almacenamiento de evidencias.");
        return path;
    }
}

/// <summary>
/// Configuración del almacenamiento de evidencias (sección <c>Minio</c>, variables <c>Minio__*</c> del
/// archivo <c>.env</c>). Con <see cref="Endpoint"/> definido se usa MinIO; sin él, el sistema de
/// archivos local, que es lo que permite ejecutar la API y las pruebas sin levantar el servicio.
/// </summary>
public sealed class EvidenceStorageOptions
{
    public const string SectionName = "Minio";

    /// <summary>Extremo de MinIO/S3. Si está vacío, se usa el almacenamiento local.</summary>
    public string? Endpoint { get; set; }

    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string Bucket { get; set; } = "evidencias-ebr";
    public bool UseSsl { get; set; }

    /// <summary>Raíz local usada por <see cref="FileSystemEvidenceStorage"/>.</summary>
    public string LocalRoot { get; set; } = "storage/evidencias";
}
