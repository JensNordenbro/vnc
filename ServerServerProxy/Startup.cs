using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServerServerProxy
{
    public class Startup
    {
        private readonly ILogger m_Logger;
        public Startup(IConfiguration configuration, ILogger<Startup> logger)
        {
            Configuration = configuration;
            m_Logger = logger;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
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
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();
            app.Use(async (context, next) =>
            {
                m_Logger.LogInformation("REQUEST RECEIVED");
                if (context.WebSockets.IsWebSocketRequest)
                {
                    m_Logger.LogInformation("Accepting websocket");
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await CreateTunnel(context, webSocket);
                }
                else
                {
                    m_Logger.LogInformation("DDR WAS HERE");
                    await next();
                }
            });

            var forwardingOptions = new ForwardedHeadersOptions()
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            forwardingOptions.KnownNetworks.Clear(); //its loopback by default
            forwardingOptions.KnownProxies.Clear();
            app.UseForwardedHeaders(forwardingOptions);




            // not the best place perhaps to start a service but it is a spike :) 
            var socketServer = new TcpListener(IPAddress.Any, 5900);
            socketServer.Start();
            Task.Run(async () =>
            {
                while (true)
                {
                    TcpClient vncClient = await socketServer.AcceptTcpClientAsync();
                    Stream vncClientStream = vncClient.GetStream();
                    m_VncClientStream.SetResult(vncClientStream);
                }
            });
        }

        TaskCompletionSource<Stream> m_VncClientStream = new TaskCompletionSource<Stream>();

        private async Task CreateTunnel(HttpContext context, WebSocket webSocket)
        {
            var vncClientStream = await m_VncClientStream.Task;
            m_VncClientStream = new TaskCompletionSource<Stream>(); // reset for next connection attempt
            // now we got the sockets/streams, lets pipe all data!
            Pipe p = new Pipe(webSocket, vncClientStream);
            await p.TunnelAsync();
        }
    }
}
