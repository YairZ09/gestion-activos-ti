using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AssetManagement.Api.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return true;

        var business = exception switch
        {
            BusinessException b => b,
            SqlException sql => SqlErrorTranslator.Translate(sql),
            BadHttpRequestException => new BusinessException(400, "BAD_REQUEST", "La solicitud no es valida."),
            _ => null
        };

        if (business is null)
        {
            logger.LogError(exception, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
            business = new BusinessException(500, "INTERNAL_ERROR", "Ocurrio un error inesperado. Si persiste, contacte a soporte indicando el traceId.");
        }
        else if (business.StatusCode >= 500)
        {
            logger.LogError(exception, "Error de servidor en {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogInformation("Error de negocio {Code} en {Method} {Path}", business.Code, context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = business.StatusCode;
        var problem = new ProblemDetails { Status = business.StatusCode, Title = Title(business.StatusCode), Detail = business.Message };
        problem.Extensions["code"] = business.Code;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }

    private static string Title(int status) => status switch
    {
        400 => "Solicitud invalida",
        401 => "No autenticado",
        403 => "Acceso denegado",
        404 => "No encontrado",
        409 => "Conflicto con el estado actual",
        429 => "Demasiadas solicitudes",
        _ => "Error del servidor"
    };
}
