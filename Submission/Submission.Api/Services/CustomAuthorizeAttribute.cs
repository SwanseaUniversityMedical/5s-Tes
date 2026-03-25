using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Serilog;


namespace Submission.Api.Services
{

    public class MyAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly string[] _requiredRoles;

        public MyAuthorizeAttribute(params string[] roles)
        {
            _requiredRoles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var isAuthenticated = context.HttpContext.User.Identity.IsAuthenticated;
            if (!isAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var hasAllRequredClaims = _requiredRoles.All(claim => context.HttpContext.User.HasClaim(x => x.Type == claim));
            if (!hasAllRequredClaims)
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class Custom2AuthorizeAttribute : TypeFilterAttribute
    {
        public string Roles { get; }

        public Custom2AuthorizeAttribute(string roles) : base(typeof(Custom2AuthorizeFilter))
        {
            Roles = roles;
            Arguments = new object[] { roles };
        }
    }

   

    public sealed class Custom2AuthorizeFilter : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User as ClaimsPrincipal;

            if (user == null || !user.Identity.IsAuthenticated)
            {
                Log.Information("User is not authenticated");
                context.Result = new UnauthorizedResult();
                return;
            }

            var customAttr = context.ActionDescriptor.EndpointMetadata.OfType<Custom2AuthorizeAttribute>().FirstOrDefault();
            if (customAttr != null)
            {
                var requiredRoles = customAttr.Roles;
                if (!string.IsNullOrEmpty(requiredRoles) && !user.IsInRole(requiredRoles))
                {
                    Log.Information($"Authorization failed for user '{user.Identity.Name}' with required roles '{requiredRoles}'");
                    context.Result = new ForbidResult();
                }
            }
        }
    }
}

