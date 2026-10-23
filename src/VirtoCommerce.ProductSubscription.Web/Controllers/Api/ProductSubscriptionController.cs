using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = VirtoCommerce.ProductSubscription.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.ProductSubscription.Web.Controllers.Api;

[Authorize]
[Route("api/product-subscription")]
public class ProductSubscriptionController : Controller
{
    // GET: api/product-subscription
    /// <summary>
    /// Get message
    /// </summary>
    /// <remarks>Return "Hello world!" message</remarks>
    [HttpGet]
    [Route("")]
    [Authorize(Permissions.Read)]
    public ActionResult<string> Get()
    {
        return Ok(new { result = "Hello world!" });
    }
}
