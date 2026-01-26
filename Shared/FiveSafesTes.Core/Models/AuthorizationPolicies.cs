using Microsoft.AspNetCore.Authorization;

namespace FiveSafesTes.Core.Models
{


    public static class AuthorizationPolicies
    {
        public static AuthorizationPolicy GetUserAllowedPolicy()

        {
            var policyBuilder = new AuthorizationPolicyBuilder();

            // Add your policy requirements here
            policyBuilder.RequireClaim("user_in_tre");
            policyBuilder.RequireClaim("allow");
            policyBuilder.RequireClaim("project_allow");
            return policyBuilder.Build();
        }

    }
}