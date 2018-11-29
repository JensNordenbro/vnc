using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VncDeviceProxy;

namespace VncDeviceProxyCloudSide
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
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });


            m_Logger.LogInformation("Adding proxy nginxproxy");
            IPAddress[] addresses = Dns.GetHostAddresses("nginxproxy");
            m_Logger.LogInformation($"Got {addresses.Length} addresses");
            for (int i = 0; i < addresses.Length; i++)
            {
                m_Logger.LogInformation($"Adding {addresses[i].ToString()} ");
            }






            services.Configure<ForwardedHeadersOptions>(options =>
            {
                m_Logger.LogInformation($"Requesting DNS");
                IPAddress[] addresses = Dns.GetHostAddresses("nginxproxy");
                m_Logger.LogInformation($"Got {addresses.Length} addresses");
                for (int i = 0; i < addresses.Length; i++)
                {
                    m_Logger.LogInformation($"Adding {addresses[i].ToString()} ");
                    options.KnownProxies.Add(addresses[i]);
                }
            });
            m_Logger.LogInformation($"Configure done");

            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
         
            }
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

    

            app.UseWebSockets();

            app.Use(async (context, next) =>
            {
       
                if (context.WebSockets.IsWebSocketRequest)
                {
                    m_Logger.LogInformation("Accepting websocket");
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
