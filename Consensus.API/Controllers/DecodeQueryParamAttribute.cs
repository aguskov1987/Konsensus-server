using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Consensus.API.Controllers
{
    public class DecodeQueryParamAttribute: ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var keys = new List<string>(context.ActionArguments.Keys);
            foreach (var key in keys)
            {
                string decoded = System.Web.HttpUtility.UrlDecode(context.ActionArguments[key] as string);
                context.ActionArguments[key] = decoded;
            }
            base.OnActionExecuting(context);
        }
    }
}