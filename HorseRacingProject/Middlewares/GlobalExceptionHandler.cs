using HorseRacingAPI.Dtos;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;

namespace HorseRacingAPI.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
           _logger.LogError(exception,$"An error occurred while processing the request: {exception.Message}");
            var statusCode = HttpStatusCode.InternalServerError;
            var message = "An unexpected error occurred. Please try again later.";
            switch (exception)
            {
                case InvalidOperationException:
                    statusCode = HttpStatusCode.BadRequest;
                    message = exception.Message;
                    break;
                case KeyNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    message = exception.Message;
                    break;
                case ArgumentException:
                    statusCode = HttpStatusCode.BadRequest;
                    message = exception.Message;
                    break;
                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Unauthorized;
                    message = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại!";
                    break;
                case Microsoft.EntityFrameworkCore.DbUpdateException:
                    statusCode = HttpStatusCode.InternalServerError;
                    message = "Lỗi đồng bộ dữ liệu xuống cơ sở dữ liệu. Vui lòng kiểm tra lại!";
                    break;
            }
            httpContext.Response.StatusCode = (int)statusCode;
            httpContext.Response.ContentType = "application/json";
            var respose = ApiResponse<object>.FailResponse(message);
            await httpContext.Response.WriteAsJsonAsync(respose, cancellationToken);
            return true;
        }
    }
}
