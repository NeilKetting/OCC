using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using System;

namespace OCC.API.Infrastructure
{
    public class SuppressRowVersionMetadataProvider : IMetadataDetailsProvider, IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            if (context.Key.Name != null && context.Key.Name.EndsWith("RowVersion", StringComparison.OrdinalIgnoreCase))
            {
                context.ValidationMetadata.IsRequired = false;
            }
        }
    }
}
