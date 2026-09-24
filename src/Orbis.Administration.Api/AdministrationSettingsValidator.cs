using Microsoft.Extensions.Options;
using Orbis.Hosting;

namespace Orbis.Administration.Api;

public sealed class AdministrationSettingsValidator(IOptions<AuthenticationSettings> authentication) : IValidateOptions<AdministrationSettings>
{
    public ValidateOptionsResult Validate(string? name, AdministrationSettings options) => options.IsValid(authentication.Value)
        ? ValidateOptionsResult.Success
        : ValidateOptionsResult.Fail("A distinct administrative audience, MFA ACR contract, allowed clients and authentication age (60–900 seconds) are required.");
}
