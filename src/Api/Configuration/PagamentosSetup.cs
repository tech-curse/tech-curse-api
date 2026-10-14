using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using TechCurse.Api.Controllers;

namespace TechCurse.Api.Configuration;

public static class PagamentosSetup
{
    public static IMvcBuilder AddPagamentosSetup(this IMvcBuilder mvc, IConfiguration configuration)
    {
        if (!configuration.GetValue("Payments:Enabled", false))
        {
            mvc.ConfigureApplicationPartManager(partes => partes.FeatureProviders.Add(new SemControllerDePagamentos()));
        }

        return mvc;
    }

    private sealed class SemControllerDePagamentos : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Remove(typeof(PaymentController).GetTypeInfo());
        }
    }
}
