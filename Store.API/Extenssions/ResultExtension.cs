using Microsoft.AspNetCore.Mvc;
using Store.Application.Common;

namespace Store.API.Extenssions;

public static class ResultExtension
{
    public static IActionResult ToActionResult<T>(this Result<T> result,ControllerBase controller )
    {
        // if the returned is success so use the extenstion method of controller base OK() to return the data of result
        if (result.IsSuccess)
            return controller.Ok(result.Value);
        //otherwise there is different way of the result returned will be deteremineded using the StatusCode of the result

        return result.StatusCode switch
        {
            404 => controller.NotFound(new { error = result.Error }),
            400 => controller.BadRequest(new { error = result.Error }),
            409 => controller.Conflict(new { error=result.Error}),
            _ => controller.StatusCode(500,new {error=result.Error})
        };
    }
    //overloading for non-Generic version of Result
    public static IActionResult ToActionResult(this Result result,ControllerBase controller)
    {
        if (result.IsSuccess)
            return controller.Ok();
        return result.StatusCode switch
        {
            404 => controller.NotFound(new { error = result.Error }),
            400 => controller.BadRequest(new { error = result.Error }),
            409 => controller.Conflict(new { error = result.Error }),
            _ => controller.StatusCode(500, new { error = result.Error })
        };
    }
}
