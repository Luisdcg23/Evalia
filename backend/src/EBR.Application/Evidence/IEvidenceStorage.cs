namespace EBR.Application.Evidence;

/// <summary>
/// Almacenamiento de objetos para las evidencias de campo. Se define con la semántica de un bucket
/// compatible con S3/MinIO —clave de objeto opaca, tipo de contenido y flujo de bytes— para que la
/// implementación real pueda sustituirse sin tocar los endpoints ni los metadatos en PostgreSQL.
/// El binario nunca entra en la base de datos: en <c>Evaluacion_Evidencia</c> solo queda la clave.
/// </summary>
public interface IEvidenceStorage
{
    /// <summary>
    /// Guarda el objeto bajo la clave indicada. La clave la decide quien llama (incluye el
    /// identificador de la evaluación), de modo que el almacenamiento no necesita conocer el dominio.
    /// </summary>
    Task SaveAsync(string objectKey, string contentType, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Abre el objeto para lectura, o devuelve <c>null</c> si la clave no existe en el almacenamiento
    /// (metadato huérfano). Quien llama es responsable de liberar el flujo.
    /// </summary>
    Task<Stream?> OpenAsync(string objectKey, CancellationToken cancellationToken);
}
