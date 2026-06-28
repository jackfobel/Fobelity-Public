namespace Fobelity.Home.MiniSplit.Server;

public static class SecurityHeadersDefinitions
{
    public static HeaderPolicyCollection GetHeaderPolicyCollection(bool isDev, string? idpHost)
    {
        ArgumentNullException.ThrowIfNull(idpHost);

    var policy = new HeaderPolicyCollection()
        .AddFrameOptionsDeny()
        .AddContentTypeOptionsNoSniff()
        .AddReferrerPolicyStrictOriginWhenCrossOrigin()
        .AddCrossOriginOpenerPolicy(builder => builder.SameOrigin())
        .AddCrossOriginResourcePolicy(builder => builder.SameOrigin())
        .AddCrossOriginEmbedderPolicy(builder => builder.RequireCorp())
        .AddContentSecurityPolicy(builder =>
        {
          builder.AddObjectSrc().None();
          builder.AddBlockAllMixedContent();
          builder.AddImgSrc().Self().From("data:");
          builder.AddFormAction().Self().From(idpHost);
          builder.AddFontSrc().Self();

          //builder.AddStyleSrc()
          //  .Self()
          //  .From("_content/MudBlazor/MudBlazor.min.css")
          //   //.WithNonce(); // ✅ This allows inline styles with the correct nonce
          //   .UnsafeInline(); // ⚠️ Not recommended in production

          builder.AddBaseUri().Self();
          builder.AddFrameAncestors().None();

          //// due to Blazor
          //builder.AddScriptSrc()
          //  .Self()
          //  .From("_content/MudBlazor")
          //  .WithNonce()
          //  .UnsafeEval()
          //  .UnsafeInline(); // fallback

          builder.AddFontSrc().Self().Data(); // MudBlazor uses font data

        })
        .RemoveServerHeader();
            //.AddPermissionsPolicyWithDefaultSecureDirectives();

        if (!isDev)
        {
            // maxage = one year in seconds
            policy.AddStrictTransportSecurityMaxAgeIncludeSubDomains();
        }

        return policy;
    }
}
