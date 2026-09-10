using Npgsql;

namespace EBR.Api.Endpoints;

internal static class PostgresCommandProblem
{
    private static readonly HashSet<string> ValidationStates = ["22001", "22P02", "23502", "23514"];
    private static readonly HashSet<string> ConflictStates = ["23505", "23P01", "40001", "40P01", "P0001"];

    public static async Task<IResult?> ExecuteAsync(Func<Task> command)
    {
        try
        {
            await command();
            return null;
        }
        catch (PostgresException exception) when (ValidationStates.Contains(exception.SqlState))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Datos inválidos",
                detail: "La operación contiene datos que no cumplen las reglas de validación.");
        }
        catch (PostgresException exception) when (ConflictStates.Contains(exception.SqlState))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto de operación",
                detail: "La operación entra en conflicto con el estado actual o con otra operación concurrente.");
        }
    }
}
