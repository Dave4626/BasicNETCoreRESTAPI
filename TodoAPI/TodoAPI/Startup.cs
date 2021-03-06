using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CacheHeaders.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Serialization;
using TodoAPI.Services;
using TodoAPI.Services.SortingServices;

namespace TodoAPI
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            //ETagger options
            services.AddETagger(expirationOpts =>
            {
                expirationOpts.MaxAge = 120;
            }, validationOpts =>
            {
                validationOpts.MustRevalidate = true;
            }
            );

            services.AddControllers(opts =>
            {
                //return 406 when content type not match
                opts.ReturnHttpNotAcceptable = true;
            })
            //use newtonsoft json serializer as default
            .AddNewtonsoftJson(setup =>
            {
                setup.SerializerSettings.ContractResolver =
                    new CamelCasePropertyNamesContractResolver();
            })
            //use xml serializer as second option
            .AddXmlDataContractSerializerFormatters()
            //return 422 unprocessable entity when validation fails
            .ConfigureApiBehaviorOptions(setupAction =>
            {
                setupAction.InvalidModelStateResponseFactory = context =>
                {
                    var problemDetails = new ValidationProblemDetails(context.ModelState)
                    {
                        Type = "https://myapp/modelvalidationproblem",
                        Title = "One or more model validation errors occured",
                        Status = StatusCodes.Status422UnprocessableEntity,
                        Detail = "See the errors property for details.",
                        Instance = context.HttpContext.Request.Path
                    };

                    problemDetails.Extensions.Add("traceId", context.HttpContext.TraceIdentifier);

                    return new UnprocessableEntityObjectResult(problemDetails)
                    {
                        ContentTypes = new Microsoft.AspNetCore.Mvc.Formatters.MediaTypeCollection() { "application/problem+json" }
                    };
                };
            });

            //register PropertyMappingService for sorting (map between DTO and ENTITY props)
            services.AddTransient<IPropertyMappingService, PropertyMappingService>();

            //register property checker service (check for property exists on specified type)
            services.AddTransient<IPropertyCheckerService, PropertyCheckerService>();

            //add automapper for mapping between entities and DTO objects
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            //add repository
            services.AddSingleton<Services.ITasksRepository, Services.TasksRepository>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //Use ETagger middleware - must be before UseRouting!
            app.UseETagger();

            app.UseRouting();

            app.UseAuthorization();

            //using attribute based endpoint routing
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
