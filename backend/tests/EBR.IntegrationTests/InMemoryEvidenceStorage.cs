using System.Collections.Concurrent;
using EBR.Application.Evidence;

namespace EBR.IntegrationTests;

/// <summary>
/// Implementación en memoria de <see cref="IEvidenceStorage"/> para las pruebas de integración: el
/// bucket vive en un diccionario, así que la suite no necesita un MinIO ni escribe en disco.
/// </summary>
public sealed class InMemoryEvidenceStorage : IEvidenceStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Keys => _objects.Keys.ToArray();

    public async Task SaveAsync(string objectKey, string contentType, Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _objects[objectKey] = buffer.ToArray();
    }

    public Task<Stream?> OpenAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(_objects.TryGetValue(objectKey, out var content)
            ? (Stream)new MemoryStream(content, writable: false)
            : null);
}
