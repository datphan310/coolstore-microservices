// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.IO;
using System.Linq;
using IdentityServer4.Quickstart.UI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;

namespace IdentityServer4
{
  public class Startup
  {
    public IHostingEnvironment Environment { get; }
    public IConfiguration Configuration { get; }

    public Startup(IHostingEnvironment environment, IConfiguration configuration)
    {
      Environment = environment;
      Configuration = configuration;
      IdentityModelEventSource.ShowPII = true;
    }

    public void ConfigureServices(IServiceCollection services)
    {
      services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

      services.AddCors(options =>
      {
        options.AddPolicy("CorsPolicy",
                  b => b.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
      });

      IIdentityServerBuilder builder = services
          .AddIdentityServer(options =>
          {
            options.Events.RaiseErrorEvents = true;
            options.Events.RaiseInformationEvents = true;
            options.Events.RaiseFailureEvents = true;
            options.Events.RaiseSuccessEvents = true;
            options.IssuerUri = "null";
          })
          .AddTestUsers(TestUsers.Users)
          .AddJwtBearerClientAuthentication();
      var hostSettings = Configuration.GetSection("HostSettings");

      // in-memory, code config
      var clients = Config.GetClients(Environment.IsDevelopment(), hostSettings).ToList();

      // get swagger and process it
      if (hostSettings != null)
      {
        clients[0].RedirectUris.Add(hostSettings.GetValue<string>("SwaggerRedirectUri"));
        clients[0].PostLogoutRedirectUris.Add(hostSettings.GetValue<string>("SwaggerPostLogoutRedirectUri"));
        clients[0].AllowedCorsOrigins.Add(hostSettings.GetValue<string>("SwaggerAllowedCorsOrigin"));
      }

      builder.AddInMemoryIdentityResources(Config.GetIdentityResources());
      builder.AddInMemoryApiResources(Config.GetApis());
      builder.AddInMemoryClients(clients);

      // in-memory, json config
      // builder.AddInMemoryIdentityResources(Configuration.GetSection("IdentityResources"));
      // builder.AddInMemoryApiResources(Configuration.GetSection("ApiResources"));
      // builder.AddInMemoryClients(Configuration.GetSection("clients"));

      builder.AddDeveloperSigningCredential();
      // builder.AddSigningCredential(Certificate.Get());

      services
          .AddAuthentication(options =>
          {
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = "Cookies";
          })
          .AddCookie()
          .AddGoogle(options =>
          {
            options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

            options.ClientId = "708996912208-9m4dkjb5hscn7cjrn5u0r4tbgkbj1fko.apps.googleusercontent.com";
            options.ClientSecret = "wdfPY6t8H8cecgjlxud__4Gh";
          });

      services
          .AddDataProtection()
          .PersistKeysToFileSystem(new DirectoryInfo("cs.idp"));
    }

    public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
    {
      if (Environment.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
        app.UseDatabaseErrorPage();
      }
      else
      {
        app.UseExceptionHandler("/Home/Error");
      }

      var basePath = Configuration.GetSection("HostSettings")?.GetValue<string>("BasePath");
      if (!string.IsNullOrEmpty(basePath))
      {
        loggerFactory.CreateLogger("init").LogDebug($"Using PATH BASE '{basePath}'");
        app.Use(async (context, next) =>
        {
          context.Request.PathBase = basePath;
          await next.Invoke();
        });
      }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
      app.Map("/liveness", lapp => lapp.Run(async ctx => ctx.Response.StatusCode = 200));
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

      if (!Environment.IsDevelopment())
      {
        app.UseForwardedHeaders();
      }

      app.UseCors("CorsPolicy");
      app.UseIdentityServer();
      app.UseStaticFiles();
      app.UseMvcWithDefaultRoute();
    }
  }
}
