﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VncDeviceProxy;

namespace VncDeviceProxyCloudSide
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseWebSockets();

            app.Use(async (context, next) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await CreateTunnel(context, webSocket);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            });

            // not the best place perhaps to start a service but it is a spike :) 
            var socketServer = new TcpListener(IPAddress.Any, 5900);
            socketServer.Start();
            Task.Run(async () =>
            {
                TcpClient vncClient = await socketServer.AcceptTcpClientAsync();
                Stream vncClientStream = vncClient.GetStream();
                m_VncClientStream.SetResult(vncClientStream);
            });
        }

        TaskCompletionSource<Stream> m_VncClientStream = new TaskCompletionSource<Stream>();

        private async Task CreateTunnel(HttpContext context, WebSocket webSocket)
        {
            var vncClientStream = await m_VncClientStream.Task;
            // now we got the sockets/streams, lets pipe all data!
            Pipe p = new Pipe(webSocket, vncClientStream);
            await p.TunnelAsync();
        }
    }
}
